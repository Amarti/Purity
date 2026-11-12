using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace Purity.Reminders.CalDav
{
	/// <summary>
	/// The calendar Purity makes on a CalDAV server, such as iCloud. Each reminder is one event at an
	/// address made from its tag, so a call that's repeated can't make a second copy. The calendar's
	/// own address starts with the calendar's tag and its description carries it too, so another
	/// device finds it whatever it's called by now, even on a server that drops descriptions.
	/// </summary>
	public sealed class CalDavCalendar : ICalendarService, IChoiceStore
	{
		/// <param name="account">The sign-in</param>
		/// <param name="getCalendar">The calendar's address, or null before one is found or made</param>
		/// <param name="setCalendar">Keeps the calendar's address on this device</param>
		/// <param name="calendarName">The name a new calendar gets</param>
		/// <param name="zone">The local time zone, which turns a local time into a moment</param>
		/// <param name="clock">What time it is, for the stamp each event carries</param>
		public CalDavCalendar(CalDavAccount account, Func<string?> getCalendar, Action<string?> setCalendar, Func<string> calendarName, TimeZoneInfo zone, TimeProvider? clock = null)
		{
			_account = account;
			_getCalendar = getCalendar;
			_setCalendar = setCalendar;
			_calendarName = calendarName;
			_zone = zone;
			_clock = clock ?? TimeProvider.System;
		}


		public async Task<IReadOnlyList<CalendarEntry>> ListAsync(CancellationToken cancellation)
		{
			var calendar = await CalendarAsync(cancellation);
			using var response = await _account.SendAsync(CalDavXml.Report, calendar, "1", CalDavXml.EventsQuery(), CalDavXml.XmlType, cancellation);
			if (response.StatusCode == HttpStatusCode.NotFound)
			{
				// The person deleted the calendar, so it's made again and starts empty
				_setCalendar(null);
				await CalendarAsync(cancellation);
				return Array.Empty<CalendarEntry>();
			}

			EnsureMultiStatus(response);
			var entries = new List<CalendarEntry>();
			foreach (var item in await CalDavXml.ReadAsync(response, cancellation))
				if (item.Found(CalDavXml.CalDav + "calendar-data")?.Value is { Length: > 0 } text && Read(text) is { } found && ReminderTag.FromNotes(found.Notes) is { } tag)
					entries.Add(new CalendarEntry(item.Url.AbsoluteUri, tag, found.Start, found.Title));
			return entries;
		}

		public async Task AddAsync(Reminder reminder, CancellationToken cancellation)
		{
			var address = new Uri(await CalendarAsync(cancellation), reminder.Tag + ".ics");
			using (var response = await PutAsync(address, reminder, cancellation, onlyIfNew: true))
				if (response.StatusCode != HttpStatusCode.PreconditionFailed)
				{
					EnsureSuccess(response);
					return;
				}

			// Already there, as when another device has just added it, or a test is sent again
			using var replaced = await PutAsync(address, reminder, cancellation);
			EnsureSuccess(replaced);
		}

		public async Task ChangeAsync(CalendarEntry entry, Reminder reminder, CancellationToken cancellation)
		{
			using var response = await PutAsync(new Uri(entry.ServiceId), reminder, cancellation);
			EnsureSuccess(response);
		}

		public async Task DeleteAsync(CalendarEntry entry, CancellationToken cancellation)
		{
			using var response = await _account.SendAsync(HttpMethod.Delete, new Uri(entry.ServiceId), null, null, CalDavXml.XmlType, cancellation);
			if (response.StatusCode is not (HttpStatusCode.NotFound or HttpStatusCode.Gone))	// already gone is what was wanted
				EnsureSuccess(response);
		}

		public async Task<SharedChoices?> ReadChoicesAsync(CancellationToken cancellation)
		{
			var calendar = await CalendarAsync(cancellation);
			var description = CalDavXml.CalDav + "calendar-description";
			using var response = await _account.SendAsync(CalDavXml.PropFind, calendar, "0", CalDavXml.PropFindBody(description), CalDavXml.XmlType, cancellation);
			if (response.StatusCode == HttpStatusCode.NotFound)
				return null;								// it's been deleted, and the next step makes it again

			EnsureMultiStatus(response);
			var found = await CalDavXml.ReadAsync(response, cancellation);
			return SharedChoices.Parse(found.FirstOrDefault()?.Found(description)?.Value);			// the one asked about, as depth 0 gives
		}

		/// <summary>On a server that won't keep a calendar's description, the choices simply aren't shared</summary>
		public async Task WriteChoicesAsync(SharedChoices choices, CancellationToken cancellation)
		{
			var text = $"{ReminderTag.NotesLine(ReminderTag.Calendar)} {choices.Format()}";
			if (!await SetDescriptionAsync(await CalendarAsync(cancellation), text, cancellation))
			{
				_setCalendar(null);							// deleted, so it's made again and takes the choices
				await SetDescriptionAsync(await CalendarAsync(cancellation), text, cancellation);
			}
		}

		/// <summary>Deletes the whole calendar with everything in it. It's Purity's own, so nothing else is lost.</summary>
		public async Task DeleteCalendarAsync(CancellationToken cancellation)
		{
			var calendar = _getCalendar() is { } known ? new Uri(known) : await FindOursAsync(cancellation);
			if (calendar == null)
				return;

			using var response = await _account.SendAsync(HttpMethod.Delete, calendar, null, null, CalDavXml.XmlType, cancellation);
			if (response.StatusCode is not (HttpStatusCode.NotFound or HttpStatusCode.Gone))
				EnsureSuccess(response);
			_setCalendar(null);
		}


		/// <summary>The title, notes and start of the first event in a calendar object, or null when it holds none</summary>
		internal (string Title, string? Notes, DateTime Start)? Read(string text)
		{
			string? title = null, notes = null;
			DateTime? start = null;
			var depth = 0;
			var inEvent = false;
			foreach (var line in IcsText.Lines(text))
			{
				var (name, parameters, value) = IcsText.Parse(line);
				if (name == "BEGIN")
				{
					depth++;
					inEvent |= depth == 2 && value.Equals("VEVENT", StringComparison.OrdinalIgnoreCase);
					continue;
				}
				if (name == "END")
				{
					if (inEvent && depth == 2)
						return (title ?? string.Empty, notes, start ?? DateTime.MinValue);
					depth--;
					continue;
				}
				if (!inEvent || depth != 2)
					continue;							// an alert's own description isn't the event's

				if (name == "SUMMARY")
					title = IcsText.Unescaped(value);
				else if (name == "DESCRIPTION")
					notes = IcsText.Unescaped(value);
				else if (name == "DTSTART")
					start = Moment(value, parameters);
			}
			return null;
		}


		/// <summary>
		/// The local time an event starts. A time this can't place, such as a date alone or a zone the
		/// computer doesn't know, gives the earliest time there is, so the next run writes it afresh.
		/// </summary>
		private DateTime Moment(string value, IReadOnlyDictionary<string, string> parameters)
		{
			var utc = value.EndsWith('Z');
			if (!DateTime.TryParseExact(value.TrimEnd('Z'), "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
				return DateTime.MinValue;
			if (utc)
				return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(time, DateTimeKind.Utc), _zone);
			if (!parameters.TryGetValue("TZID", out var zone))
				return time;							// floating, so already on the local clock

			try
			{
				var given = TimeZoneInfo.FindSystemTimeZoneById(zone);
				return TimeZoneInfo.ConvertTime(time, given, _zone);
			}
			catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
			{
				return DateTime.MinValue;
			}
		}

		private Task<HttpResponseMessage> PutAsync(Uri address, Reminder reminder, CancellationToken cancellation, bool onlyIfNew = false)
		{
			var text = new StringBuilder();
			IcsText.Add(text, "BEGIN:VCALENDAR");
			IcsText.Add(text, "VERSION:2.0");
			IcsText.Add(text, "PRODID:" + IcsText.Product);
			var start = InZone(reminder.Moment);
			IcsText.AddEvent(text, reminder, IcsText.AtUtc(start), IcsText.AtUtc(start.Add(IcsText.Length)), _clock.GetUtcNow().UtcDateTime);
			IcsText.Add(text, "END:VCALENDAR");
			return _account.SendAsync(HttpMethod.Put, address, null, text.ToString(), "text/calendar", cancellation, ifNoneMatch: onlyIfNew);
		}

		/// <summary>A server that won't keep descriptions isn't a failure: the calendar's address still carries its tag</summary>
		/// <returns>False when the calendar isn't there</returns>
		private async Task<bool> SetDescriptionAsync(Uri calendar, string text, CancellationToken cancellation)
		{
			using var response = await _account.SendAsync(CalDavXml.PropPatch, calendar, null, CalDavXml.SetDescriptionBody(text), CalDavXml.XmlType, cancellation);
			if (response.StatusCode == HttpStatusCode.NotFound)
				return false;
			if ((int)response.StatusCode is 403 or 405 or 409 or 422 or 501)
				return true;

			EnsureSuccess(response);
			return true;
		}

		private async Task<Uri> CalendarAsync(CancellationToken cancellation)
		{
			if (_getCalendar() is { } known)
				return new Uri(known);

			var calendar = await FindOursAsync(cancellation) ?? await MakeAsync(cancellation);
			_setCalendar(calendar.AbsoluteUri);
			return calendar;
		}

		/// <summary>The calendar another device already made in this account, known by its tag, whatever it's called by now</summary>
		private async Task<Uri?> FindOursAsync(CancellationToken cancellation)
		{
			var home = await _account.HomeAsync(cancellation);
			var description = CalDavXml.CalDav + "calendar-description";
			var kind = CalDavXml.Dav + "resourcetype";
			using var response = await _account.SendAsync(CalDavXml.PropFind, home, "1", CalDavXml.PropFindBody(kind, description), CalDavXml.XmlType, cancellation);
			if (response.StatusCode == HttpStatusCode.NotFound)
			{
				_account.ForgetHome();						// moved, so it's looked for again next time
				throw new CalendarServiceException(CalendarProblem.Refused, "The calendar home isn't there any more");
			}

			EnsureMultiStatus(response);
			foreach (var item in await CalDavXml.ReadAsync(response, cancellation))
			{
				if (CalDavXml.SameAddress(item.Url, home) || item.Found(kind)?.Element(CalDavXml.CalDav + "calendar") == null)
					continue;

				var name = item.Url.Segments[^1].TrimEnd('/');
				if (ReminderTag.FromNotes(item.Found(description)?.Value) == ReminderTag.Calendar || name.StartsWith(ReminderTag.Calendar, StringComparison.Ordinal))
					return CalDavXml.AsFolder(item.Url);
			}
			return null;
		}

		/// <summary>
		/// A new calendar for events, at an address that starts with the calendar's tag. The rest is
		/// new every time, since some servers never let a deleted calendar's address be used again.
		/// </summary>
		private async Task<Uri> MakeAsync(CancellationToken cancellation)
		{
			var home = await _account.HomeAsync(cancellation);
			var calendar = new Uri(home, $"{ReminderTag.Calendar}-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4))}/");
			var mark = ReminderTag.NotesLine(ReminderTag.Calendar);
			using (var response = await _account.SendAsync(CalDavXml.MkCalendar, calendar, null, CalDavXml.MakeCalendarBody(_calendarName(), mark), CalDavXml.XmlType, cancellation))
				EnsureSuccess(response);

			// Not every server keeps a description it's given when the calendar is made
			var description = CalDavXml.CalDav + "calendar-description";
			using var check = await _account.SendAsync(CalDavXml.PropFind, calendar, "0", CalDavXml.PropFindBody(description), CalDavXml.XmlType, cancellation);
			if (check.StatusCode == HttpStatusCode.MultiStatus)
			{
				var found = await CalDavXml.ReadAsync(check, cancellation);
				if (ReminderTag.FromNotes(found.FirstOrDefault()?.Found(description)?.Value) == ReminderTag.Calendar)
					return calendar;
			}
			await SetDescriptionAsync(calendar, mark, cancellation);
			return calendar;
		}

		private DateTime InZone(DateTime moment) => new DateTimeOffset(moment, _zone.GetUtcOffset(moment)).UtcDateTime;

		private static void EnsureMultiStatus(HttpResponseMessage response)
		{
			if (response.StatusCode != HttpStatusCode.MultiStatus)
				throw new CalendarServiceException(CalendarProblem.Refused, string.Create(CultureInfo.InvariantCulture, $"The server answered {(int)response.StatusCode}, not with a list"));
		}

		private static void EnsureSuccess(HttpResponseMessage response)
		{
			if (!response.IsSuccessStatusCode)
				throw new CalendarServiceException(CalendarProblem.Refused, string.Create(CultureInfo.InvariantCulture, $"The server answered {(int)response.StatusCode}"));
		}


		private readonly CalDavAccount _account;
		private readonly Func<string?> _getCalendar;
		private readonly Action<string?> _setCalendar;
		private readonly Func<string> _calendarName;
		private readonly TimeZoneInfo _zone;
		private readonly TimeProvider _clock;
	}
}
