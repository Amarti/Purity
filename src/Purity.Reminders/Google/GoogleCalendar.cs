using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;


namespace Purity.Reminders.Google
{
	/// <summary>
	/// The calendar Purity makes in a Google account. An entry's ID at Google is its tag, so a call
	/// that's repeated can't make a second copy.
	/// </summary>
	public sealed class GoogleCalendar : ICalendarService, IChoiceStore
	{
		/// <param name="account">The sign-in</param>
		/// <param name="getCalendarId">The calendar's ID at Google, or null before one is made</param>
		/// <param name="setCalendarId">Keeps the calendar's ID on this device</param>
		/// <param name="calendarName">The name a new calendar gets</param>
		/// <param name="zone">The local time zone, which turns a local time into a moment</param>
		/// <param name="wait">Waits before trying again</param>
		public GoogleCalendar(GoogleAccount account, Func<string?> getCalendarId, Action<string?> setCalendarId, Func<string> calendarName, TimeZoneInfo zone, Func<TimeSpan, CancellationToken, Task>? wait = null)
		{
			_account = account;
			_getCalendarId = getCalendarId;
			_setCalendarId = setCalendarId;
			_calendarName = calendarName;
			_zone = zone;
			_wait = wait ?? Task.Delay;
		}


		public async Task<IReadOnlyList<CalendarEntry>> ListAsync(CancellationToken cancellation)
		{
			var calendar = await CalendarIdAsync(cancellation);
			var entries = new List<CalendarEntry>();
			string? page = null;
			do
			{
				var path = $"{EventsPath(calendar)}?maxResults={PageSize}{(page == null ? string.Empty : "&pageToken=" + Uri.EscapeDataString(page))}";
				using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, Url(path)), cancellation);
				if (response.StatusCode == HttpStatusCode.NotFound)
				{
					// The person deleted the calendar at Google, so it's made again and starts empty
					_setCalendarId(null);
					await CalendarIdAsync(cancellation);
					return Array.Empty<CalendarEntry>();
				}

				EnsureSuccess(response);
				var list = await response.Content.ReadFromJsonAsync(GoogleJson.Default.GoogleEventList, cancellation);
				foreach (var item in list?.Items ?? new List<GoogleEvent>())
					if (ReminderTag.FromNotes(item.Description) is { } tag && item.Start?.DateTime is { } start && item.Id != null)
						entries.Add(new CalendarEntry(item.Id, tag, TimeZoneInfo.ConvertTime(start, _zone).DateTime, item.Summary ?? string.Empty));
				page = list?.NextPageToken;
			}
			while (page != null);

