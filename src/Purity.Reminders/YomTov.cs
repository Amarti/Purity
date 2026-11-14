using System;
using System.Globalization;


namespace Purity.Reminders
{
	/// <summary>
	/// The days of Yom Tov, when work is forbidden as on Shabbat: Rosh Hashana, Yom Kippur, the first
	/// day of Sukkot, Shemini Atzeret, the first and seventh days of Pesach, and Shavuot, with their
	/// second days outside Israel. Chol HaMoed, Purim, Chanukah and the fasts aren't among them. Only
	/// reminders read these; no calculated date depends on them.
	/// </summary>
	public static class YomTov
	{
		/// <summary>Whether a day on the civil calendar is Yom Tov, from its evening before to its own nightfall</summary>
		/// <param name="date">The civil date of its daytime</param>
		/// <param name="inIsrael">One day of each Yom Tov, as kept in Israel, rather than the two kept outside it</param>
		public static bool IsYomTov(DateTime date, bool inIsrael)
		{
			if (date < Calendar.MinSupportedDateTime || date > Calendar.MaxSupportedDateTime)
				return false;

			var month = Calendar.GetMonth(date);
			var day = Calendar.GetDayOfMonth(date);
			var nisan = Calendar.IsLeapYear(Calendar.GetYear(date)) ? 8 : 7;		// a leap year has two Adars before it
			if (month == Tishrei)
				return day is 1 or 2 or 10 or 15 or 22 || (!inIsrael && day is 16 or 23);
			if (month == nisan)
				return day is 15 or 21 || (!inIsrael && day is 16 or 22);
			if (month == nisan + 2)												// Sivan
				return day == 6 || (!inIsrael && day == 7);
			return false;
		}

		/// <summary>Whether work is forbidden on a day: Shabbat or Yom Tov</summary>
		public static bool IsRestDay(DateTime date, bool inIsrael) => date.DayOfWeek == DayOfWeek.Saturday || IsYomTov(date, inIsrael);

		/// <summary>Whether a computer's time zone is Israel's, the first guess at which days are kept</summary>
		public static bool IsIsraelZone(TimeZoneInfo zone) =>
			IsIsraelId(zone.Id) || (TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var iana) && IsIsraelId(iana));


		private static bool IsIsraelId(string id) => id is "Asia/Jerusalem" or "Asia/Tel_Aviv" or "Israel";


		private const int Tishrei = 1;

		private static readonly HebrewCalendar Calendar = new();
	}
}
