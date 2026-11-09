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
	public sealed partial class LookTests : IDisposable
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
			Assert.Equal(Color.Parse("#22324D"), FillColor("AfterDuskIconDrawingGroup"));
			Assert.Equal(Color.Parse("#E3A008"), FillColor("AfterDawnIconDrawingGroup"));

			App.Apply(Appearance.Dark);
			Dispatcher.UIThread.RunJobs();
			Assert.Equal(Color.Parse("#1F2023"), ColorOf(window.Background));
			Assert.Equal(Color.Parse("#E4E6EA"), ColorOf(window.Foreground));
			Assert.Equal(Color.Parse("#7FB0E6"), IconColor("RefreshIconDrawingGroup"));
			Assert.Equal(Color.Parse("#C9D6EA"), FillColor("AfterDuskIconDrawingGroup"));
			Assert.Equal(Color.Parse("#F5BD3A"), FillColor("AfterDawnIconDrawingGroup"));

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
			var viewModel = (MainWindowViewModel)window.DataContext!;
			await Ui.WaitUntil(() => viewModel.Periods.Count == 4);
			foreach (var period in viewModel.Periods)
				period.IsFolded = false;
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
			Assert.Equal(620, window.Width);
			Assert.Equal(861, window.Height);
			Assert.Equal(620, window.MinWidth);
			Assert.Equal(WindowFit.MinimumHeight, window.MinHeight);
		});

		[Fact]
		public Task ThePeriodsKeepTheirWidthAndStayInTheMiddleOfAWiderWindow() => Ui.Run(async () =>
		{
			var window = await OpenPeriodsAsync();
			var periods = window.FindControl<ListBox>("Periods")!;
			double Left() => periods.TranslatePoint(default, window)!.Value.X;
			void Resize(double width)
			{
				window.Width = width;
				Dispatcher.UIThread.RunJobs();
			}

			Assert.Equal(580, periods.Bounds.Width);
			Assert.Equal(20, Left());

			Resize(1000);
			Assert.Equal(580, periods.Bounds.Width);
			Assert.Equal(210, Left());

			Resize(window.MinWidth);
			Assert.Equal(580, periods.Bounds.Width);
			Assert.Equal(20, Left());

			// Only Skip, 78 wide at its widest, depends on the font
			var events = periods.GetVisualDescendants().OfType<ListBox>().First(el => el.Classes.Contains("Events"));
			Assert.True(events.Bounds.Width - RestOfTheDateLine >= 78, $"The lines have {events.Bounds.Width} to fit in");

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
			Assert.Equal(620, window.Width);
			Assert.Equal(WindowFit.MinimumHeight, window.Height);
			Assert.Equal(WindowState.Normal, window.WindowState);
			window = new MainWindow(new StartOptions(Path.Combine(_root, "pureData.json"), Path.Combine(_root, "picture.png")));
			Assert.Equal(620, window.Width);
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
			var choice = window.GetVisualDescendants().OfType<ComboBox>().Single(el => el.ItemCount == 3);

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
			var period = viewModel.Periods[0];
			var begin = period.Period.Begin;
			var end = period.Period.End;
			var (beginSwitch, endSwitch) = SwitchesOf(window, period);

			Assert.Equal("From day or night", AutomationProperties.GetName(beginSwitch));
			Assert.Equal("To day or night", AutomationProperties.GetName(endSwitch));
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
			var (beginSwitch, endSwitch) = SwitchesOf(window, viewModel.Periods[0]);
			var card = beginSwitch.FindAncestorOfType<Border>(includeSelf: false)!;
			while (!card.Classes.Contains("Period"))
				card = card.FindAncestorOfType<Border>(includeSelf: false)!;
			var fields = card.GetVisualDescendants().OfType<DateField>().ToArray();
			var events = card.GetVisualDescendants().OfType<ListBox>().Single(el => el.Classes.Contains("Events"));
			Point At(Visual visual) => visual.TranslatePoint(default, card)!.Value;

			Assert.Equal(2, fields.Length);
			Assert.All(fields, el => Assert.Equal(WholeDateWidth, el.Bounds.Width));
			Assert.Equal(At(fields[0]).X, At(fields[1]).X);
			Assert.Equal(At(fields[0]).Y + 30, At(fields[1]).Y);
			Assert.Equal(At(beginSwitch).X, At(endSwitch).X);
			Assert.Equal(At(fields[0]).Y, At(beginSwitch).Y);
			var startDrop = ((Grid)fields[0].Parent!).Children.OfType<Image>().Single();
			Assert.Equal(At(events).X, At(startDrop).X);
			Assert.True(At(startDrop).X + startDrop.Bounds.Width <= At(fields[0]).X, "The drop should be before the date");
			Assert.Equal(126, beginSwitch.Bounds.Width);

			window.Close();
			await Ui.WaitUntil(() => !window.IsVisible);
		});

		[Fact]
		public Task TheFirstPeriodHasRemoveAtTheLeftAndTheOthersTheArrowThatFoldsThem() => Ui.Run(async () =>
		{
			var window = await OpenPeriodsAsync();
			var viewModel = (MainWindowViewModel)window.DataContext!;
			var periods = window.FindControl<ListBox>("Periods")!;
			var (first, second) = (viewModel.Periods[0], viewModel.Periods[1]);
			second.IsFolded = false;
			await Ui.WaitUntil(() => SwitchCount(window, second) == 2);
			Button Named(PeriodViewModel period, string name) =>
				window.GetVisualDescendants().OfType<Button>().Single(el => el.DataContext == period && AutomationProperties.GetName(el) == name);

			var remove = Named(first, "Remove period");
			Assert.Equal(0, Grid.GetColumn(remove));
			Assert.True(remove.IsEffectivelyVisible);
			Assert.False(Named(first, "Fold this period").IsEffectivelyVisible);
			Assert.False(Named(second, "Remove period").IsEffectivelyVisible);
			Assert.True(Named(second, "Fold this period").IsEffectivelyVisible);

			// The highlight around a period is as thin at the sides as above and below
			Assert.Equal(new Thickness(1), ((ListBoxItem)periods.ContainerFromIndex(0)!).Padding);

			window.Close();
			await Ui.WaitUntil(() => !window.IsVisible);
		});

		[Fact]
		public Task TheWideButtonIsAPinkDropThenABlueOneAndPeriodsHaveNoTick() => Themed(async () =>
		{
			App.Apply(Appearance.Light);
			var window = await OpenPeriodsAsync();
			var viewModel = (MainWindowViewModel)window.DataContext!;
			var button = window.GetVisualDescendants().OfType<Button>().Single(el => AutomationProperties.GetName(el) == "New period");
			Image Shown() => button.GetVisualDescendants().OfType<Panel>().Single(el => el.Classes.Contains("Shown")).Children.OfType<Image>().Single();

			Assert.Same(Resource("AddPeriodIconDrawingImage"), Shown().Source);
			Assert.Equal(Color.Parse("#FF0061"), IconColor("AddPeriodIconDrawingGroup"));
			Assert.Equal(Color.Parse("#5683A9"), IconColor("ClosePeriodIconDrawingGroup"));

			await viewModel.AddOrClosePeriodCommand.ExecuteAsync(null);
			Dispatcher.UIThread.RunJobs();
			Assert.Equal("Close period", AutomationProperties.GetName(button));
			Assert.Same(Resource("ClosePeriodIconDrawingImage"), Shown().Source);
			Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), el => AutomationProperties.GetName(el) == "Accept period");

			App.Apply(Appearance.Dark);
			Dispatcher.UIThread.RunJobs();
			Assert.Equal(Color.Parse("#7FB0E6"), IconColor("ClosePeriodIconDrawingGroup"));

			window.Close();
			await Ui.WaitUntil(() => !window.IsVisible);
		});

		[Fact]
		public Task EachDateHasADropAndAnOpenPeriodsEndDropIsHollow() => Themed(async () =>
		{
			App.Apply(Appearance.Light);
			var window = await OpenPeriodsAsync();
			var viewModel = (MainWindowViewModel)window.DataContext!;
			var pink = Resource("AddPeriodIconDrawingImage");
			var blue = Resource("ClosePeriodIconDrawingImage");
			var hollow = Resource("OpenEndIconDrawingImage");

			Assert.Equal(new[] { pink, blue }, DropsOf(window, viewModel.Periods[0]));

			viewModel.AddPeriodCommand.Execute(null);
			var open = viewModel.Periods[0];
			await Ui.WaitUntil(() => SwitchCount(window, open) == 2);
			Dispatcher.UIThread.RunJobs();
			Assert.Equal(new[] { pink, hollow }, DropsOf(window, open));

			var outline = Icon("OpenEndIconDrawingGroup").Children.OfType<GeometryDrawing>().Single();
			Assert.Equal(Colors.Transparent, ColorOf(outline.Brush));
			Assert.Equal(Color.Parse("#5683A9"), ColorOf(outline.Pen!.Brush));
			App.Apply(Appearance.Dark);
			Dispatcher.UIThread.RunJobs();
			Assert.Equal(Color.Parse("#7FB0E6"), ColorOf(outline.Pen!.Brush));

			window.Close();
			await Ui.WaitUntil(() => !window.IsVisible);
		});

		[Fact]
		public Task TheNextStripTakesTheColorsOfThatKindOfDatesChip() => Themed(async () =>
		{
			App.Apply(Appearance.Light);
			var window = await OpenPeriodsAsync();
			var viewModel = (MainWindowViewModel)window.DataContext!;
			var periods = window.FindControl<ListBox>("Periods")!;
			var strip = window.GetVisualDescendants().OfType<EventChip>().Single(el => el.Classes.Contains("Strip"));
			var type = strip.GetVisualDescendants().OfType<TextBlock>().First(el => el.Text == viewModel.NextEvent!.TypeRepr);

			Assert.True(strip.IsVisible);
			Assert.Equal(viewModel.NextEvent!.Type, strip.Type);
			Assert.Equal(PurityEventType.Mikveh, strip.Type);
			Assert.Equal(Color.Parse("#E2EBF3"), ColorOf(strip.Background));
			Assert.Equal(Color.Parse("#24496A"), ColorOf(type.Foreground));
			Assert.True(strip.TranslatePoint(new Point(0, strip.Bounds.Height), window)!.Value.Y <= periods.TranslatePoint(default, window)!.Value.Y, "The strip should be above the list");

			var card = periods.GetVisualDescendants().OfType<Border>().First(el => el.Classes.Contains("Period"));
			Assert.Equal(card.TranslatePoint(default, window)!.Value.X, strip.TranslatePoint(default, window)!.Value.X);
			Assert.Equal(card.Bounds.Width, strip.Bounds.Width);

			App.Apply(Appearance.Dark);
			Dispatcher.UIThread.RunJobs();
			Assert.Equal(Color.Parse("#2C4A66"), ColorOf(strip.Background));
			Assert.Equal(Color.Parse("#E2EBF3"), ColorOf(type.Foreground));

			strip.Type = PurityEventType.OnaBeinonit;
			Dispatcher.UIThread.RunJobs();
			Assert.Equal(Color.Parse("#633806"), ColorOf(strip.Background));

			window.Close();
			await Ui.WaitUntil(() => !window.IsVisible);
		});

		[Fact]
		public Task TheNextStripsDateLinesUpWithTheDatesInTheList() => Ui.Run(async () =>
		{
			var window = await OpenPeriodsAsync();
			var periods = window.FindControl<ListBox>("Periods")!;
			var strip = window.GetVisualDescendants().OfType<EventChip>().Single(el => el.Classes.Contains("Strip"));
			var cells = ((Grid)strip.Child!).Children;
			var row = periods.GetVisualDescendants().OfType<StackPanel>().First(el => el.Classes.Contains("EventLine"));
			var texts = row.Children.OfType<TextBlock>().ToArray();
			double Left(Visual visual) => visual.TranslatePoint(default, window)!.Value.X;
			Control Cell(int column) => cells.Single(el => Grid.GetColumn(el) == column && (column != 0 || el is TextBlock));

			Assert.Equal(Left(row.Children.OfType<Image>().First(el => el.IsVisible)), Left(Cell(2)));
			Assert.Equal(Left(texts[0]), Left(Cell(3)));
			Assert.Equal(Left(texts[1]), Left(Cell(4)));

			var chip = row.GetVisualDescendants().OfType<EventChip>().Single();
			Assert.Equal(Left(chip.Child!), Left(Cell(1)));

			window.Close();
			await Ui.WaitUntil(() => !window.IsVisible);
		});

		[Fact]
		public Task TheNextStripGoesWhenAPeriodIsOpenedAndReturnsWhenItIsRemoved() => Ui.Run(async () =>
		{
			var window = await OpenPeriodsAsync();
			var viewModel = (MainWindowViewModel)window.DataContext!;
			var strip = window.GetVisualDescendants().OfType<EventChip>().Single(el => el.Classes.Contains("Strip"));
			Assert.True(strip.IsVisible);

			viewModel.AddPeriodCommand.Execute(null);
			await Ui.WaitUntil(() => !strip.IsVisible);

			await viewModel.Periods[0].RemoveCommand.ExecuteAsync(null);
			await Ui.WaitUntil(() => strip.IsVisible);

			window.Close();
			await Ui.WaitUntil(() => !window.IsVisible);
		});

		[Fact]
		public Task DatesThatHavePassedAreGreyedAndTheStripGoes() => Ui.Run(async () =>
		{
			var ledger = new PeriodLedger();
			ledger.AcceptPeriod(ledger.AddPeriod(Stamps.FromDate(new DateTime(2001, 1, 7), false), Stamps.FromDate(new DateTime(2001, 1, 12), false)));
			var dataFile = Path.Combine(_root, "pureData.json");
			File.WriteAllText(dataFile, PurityFile.Format(ledger.Data));
			var window = new MainWindow(new StartOptions(dataFile));
			window.Show();
			IEnumerable<StackPanel> Lines() => window.GetVisualDescendants().OfType<StackPanel>().Where(el => el.Classes.Contains("EventLine"));
			await Ui.WaitUntil(() => Lines().Any());

			Assert.All(Lines(), el => Assert.Equal(0.4, el.Opacity));
			Assert.False(window.GetVisualDescendants().OfType<EventChip>().Single(el => el.Classes.Contains("Strip")).IsVisible);
			Assert.All(window.GetVisualDescendants().OfType<Grid>().Where(el => el.Classes.Contains("TodayLine")), el => Assert.False(el.IsVisible));

			window.Close();
			await Ui.WaitUntil(() => !window.IsVisible);
		});

		[Fact]
		public Task AnOpenPeriodStaysAboveTheListInLineWithItsPeriods() => Ui.Run(async () =>
		{
			var window = await OpenPeriodsAsync();
			var viewModel = (MainWindowViewModel)window.DataContext!;
			var periods = window.FindControl<ListBox>("Periods")!;
			var count = periods.ItemCount;
			viewModel.AddPeriodCommand.Execute(null);
			var open = viewModel.Periods[0];
			await Ui.WaitUntil(() => SwitchCount(window, open) == 2);
			Dispatcher.UIThread.RunJobs();

			Assert.Equal(count, periods.ItemCount);
			Assert.DoesNotContain(periods.GetVisualDescendants().OfType<DayNightSwitch>(), el => el.DataContext == open);
			var card = window.GetVisualDescendants().OfType<Border>().Single(el => el.Classes.Contains("Period") && el.DataContext == open);
			var inList = periods.GetVisualDescendants().OfType<Border>().First(el => el.Classes.Contains("Period"));
			double Left(Visual visual) => visual.TranslatePoint(default, window)!.Value.X;
			double Bottom(Control control) => control.TranslatePoint(new Point(0, control.Bounds.Height), window)!.Value.Y;
			Assert.True(Bottom(card) <= periods.TranslatePoint(default, window)!.Value.Y, "The open period should be above the list");
			Assert.Equal(Left(inList), Left(card));
			Assert.Equal(inList.Bounds.Width, card.Bounds.Width);
			Assert.DoesNotContain(card.GetVisualDescendants().OfType<ListBox>(), el => el.IsEffectivelyVisible);

			window.Close();
			await Ui.WaitUntil(() => !window.IsVisible);
		});

		[Fact]
		public Task AnEmptyHistorySaysWhatToDoUntilAPeriodIsAdded() => Ui.Run(async () =>
		{
			var dataFile = Path.Combine(_root, "pureData.json");
			File.WriteAllText(dataFile, "[]");
			var window = new MainWindow(new StartOptions(dataFile));
			window.Show();
			var viewModel = (MainWindowViewModel)window.DataContext!;
			var hint = window.GetVisualDescendants().OfType<StackPanel>().Single(el => el.Classes.Contains("EmptyHint"));

			await Ui.WaitUntil(() => hint.IsVisible);
			Assert.True(Application.Current!.TryGetResource("AddPeriodIconDrawingImage", Application.Current.ActualThemeVariant, out var drop));
			Assert.Same(drop, hint.GetVisualDescendants().OfType<Image>().Single().Source);
			viewModel.AddPeriodCommand.Execute(null);
			await Ui.WaitUntil(() => !hint.IsVisible);

			window.Close();
			await Ui.WaitUntil(() => !window.IsVisible);
		});

		[Fact]
		public Task ADateTypedJustBeforeClosingIsTheOneThatCounts() => Ui.Run(async () =>
		{
			var window = await OpenPeriodsAsync();
			var viewModel = (MainWindowViewModel)window.DataContext!;
			viewModel.AddPeriodCommand.Execute(null);
			var open = viewModel.Periods[0];
			await Ui.WaitUntil(() => window.GetVisualDescendants().OfType<DateField>().Count(el => el.DataContext == open) == 2);

			var to = window.GetVisualDescendants().OfType<DateField>().Where(el => el.DataContext == open).ElementAt(1);
			var text = to.GetVisualDescendants().OfType<TextBox>().Single();
			text.Focus();
			text.Text = "Thursday, 18 March 2010";		// already past, so closing asks nothing
			var wide = window.GetVisualDescendants().OfType<Button>().Single(el => AutomationProperties.GetName(el) == "Close period");

			Press(wide, window);
			await Ui.WaitUntil(() => open.IsClosed);

			Assert.Equal(new DateTime(2010, 3, 18), open.EndDate);
			window.Close();
			await Ui.WaitUntil(() => !window.IsVisible);
		});

		[Fact]
		public Task TheButtonsAreAboveTheListAndTheNewestPeriodIsOnTop() => Ui.Run(async () =>
		{
			var window = await OpenPeriodsAsync();
			var viewModel = (MainWindowViewModel)window.DataContext!;
			var periods = window.FindControl<ListBox>("Periods")!;
			double Top(Visual visual) => visual.TranslatePoint(default, window)!.Value.Y;

			var buttons = TopButtons(window);
			Assert.Equal(3, buttons.Length);
			Assert.All(buttons, el => Assert.True(Top(el) + el.Bounds.Height < Top(periods), "The buttons should be above the list"));

			Assert.Equal(0, periods.Scroll!.Offset.Y);
			Assert.Equal(viewModel.Periods.Max(el => el.Period.Begin), viewModel.Periods[0].Period.Begin);
			var shown = periods.GetVisualDescendants().OfType<LazyPeriod>().Where(el => el.IsBuilt).OrderBy(Top).ToArray();
			Assert.Same(viewModel.Periods[0], shown[0].DataContext);
			Assert.Same(viewModel.Periods[1], shown[1].DataContext);

			window.Close();
			await Ui.WaitUntil(() => !window.IsVisible);
		});

		[Fact]
		public Task TabGoesFromTheButtonsToTheNewestPeriod() => Ui.Run(async () =>
		{
			var window = await OpenPeriodsAsync();
			var viewModel = (MainWindowViewModel)window.DataContext!;
			var names = new List<string?>();
			Control Focused() => (Control)window.FocusManager!.GetFocusedElement()!;
			void Tab()
			{
				window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.None, null);
				window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.None, null);
				Dispatcher.UIThread.RunJobs();
			}

			TopButtons(window)[0].Focus();
			names.Add(AutomationProperties.GetName(Focused()));
			Tab();
			names.Add(AutomationProperties.GetName(Focused()));
			Tab();
			names.Add(AutomationProperties.GetName(Focused()));
			Assert.Equal(new[] { "Settings", "New period", "Refresh" }, names);

			Tab();
			Assert.Same(viewModel.Periods[0], Focused().DataContext);

			window.Close();
			await Ui.WaitUntil(() => !window.IsVisible);
		});


		private static Button[] TopButtons(Window window) =>
			window.GetVisualDescendants().OfType<Button>().Where(el => AutomationProperties.GetName(el) is "Settings" or "New period" or "Refresh").ToArray();


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

		private static object Resource(string key)
		{
			Assert.True(Application.Current!.TryGetResource(key, Application.Current.ActualThemeVariant, out var value));
			return value!;
		}

		/// <summary>The drops showing before a period's two dates, start first</summary>
		private static object?[] DropsOf(Window window, PeriodViewModel period)
		{
			var field = window.GetVisualDescendants().OfType<DateField>().First(el => el.DataContext == period);
			var column = ((Grid)field.Parent!).Children.Where(el => Grid.GetColumn(el) == 1).ToArray();
			return column.SelectMany(el => el is Image ? new[] { (Image)el } : el.GetVisualDescendants().OfType<Image>()).Where(el => el.IsVisible).Select(el => (object?)el.Source).ToArray();
		}

		private static int SwitchCount(Window window, PeriodViewModel period) =>
			window.GetVisualDescendants().OfType<DayNightSwitch>().Count(el => el.DataContext == period);

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

		private static IEnumerable<EventChip> Chips(Window window) => window.GetVisualDescendants().OfType<EventChip>().Where(el => !el.Classes.Contains("Strip"));

		private static Color ColorOf(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

		private static DrawingGroup Icon(string key)
		{
			var app = Application.Current!;
			Assert.True(app.TryGetResource(key, app.ActualThemeVariant, out var value));
			return Assert.IsType<DrawingGroup>(value);
		}

		private static Color IconColor(string key) => ColorOf(Icon(key).Children.OfType<GeometryDrawing>().First().Brush);

		/// <summary>The color a day or night icon is drawn in</summary>
		private static Color FillColor(string key) => ColorOf(Icon(key).Children.OfType<GeometryDrawing>().Last().Brush);

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

		/// <summary>A date line's drop, date field and switch, with their gaps and the one before Skip</summary>
		private const double RestOfTheDateLine = 13 + 215 + 132 + 15;

		private readonly string _root = Path.Combine(Path.GetTempPath(), $"purity-desktop-{Guid.NewGuid():N}");
	}
}
