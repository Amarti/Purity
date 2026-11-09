using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Purity.Desktop.ViewModels;


namespace Purity.Desktop.Views
{
	public partial class MainWindow : Window, IAppDialogs
	{
		/// <summary>For the XAML previewer</summary>
		public MainWindow()
			: this(new StartOptions(null))
		{}

		public MainWindow(StartOptions options)
		{
			AvaloniaXamlLoader.Load(this);
			Title = "Purity " + AppVersion.Number;			// WPF's title was "Purity" alone
			_periods = this.FindControl<ListBox>("Periods")!;
			_periods.AddHandler(KeyDownEvent, OnPeriodsKeyDown, RoutingStrategies.Tunnel);

			_viewModel = new MainWindowViewModel(this, options.DataFile, TimeProvider.System, work => Dispatcher.UIThread.Post(() => _ = work()));
			DataContext = _viewModel;
			_options = options;

			// Before the window shows, so it never flashes the other theme or jumps into place
			App.Apply(_viewModel.Appearance);
			_newPeriodDrop = this.FindControl<Image>("NewPeriodDrop")!;
			_closePeriodDrop = this.FindControl<Image>("ClosePeriodDrop")!;
			_viewModel.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName == nameof(MainWindowViewModel.Appearance))
					App.Apply(_viewModel.Appearance);
				else if (e.PropertyName == nameof(MainWindowViewModel.HasOpenPeriod))
					PopDropIfChanged();
			};
			if (options.PictureFile == null)						// pictures are always of the window's starting size
				RestorePlacement(_viewModel.WindowPlacement);
			PositionChanged += (_, _) => Dispatcher.UIThread.Post(TrackPlacement, DispatcherPriority.Background);

