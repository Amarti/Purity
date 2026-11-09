using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Purity.Core;


namespace Purity.Desktop.Localization
{
	/// <summary>The app's languages. Saved by name, so their order here doesn't matter.</summary>
	public enum Language
	{
		English,
		Hebrew,
	}


	/// <summary>
	/// The language the app is showing. Windows bind to <see cref="Text"/>, so changing it changes
	/// them at once, with no restart. Only what's shown changes: the calculated dates, the data file
	/// and the engine's own text stay exactly as they are in every language.
	/// </summary>
	public sealed class Loc : ObservableObject
	{
		public static Loc Instance { get; } = new();

		/// <summary>The words of the language being shown, for code</summary>
		public static UiText T => Instance.Text;


		public Language Language { get; private set; }

		/// <summary>The words of the language being shown, for bindings</summary>
		public UiText Text { get; private set; } = UiText.English;

		public FlowDirection FlowDirection => Language == Language.Hebrew ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

		/// <summary>Writes a date for the screen in the language being shown</summary>
		public CultureInfo DateCulture { get; } = (CultureInfo)CultureInfo.InvariantCulture.Clone();

		/// <summary>The Gregorian text of a calculated date, in the language being shown. In English it is the engine's own.</summary>
		public string GregorianText(PurityEvent item) => Language == Language.Hebrew ? item.GregorianText(HebrewDates.ShortMonths) : item.StampGrgRepr;

		/// <summary>
		/// Makes <see cref="DateCulture"/> the culture of the whole app, for the date pickers, which
		/// take their month and day names from the current culture. Called before anything is queued
		/// on the UI thread: Avalonia's dispatcher runs each job with the culture it was queued under, so
		/// changing the current culture later wouldn't reach all of them. Instead there's this one
		/// culture from the start, and <see cref="Use"/> changes its names in place.
		/// </summary>
		public void TakeOverCulture()
		{
			CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = DateCulture;
			CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = DateCulture;
		}

		/// <summary>Shows the app in this language</summary>
		public void Use(Language language)
		{
			if (language == Language)
				return;

			Language = language;
			Text = language == Language.Hebrew ? UiText.Hebrew : UiText.English;
			CopyNames(language == Language.Hebrew ? HebrewDates.Culture.DateTimeFormat : CultureInfo.InvariantCulture.DateTimeFormat);
			OnPropertyChanged(nameof(Language));
			OnPropertyChanged(nameof(Text));
			OnPropertyChanged(nameof(FlowDirection));
		}


		private void CopyNames(DateTimeFormatInfo from)
		{
			var to = DateCulture.DateTimeFormat;
			to.MonthNames = from.MonthNames;
			to.MonthGenitiveNames = from.MonthGenitiveNames;
			to.AbbreviatedMonthNames = from.AbbreviatedMonthNames;
			to.AbbreviatedMonthGenitiveNames = from.AbbreviatedMonthGenitiveNames;
			to.DayNames = from.DayNames;
			to.AbbreviatedDayNames = from.AbbreviatedDayNames;
			to.ShortestDayNames = from.ShortestDayNames;
			to.YearMonthPattern = from.YearMonthPattern;
		}
	}


	/// <summary>
	/// Gregorian month and day names in Hebrew. The app runs without OS culture data, so that no
	/// machine setting can change a date's text, which means these can't come from the system.
	/// </summary>
	internal static class HebrewDates
	{
		/// <summary>As in "4 בספט׳", beside a day number</summary>
		public static IReadOnlyList<string> ShortMonths { get; } = new[]
		{
			"בינו׳", "בפבר׳", "במרץ", "באפר׳", "במאי", "ביוני", "ביולי", "באוג׳", "בספט׳", "באוק׳", "בנוב׳", "בדצמ׳",
		};

		/// <summary>The invariant culture with Hebrew names, so numbers and everything else stay the same</summary>
		public static CultureInfo Culture { get; } = Build();


		private static CultureInfo Build()
		{
			var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
			var format = culture.DateTimeFormat;
			format.MonthNames = Months("ינואר", "פברואר", "מרץ", "אפריל", "מאי", "יוני", "יולי", "אוגוסט", "ספטמבר", "אוקטובר", "נובמבר", "דצמבר");
			format.MonthGenitiveNames = Months("בינואר", "בפברואר", "במרץ", "באפריל", "במאי", "ביוני", "ביולי", "באוגוסט", "בספטמבר", "באוקטובר", "בנובמבר", "בדצמבר");
			format.AbbreviatedMonthNames = Months("ינו׳", "פבר׳", "מרץ", "אפר׳", "מאי", "יוני", "יולי", "אוג׳", "ספט׳", "אוק׳", "נוב׳", "דצמ׳");
			format.AbbreviatedMonthGenitiveNames = Months(ShortMonths.ToArray());
			format.DayNames = new[] { "יום ראשון", "יום שני", "יום שלישי", "יום רביעי", "יום חמישי", "יום שישי", "שבת" };
			format.AbbreviatedDayNames = new[] { "יום א׳", "יום ב׳", "יום ג׳", "יום ד׳", "יום ה׳", "יום ו׳", "שבת" };
			format.ShortestDayNames = new[] { "א׳", "ב׳", "ג׳", "ד׳", "ה׳", "ו׳", "ש׳" };
			format.YearMonthPattern = "MMMM yyyy";
			return culture;
		}

		/// <summary>.NET's month lists have a thirteenth place, for calendars with thirteen months</summary>
		private static string[] Months(params string[] names) => names.Append(string.Empty).ToArray();
	}
}
