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
			Add(text, "BEGIN:VCALENDAR");
			Add(text, "VERSION:2.0");
			Add(text, "PRODID:-//Personal//Reminders//EN");
			Add(text, "CALSCALE:GREGORIAN");
			Add(text, "METHOD:PUBLISH");
			Add(text, "X-WR-CALNAME:" + Escaped(calendarName));
			foreach (var reminder in reminders)
			{
				Add(text, "BEGIN:VEVENT");
				Add(text, "UID:" + reminder.Tag);
				Add(text, "DTSTAMP:" + utcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture));
				Add(text, "DTSTART:" + Local(reminder.Moment));
				Add(text, "DTEND:" + Local(reminder.Moment.Add(Length)));
				Add(text, "SUMMARY:" + Escaped(reminder.Title));
				Add(text, "DESCRIPTION:" + Escaped(ReminderTag.NotesLine(reminder.Tag)));
				Add(text, "CLASS:PRIVATE");
				Add(text, "TRANSP:TRANSPARENT");			// a reminder doesn't make anyone busy
				Add(text, "BEGIN:VALARM");
				Add(text, "ACTION:DISPLAY");
				Add(text, "DESCRIPTION:" + Escaped(reminder.Title));
				Add(text, "TRIGGER:PT0S");
				Add(text, "END:VALARM");
				Add(text, "END:VEVENT");
			}
			Add(text, "END:VCALENDAR");
			return text.ToString();
		}


		private static string Local(DateTime moment) => moment.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);

		private static string Escaped(string value) =>
			value.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r\n", "\\n").Replace("\n", "\\n");

		/// <summary>Lines end in CR LF, and a line over 75 bytes continues on the next one, never in the middle of a letter</summary>
		private static void Add(StringBuilder text, string line)
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


		private const int MaxLineBytes = 75;
		private static readonly TimeSpan Length = TimeSpan.FromMinutes(15);
	}
}
