using System;
using System.Globalization;


namespace Purity.Core
{
	/// <summary>
	/// One month of the Hebrew calendar, for a date picker on any platform. .NET numbers the months
	/// from Tishrei, and a leap year's Adar I moves every later month up by one, so the same month
	/// has different numbers in different years. This is the one place that deals with that.
	/// </summary>
	/// <param name="Year">The Hebrew year</param>
	/// <param name="Month">As .NET numbers it</param>
	public sealed record HebrewMonth(int Year, int Month)
	{
		/// <summary>The first month .NET's Hebrew calendar covers whole: Shevat 5343</summary>
		public static HebrewMonth First { get; } = new(5343, 5);

		/// <summary>The last month .NET's Hebrew calendar covers: Elul 5999</summary>
		public static HebrewMonth Last { get; } = new(5999, 13);

		/// <summary>The month holding this Gregorian day, or the first month for a day before it</summary>
		public static HebrewMonth Of(DateTime day)
		{
			var month = new HebrewMonth(Calendar.GetYear(day), Calendar.GetMonth(day));
			return month.Order < First.Order ? First : month;
		}


		/// <summary>29 or 30</summary>
		public int DayCount => Calendar.GetDaysInMonth(Year, Month);

		/// <summary>The month's name, such as אדר ב׳</summary>
		public string Name => DateText.HebrewMonthName(Year, Month);

		/// <summary>The month and year, such as שבט תשצ"ד</summary>
		public string Title => $"{Name} {DateText.HebrewYearName(Year)}";

		/// <summary>The month's name in English letters, such as Adar II</summary>
		public string EnglishName => DateText.HebrewMonthNameInEnglish(Year, Month);

		/// <summary>The month and year in English letters, such as Shevat 5794</summary>
		public string EnglishTitle => DateText.Invariant($"{EnglishName} {Year}");

		/// <summary>The Gregorian day holding the daytime of this date in the month</summary>
		public DateTime Daytime(int day) => Calendar.ToDateTime(Year, Month, day, 0, 0, 0, 0);

		/// <summary>The month after this one, or null at the end of the calendar</summary>
		public HebrewMonth? Next
		{
			get
			{
				if (this == Last)
					return null;

				return Month < Calendar.GetMonthsInYear(Year) ? new HebrewMonth(Year, Month + 1) : new HebrewMonth(Year + 1, 1);
			}
		}

		/// <summary>The month before this one, or null at the start of the calendar</summary>
		public HebrewMonth? Previous
		{
			get
			{
				if (this == First)
					return null;

				return Month > 1 ? new HebrewMonth(Year, Month - 1) : new HebrewMonth(Year - 1, Calendar.GetMonthsInYear(Year - 1));
			}
		}

		/// <summary>
		/// The same month in another year, or null outside the calendar. Adar and Adar II count as
		/// the same month; Adar I goes to plain Adar in a year that has only one.
		/// </summary>
		public HebrewMonth? InYear(int year)
		{
			if (year < First.Year || year > Last.Year)
				return null;

			var (fromLeap, toLeap) = (Calendar.IsLeapYear(Year), Calendar.IsLeapYear(year));
			var month = fromLeap && !toLeap && Month >= 7 ? Month - 1
				: !fromLeap && toLeap && Month >= 6 ? Month + 1
				: Month;

			var moved = new HebrewMonth(year, month);
			return moved.Order < First.Order ? null : moved;
		}


		private int Order => Year * 100 + Month;

		private static readonly HebrewCalendar Calendar = new();
	}
}
