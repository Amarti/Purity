using Purity.Desktop.Localization;
using Purity.Desktop.ViewModels;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>Dates picked and shown in the Hebrew calendar: the same stored value, reached from the Hebrew date</summary>
	public sealed partial class MainWindowTests
	{
		private static readonly DateTimeOffset SecondOfFebruaryNight = new(2034, 2, 3, 12, 0, 0, TimeSpan.Zero);
		private static readonly DateTime FifteenShevat = new(2034, 2, 4);
		private static readonly DateTime SixteenShevat = new(2034, 2, 5);

		private static DateTimeOffset Utc(int year, int month, int day, int hour) => new(year, month, day, hour, 0, 0, TimeSpan.Zero);

		private async Task<MainWindowViewModel> OpenInHebrewAsync()
		{
			WriteHistory();
			new DesktopSettings { Calendar = CalendarKind.Hebrew }.Save();
			_viewModel = new MainWindowViewModel(_dialogs, _dataFile, new FixedClock(), _pending.Enqueue);
			await _viewModel.StartAsync();
			return _viewModel;
		}


		[Fact]
		public async Task PickingAHebrewStartDateKeepsItsOnah()
		{
			var period = (await OpenInHebrewAsync()).Periods[0];
			_dialogs.ConfirmsChanges = true;

			period.BeginHebrewDay = FifteenShevat;
			Assert.Equal(Utc(2034, 2, 4, 0), period.Period.Begin);
			Assert.Equal("ט\"ו שבט תשצ\"ד", period.BeginText);

			period.ToggleBeginNightCommand.Execute(null);
			Assert.Equal(Utc(2034, 2, 3, 12), period.Period.Begin);
			Assert.Equal(FifteenShevat, period.BeginHebrewDay);
			Assert.Equal("ט\"ו שבט תשצ\"ד", period.BeginText);

			period.BeginHebrewDay = SixteenShevat;
			Assert.Equal(Utc(2034, 2, 4, 12), period.Period.Begin);
			Assert.True(period.BeginIsAfterDusk);

			period.ToggleBeginNightCommand.Execute(null);
			Assert.Equal(Utc(2034, 2, 5, 0), period.Period.Begin);
			Assert.Equal(SixteenShevat, period.BeginHebrewDay);
		}

		[Fact]
		public async Task PickingAHebrewEndDateKeepsItsOnah()
		{
			var period = (await OpenInHebrewAsync()).Periods[0];
			_dialogs.ConfirmsChanges = true;

			period.EndHebrewDay = FifteenShevat;
			Assert.Equal(Utc(2034, 2, 4, 0), period.Period.End);
			Assert.Equal("ט\"ו שבט תשצ\"ד", period.EndText);

			period.ToggleEndNightCommand.Execute(null);
			Assert.Equal(Utc(2034, 2, 3, 12), period.Period.End);
			Assert.Equal(FifteenShevat, period.EndHebrewDay);

			period.EndHebrewDay = SixteenShevat;
			Assert.Equal(Utc(2034, 2, 4, 12), period.Period.End);

			period.ToggleEndNightCommand.Execute(null);
			Assert.Equal(Utc(2034, 2, 5, 0), period.Period.End);
		}

		[Fact]
		public async Task EverythingAboutADateIsAnnouncedWhenItChanges()
		{
			var period = (await OpenInHebrewAsync()).Periods[0];
			var changes = new List<string?>();
			period.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

			period.BeginHebrewDay = FifteenShevat;
			period.EndHebrewDay = FifteenShevat;
			period.ToggleBeginNightCommand.Execute(null);
			period.ToggleEndNightCommand.Execute(null);

			foreach (var name in new[]
			{
				nameof(PeriodViewModel.BeginDay), nameof(PeriodViewModel.BeginDate), nameof(PeriodViewModel.BeginHebrewDay), nameof(PeriodViewModel.BeginIsAfterDusk), nameof(PeriodViewModel.BeginText), nameof(PeriodViewModel.BeginInOtherCalendar), nameof(PeriodViewModel.SkipLabel),
				nameof(PeriodViewModel.EndDay), nameof(PeriodViewModel.EndDate), nameof(PeriodViewModel.EndHebrewDay), nameof(PeriodViewModel.EndIsAfterDusk), nameof(PeriodViewModel.EndText), nameof(PeriodViewModel.EndInOtherCalendar),
			})
				Assert.Contains(name, changes);
		}

		[Fact]
		public async Task TheFieldHoldsTheDateOfTheCalendarOnShow()
		{
			var period = (await OpenInHebrewAsync()).Periods[0];
			_dialogs.ConfirmsChanges = true;
			period.BeginHebrewDay = FifteenShevat;
			period.EndHebrewDay = FifteenShevat;
			if (!period.BeginIsAfterDusk)
				period.ToggleBeginNightCommand.Execute(null);
			if (period.EndIsAfterDusk)
				period.ToggleEndNightCommand.Execute(null);

			Assert.Equal(FifteenShevat, period.BeginDay);					// a night is stored on the Gregorian day before
			Assert.Equal(FifteenShevat.AddDays(-1), period.BeginDate);
			Assert.Equal(FifteenShevat, period.EndDay);

			period.BeginDay = SixteenShevat;
			period.EndDay = SixteenShevat;

			Assert.Equal(SixteenShevat, period.BeginHebrewDay);
			Assert.True(period.BeginIsAfterDusk);
			Assert.Equal(SixteenShevat, period.EndHebrewDay);
			Assert.False(period.EndIsAfterDusk);
		}

		[Fact]
		public async Task ADateNobodyChangesIsLeftAlone()
		{
			var period = (await OpenInHebrewAsync()).Periods[0];
			period.Period.End = SecondOfFebruaryNight;

			period.EndHebrewDay = FifteenShevat;				// the same Hebrew date, picked again

			Assert.Equal(SecondOfFebruaryNight, period.Period.End);
		}

		/// <summary>The other way round from the Gregorian view, which a test of the Day | Night switch pins</summary>
		[Fact]
		public async Task TheGregorianViewKeepsItsOwnDirection()
		{
			WriteHistory();
			var period = (await OpenAsync()).Periods[0];
			_dialogs.ConfirmsChanges = true;
			period.BeginDate = FifteenShevat;

			period.ToggleBeginNightCommand.Execute(null);

			Assert.Equal(Utc(2034, 2, 4, 12), period.Period.Begin);
		}

		/// <summary>The texts the owner approved (W6 to W10)</summary>
		[Fact]
		public Task EachHebrewDateNamesItsGregorianDays() => BackToEnglishAfter(async () =>
		{
			var period = (await OpenInHebrewAsync()).Periods[0];
			var cases = new (DateTimeOffset Stamp, string English, string Hebrew)[]
			{
				(Utc(2034, 2, 3, 0), "Friday, 3 February", "יום שישי, 3 בפברואר"),																// W6
				(Utc(2034, 2, 3, 12), "Night of Friday 3 to Saturday 4 February", "הלילה שבין יום שישי ה־3 לשבת ה־4 בפברואר"),					// W7
				(Utc(2034, 2, 4, 0), "Saturday, 4 February", "שבת, 4 בפברואר"),																	// W8
				(Utc(2033, 2, 28, 12), "Night of Monday 28 February to Tuesday 1 March", "הלילה שבין יום שני ה־28 בפברואר ליום שלישי ה־1 במרץ"),	// W9
				(Utc(2034, 9, 13, 12), "Night of Wednesday 13 to Thursday 14 September", "הלילה שבין יום רביעי ה־13 ליום חמישי ה־14 בספטמבר"),	// W10
			};

			foreach (var item in cases)
			{
				period.Period.Begin = period.Period.End = item.Stamp;

				Loc.Instance.Use(Language.English);
				Assert.Equal(item.English, period.BeginInOtherCalendar);
				Assert.Equal(item.English, period.EndInOtherCalendar);

				Loc.Instance.Use(Language.Hebrew);
				Assert.Equal(item.Hebrew, period.BeginInOtherCalendar);
				Assert.Equal(item.Hebrew, period.EndInOtherCalendar);
				Loc.Instance.Use(Language.English);
			}
		});

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public async Task ADateTheHebrewCalendarDoesntReachShowsNothingExtra(bool hebrew)
		{
			var window = hebrew ? await OpenInHebrewAsync() : await OpenFromSettingsWithOneChangeAsync();
			var period = window.Periods[0];

			period.Period.Begin = Utc(1400, 1, 1, 0);

			Assert.Null(period.BeginInOtherCalendar);
			Assert.Null(period.BeginHebrewDay);
			Assert.Contains("1400", period.BeginText);
		}
	}
}
