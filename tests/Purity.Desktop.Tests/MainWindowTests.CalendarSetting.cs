using Purity.Core;
using Purity.Desktop.Localization;
using Purity.Desktop.ViewModels;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>The calendar choices in Settings: they show at once, OK keeps them, and leaving any other way puts them back</summary>
	public sealed partial class MainWindowTests
	{
		private static readonly DateTimeOffset NightOf15Shevat = new(2034, 2, 3, 12, 0, 0, TimeSpan.Zero);


		[Fact]
		public Task ChoosingHebrewOnlyShowsAtOnceAndOkKeepsIt() => BackToEnglishAfter(async () =>
		{
			var window = await OpenFromSettingsWithOneChangeAsync();
			var period = window.Periods[1];
			period.Period.Begin = NightOf15Shevat;
			_dialogs.InSettings = settings =>
			{
				Assert.Equal(CalendarKind.Gregorian, settings.Calendar);

				settings.CalendarIndex = (int)CalendarKind.Hebrew;

				Assert.True(window.UsesHebrewCalendar);
				Assert.Equal("ט\"ו שבט תשצ\"ד", period.BeginText);
				Assert.Equal(CalendarKind.Gregorian, DesktopSettings.Load().Calendar);		// nothing is saved before OK
				return true;
			};

			await window.OpenSettingsCommand.ExecuteAsync(null);

			Assert.Equal(CalendarKind.Hebrew, DesktopSettings.Load().Calendar);
			Assert.Equal(_dataFile, DesktopSettings.Load().DataFilePath);
			Assert.Empty(_dialogs.Shown);

			Loc.Instance.UseCalendar(CalendarKind.Gregorian, false);
			using var next = new MainWindowViewModel(_dialogs, null, new FixedClock(), _pending.Enqueue);
			Assert.True(next.UsesHebrewCalendar);
		});

		[Fact]
		public Task CancelPutsTheCalendarAndLettersBack() => BackToEnglishAfter(async () =>
		{
			var window = await OpenFromSettingsWithOneChangeAsync();
			var period = window.Periods[1];
			period.Period.Begin = NightOf15Shevat;
			_dialogs.InSettings = settings =>
			{
				settings.CalendarIndex = (int)CalendarKind.Hebrew;
				settings.HebrewInEnglish = true;
				Assert.Equal("15 Shevat 5794", period.BeginText);
				return false;
			};

			await window.OpenSettingsCommand.ExecuteAsync(null);

			Assert.False(window.UsesHebrewCalendar);
			Assert.False(Loc.Instance.HebrewInEnglish);
			Assert.Equal("Friday, 3 February 2034", period.BeginText);
			Assert.Equal(CalendarKind.Gregorian, DesktopSettings.Load().Calendar);
			Assert.False(DesktopSettings.Load().HebrewDatesInEnglish);
		});

		[Fact]
		public Task EnglishLettersReachEveryPlaceAHebrewDateShows() => BackToEnglishAfter(async () =>
		{
			var window = await OpenFromSettingsWithOneChangeAsync();
			var period = window.Periods[1];
			period.Period.Begin = NightOf15Shevat;
			_dialogs.InSettings = settings =>
			{
				settings.HebrewInEnglish = true;
				return true;
			};

			await window.OpenSettingsCommand.ExecuteAsync(null);

			Assert.True(DesktopSettings.Load().HebrewDatesInEnglish);
			Assert.Equal("15 Shevat, night", period.BeginInOtherCalendar);
			Assert.NotEmpty(period.EventRows);
			Assert.All(period.EventRows, row =>
			{
				Assert.Equal(DateText.HebrewOfOnahInEnglish(row.Event.Stamp), row.HebrewText);
				Assert.Equal(85, row.HebrewWidth);
			});
			Assert.Equal(DateText.HebrewOfOnahInEnglish(window.NextEvent!.Stamp), window.NextRow!.HebrewText);

			_dialogs.InSettings = settings =>
			{
				settings.CalendarIndex = (int)CalendarKind.Hebrew;
				return true;
			};
			await window.OpenSettingsCommand.ExecuteAsync(null);

			Assert.Equal("15 Shevat 5794", period.BeginText);
			Assert.Equal("Night of Friday 3 to Saturday 4 February", period.BeginInOtherCalendar);
		});

		/// <summary>The date stays in English letters in every interface language, and the rest of the text follows the language</summary>
		[Fact]
		public Task EnglishLettersStayInTheHebrewInterface() => BackToEnglishAfter(async () =>
		{
			var period = (await OpenFromSettingsWithOneChangeAsync()).Periods[1];
			period.Period.Begin = NightOf15Shevat;

			Loc.Instance.Use(Language.Hebrew);
			Loc.Instance.UseCalendar(CalendarKind.Gregorian, true);

			Assert.Equal(Loc.LeftToRightMark + "15 Shevat, לילה", period.BeginInOtherCalendar);
		});

		[Fact]
		public Task HebrewOnlyRowsDropTheGregorianDateButKeepTheInterval() => BackToEnglishAfter(() =>
		{
			var flaga = new PurityEvent(PurityEventType.VesetAflaga, NightOf15Shevat, "(35)");
			var mikveh = new PurityEvent(PurityEventType.Mikveh, NightOf15Shevat);

			Assert.Equal(flaga.StampGrgRepr, new EventRow(flaga, false, null).DetailText);
			Assert.Equal(60, new EventRow(flaga, false, null).HebrewWidth);

			Loc.Instance.UseCalendar(CalendarKind.Hebrew, false);

			Assert.Equal("(35)", new EventRow(flaga, false, null).DetailText);
			Assert.Equal(string.Empty, new EventRow(mikveh, false, null).DetailText);
			Assert.Equal("ט\"ו שבט", new EventRow(mikveh, false, null).HebrewText);
			return Task.CompletedTask;
		});

		[Fact]
		public Task DatesInDialogsAndTheTodayLineFollowTheCalendar() => BackToEnglishAfter(() =>
		{
			var day = new DateTime(2034, 2, 4);
			var moment = new DateTimeOffset(2034, 2, 4, 14, 30, 0, TimeSpan.Zero);

			Assert.Equal("Friday, 3 February 2034", Loc.Instance.LongDate(NightOf15Shevat));
			Assert.Equal("3 February 2034", Loc.Instance.Date(NightOf15Shevat));
			Assert.Equal("Saturday, 4 February 2034", Loc.Instance.LongDay(day));
			Assert.Equal("4 February 2034, 14:30", Loc.Instance.Moment(moment, withWeekday: false));
			Assert.Equal("Saturday, 4 February 2034, 14:30", Loc.Instance.Moment(moment, withWeekday: true));

			Loc.Instance.UseCalendar(CalendarKind.Hebrew, false);
			Assert.Equal("ט\"ו שבט תשצ\"ד", Loc.Instance.LongDate(NightOf15Shevat));
			Assert.Equal("ט\"ו שבט תשצ\"ד", Loc.Instance.Date(NightOf15Shevat));
			Assert.Equal("ט\"ו שבט תשצ\"ד", Loc.Instance.LongDay(day));
			Assert.Equal("ט\"ו שבט תשצ\"ד, 14:30", Loc.Instance.Moment(moment, withWeekday: true));

			Loc.Instance.UseCalendar(CalendarKind.Hebrew, true);
			Assert.Equal("15 Shevat 5794", Loc.Instance.LongDate(NightOf15Shevat));
			Assert.Equal("15 Shevat 5794", Loc.Instance.LongDay(day));
			Assert.Equal("15 Shevat 5794, 14:30", Loc.Instance.Moment(moment, withWeekday: false));
			return Task.CompletedTask;
		});

		[Fact]
		public Task TheListOfEarlierVersionsFollowsTheCalendar() => BackToEnglishAfter(async () =>
		{
			new DesktopSettings { Calendar = CalendarKind.Hebrew, HebrewDatesInEnglish = true }.Save();
			var window = await OpenWithOneChangeAsync();
			IReadOnlyList<BackupChoice>? listed = null;
			_dialogs.InSettings = settings =>
			{
				settings.RequestRestore();
				return false;
			};
			_dialogs.ChooseBackup = choices =>
			{
				listed = choices;
				return null;
			};

			await window.OpenSettingsCommand.ExecuteAsync(null);

			Assert.NotEmpty(listed!);
			Assert.All(listed!, choice => Assert.Matches(@"^Until \d+ [A-Z][a-z]+( I+)? 5\d{3}, \d\d:\d\d: ", choice.Label));
		});

		[Fact]
		public Task ChangingTheLanguageKeepsTheCalendarChoice() => BackToEnglishAfter(async () =>
		{
			var window = await OpenFromSettingsWithOneChangeAsync();
			_dialogs.InSettings = settings =>
			{
				settings.CalendarIndex = (int)CalendarKind.Hebrew;

				settings.IsHebrew = true;

				Assert.Equal((int)CalendarKind.Hebrew, settings.CalendarIndex);
				Assert.Equal(UiText.Hebrew.HebrewOnly, settings.CalendarChoices[1]);
				return false;
			};

			await window.OpenSettingsCommand.ExecuteAsync(null);
		});

		[Fact]
		public void AnUnknownSavedCalendarMeansGregorian() =>
			Assert.Equal(CalendarKind.Gregorian, new DesktopSettings { CalendarText = "Klingon" }.Calendar);
	}
}
