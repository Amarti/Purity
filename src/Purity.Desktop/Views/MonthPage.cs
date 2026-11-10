using System.Globalization;
using Avalonia.Media;
using Purity.Core;
using Purity.Desktop.Localization;


namespace Purity.Desktop.Views
{
	/// <summary>One month of the grid, and what the grid needs to know to draw it, in either calendar</summary>
	internal abstract class MonthPage
	{
		public static MonthPage Of(DateTime day, CalendarKind calendar) =>
			calendar == CalendarKind.Hebrew ? new Hebrew(HebrewMonth.Of(day)) : new Gregorian(day.Year, day.Month);


		public abstract string Title { get; }
		public abstract int DayCount { get; }
		public abstract FlowDirection Direction { get; }
		public abstract IReadOnlyList<string> Weekdays { get; }
		public abstract MonthPage? Previous { get; }
		public abstract MonthPage? Next { get; }
		public abstract MonthPage? PreviousYear { get; }
		public abstract MonthPage? NextYear { get; }

		/// <summary>The Gregorian day a date of this month stands for. For a Hebrew date, the one holding its daytime.</summary>
		public abstract DateTime Day(int day);

		/// <summary>What the date's cell says</summary>
		public abstract string Label(int day);

		/// <summary>What a screen reader says for the date</summary>
		public abstract string Name(int day);


		private static readonly string[] HebrewWeekdays = { "א׳", "ב׳", "ג׳", "ד׳", "ה׳", "ו׳", "ש׳" };
		private static readonly string[] EnglishWeekdays = { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };


		/// <summary>Weeks start on the right, as on a Hebrew calendar, in every language, unless Hebrew dates are being written in English letters</summary>
		private sealed class Hebrew(HebrewMonth month) : MonthPage
		{
			public override string Title => InEnglish ? month.EnglishTitle : month.Title;
			public override int DayCount => month.DayCount;
			public override FlowDirection Direction => InEnglish ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
			public override IReadOnlyList<string> Weekdays => InEnglish ? EnglishWeekdays : HebrewWeekdays;
			public override MonthPage? Previous => Page(month.Previous);
			public override MonthPage? Next => Page(month.Next);
			public override MonthPage? PreviousYear => Page(month.InYear(month.Year - 1));
			public override MonthPage? NextYear => Page(month.InYear(month.Year + 1));
			public override DateTime Day(int day) => month.Daytime(day);
			public override string Label(int day) => InEnglish ? day.ToString(CultureInfo.InvariantCulture) : DateText.HebrewDayName(day);
			public override string Name(int day) => $"{Label(day)} {(InEnglish ? month.EnglishName : month.Name)}";

			private static bool InEnglish => Loc.Instance.HebrewInEnglish;

			private static MonthPage? Page(HebrewMonth? other) => other == null ? null : new Hebrew(other);
		}


		/// <summary>In the language of the app, and running the way it runs</summary>
		private sealed class Gregorian(int year, int month) : MonthPage
		{
			public override string Title => new DateTime(year, month, 1).ToString("MMMM yyyy", Loc.Instance.DateCulture);
			public override int DayCount => DateTime.DaysInMonth(year, month);
			public override FlowDirection Direction => Loc.Instance.FlowDirection;
			public override IReadOnlyList<string> Weekdays => Loc.Instance.Language == Language.Hebrew ? HebrewWeekdays : EnglishWeekdays;
			public override MonthPage? Previous => month == 1 ? Page(year - 1, 12) : Page(year, month - 1);
			public override MonthPage? Next => month == 12 ? Page(year + 1, 1) : Page(year, month + 1);
			public override MonthPage? PreviousYear => Page(year - 1, month);
			public override MonthPage? NextYear => Page(year + 1, month);
			public override DateTime Day(int day) => new(year, month, day);
			public override string Label(int day) => day.ToString(CultureInfo.InvariantCulture);
			public override string Name(int day) => Day(day).ToString("d MMMM", Loc.Instance.DateCulture);

			private static MonthPage? Page(int year, int month) =>
				year >= DateTime.MinValue.Year && year <= DateTime.MaxValue.Year ? new Gregorian(year, month) : null;
		}
	}
}