			// Show the newest periods whenever the list changes, as the WPF app does; here they're at the top
			_viewModel.ClosedPeriods.CollectionChanged += (_, _) => Dispatcher.UIThread.Post(ScrollToNewest, DispatcherPriority.Background);
			Opened += async (_, _) =>
			{
				FitToScreen();
				if (!await _viewModel.StartAsync())
				{
					Close();								// the first question was closed: nothing was set, so it comes back next time
					return;
				}
				if (options.PictureFile is { } picture)
					DispatcherTimer.RunOnce(() => TakePictureAndClose(picture), TimeSpan.FromSeconds(1.5));
			};
			// A sync app can leave a copy beside the data file at any time; coming back to the window is a good moment to look
			Activated += async (_, _) => await _viewModel.CheckForCopiesAsync();
			Closing += OnClosing;
		}


		public Task<int?> AskAsync(string text, params string[] buttons) => MessageWindow.ShowAsync(this, text, buttons);

		/// <summary>Another start of Purity asked for this window instead of opening a second one</summary>
		public void BringToFront()
		{
			if (WindowState == WindowState.Minimized)
				WindowState = WindowState.Normal;
			Activate();
		}

		public Task<bool> ShowSettingsAsync(SettingsWindowViewModel settings) => new SettingsWindow(settings).ShowDialog<bool>(this);

		public Task<Storage.Backup?> ChooseBackupAsync(IReadOnlyList<BackupChoice> choices) => BackupsWindow.ShowAsync(this, choices);

		public Task<string?> PickDataFileAsync() => DataFilePicker.OpenAsync(StorageProvider);


		/// <summary>Draws the eye to the wide button when what it does changes (E4), along with the fade between its faces in the styles</summary>
		private void PopDropIfChanged()
		{
			if (_viewModel.HasOpenPeriod == _dropClosesPeriod)
				return;

			_dropClosesPeriod = _viewModel.HasOpenPeriod;
			_ = DropPop.RunAsync(_dropClosesPeriod ? _closePeriodDrop : _newPeriodDrop);
		}

		private static KeyFrame DropSize(double cue, double scale) => new()
		{
			Cue = new Cue(cue),
			Setters =
			{
				new Setter(ScaleTransform.ScaleXProperty, scale),
				new Setter(ScaleTransform.ScaleYProperty, scale),
			},
		};

		private void ScrollToNewest()
		{
			if (_periods.Scroll is ScrollViewer viewer)
				viewer.ScrollToHome();
		}

		/// <summary>
		/// Page Up and Page Down turn the list a screen at a time, wherever the focus is in it. A
		/// period's list of events would otherwise take them, and do nothing visible.
		/// </summary>
		private void OnPeriodsKeyDown(object? sender, KeyEventArgs e)
		{
			// An open calendar is outside the list's own tree, and uses them to change month
			if (e.KeyModifiers != KeyModifiers.None || _periods.Scroll is not ScrollViewer viewer
				|| e.Source is not Visual source || source != _periods && !_periods.IsVisualAncestorOf(source))
				return;

			if (e.Key == Key.PageUp)
				viewer.PageUp();
			else if (e.Key == Key.PageDown)
				viewer.PageDown();
			else
				return;
			e.Handled = true;
		}

		/// <inheritdoc cref="WindowFit"/>
		private void FitToScreen()
		{
			if (WindowState != WindowState.Normal || (Screens.ScreenFromWindow(this) ?? Screens.Primary) is not { } screen)
				return;

			var frameWidth = FrameSize is { } outerWidth ? Math.Max(0, outerWidth.Width - ClientSize.Width) : 0;
			var width = WindowFit.Width(Width, frameWidth, screen.WorkingArea.Width / screen.Scaling, MinWidth);
			if (width < Width)
			{
				Width = width;
				Position = new PixelPoint(screen.WorkingArea.X, Position.Y);
			}

			var frame = FrameSize is { } outer ? Math.Max(0, outer.Height - ClientSize.Height) : 0;
			var height = WindowFit.Height(Height, frame, screen.WorkingArea.Height / screen.Scaling);
			if (height >= Height)
				return;

			Height = height;
			Position = new PixelPoint(Position.X, screen.WorkingArea.Y);
			_fitted = true;
		}

		/// <summary>Opens where the window was last time (T8): its size always, its place only while that is still on a screen</summary>
		private void RestorePlacement(WindowPlacement? placement)
		{
			if (placement == null)
				return;

			Width = Math.Max(MinWidth, placement.Width);
			Height = Math.Max(MinHeight, placement.Height);
			var corner = new PixelPoint(placement.X, placement.Y);
			if (WindowFit.ShowsTitleBar(corner, Screens.All.Select(el => el.WorkingArea)))
			{
				WindowStartupLocation = WindowStartupLocation.Manual;
				Position = corner;
			}
			if (placement.Maximized)
				WindowState = WindowState.Maximized;
			_lastNormal = placement with { Maximized = false };
		}

		/// <summary>
		/// Notes the place and size while the window is neither maximized nor minimized, which is
		/// where it goes back to. Runs after the window settles, so a window being maximized isn't
		/// taken for one the person made that big.
		/// </summary>
		private void TrackPlacement()
		{
			if (IsVisible && WindowState == WindowState.Normal)
				_lastNormal = new WindowPlacement(Position.X, Position.Y, ClientSize.Width, ClientSize.Height, false);
		}

		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == ClientSizeProperty || change.Property == WindowStateProperty)
				Dispatcher.UIThread.Post(TrackPlacement, DispatcherPriority.Background);
		}

		private void RememberPlacement()
		{
			if (_options.PictureFile != null)
				return;

			TrackPlacement();
			if (_lastNormal is { } normal)
				_viewModel.RememberWindow(normal with { Maximized = WindowState == WindowState.Maximized });
		}

		private void TakePictureAndClose(string file)
		{
			var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
			var saved = WindowPicture.Save(this, file, new (string, object?)[]
			{
				("version", AppVersion.Number),
				("screen for windows", screen == null ? "unknown" : $"{screen.WorkingArea.Width / screen.Scaling:0} x {screen.WorkingArea.Height / screen.Scaling:0}"),
				("window", $"{ClientSize.Width:0} x {ClientSize.Height:0}{(_fitted ? ", made shorter to fit" : "")}"),
				("font", FontFamily.Name),
				("self-test", _viewModel.SelfTestResult?.ToString() ?? "not run"),
				("periods", _viewModel.Periods.Count),
				("warning", _viewModel.Warning ?? "none"),
			});
			Program.ExitCode = saved ? 0 : 1;
			Close();
		}

		/// <summary>Saving takes a moment, so the first close is held back until it's done</summary>
		private async void OnClosing(object? sender, WindowClosingEventArgs e)
		{
			if (_mayClose)
				return;

			e.Cancel = true;
			if (_closing)
				return;

			_closing = true;
			try
			{
				if (!await _viewModel.PrepareToCloseAsync())
					return;

				_mayClose = true;
				RememberPlacement();
				_viewModel.Dispose();
				Close();
			}
			finally
			{
				_closing = false;
			}
		}


		private static readonly Animation DropPop = new()
		{
			Duration = TimeSpan.FromMilliseconds(380),
			Easing = new CubicEaseOut(),
			Children = { DropSize(0, 0.6), DropSize(0.6, 1.25), DropSize(1, 1) },
		};

		private readonly MainWindowViewModel _viewModel;
		private readonly StartOptions _options;
		private readonly ListBox _periods;
		private readonly Image _newPeriodDrop;
		private readonly Image _closePeriodDrop;
		private bool _dropClosesPeriod;
		private WindowPlacement? _lastNormal;
		private bool _closing;
		private bool _mayClose;
		private bool _fitted;
	}
}
