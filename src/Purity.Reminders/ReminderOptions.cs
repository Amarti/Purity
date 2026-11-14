using System;
using System.Collections.Generic;
using Purity.Core;


namespace Purity.Reminders
{
	public enum ReminderWording
	{
		/// <summary>Names nothing, for lock screens and calendars others can see</summary>
		Discreet,
		Detailed,
	}


	public enum ReminderLanguage
	{
		English,
		Hebrew,
	}


	/// <summary>What to remind about, and how. The defaults are the ones the owner approved.</summary>
	public sealed record ReminderOptions
	{
		public required IReadOnlySet<PurityEventType> Kinds { get; init; }
		public ReminderLanguage Language { get; init; }
		public ReminderWording Wording { get; init; }

		/// <summary>When a mikveh's reminder fires, on the day itself</summary>
		public TimeOnly MikvehTime { get; init; } = new(8, 0);

		/// <summary>When every other reminder fires, on the day it's due</summary>
		public TimeOnly OtherTime { get; init; } = new(17, 0);

		/// <summary>Whether a reminder that would fire around Shabbat or Yom Tov fires on the morning before instead</summary>
		public bool MoveOffShabbat { get; init; } = true;

		/// <summary>Whether Yom Tov is kept for one day, as in Israel, rather than two</summary>
		public bool InIsrael { get; init; } = true;
	}


	/// <param name="Tag">What identifies it as this reminder, on every platform and in every language</param>
	/// <param name="Moment">When it fires, on the local clock</param>
	/// <param name="Title">What it says</param>
	/// <param name="Kind">The kind of date it's about</param>
	public sealed record Reminder(string Tag, DateTime Moment, string Title, PurityEventType Kind);
}
