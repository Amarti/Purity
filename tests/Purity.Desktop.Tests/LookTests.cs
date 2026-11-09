using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using Purity.Core;
using Purity.Desktop.ViewModels;
using Purity.Desktop.Views;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>
	/// The look the owner approved for 2.2.0: colored chips (T4), rounded periods (T6), a dark
	/// theme (T7) and a window that can be resized (T8); and for 2.2.1, Begin and End on their own
	/// lines with a Day | Night switch (T3b). Every date is made up. The theme is one setting for
	/// the whole app, so every test puts it back.
	/// </summary>
	[Collection(AppDataCollection.Name)]
	public sealed class LookTests : IDisposable
	{
		public LookTests()
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
		public Task TheDarkThemeRepaintsWindowsAndIcons() => Themed(() =>
		{
			App.Apply(Appearance.Light);
			var window = new Window { Width = 200, Height = 100 };
			window.Show();
			Assert.Equal(Colors.White, ColorOf(window.Background));
			Assert.Equal(Colors.Black, ColorOf(window.Foreground));
			Assert.Equal(Color.Parse("#5683A9"), IconColor("RefreshIconDrawingGroup"));
			Assert.Equal(Colors.Black, FrameColor("AfterDuskIconDrawingGroup"));

			App.Apply(Appearance.Dark);
			Dispatcher.UIThread.RunJobs();
			Assert.Equal(Color.Parse("#1F2023"), ColorOf(window.Background));
			Assert.Equal(Color.Parse("#E4E6EA"), ColorOf(window.Foreground));
			Assert.Equal(Color.Parse("#7FB0E6"), IconColor("RefreshIconDrawingGroup"));
			Assert.Equal(Color.Parse("#9A9EA6"), FrameColor("AfterDuskIconDrawingGroup"));

			window.Close();
		});

		[Fact]
		public Task APeriodIsRoundedBlueOnceAcceptedAndRedWhileOpen() => Themed(() =>
		{
			App.Apply(Appearance.Light);
			var accepted = new Border { Classes = { "Period" }, Width = 100, Height = 40 };
			var open = new Border { Classes = { "Period", "Open" }, Width = 100, Height = 40 };
			var stack = new StackPanel();
			stack.Children.Add(accepted);
			stack.Children.Add(open);
			var window = new Window { Content = stack, Width = 200, Height = 100 };
			window.Show();

			Assert.Equal(new CornerRadius(6), accepted.CornerRadius);
			Assert.Equal(Color.Parse("#5683A9"), ColorOf(accepted.BorderBrush));
			Assert.Equal(Color.Parse("#FF0061"), ColorOf(open.BorderBrush));

			App.Apply(Appearance.Dark);
			Dispatcher.UIThread.RunJobs();
			Assert.Equal(Color.Parse("#6F9CC4"), ColorOf(accepted.BorderBrush));
			Assert.Equal(Color.Parse("#FF0061"), ColorOf(open.BorderBrush));			// an open period stays red

			window.Close();
		});

		[Fact]
		public Task EachKindOfDateHasItsOwnChipInBothThemes() => Themed(async () =>
		{
			App.Apply(Appearance.Light);
			var dataFile = Path.Combine(_root, "pureData.json");
			File.WriteAllText(dataFile, History());
			var window = new MainWindow(new StartOptions(dataFile));
			window.Show();
			await Ui.WaitUntil(() => Chips(window).Select(el => el.Type).Distinct().Count() == 4);

			var light = Chips(window).GroupBy(el => el.Type).ToDictionary(el => el.Key, el => el.Select(chip => ColorOf(chip.Background)).Distinct().Single());
			Assert.Equal(Color.Parse("#E2EBF3"), light[PurityEventType.Mikveh]);
			Assert.Equal(Color.Parse("#FAEEDA"), light[PurityEventType.OnaBeinonit]);
			Assert.Equal(Color.Parse("#E1F5EE"), light[PurityEventType.VesetHodesh]);
			Assert.Equal(Color.Parse("#EEEDFE"), light[PurityEventType.VesetAflaga]);
			var mikveh = Chips(window).First(el => el.Type == PurityEventType.Mikveh);
			var text = Assert.IsType<TextBlock>(mikveh.Child);
			Assert.Equal("Mikveh", text.Text);
			Assert.Equal(Color.Parse("#24496A"), ColorOf(text.Foreground));

			App.Apply(Appearance.Dark);
			Dispatcher.UIThread.RunJobs();
			Assert.Equal(Color.Parse("#2C4A66"), ColorOf(mikveh.Background));
			Assert.Equal(Color.Parse("#E2EBF3"), ColorOf(text.Foreground));

			window.Close();
			await Ui.WaitUntil(() => !window.IsVisible);
		});

		[Fact]
		public Task TheWindowCanGrowButNeverGetsNarrowerThanItsRows() => Ui.Run(() =>
		{
			var window = new MainWindow(new StartOptions(Path.Combine(_root, "pureData.json")));

			Assert.True(window.CanResize);
			Assert.Equal(694, window.Width);
			Assert.Equal(861, window.Height);
			Assert.Equal(650, window.MinWidth);
			Assert.Equal(WindowFit.MinimumHeight, window.MinHeight);
		});

		[Fact]
		public Task ThePeriodsNarrowWithTheWindowAndStayInTheMiddleOfAWiderOne() => Ui.Run(async () =>
		{
			var window = await OpenPeriodsAsync();
			var periods = window.FindControl<ListBox>("Periods")!;
			double Left() => periods.TranslatePoint(default, window)!.Value.X;
			void Resize(double width)
			{
				window.Width = width;
				Dispatcher.UIThread.RunJobs();
			}

			Assert.Equal(654, periods.Bounds.Width);
			Assert.Equal(20, Left());

			Resize(1000);
			Assert.Equal(660, periods.Bounds.Width);
			Assert.Equal(170, Left());

			Resize(window.MinWidth);
			Assert.Equal(610, periods.Bounds.Width);
			Assert.Equal(20, Left());

			// Only the "Begin" label, 30 wide in Segoe UI, depends on the font; the rest of the line is fixed
			var events = periods.GetVisualDescendants().OfType<ListBox>().First(el => el.Classes.Contains("Events"));
			Assert.True(events.Bounds.Width - RestOfTheDateLine >= 40, $"The lines have {events.Bounds.Width} to fit in");

			window.Close();
			await Ui.WaitUntil(() => !window.IsVisible);
		});

		[Fact]
		public Task TheWindowOpensAtItsLastSize() => Ui.Run(() =>
		{
			new DesktopSettings { DataFilePath = Path.Combine(_root, "pureData.json"), Window = new WindowPlacement(40, 30, 900, 700, true) }.Save();

			var window = new MainWindow(new StartOptions(null));
			Assert.Equal(900, window.Width);
			Assert.Equal(700, window.Height);
			Assert.Equal(WindowState.Maximized, window.WindowState);

			// Never smaller than the window allows, and pictures always show the starting size
			new DesktopSettings { DataFilePath = Path.Combine(_root, "pureData.json"), Window = new WindowPlacement(40, 30, 300, 200, false) }.Save();
			window = new MainWindow(new StartOptions(null));
			Assert.Equal(650, window.Width);
			Assert.Equal(WindowFit.MinimumHeight, window.Height);
			Assert.Equal(WindowState.Normal, window.WindowState);
			window = new MainWindow(new StartOptions(Path.Combine(_root, "pureData.json"), Path.Combine(_root, "picture.png")));
			Assert.Equal(694, window.Width);
			Assert.Equal(861, window.Height);
		});

		[Fact]
		public void TheWindowGoesBackToTheMiddleWhenItsScreenIsGone()
		{
			var screens = new[] { new PixelRect(0, 0, 1920, 1040), new PixelRect(1920, 0, 1280, 984) };

			Assert.True(WindowFit.ShowsTitleBar(new PixelPoint(300, 100), screens));
			Assert.True(WindowFit.ShowsTitleBar(new PixelPoint(2100, 50), screens));
			Assert.True(WindowFit.ShowsTitleBar(new PixelPoint(-8, -8), screens));			// snapped to the edge, its frame just off screen
			Assert.False(WindowFit.ShowsTitleBar(new PixelPoint(3300, 100), screens));			// on a screen that was taken away
			Assert.False(WindowFit.ShowsTitleBar(new PixelPoint(300, -200), screens));			// title bar above every screen
		}

		[Fact]
		public void AWindowWiderThanTheScreenGetsNarrowerButNotNarrowerThanItsRows()
		{
			Assert.Equal(900, WindowFit.Width(900, 16, 1920, 694));
			Assert.Equal(784, WindowFit.Width(900, 16, 800, 694));
			Assert.Equal(694, WindowFit.Width(900, 16, 600, 694));
		}

		[Fact]
		public Task TheSettingsOfferTheSystemsChoiceLightAndDark() => Ui.Run(() =>
		{
			var settings = new SettingsWindowViewModel("pureData.json", null, appearance: Appearance.Dark);
			var window = new SettingsWindow(settings);
			window.Show();
			var choice = window.GetVisualDescendants().OfType<ComboBox>().Single();

			Assert.Equal(new[] { "Same as the system", "Light", "Dark" }, settings.AppearanceChoices);
			Assert.Equal((int)Appearance.Dark, choice.SelectedIndex);
			choice.SelectedIndex = 0;
			Assert.Equal(Appearance.System, settings.Appearance);

			window.Close();
		});


		[Fact]
		public Task TheSwitchShowsTheOnahAndOnlyTheOtherHalfChangesIt() => Ui.Run(() =>
		{
			var pressed = 0;
			var control = new DayNightSwitch { Height = 25 };
			control.Command = new RelayCommand(() =>
			{
				pressed++;
				control.IsNight = !control.IsNight;
			});
			var window = new Window { Content = control, Width = 300, Height = 100 };
			window.Show();
			var (day, night) = Halves(control);
			Assert.Equal(new bool?[] { true, false }, new[] { day.IsChecked, night.IsChecked });

			Press(day, window);
			Assert.Equal(0, pressed);
			Assert.False(control.IsNight);
			Assert.True(day.IsChecked);

			Press(night, window);
			Assert.Equal(1, pressed);
			Assert.True(control.IsNight);
			Assert.Equal(new bool?[] { false, true }, new[] { day.IsChecked, night.IsChecked });

			Press(night, window);
			Assert.Equal(1, pressed);
			Assert.True(night.IsChecked);

			Press(day, window);
			Assert.Equal(2, pressed);
			Assert.False(control.IsNight);
			Assert.Equal(new bool?[] { true, false }, new[] { day.IsChecked, night.IsChecked });

			// It shows what the period holds, even if the press didn't change that
			control.Command = new RelayCommand(() => {});
			Press(night, window);
			Assert.Equal(new bool?[] { true, false }, new[] { day.IsChecked, night.IsChecked });

			window.Close();
		});

		[Fact]
		public Task APeriodsSwitchesMoveItsDatesToTheOtherOnah() => Ui.Run(async () =>
		{
			var window = await OpenPeriodsAsync();
			var viewModel = (MainWindowViewModel)window.DataContext!;
			var period = viewModel.Periods[^1];
			var begin = period.Period.Begin;
			var end = period.Period.End;
			var (beginSwitch, endSwitch) = SwitchesOf(window, period);

			Assert.Equal("Begin day or night", AutomationProperties.GetName(beginSwitch));
			Assert.Equal("End day or night", AutomationProperties.GetName(endSwitch));
			Assert.False(beginSwitch.IsNight);

			Press(Halves(beginSwitch).Night, window);
			Assert.Equal(begin.AddHours(12), period.Period.Begin);
			Assert.Equal(end, period.Period.End);
			Assert.True(beginSwitch.IsNight);
			Assert.False(endSwitch.IsNight);

			Press(Halves(endSwitch).Night, window);
			Assert.Equal(end.AddHours(12), period.Period.End);
			Assert.True(endSwitch.IsNight);

			Press(Halves(beginSwitch).Day, window);
			Assert.Equal(begin, period.Period.Begin);
			Assert.False(beginSwitch.IsNight);

			window.Close();
			await Ui.WaitUntil(() => !window.IsVisible);
		});

		[Fact]
		public Task BeginAndEndHaveTheirOwnLinesStartingWhereTheDividerStarts() => Ui.Run(async () =>
		{
			var window = await OpenPeriodsAsync();
			var viewModel = (MainWindowViewModel)window.DataContext!;
			var (beginSwitch, endSwitch) = SwitchesOf(window, viewModel.Periods[^1]);
			var card = beginSwitch.FindAncestorOfType<Border>(includeSelf: false)!;
			while (!card.Classes.Contains("Period"))
				card = card.FindAncestorOfType<Border>(includeSelf: false)!;
			var fields = card.GetVisualDescendants().OfType<DateField>().ToArray();
			var begin = card.GetVisualDescendants().OfType<TextBlock>().First(el => el.Text == "Begin");
			var events = card.GetVisualDescendants().OfType<ListBox>().Single(el => el.Classes.Contains("Events"));
			Point At(Visual visual) => visual.TranslatePoint(default, card)!.Value;

			Assert.Equal(2, fields.Length);
			Assert.All(fields, el => Assert.Equal(WholeDateWidth, el.Bounds.Width));
			Assert.Equal(At(fields[0]).X, At(fields[1]).X);
			Assert.Equal(At(fields[0]).Y + 30, At(fields[1]).Y);
			Assert.Equal(At(beginSwitch).X, At(endSwitch).X);
			Assert.Equal(At(fields[0]).Y, At(beginSwitch).Y);
			Assert.Equal(At(events).X, At(begin).X);
			Assert.Equal(126, beginSwitch.Bounds.Width);

			window.Close();
			await Ui.WaitUntil(() => !window.IsVisible);
		});


		private async Task<MainWindow> OpenPeriodsAsync()
		{
			var dataFile = Path.Combine(_root, "pureData.json");
			File.WriteAllText(dataFile, History());
			var window = new MainWindow(new StartOptions(dataFile));
			window.Show();
			await Ui.WaitUntil(() => window.GetVisualDescendants().OfType<DayNightSwitch>().Any(el => el.Bounds.Width > 0));
			await Task.Delay(200);
			await Ui.WaitUntil(() => true);					// the list scrolls to the newest periods first
			return window;
		}

		private static (DayNightSwitch Begin, DayNightSwitch End) SwitchesOf(Window window, PeriodViewModel period)
		{
			var switches = window.GetVisualDescendants().OfType<DayNightSwitch>().Where(el => el.DataContext == period).ToArray();
			Assert.Equal(2, switches.Length);
			return (switches[0], switches[1]);
		}

		private static (RadioButton Day, RadioButton Night) Halves(DayNightSwitch control)
		{
			var halves = control.GetVisualDescendants().OfType<RadioButton>().ToArray();
			return (halves.Single(el => el.Name == "PART_Day"), halves.Single(el => el.Name == "PART_Night"));
		}

		private static void Press(Control control, Window window)
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

		/// <summary>Runs a test that changes the theme, and always puts the system's back</summary>
		private static Task Themed(Action test) => Ui.Run(() =>
		{
			try
			{
				test();
			}
			finally
			{
				App.Apply(Appearance.System);
			}
		});

		private static Task Themed(Func<Task> test) => Ui.Run(async () =>
		{
			try
			{
				await test();
			}
			finally
			{
				App.Apply(Appearance.System);
			}
		});

		private static IEnumerable<EventChip> Chips(Window window) => window.GetVisualDescendants().OfType<EventChip>();

		private static Color ColorOf(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

		private static DrawingGroup Icon(string key)
		{
			var app = Application.Current!;
			Assert.True(app.TryGetResource(key, app.ActualThemeVariant, out var value));
			return Assert.IsType<DrawingGroup>(value);
		}

		private static Color IconColor(string key) => ColorOf(Icon(key).Children.OfType<GeometryDrawing>().First().Brush);

		/// <summary>The frame around a day or night tile, its last part</summary>
		private static Color FrameColor(string key) => ColorOf(Icon(key).Children.OfType<GeometryDrawing>().Last().Brush);

		/// <summary>Four periods, so every kind of date shows</summary>
		private static string History()
		{
			var ledger = new PeriodLedger();
			foreach (var begin in new[] { new DateTime(2031, 1, 5), new DateTime(2031, 2, 3), new DateTime(2031, 3, 6), new DateTime(2031, 4, 4) })
				ledger.AcceptPeriod(ledger.AddPeriod(Stamps.FromDate(begin, false), Stamps.FromDate(begin.AddDays(5), false)));
			return PurityFile.Format(ledger.Data);
		}


		/// <summary>The longest date, "Wednesday, 29 September ...", is 170 wide in Segoe UI and 175 in Linux's Liberation Sans; the field's frame and calendar page take 29</summary>
		private const double WholeDateWidth = 210;

		/// <summary>A date line's date field, switch, Skip and two buttons, with their gaps</summary>
		private const double RestOfTheDateLine = 215 + 132 + 80 + 40 + 40;

		private readonly string _root = Path.Combine(Path.GetTempPath(), $"purity-desktop-{Guid.NewGuid():N}");
	}
}
