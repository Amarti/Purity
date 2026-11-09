using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Purity.Core;
using Purity.Desktop.Localization;
using Purity.Desktop.ViewModels;
using Purity.Desktop.Views;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>The Hebrew date field and its month grid, on real windows. Every date is made up.</summary>
	public sealed partial class LookTests
	{
		[Fact]
		public Task InHebrewModeTheHebrewFieldsShow() => BackToEnglishAfter(async () =>
		{
			var window = await OpenInHebrewAsync();
			var first = ((MainWindowViewModel)window.DataContext!).Periods[0];

			var fields = HebrewFieldsOf(window, first);
			Assert.Equal(2, fields.Length);
			Assert.All(fields, el => Assert.True(el.IsVisible));
			Assert.Equal(first.BeginText, fields[0].Text);
			Assert.Equal(first.BeginInOtherCalendar, ToolTip.GetTip(fields[0]));
			Assert.Equal(UiText.English.From, AutomationProperties.GetName(fields[0].GetVisualDescendants().OfType<Button>().Single(el => el.Name == "PART_Button")));
			Assert.All(window.GetVisualDescendants().OfType<DateField>().Where(el => el.DataContext == first), el => Assert.False(el.IsVisible));
			window.Close();
		});

		[Fact]
		public Task InGregorianModeTheHebrewFieldsStayHidden() => BackToEnglishAfter(async () =>
		{
			var window = await OpenPeriodsAsync();
			var first = ((MainWindowViewModel)window.DataContext!).Periods[0];

			Assert.All(HebrewFieldsOf(window, first), el => Assert.False(el.IsVisible));
			Assert.All(window.GetVisualDescendants().OfType<DateField>().Where(el => el.DataContext == first), el => Assert.True(el.IsVisible));
			window.Close();
		});

		[Fact]
		public Task OnAHebrewDateTheNightComesFirst() => BackToEnglishAfter(async () =>
		{
			var window = await OpenInHebrewAsync();
			var first = ((MainWindowViewModel)window.DataContext!).Periods[0];

			var (day, night) = Halves(SwitchesOf(window, first).Begin);

			Assert.Equal(0, Grid.GetColumn(night));
			Assert.Equal(1, Grid.GetColumn(day));
			window.Close();
		});

		[Fact]
		public Task PickingFromTheGridChangesTheStoredDateAndCloses() => BackToEnglishAfter(async () =>
		{
			var window = await OpenInHebrewAsync();
			var first = ((MainWindowViewModel)window.DataContext!).Periods[0];
			first.Period.Begin = Stamps.FromDate(new DateTime(2034, 2, 4), false);	// 15 Shevat 5794
			first.Refresh();
			var field = HebrewFieldsOf(window, first)[0];
			var popup = field.GetVisualDescendants().OfType<Popup>().Single();

			Press(field.GetVisualDescendants().OfType<Button>().Single(el => el.Name == "PART_Button"), window);
			await Ui.WaitUntil(() => popup.IsOpen);
			var month = Assert.IsType<HebrewMonthView>(popup.Child);
			await Ui.WaitUntil(() => DaysOf(month).Length > 0);
			Assert.Equal(new HebrewMonth(5794, 5), month.Month);
			Assert.Contains("Selected", Day(month, "ט\"ו שבט").Classes);

			Day(month, "ט\"ז שבט").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			Dispatcher.UIThread.RunJobs();

			Assert.False(popup.IsOpen);
			Assert.Equal(new DateTimeOffset(2034, 2, 5, 0, 0, 0, TimeSpan.Zero), first.Period.Begin);
			Assert.Equal("ט\"ז שבט תשצ\"ד", field.Text);
			Assert.Equal(new DateTime(2034, 2, 5), field.SelectedDay);
			window.Close();
		});

		[Fact]
		public Task TheGridLaysOutAMonth() => Ui.Run(() =>
		{
			var (window, month) = ShowMonth(new DateTime(2034, 2, 4));

			Assert.Equal(30, DaysOf(month).Length);
			Assert.Equal("שבט תשצ\"ד", Title(month));
			var first = Day(month, "א' שבט");
			var firstDate = new HebrewMonth(5794, 5).Daytime(1);
			Assert.Equal((int)firstDate.DayOfWeek, Grid.GetColumn(first));
			Assert.Equal(1, Grid.GetRow(first));
			Assert.Equal((int)firstDate.AddDays(14).DayOfWeek, Grid.GetColumn(Day(month, "ט\"ו שבט")));
			window.Close();
		});

		[Fact]
		public Task TheGridMarksTodayAndTheSelectedDate() => Ui.Run(() =>
		{
			var (window, month) = ShowMonth(new DateTime(2034, 2, 4), today: new DateTime(2034, 2, 5));

			Assert.Contains("Selected", Day(month, "ט\"ו שבט").Classes);
			Assert.Contains("Today", Day(month, "ט\"ז שבט").Classes);
			Assert.DoesNotContain("Selected", Day(month, "י\"ז שבט").Classes);
			window.Close();
		});

		[Fact]
		public Task TheArrowsMoveByMonthAndYearThroughALeapYear() => Ui.Run(() =>
		{
			var (window, month) = ShowMonth(new HebrewMonth(5795, 5).Daytime(1));		// Shevat in a leap year

			Press("PART_Next", month);
			Assert.Equal("אדר א׳ תשצ\"ה", Title(month));
			Assert.Equal(30, DaysOf(month).Length);

			Press("PART_Next", month);
			Assert.Equal("אדר ב׳ תשצ\"ה", Title(month));
			Assert.Equal(29, DaysOf(month).Length);

			Press("PART_PreviousYear", month);
			Assert.Equal("אדר תשצ\"ד", Title(month));

			Press("PART_NextYear", month);
			Assert.Equal("אדר ב׳ תשצ\"ה", Title(month));

			Press("PART_Previous", month);
			Press("PART_Previous", month);
			Assert.Equal("שבט תשצ\"ה", Title(month));
			window.Close();
		});

		[Fact]
		public Task TheArrowsStopAtTheEndsOfTheCalendar() => Ui.Run(() =>
		{
			var (window, month) = ShowMonth(HebrewMonth.First.Daytime(1));

			Assert.False(Arrow("PART_Previous", month).IsEnabled);
			Assert.False(Arrow("PART_PreviousYear", month).IsEnabled);
			Assert.True(Arrow("PART_Next", month).IsEnabled);
			window.Close();

			(window, month) = ShowMonth(HebrewMonth.Last.Daytime(1));
			Assert.False(Arrow("PART_Next", month).IsEnabled);
			Assert.False(Arrow("PART_NextYear", month).IsEnabled);
			Assert.True(Arrow("PART_Previous", month).IsEnabled);
			window.Close();
		});

		[Fact]
		public Task TheGridRunsRightToLeftInEveryLanguage() => Ui.Run(() =>
		{
			var (window, month) = ShowMonth(new DateTime(2034, 2, 4));

			Assert.Equal(Avalonia.Media.FlowDirection.RightToLeft, month.FlowDirection);
			window.Close();
		});


		private async Task<MainWindow> OpenInHebrewAsync()
		{
			var dataFile = Path.Combine(_root, "pureData.json");
			File.WriteAllText(dataFile, History());
			var window = new MainWindow(new StartOptions(dataFile, Calendar: CalendarKind.Hebrew));
			window.Show();
			await Ui.WaitUntil(() => window.GetVisualDescendants().OfType<HebrewDateField>().Any(el => el.IsVisible && el.Bounds.Width > 0));
			await Task.Delay(200);
			await Ui.WaitUntil(() => true);					// the list scrolls to the newest periods first
			return window;
		}

		private static HebrewDateField[] HebrewFieldsOf(Window window, PeriodViewModel period) =>
			window.GetVisualDescendants().OfType<HebrewDateField>().Where(el => el.DataContext == period).ToArray();

		private static (Window Window, HebrewMonthView Month) ShowMonth(DateTime selected, DateTime? today = null)
		{
			var month = new HebrewMonthView();
			var window = new Window { Width = 300, Height = 300, Content = month };
			window.Show();
			month.Show(selected, today ?? new DateTime(2000, 1, 1));
			Dispatcher.UIThread.RunJobs();
			return (window, month);
		}

		private static Button[] DaysOf(HebrewMonthView month) =>
			month.GetVisualDescendants().OfType<Button>().Where(el => Grid.GetRow(el) > 0 && string.IsNullOrEmpty(el.Name)).ToArray();

		private static Button Day(HebrewMonthView month, string name) =>
			DaysOf(month).Single(el => AutomationProperties.GetName(el) == name);

		private static Button Arrow(string name, HebrewMonthView month) =>
			month.GetVisualDescendants().OfType<Button>().Single(el => el.Name == name);

		private static string? Title(HebrewMonthView month) =>
			month.GetVisualDescendants().OfType<TextBlock>().Single(el => el.Name == "PART_Title").Text;

		private static void Press(string arrow, HebrewMonthView month)
		{
			Arrow(arrow, month).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			Dispatcher.UIThread.RunJobs();
		}
	}
}
