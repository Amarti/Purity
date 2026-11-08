using System;


namespace Purity.Core
{
	/// <summary>
	/// The single way a picked date becomes a stamp. Stamps are symbolic, not moments in time, so
	/// the machine's time zone must never reach them: a stamp carrying +03:00 lands a day early in
	/// the Hebrew month calculation.
	/// </summary>
	public static class Stamps
	{
		/// <summary>Builds a stamp from the calendar date the user sees, whatever kind of date the picker returned</summary>
		public static DateTimeOffset FromDate(DateTime pickedDate, bool afterDusk)
		{
			var day = new DateTimeOffset(pickedDate.Year, pickedDate.Month, pickedDate.Day, 0, 0, 0, TimeSpan.Zero);
			return afterDusk ? day.AddHours(12) : day;
		}

		/// <summary>Same, for pickers that return a date with an offset: the date the user saw is the one kept</summary>
		public static DateTimeOffset FromDate(DateTimeOffset pickedDate, bool afterDusk) => FromDate(pickedDate.DateTime, afterDusk);
	}
}
