using Purity.Desktop.Localization;
using Purity.Desktop.ViewModels;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>What a date says about itself in the other calendar: the texts the owner approved (W1 to W5)</summary>
	public sealed partial class MainWindowTests
	{
		[Fact]
		public Task EachDateNamesItsHebrewDateAndOnah() => BackToEnglishAfter(async () =>
		{
			var window = await OpenFromSettingsWithOneChangeAsync();
			var period = window.Periods[0];
			_dialogs.ConfirmsChanges = true;
			var cases = new (DateTime Date, bool Night, string English, string Hebrew)[]
			{
				(new DateTime(2034, 2, 3), false, "י\"ד שבט, day", "י\"ד שבט, יום"),		// W1
				(new DateTime(2034, 2, 3), true, "ט\"ו שבט, night", "ט\"ו שבט, לילה"),		// W2
				(new DateTime(2034, 2, 4), false, "ט\"ו שבט, day", "ט\"ו שבט, יום"),		// W3
				(new DateTime(2033, 2, 28), true, "ל' אדר א׳, night", "ל' אדר א׳, לילה"),	// W4
				(new DateTime(2034, 9, 13), true, "א' תשרי, night", "א' תשרי, לילה"),		// W5
			};

			foreach (var item in cases)
			{
				period.BeginDate = item.Date;
				period.EndDate = item.Date;
				if (period.BeginIsAfterDusk != item.Night)
					period.ToggleBeginNightCommand.Execute(null);
				if (period.EndIsAfterDusk != item.Night)
					period.ToggleEndNightCommand.Execute(null);

				Loc.Instance.Use(Language.English);
				Assert.Equal(item.English, period.BeginInOtherCalendar);
				Assert.Equal(item.English, period.EndInOtherCalendar);

				Loc.Instance.Use(Language.Hebrew);
				Assert.Equal(item.Hebrew, period.BeginInOtherCalendar);
				Assert.Equal(item.Hebrew, period.EndInOtherCalendar);
				Loc.Instance.Use(Language.English);
			}
		});

		[Fact]
		public Task TheOtherCalendarFollowsTheDateAndTheSwitch() => BackToEnglishAfter(async () =>
		{
			var window = await OpenFromSettingsWithOneChangeAsync();
			var period = window.Periods[0];
			_dialogs.ConfirmsChanges = true;
			var changes = new List<string?>();
			period.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

			period.BeginDate = new DateTime(2034, 2, 3);
			Assert.Equal("י\"ד שבט, day", period.BeginInOtherCalendar);
			Assert.Contains(nameof(PeriodViewModel.BeginInOtherCalendar), changes);

			changes.Clear();
			period.ToggleBeginNightCommand.Execute(null);
			Assert.Equal("ט\"ו שבט, night", period.BeginInOtherCalendar);
			Assert.Contains(nameof(PeriodViewModel.BeginInOtherCalendar), changes);

			changes.Clear();
			period.EndDate = new DateTime(2034, 2, 3);
			Assert.Equal("י\"ד שבט, day", period.EndInOtherCalendar);
			Assert.Contains(nameof(PeriodViewModel.EndInOtherCalendar), changes);

			changes.Clear();
			period.ToggleEndNightCommand.Execute(null);
			Assert.Equal("ט\"ו שבט, night", period.EndInOtherCalendar);
			Assert.Contains(nameof(PeriodViewModel.EndInOtherCalendar), changes);
		});
	}
}
