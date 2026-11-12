using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;


namespace Purity.Reminders
{
	/// <summary>
	/// The reminders as a calendar file (RFC 5545), a one-time copy for any calendar app. Times carry
	/// no time zone, so 08:00 stays 08:00 wherever the calendar is opened. Nothing in the file names
	/// the app or says what the entries are about, beyond the titles themselves.
	/// </summary>
	public static class IcsFile
	{
		/// <param name="reminders">The reminders to include</param>
		/// <param name="calendarName">The name a calendar app shows for them</param>
		/// <param name="utcNow">When the file is being made</param>
		public static string Write(IEnumerable<Reminder> reminders, string calendarName, DateTime utcNow)
		{
			var text = new StringBuilder();
			IcsText.Add(text, "BEGIN:VCALENDAR");
			IcsText.Add(text, "VERSION:2.0");
			IcsText.Add(text, "PRODID:" + IcsText.Product);
			IcsText.Add(text, "CALSCALE:GREGORIAN");
			IcsText.Add(text, "METHOD:PUBLISH");
			IcsText.Add(text, "X-WR-CALNAME:" + IcsText.Escaped(calendarName));
			foreach (var reminder in reminders)
				IcsText.AddEvent(text, reminder, IcsText.Floating(reminder.Moment), IcsText.Floating(reminder.Moment.Add(IcsText.Length)), utcNow);
			IcsText.Add(text, "END:VCALENDAR");
			return text.ToString();
		}
	}


	/// <summary>Writing and reading calendar text, for the file and for CalDAV calendars</summary>
	internal static class IcsText
	{
		public const string Product = "-//Personal//Reminders//EN";

		/// <summary>How long an entry lasts. It only has to be seen; the alert is what matters.</summary>
		public static readonly TimeSpan Length = TimeSpan.FromMinutes(15);


		/// <summary>One reminder as an event, with an alert at its very start</summary>
		/// <param name="start">When it starts, as the event's text gives it</param>
		/// <param name="end">When it ends, likewise</param>
		/// <param name="utcNow">When it's being written</param>
		public static void AddEvent(StringBuilder text, Reminder reminder, string start, string end, DateTime utcNow)
		{
			Add(text, "BEGIN:VEVENT");
			Add(text, "UID:" + reminder.Tag);
			Add(text, "DTSTAMP:" + Utc(utcNow));
			Add(text, "DTSTART" + start);
			Add(text, "DTEND" + end);
			Add(text, "SUMMARY:" + Escaped(reminder.Title));
			Add(text, "DESCRIPTION:" + Escaped(ReminderTag.NotesLine(reminder.Tag)));
			Add(text, "CLASS:PRIVATE");
			Add(text, "TRANSP:TRANSPARENT");				// a reminder doesn't make anyone busy
			Add(text, "BEGIN:VALARM");
			Add(text, "ACTION:DISPLAY");
			Add(text, "DESCRIPTION:" + Escaped(reminder.Title));
			Add(text, "TRIGGER:PT0S");
			Add(text, "END:VALARM");
			Add(text, "END:VEVENT");
		}

		/// <summary>A time on the clock of wherever the calendar is opened, as the property's value with its colon</summary>
		public static string Floating(DateTime moment) => ":" + moment.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);

		/// <summary>A moment, as the property's value with its colon</summary>
		public static string AtUtc(DateTime utc) => ":" + Utc(utc);

		public static string Escaped(string value) =>
			value.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r\n", "\\n").Replace("\n", "\\n");

		public static string Unescaped(string value)
		{
			var text = new StringBuilder(value.Length);
			for (var i = 0; i < value.Length; i++)
			{
				if (value[i] != '\\' || i + 1 == value.Length)
				{
					text.Append(value[i]);
					continue;
				}

				var next = value[++i];
				text.Append(next is 'n' or 'N' ? '\n' : next);
			}
			return text.ToString();
		}

		/// <summary>Lines end in CR LF, and a line over 75 bytes continues on the next one, never in the middle of a letter</summary>
		public static void Add(StringBuilder text, string line)
		{
			var bytes = 0;
			foreach (var letter in line.EnumerateRunes())
			{
				if (bytes + letter.Utf8SequenceLength > MaxLineBytes)
				{
					text.Append("\r\n ");
					bytes = 1;								// the space that marks a continued line counts
				}
				text.Append(letter.ToString());
				bytes += letter.Utf8SequenceLength;
			}
			text.Append("\r\n");
		}

		/// <summary>The lines with continued ones joined back, whatever line ends the text uses</summary>
		public static IEnumerable<string> Lines(string text)
		{
			var current = new StringBuilder();
			foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
			{
				if (raw.Length > 0 && raw[0] is ' ' or '\t')
				{
					current.Append(raw, 1, raw.Length - 1);
					continue;
				}

				if (current.Length > 0)
					yield return current.ToString();
				current.Clear().Append(raw);
			}
			if (current.Length > 0)
				yield return current.ToString();
		}

		/// <summary>A line's name, its parameters and its value: "DTSTART;TZID=X:2026…" gives "DTSTART", { TZID: X } and "2026…"</summary>
		public static (string Name, IReadOnlyDictionary<string, string> Parameters, string Value) Parse(string line)
		{
			var quoted = false;
			var colon = -1;
			for (var i = 0; i < line.Length && colon < 0; i++)
			{
				if (line[i] == '"')
					quoted = !quoted;
				else if (line[i] == ':' && !quoted)
					colon = i;
			}

			var head = colon < 0 ? line : line[..colon];
			var parts = head.Split(';');
			var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (var part in parts[1..])
			{
				var equals = part.IndexOf('=');
				if (equals > 0)
					parameters[part[..equals]] = part[(equals + 1)..].Trim('"');
			}
			return (parts[0].ToUpperInvariant(), parameters, colon < 0 ? string.Empty : line[(colon + 1)..]);
		}


		private static string Utc(DateTime utc) => utc.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);


		private const int MaxLineBytes = 75;
	}
}