			return entries;
		}

		public async Task AddAsync(Reminder reminder, CancellationToken cancellation)
		{
			var calendar = await CalendarIdAsync(cancellation);
			var body = Event(reminder);
			body.Id = reminder.Tag;
			using var response = await SendAsync(() => WithBody(HttpMethod.Post, Url(EventsPath(calendar)), body), cancellation);
			if (response.StatusCode == HttpStatusCode.Conflict)
			{
				// Google keeps a deleted entry's ID for a while, so a reminder that comes back is restored
				await PutAsync(calendar, reminder, cancellation);
				return;
			}

			EnsureSuccess(response);
		}

		public async Task ChangeAsync(CalendarEntry entry, Reminder reminder, CancellationToken cancellation) =>
			await PutAsync(await CalendarIdAsync(cancellation), reminder, cancellation);

		public async Task DeleteAsync(CalendarEntry entry, CancellationToken cancellation)
		{
			var calendar = await CalendarIdAsync(cancellation);
			using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Delete, Url($"{EventsPath(calendar)}/{Uri.EscapeDataString(entry.ServiceId)}")), cancellation);
			if (response.StatusCode is not (HttpStatusCode.NotFound or HttpStatusCode.Gone))	// already gone is what was wanted
				EnsureSuccess(response);
		}

		public async Task<SharedChoices?> ReadChoicesAsync(CancellationToken cancellation)
		{
			var calendar = await CalendarIdAsync(cancellation);
			using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, Url($"calendars/{Uri.EscapeDataString(calendar)}")), cancellation);
			if (response.StatusCode == HttpStatusCode.NotFound)
				return null;								// it's been deleted at Google, and the next step makes it again

			EnsureSuccess(response);
			var found = await response.Content.ReadFromJsonAsync(GoogleJson.Default.GoogleCalendarResource, cancellation);
			return SharedChoices.Parse(found?.Description);
		}

		public async Task WriteChoicesAsync(SharedChoices choices, CancellationToken cancellation)
		{
			var calendar = await CalendarIdAsync(cancellation);
			var response = await PatchChoicesAsync(calendar, choices, cancellation);
			if (response.StatusCode == HttpStatusCode.NotFound)
			{
				response.Dispose();							// deleted at Google, so it's made again and takes the choices
				_setCalendarId(null);
				response = await PatchChoicesAsync(await CalendarIdAsync(cancellation), choices, cancellation);
			}

			using (response)
				EnsureSuccess(response);
		}

		/// <summary>Deletes the whole calendar with everything in it. It's Purity's own, so nothing else is lost.</summary>
		public async Task DeleteCalendarAsync(CancellationToken cancellation)
		{
			if (_getCalendarId() is not { } calendar)
				return;

			using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Delete, Url($"calendars/{Uri.EscapeDataString(calendar)}")), cancellation);
			if (response.StatusCode is not (HttpStatusCode.NotFound or HttpStatusCode.Gone))
				EnsureSuccess(response);
			_setCalendarId(null);
		}


		private Task<HttpResponseMessage> PatchChoicesAsync(string calendar, SharedChoices choices, CancellationToken cancellation)
		{
			var body = new GoogleCalendarResource { Description = $"{ReminderTag.NotesLine(ReminderTag.Calendar)} {choices.Format()}" };
			return SendAsync(() => WithBody(HttpMethod.Patch, Url($"calendars/{Uri.EscapeDataString(calendar)}"), body), cancellation);
		}

		private async Task PutAsync(string calendar, Reminder reminder, CancellationToken cancellation)
		{
			var body = Event(reminder);
			body.Status = "confirmed";						// also what brings a deleted entry back
			using var response = await SendAsync(() => WithBody(HttpMethod.Put, Url($"{EventsPath(calendar)}/{Uri.EscapeDataString(reminder.Tag)}"), body), cancellation);
			EnsureSuccess(response);
		}

		private async Task<string> CalendarIdAsync(CancellationToken cancellation)
		{
			if (_getCalendarId() is { } known)
				return known;

			var id = await FindOursAsync(cancellation) ?? await MakeAsync(cancellation);
			_setCalendarId(id);
			return id;
		}

		/// <summary>The calendar another device already made in this account, known by its tag, whatever it's called by now</summary>
		private async Task<string?> FindOursAsync(CancellationToken cancellation)
		{
			string? page = null;
			do
			{
				var path = $"users/me/calendarList?maxResults={CalendarPageSize}{(page == null ? string.Empty : "&pageToken=" + Uri.EscapeDataString(page))}";
				using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, Url(path)), cancellation);
				EnsureSuccess(response);
				var list = await response.Content.ReadFromJsonAsync(GoogleJson.Default.GoogleCalendarList, cancellation);
				foreach (var item in list?.Items ?? new List<GoogleCalendarResource>())
					if (item.Id != null && item.Deleted != true && ReminderTag.FromNotes(item.Description) == ReminderTag.Calendar)
						return item.Id;
				page = list?.NextPageToken;
			}
			while (page != null);

			return null;
		}

		private async Task<string> MakeAsync(CancellationToken cancellation)
		{
			var mark = ReminderTag.NotesLine(ReminderTag.Calendar);
			var body = new GoogleCalendarResource { Summary = _calendarName(), Description = mark };
			using var response = await SendAsync(() => WithBody(HttpMethod.Post, Url("calendars"), body), cancellation);
			EnsureSuccess(response);
			var made = await response.Content.ReadFromJsonAsync(GoogleJson.Default.GoogleCalendarResource, cancellation);
			var id = made?.Id ?? throw new CalendarServiceException(CalendarProblem.Refused, "Google didn't give the new calendar an ID");
			if (made.Description == mark)
				return id;

			// Google only promises to take the name when a calendar is made, so the tag may need a second call
			var addition = new GoogleCalendarResource { Description = mark };
			using var marked = await SendAsync(() => WithBody(HttpMethod.Patch, Url($"calendars/{Uri.EscapeDataString(id)}"), addition), cancellation);
			EnsureSuccess(marked);
			return id;
		}

		private GoogleEvent Event(Reminder reminder) => new()
		{
			Summary = reminder.Title,
			Description = ReminderTag.NotesLine(reminder.Tag),
			Visibility = "private",
			Transparency = "transparent",					// a reminder doesn't make anyone busy
			Start = new GoogleTime { DateTime = InZone(reminder.Moment) },
			End = new GoogleTime { DateTime = InZone(reminder.Moment.Add(Length)) },
			Reminders = new GoogleReminders
			{
				UseDefault = false,
				Overrides = new List<GoogleReminder> { new() { Method = "popup", Minutes = 0 } },
			},
		};

		private DateTimeOffset InZone(DateTime moment) => new(moment, _zone.GetUtcOffset(moment));

		private Uri Url(string path) => new(_account.Endpoints.Calendar, path);

		private static string EventsPath(string calendar) => $"calendars/{Uri.EscapeDataString(calendar)}/events";

		private static HttpRequestMessage WithBody(HttpMethod method, Uri url, GoogleCalendarResource body) =>
			new(method, url) { Content = JsonContent.Create(body, GoogleJson.Default.GoogleCalendarResource) };

		private static HttpRequestMessage WithBody(HttpMethod method, Uri url, GoogleEvent body) =>
			new(method, url) { Content = JsonContent.Create(body, GoogleJson.Default.GoogleEvent) };

		/// <summary>Sends a call with a good token, renewing it once and waiting out the failures Google says to wait out</summary>
		private async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> makeRequest, CancellationToken cancellation)
		{
			var renewed = false;
			for (var attempt = 0; ; attempt++)
			{
				using var request = makeRequest();
				request.Headers.Authorization = new("Bearer", await _account.AccessTokenAsync(cancellation));
				var response = await _account.SendAsync(request, cancellation);
				if (response.StatusCode == HttpStatusCode.Unauthorized)
				{
					response.Dispose();
					if (renewed)
						throw new CalendarServiceException(CalendarProblem.SignInNeeded, "Google turned the sign-in down");

					renewed = true;
					_account.ForgetAccessToken();
					continue;
				}

				if (attempt >= Retries || !await IsWorthWaitingAsync(response, cancellation))
					return response;

				response.Dispose();
				await _wait(TimeSpan.FromSeconds(1 << attempt), cancellation);
			}
		}

		private static async Task<bool> IsWorthWaitingAsync(HttpResponseMessage response, CancellationToken cancellation)
		{
			if ((int)response.StatusCode is 429 or >= 500)
				return true;
			if (response.StatusCode != HttpStatusCode.Forbidden)
				return false;

			var body = await response.Content.ReadAsStringAsync(cancellation);
			return body.Contains("rateLimitExceeded", StringComparison.Ordinal) || body.Contains("userRateLimitExceeded", StringComparison.Ordinal);
		}

		private static void EnsureSuccess(HttpResponseMessage response)
		{
			if (!response.IsSuccessStatusCode)
				throw new CalendarServiceException(CalendarProblem.Refused, string.Create(CultureInfo.InvariantCulture, $"Google answered {(int)response.StatusCode}"));
		}


		private const int PageSize = 2500;
		private const int CalendarPageSize = 250;
		private const int Retries = 3;
		private static readonly TimeSpan Length = TimeSpan.FromMinutes(15);

		private readonly GoogleAccount _account;
		private readonly Func<string?> _getCalendarId;
		private readonly Action<string?> _setCalendarId;
		private readonly Func<string> _calendarName;
		private readonly TimeZoneInfo _zone;
		private readonly Func<TimeSpan, CancellationToken, Task> _wait;
	}


	internal sealed class GoogleTokens
	{
		[JsonPropertyName("access_token")] public string? AccessToken { get; set; }
		[JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
		[JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
	}


	internal sealed class GoogleCalendarResource
	{
		public string? Id { get; set; }
		public string? Summary { get; set; }
		public string? Description { get; set; }
		public bool? Deleted { get; set; }
	}


	internal sealed class GoogleCalendarList
	{
		public List<GoogleCalendarResource>? Items { get; set; }
		public string? NextPageToken { get; set; }
	}


	internal sealed class GoogleEventList
	{
		public List<GoogleEvent>? Items { get; set; }
		public string? NextPageToken { get; set; }
	}


	internal sealed class GoogleEvent
	{
		public string? Id { get; set; }
		public string? Status { get; set; }
		public string? Summary { get; set; }
		public string? Description { get; set; }
		public string? Visibility { get; set; }
		public string? Transparency { get; set; }
		public GoogleTime? Start { get; set; }
		public GoogleTime? End { get; set; }
		public GoogleReminders? Reminders { get; set; }
	}


	internal sealed class GoogleTime
	{
		public DateTimeOffset? DateTime { get; set; }
	}


	internal sealed class GoogleReminders
	{
		public bool UseDefault { get; set; }
		public List<GoogleReminder>? Overrides { get; set; }
	}


	internal sealed class GoogleReminder
	{
		public string? Method { get; set; }
		public int Minutes { get; set; }
	}


	[JsonSerializable(typeof(GoogleTokens))]
	[JsonSerializable(typeof(GoogleCalendarResource))]
	[JsonSerializable(typeof(GoogleEventList))]
	[JsonSerializable(typeof(GoogleCalendarList))]
	[JsonSerializable(typeof(GoogleEvent))]
	[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
	internal sealed partial class GoogleJson : JsonSerializerContext
	{}
}
