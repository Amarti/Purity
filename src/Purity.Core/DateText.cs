using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;


namespace Purity.Core
{
	/// <summary>
	/// All visible date text. Month names used to come from the operating system, which spells them
	/// differently per platform (NLS even shows Adar I as plain Adar). These tables hold the spelling
	/// of the production app on Windows ICU, so every platform shows exactly the same text.
	/// </summary>
	public static class DateText
	{
		/// <summary>Formats numbers without regard to the machine's culture.</summary>
		public static string Invariant(FormattableString text) => FormattableString.Invariant(text);

		public static string EventType(PurityEventType type) => type switch
		{
			PurityEventType.Mikveh => "Mikveh",
			PurityEventType.VesetHodesh => "Veset Hachodesh",
			PurityEventType.VesetAflaga => "Veset Aflaga",
			PurityEventType.OnaBeinonit => "Ona Benonis",
			_ => string.Empty,
		};

		/// <summary>Hebrew date of the stamp's calendar day, such as ט"ו אדר ב׳</summary>
		public static string Hebrew(DateTimeOffset stamp)
		{
			var date = stamp.DateTime;
			var year = Calendar.GetYear(date);
			var months = Calendar.IsLeapYear(year) ? LeapYearMonths : CommonYearMonths;
			return $"{HebrewDays[Calendar.GetDayOfMonth(date) - 1]} {months[Calendar.GetMonth(date) - 1]}";
		}

		/// <summary>Hebrew date of the onah a stamp stands for: a night belongs to the date it opens</summary>
		public static string HebrewOfOnah(DateTimeOffset stamp) => Hebrew(PurityEvent.IsDateAfterDusk(stamp) ? stamp.AddDays(1) : stamp);

		/// <summary>Hebrew date and year of the onah a stamp stands for, such as ט"ו שבט תשצ"ד</summary>
		public static string HebrewOfOnahWithYear(DateTimeOffset stamp) =>
			$"{HebrewOfOnah(stamp)} {HebrewYearName(Calendar.GetYear(Stamps.HebrewDaytime(stamp)))}";

		/// <summary>Hebrew date of the stamp's calendar day in English letters, such as 15 Adar II</summary>
		public static string HebrewInEnglish(DateTimeOffset stamp)
		{
			var date = stamp.DateTime;
			return Invariant($"{Calendar.GetDayOfMonth(date)} {HebrewMonthNameInEnglish(Calendar.GetYear(date), Calendar.GetMonth(date))}");
		}

		/// <summary>The same for the onah a stamp stands for: a night belongs to the date it opens</summary>
		public static string HebrewOfOnahInEnglish(DateTimeOffset stamp) => HebrewInEnglish(PurityEvent.IsDateAfterDusk(stamp) ? stamp.AddDays(1) : stamp);

		/// <summary>The same with the year, such as 15 Shevat 5794</summary>
		public static string HebrewOfOnahInEnglishWithYear(DateTimeOffset stamp) =>
			Invariant($"{HebrewOfOnahInEnglish(stamp)} {Calendar.GetYear(Stamps.HebrewDaytime(stamp))}");

		/// <summary>A month's name in English letters, by the numbers .NET gives it in that year</summary>
		public static string HebrewMonthNameInEnglish(int year, int month) => (Calendar.IsLeapYear(year) ? LeapYearMonthsInEnglish : CommonYearMonthsInEnglish)[month - 1];

		/// <summary>A month's name, by the numbers .NET gives it in that year</summary>
		public static string HebrewMonthName(int year, int month) => (Calendar.IsLeapYear(year) ? LeapYearMonths : CommonYearMonths)[month - 1];

		/// <summary>A day of the month in Hebrew numerals, such as ט"ו</summary>
		public static string HebrewDayName(int day) => HebrewDays[day - 1];

		/// <summary>A Hebrew year in letters without its thousands, such as תשצ"ד for 5794</summary>
		public static string HebrewYearName(int year)
		{
			var letters = new StringBuilder();
			var rest = year % 1000;
			for (; rest >= 400; rest -= 400)
				letters.Append('ת');
			if (rest >= 100)
			{
				letters.Append("קרש"[rest / 100 - 1]);
				rest %= 100;
			}

			if (rest is 15 or 16)
				letters.Append(rest == 15 ? "טו" : "טז");	// never spelled as the letters of God's name
			else
			{
				if (rest >= 10)
					letters.Append("יכלמנסעפצ"[rest / 10 - 1]);
				if (rest % 10 > 0)
					letters.Append("אבגדהוזחט"[rest % 10 - 1]);
			}

			return letters.Length == 1 ? letters.Append('\'').ToString() : letters.Insert(letters.Length - 1, '"').ToString();
		}

		/// <summary>Short Gregorian date, such as 4 Sept</summary>
		public static string Gregorian(DateTimeOffset stamp) => Gregorian(stamp, GregorianMonths);

		/// <summary>The same with another language's month names, for the screen only</summary>
		public static string Gregorian(DateTimeOffset stamp, IReadOnlyList<string> months) => Invariant($"{stamp.Day} {months[stamp.Month - 1]}");

		/// <summary>
		/// This is a temporary solution for bug in Avalonia (v.10) that reverses order of hebrew words in the string<br/>
		/// It also reverses letters of the first word (day, since it always contains " sign, which confuses Avalonia even further)
		/// </summary>
		public static string ReversedForOldAvalonia(string text)
		{
			var tks = text.Split(' ');
			tks[0] = new string(Enumerable.Reverse(tks[0]).ToArray());
			return string.Join(' ', Enumerable.Reverse(tks));
		}


		private static readonly HebrewCalendar Calendar = new();

		private static readonly string[] HebrewDays =
		{
			"א'", "ב'", "ג'", "ד'", "ה'", "ו'", "ז'", "ח'", "ט'", "י'",
			"י\"א", "י\"ב", "י\"ג", "י\"ד", "ט\"ו", "ט\"ז", "י\"ז", "י\"ח", "י\"ט", "כ'",
			"כ\"א", "כ\"ב", "כ\"ג", "כ\"ד", "כ\"ה", "כ\"ו", "כ\"ז", "כ\"ח", "כ\"ט", "ל'",
		};
		private static readonly string[] CommonYearMonths =
		{
			"תשרי", "חשוון", "כסלו", "טבת", "שבט", "אדר", "ניסן", "אייר", "סיוון", "תמוז", "אב", "אלול",
		};
		private static readonly string[] LeapYearMonths =
		{
			"תשרי", "חשוון", "כסלו", "טבת", "שבט", "אדר א׳", "אדר ב׳", "ניסן", "אייר", "סיוון", "תמוז", "אב", "אלול",
		};
		private static readonly string[] CommonYearMonthsInEnglish =
		{
			"Tishrei", "Cheshvan", "Kislev", "Tevet", "Shevat", "Adar", "Nisan", "Iyar", "Sivan", "Tammuz", "Av", "Elul",
		};
		private static readonly string[] LeapYearMonthsInEnglish =
		{
			"Tishrei", "Cheshvan", "Kislev", "Tevet", "Shevat", "Adar I", "Adar II", "Nisan", "Iyar", "Sivan", "Tammuz", "Av", "Elul",
		};
		internal static readonly string[] GregorianMonths =
		{
			"Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sept", "Oct", "Nov", "Dec",
		};
	}
}
