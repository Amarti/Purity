using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;


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
		internal static readonly string[] GregorianMonths =
		{
			"Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sept", "Oct", "Nov", "Dec",
		};
	}
}
