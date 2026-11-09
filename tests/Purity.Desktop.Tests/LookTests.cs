using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Purity.Core;
using Purity.Desktop.ViewModels;
using Purity.Desktop.Views;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>
	/// The look the owner approved for 2.2.0: colored chips (T4), rounded periods (T6), a dark
	/// theme (T7) and a window that can be resized (T8). Every date is made up. The theme is one
	/// setting for the whole app, so every test puts it back.
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
			Assert.Equal(694, window.MinWidth);
			Assert.Equal(WindowFit.MinimumHeight, window.MinHeight);
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
			Assert.Equal(694, window.Width);
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


		private readonly string _root = Path.Combine(Path.GetTempPath(), $"purity-desktop-{Guid.NewGuid():N}");
	}
}
