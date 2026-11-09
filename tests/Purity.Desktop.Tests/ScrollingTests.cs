using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Purity.Core;
using Purity.Desktop.ViewModels;
using Purity.Desktop.Views;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>
	/// A long history must scroll like a short one. With Avalonia's virtualizing list, periods with
	/// different numbers of events made the list guess its height, and the guess kept changing:
	/// the wheel jumped, and going down could loop between two places without reaching the end.
	/// </summary>
	[Collection(AppDataCollection.Name)]
	public sealed class ScrollingTests : IDisposable
	{
		public ScrollingTests()
		{
			Directory.CreateDirectory(_root);
			Environment.SetEnvironmentVariable("PURITY_APP_DATA", Path.Combine(_root, "app data"));
		}

		public void Dispose()
		{
			Environment.SetEnvironmentVariable("PURITY_APP_DATA", null);
			try
			{
				Directory.Delete(_root, true);
			}
			catch (IOException)
			{
				// a watcher may hold the folder for a moment; temp gets cleaned anyway
			}
		}


		[Fact]
		public Task TheWheelGoesSteadilyToBothEnds() => Ui.Run(async () =>
		{
			var (window, periods) = await OpenAsync(150);
			var scroll = periods.Scroll!;
			var extent = scroll.Extent.Height;
			Assert.True(LongestScroll(scroll) > 20000, $"The history should be long, not {LongestScroll(scroll)}");
			Assert.Equal(0, scroll.Offset.Y);			// it opens at the newest periods, at the top
			Assert.True(Built(periods) < 20, $"Only the periods near the screen should be built, not {Built(periods)}");

			var at = periods.TranslatePoint(new Point(periods.Bounds.Width / 2, periods.Bounds.Height / 2), window)!.Value;
			await WheelUntil(window, at, up: false, () => scroll.Offset.Y >= LongestScroll(scroll) - 0.5, scroll, extent);
			await WheelUntil(window, at, up: true, () => scroll.Offset.Y <= 0, scroll, extent);

			// By now every period was built, and none changed the list's height when it was
			Assert.Equal(periods.ItemCount, Built(periods));
			await CloseAsync(window);
		});

		[Fact]
		public Task PageUpAndPageDownWorkFromAnEvent() => Ui.Run(async () =>
		{
			var (window, periods) = await OpenAsync(150);
			var scroll = periods.Scroll!;

			// Click an event of the newest period, which puts the focus in that period's own list
			var events = periods.GetVisualDescendants().OfType<ListBox>().First(el => el.Classes.Contains("Events"));
			var row = events.GetVisualDescendants().OfType<ListBoxItem>().First();
			var center = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window)!.Value;
			window.MouseDown(center, MouseButton.Left);
			window.MouseUp(center, MouseButton.Left);
			await Ui.WaitUntil(() => true);
			Assert.True(events.IsVisualAncestorOf((Visual)window.FocusManager!.GetFocusedElement()!), "The focus should be on the event");

			var start = scroll.Offset.Y;
			for (var page = 1; page <= 3; page++)
			{
				Press(window, Key.PageDown);
				await Ui.WaitUntil(() => true);
				Assert.Equal(start + page * scroll.Viewport.Height, scroll.Offset.Y, 1);
			}
			Press(window, Key.PageUp);
			await Ui.WaitUntil(() => true);
			Assert.Equal(start + 2 * scroll.Viewport.Height, scroll.Offset.Y, 1);

			await CloseAsync(window);
		});

		[Fact]
		public Task AddingAPeriodLeavesTheListWhereItIs() => Ui.Run(async () =>
		{
			var (window, periods) = await OpenAsync(150);
			var viewModel = (MainWindowViewModel)window.DataContext!;
			var scroll = periods.Scroll!;
			((ScrollViewer)scroll).ScrollToEnd();
			await Ui.WaitUntil(() => scroll.Offset.Y > 20000);

			var offset = scroll.Offset.Y;
			viewModel.AddPeriodCommand.Execute(null);
			await Task.Delay(100);
			await Ui.WaitUntil(() => viewModel.OpenPeriods.Count == 1);

			Assert.False(viewModel.Periods[0].IsClosed);
			Assert.Equal(150, periods.ItemCount);
			Assert.True(scroll.Offset.Y > offset - 2000, $"The list went from {offset:0} to {scroll.Offset.Y:0}");
			await CloseAsync(window);
		});

		[Fact]
		public Task AFoldedHistoryScrollsSteadilyToBothEnds() => Ui.Run(async () =>
		{
			var (window, periods) = await OpenAsync(150, folded: true);
			var scroll = periods.Scroll!;
			var extent = scroll.Extent.Height;
			Assert.True(LongestScroll(scroll) > 3000, $"The history should still need scrolling, not {LongestScroll(scroll)}");
			Assert.Single(periods.GetVisualDescendants().OfType<LazyPeriod>(), el => !el.IsFolded);

			var at = periods.TranslatePoint(new Point(periods.Bounds.Width / 2, periods.Bounds.Height / 2), window)!.Value;
			await WheelUntil(window, at, up: false, () => scroll.Offset.Y >= LongestScroll(scroll) - 0.5, scroll, extent);
			await WheelUntil(window, at, up: true, () => scroll.Offset.Y <= 0, scroll, extent);

			await CloseAsync(window);
		});

		[Fact]
		public Task PressingAFoldedLineOpensItAndItsArrowFoldsItBack() => Ui.Run(async () =>
		{
			var (window, periods) = await OpenAsync(30, folded: true);
			var scroll = periods.Scroll!;
			var extent = scroll.Extent.Height;
			var line = FoldedLines(periods).First();
			var period = (PeriodViewModel)line.DataContext!;
			Assert.Equal(2, periods.GetVisualDescendants().OfType<DateField>().Count());

			PressOn(line, window);
			await Ui.WaitUntil(() => !period.IsFolded && periods.GetVisualDescendants().OfType<DateField>().Count() == 4);
			await Ui.WaitUntil(() => scroll.Extent.Height > extent);

			var arrow = periods.GetVisualDescendants().OfType<Button>().Single(el => AutomationProperties.GetName(el) == "Fold this period" && el.DataContext == period);
			PressOn(arrow, window);
			await Ui.WaitUntil(() => period.IsFolded && periods.GetVisualDescendants().OfType<DateField>().Count() == 2);
			await Ui.WaitUntil(() => scroll.Extent.Height == extent);			// back to exactly the height it had

			await CloseAsync(window);
		});

		[Fact]
		public Task AFoldedLineSaysItsDatesAndLinesUpWithAnOpenPeriod() => Ui.Run(async () =>
		{
			var (window, periods) = await OpenAsync(30, folded: true);
			var skip = periods.GetVisualDescendants().OfType<CheckBox>().Single();
			var line = FoldedLines(periods).First();
			var period = (PeriodViewModel)line.DataContext!;
			var texts = line.GetVisualDescendants().OfType<TextBlock>().Select(el => el.Text).ToArray();
			var number = line.GetVisualDescendants().OfType<TextBlock>().Single(el => el.Text == period.FoldedNote);

			Assert.Equal(new[] { period.BeginText, period.EndText, period.FoldedNote }, texts);
			Assert.Single(line.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>());			// only the arrow that opens it
			var shown = line.GetVisualDescendants().OfType<Image>().Where(el => el.IsVisible).Select(el => el.Source).ToArray();
			Assert.True(Application.Current!.TryGetResource("AddPeriodIconDrawingImage", Application.Current.ActualThemeVariant, out var pink));
			Assert.True(Application.Current!.TryGetResource("ClosePeriodIconDrawingImage", Application.Current.ActualThemeVariant, out var blue));
			Assert.Same(pink, shown[0]);			// each date has its drop, then its day or night
			Assert.Same(blue, shown[2]);
			Assert.Equal(4, shown.Length);

			// Each is last on a line with the same right margin. The test screen's made-up font is wider than any
			// real one and pushes Skip out past the edge, so the positions themselves are checked in the real app.
			var skipLine = (Grid)skip.Parent!;
			var numberLine = (Grid)number.Parent!;
			Assert.Equal(numberLine.Margin.Right, skipLine.Margin.Right);
			Assert.Equal(Avalonia.Layout.HorizontalAlignment.Right, skip.HorizontalAlignment);
			Assert.Equal(skipLine.ColumnDefinitions.Count - 1, Grid.GetColumn(skip));
			Assert.Equal(numberLine.ColumnDefinitions.Count - 1, Grid.GetColumn(number));

			// The start drop is in the same place whether a period is folded or not
			var openDrop = skipLine.Children.OfType<Image>().Single();
			var foldedDrop = line.GetVisualDescendants().OfType<Image>().First(el => el.IsVisible);
			Assert.Equal(openDrop.TranslatePoint(default, periods)!.Value.X, foldedDrop.TranslatePoint(default, periods)!.Value.X);
			Assert.Equal(openDrop.Bounds.Width, foldedDrop.Bounds.Width);

			await CloseAsync(window);
		});

		[Fact]
		public Task APeriodOpenedAtTheBottomComesFullyIntoView() => Ui.Run(async () =>
		{
			var (window, periods) = await OpenAsync(60, folded: true);
			var scroll = (ScrollViewer)periods.Scroll!;
			scroll.ScrollToEnd();
			await Ui.WaitUntil(() => scroll.Offset.Y >= LongestScroll(scroll) - 0.5);
			await Task.Delay(50);
			await Ui.WaitUntil(() => FoldedLines(periods).Any());

			var line = FoldedLines(periods).OrderBy(el => el.TranslatePoint(default, periods)!.Value.Y).Last();
			var period = (PeriodViewModel)line.DataContext!;
			PressOn(line, window);
			await Ui.WaitUntil(() => !period.IsFolded);
			await Task.Delay(100);
			await Ui.WaitUntil(() => true);

			var row = periods.GetVisualDescendants().OfType<LazyPeriod>().Single(el => el.DataContext == period);
			var bottom = row.TranslatePoint(new Point(0, row.Bounds.Height), periods)!.Value.Y;
			Assert.True(bottom <= periods.Bounds.Height + 1, $"The opened period ends at {bottom:0}, past the list's {periods.Bounds.Height:0}");

			await CloseAsync(window);
		});


		private static IEnumerable<Button> FoldedLines(ListBox periods) =>
			periods.GetVisualDescendants().OfType<Button>().Where(el => AutomationProperties.GetName(el)?.StartsWith("Open the period") == true);

		private static void PressOn(Control control, Window window)
		{
			// The pointer finds what was last drawn, so a frame has to be drawn first
			Dispatcher.UIThread.RunJobs();
			AvaloniaHeadlessPlatform.ForceRenderTimerTick();
			Dispatcher.UIThread.RunJobs();

			var center = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
			window.MouseDown(center, MouseButton.Left);
			window.MouseUp(center, MouseButton.Left);
			Dispatcher.UIThread.RunJobs();
		}

		private async Task<(MainWindow, ListBox)> OpenAsync(int periodCount, bool folded = false)
		{
			var dataFile = Path.Combine(_root, "pureData.json");
			File.WriteAllBytes(dataFile, Encoding.UTF8.GetBytes(History(periodCount)));

			var window = new MainWindow(new StartOptions(dataFile));
			window.Show();
			var viewModel = (MainWindowViewModel)window.DataContext!;
			var periods = window.FindControl<ListBox>("Periods")!;
			await Ui.WaitUntil(() => viewModel.Periods.Count == periodCount && periods.GetVisualDescendants().OfType<CalendarDatePicker>().Any());
			if (!folded)
				foreach (var period in viewModel.Periods)
					period.IsFolded = false;
			await Task.Delay(200);
			await Ui.WaitUntil(() => true);					// the list scrolls to the newest periods first
			((ScrollViewer)periods.Scroll!).ScrollToHome();			// opening them all scrolled to the last one opened
			await Ui.WaitUntil(() => true);
			return (window, periods);
		}

		private static async Task CloseAsync(Window window)
		{
			window.Close();
			await Ui.WaitUntil(() => !window.IsVisible);
		}

		/// <summary>Turns the wheel one notch at a time, checking each notch moves the list the same way without changing its height</summary>
		private static async Task WheelUntil(Window window, Point at, bool up, Func<bool> done, IScrollable scroll, double extent)
		{
			var limit = (int)(extent / 40);
			for (var notch = 0; !done(); notch++)
			{
				Assert.True(notch < limit, $"The wheel should have reached the {(up ? "top" : "end")} by now, but is at {scroll.Offset.Y:0} of {extent:0}");
				var before = scroll.Offset.Y;
				window.MouseWheel(at, new Vector(0, up ? 1 : -1));
				await Ui.WaitUntil(() => true);

				Assert.Equal(extent, scroll.Extent.Height);
				Assert.True(up ? scroll.Offset.Y < before : scroll.Offset.Y > before, $"The wheel went the wrong way at {before:0}: {scroll.Offset.Y:0}");
			}
		}

		private static double LongestScroll(IScrollable scroll) => scroll.Extent.Height - scroll.Viewport.Height;

		private static int Built(ListBox periods) => periods.GetVisualDescendants().OfType<LazyPeriod>().Count(el => el.IsBuilt);

		private static void Press(Window window, Key key)
		{
			window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
			window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
		}

		/// <summary>Periods with between three and seven events each, as intervals of different lengths give</summary>
		private static string History(int count)
		{
			var random = new Random(7);
			var ledger = new PeriodLedger();
			var begin = new DateTime(2031, 1, 5);
			for (var i = 0; i < count; i++)
			{
				ledger.AcceptPeriod(ledger.AddPeriod(Stamps.FromDate(begin, random.Next(2) == 0), Stamps.FromDate(begin.AddDays(5), false)));
				begin = begin.AddDays(random.Next(24, 40));
			}
			return PurityFile.Format(ledger.Data);
		}


		private readonly string _root = Path.Combine(Path.GetTempPath(), $"purity-desktop-{Guid.NewGuid():N}");
	}
}
