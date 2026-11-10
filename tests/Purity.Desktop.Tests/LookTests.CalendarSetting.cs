using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Purity.Desktop.Localization;
using Purity.Desktop.ViewModels;
using Purity.Desktop.Views;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>The calendar choices in the real Settings window, and what they do to the other windows</summary>
	public sealed partial class LookTests
	{
		[Fact]
		public Task TheCalendarChoicesChangeTheWindowAtOnceAndCancelPutsThemBack() => BackToEnglishAfter(async () =>
		{
			var window = await OpenPeriodsAsync();
			var first = ((MainWindowViewModel)window.DataContext!).Periods[0];
			var (settings, closed) = await OpenSettingsAsync(window);
			var calendar = settings.GetVisualDescendants().OfType<ComboBox>().Single(el => el.ItemCount == 2);
			Assert.Equal(UiText.English.HebrewOnly, calendar.Items[1]);
			Assert.Equal(0, calendar.SelectedIndex);

			calendar.SelectedIndex = 1;
			Dispatcher.UIThread.RunJobs();

			Assert.Equal(first.BeginHebrewDay, FieldsOf(window, first)[0].SelectedDay);
			Assert.Equal(first.BeginText, FieldsOf(window, first)[0].Text);

			settings.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked = true;
			Dispatcher.UIThread.RunJobs();

			Assert.Matches(@"^\d+ [A-Z]", FieldsOf(window, first)[0].Text);

			settings.Close(false);
			await closed;
			Dispatcher.UIThread.RunJobs();

			Assert.Equal(first.BeginDate, FieldsOf(window, first)[0].SelectedDay);
			Assert.Equal(first.BeginText, FieldsOf(window, first)[0].Text);
			Assert.False(Loc.Instance.HebrewInEnglish);
			window.Close();
		});

		/// <summary>Reads a line as a right-to-left window lays it out, from its left edge</summary>
		private static string AsShownRightToLeft(string text)
		{
			var line = new TextLayout(text, Typeface.Default, 12, Brushes.Black, flowDirection: FlowDirection.RightToLeft).TextLines[0];
			return string.Concat(line.TextRuns.Select(el => el.Text.ToString())).Replace(Loc.LeftToRightMark.ToString(), string.Empty).Trim();
		}

		[Fact]
		public Task EnglishLettersStayInReadingOrderInAHebrewWindow() => BackToEnglishAfter(() =>
		{
			var night = new DateTimeOffset(2034, 2, 3, 12, 0, 0, TimeSpan.Zero);
			Assert.NotEqual("15 Shevat 5794", AsShownRightToLeft("15 Shevat 5794"));		// what the mark is there to prevent

			Loc.Instance.UseCalendar(CalendarKind.Hebrew, true);
			Assert.Equal("15 Shevat 5794", Loc.Instance.LongDate(night));					// a left-to-right window needs no mark

			Loc.Instance.Use(Language.Hebrew);
			Assert.Equal("15 Shevat 5794", AsShownRightToLeft(Loc.Instance.LongDate(night)));
			Assert.Equal("15 Shevat", AsShownRightToLeft(Loc.Instance.HebrewDate(night)));
			Assert.Equal("15 Shevat 5794, 14:30", AsShownRightToLeft(Loc.Instance.Moment(new DateTimeOffset(2034, 2, 4, 14, 30, 0, TimeSpan.Zero), withWeekday: false)));
			return Task.CompletedTask;
		});

		[Fact]
		public Task TheGridWritesEnglishLettersLeftToRight() => BackToEnglishAfter(() =>
		{
			Loc.Instance.UseCalendar(CalendarKind.Hebrew, true);

			var (window, month) = ShowMonth(new DateTime(2034, 2, 4));

			Assert.Equal("Shevat 5794", Title(month));
			Assert.Equal(30, DaysOf(month).Length);
			Assert.Contains("Selected", Day(month, "15 Shevat").Classes);
			Assert.Equal(FlowDirection.LeftToRight, month.FlowDirection);
			Assert.Contains(month.GetVisualDescendants().OfType<TextBlock>(), el => el.Text == "Su");
			window.Close();
			return Task.CompletedTask;
		});
	}
}
