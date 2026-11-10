using System;
using System.Globalization;


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

		/// <summary>The Hebrew date and onah a stamp stands for. A night belongs to the Hebrew date it opens, the one whose daytime is the next Gregorian day.</summary>
		public static HebrewOnah ToHebrew(DateTimeOffset stamp)
		{
			var day = HebrewDaytime(stamp);
			return new HebrewOnah(Calendar.GetYear(day), Calendar.GetMonth(day), Calendar.GetDayOfMonth(day), PurityEvent.IsDateAfterDusk(stamp));
		}

		/// <summary>The stamp of a Hebrew date and onah. Its night is stored on the Gregorian day before its daytime.</summary>
		/// <exception cref="ArgumentOutOfRangeException">The Hebrew calendar has no such date</exception>
		public static DateTimeOffset FromHebrew(HebrewOnah onah)
		{
			return FromHebrewDaytime(Calendar.ToDateTime(onah.Year, onah.Month, onah.Day, 0, 0, 0, 0), onah.IsNight);
		}

		/// <summary>The Gregorian day holding the daytime of the Hebrew date a stamp's onah belongs to</summary>
		public static DateTime HebrewDaytime(DateTimeOffset stamp) => (PurityEvent.IsDateAfterDusk(stamp) ? stamp.AddDays(1) : stamp).DateTime.Date;

		/// <summary>The stamp of one onah of the Hebrew date whose daytime is on this Gregorian day</summary>
		public static DateTimeOffset FromHebrewDaytime(DateTime daytime, bool night) => night ? FromDate(daytime.AddDays(-1), true) : FromDate(daytime, false);

		/// <summary>Whether .NET's Hebrew calendar reaches this stamp, which covers 1583 to 2239</summary>
		public static bool IsInHebrewCalendar(DateTimeOffset stamp) =>
			stamp.DateTime >= Calendar.MinSupportedDateTime && stamp.DateTime.Date < Calendar.MaxSupportedDateTime.Date;


		private static readonly HebrewCalendar Calendar = new();
	}


	/// <summary>One onah, named by its Hebrew date</summary>
	/// <param name="Year">The Hebrew year</param>
	/// <param name="Month">As .NET numbers them: Tishrei is 1, and in a leap year Adar I is 6 and Adar II is 7</param>
	/// <param name="Day">The day of the month</param>
	/// <param name="IsNight">The night that opens this date, rather than its daytime</param>
	public readonly record struct HebrewOnah(int Year, int Month, int Day, bool IsNight);
}
