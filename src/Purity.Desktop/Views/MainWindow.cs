using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Purity.UI.Views;


namespace Purity.Desktop.Views
{
	/// <summary>
	/// The desktop's window around the history. WPF's 710x900 included the window frame, so the size
	/// here is the area inside it on Windows. It can be resized (T8), and it opens where it was last time.
	/// </summary>
	public sealed class MainWindow : Window, IAppDialogs, IDialogHost
	{
		/// <summary>For the XAML previewer</summary>
		public MainWindow()
			: this(new StartOptions(null))
		{}

		public MainWindow(StartOptions options)
		{
			Title = "Purity " + AppVersion.Number;			// WPF's title was "Purity" alone
			Width = 620;
			Height = 600;
			MinWidth = 620;
			MinHeight = 400;
			WindowStartupLocation = WindowStartupLocation.CenterScreen;
			Icon = DialogWindow.AppIcon;
			var direction = Loc.Instance.FollowFlowDirection(this);
			Closed += (_, _) => direction.Dispose();
			_view = new MainView();
			Content = _view;

			// Held from here, on the UI thread: notices come from the file watcher's own thread, and asking
			// for the UI thread from there can make that thread the UI one, as happened between tests
			var ui = Dispatcher.UIThread;
			_viewModel = new MainWindowViewModel(this, options.DataFile, TimeProvider.System, work => ui.Post(() => _ = work()), reminderConnections: settings => Connections(options, settings));
			DataContext = _viewModel;
			_options = options;

			// Before the window shows, so it never flashes the other theme or jumps into place
			App.Apply(_viewModel.Appearance);
			_viewModel.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName == nameof(MainWindowViewModel.Appearance))
					App.Apply(_viewModel.Appearance);
			};
			if (options.PictureFile == null)						// pictures are always of the window's starting size
				RestorePlacement(_viewModel.WindowPlacement);
			PositionChanged += (_, _) => Dispatcher.UIThread.Post(TrackPlacement, DispatcherPriority.Background);

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


		public void Tell(string text, MessageKind kind) => _view.Messages.Tell(text, kind);

		public Task<int?> AskAsync(Question question, params string[] buttons) => _view.Messages.AskAsync(question, buttons);

		/// <summary>Another start of Purity asked for this window instead of opening a second one</summary>
		public void BringToFront()
		{
			if (WindowState == WindowState.Minimized)
				WindowState = WindowState.Normal;
			Activate();
		}

		public async Task<bool> ShowSettingsAsync(SettingsWindowViewModel settings) => await ShowAsync(new SettingsView(settings)) is true;

		public async Task<Storage.Backup?> ChooseBackupAsync(IReadOnlyList<BackupChoice> choices) => await ShowAsync(new BackupsView(choices)) as Storage.Backup;

		public Task<string?> PickDataFileAsync() => DataFilePicker.OpenAsync(StorageProvider);

		/// <summary>After the question that leads to it is answered, so the close isn't caught up in it</summary>
		public void CloseApp() => Dispatcher.UIThread.Post(Close);

		public Task<object?> ShowAsync(DialogView view) => DialogWindow.For(view).ShowDialog<object?>(this);


		private async Task OpenInBrowserAsync(Uri page) => await Launcher.LaunchUriAsync(page);

		private ReminderConnections Connections(StartOptions options, DesktopSettings settings) => new(new Dictionary<ReminderPlace, IReminderConnection>
		{
			[ReminderPlace.Google] = GoogleConnection.ForThisBuild(options, settings, OpenInBrowserAsync),
			[ReminderPlace.ICloud] = CalDavConnection.ForICloud(settings),
			[ReminderPlace.CalDav] = CalDavConnection.ForOther(settings),
		});

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
			_view.Messages.CancelQuestion();						// closing the window answers nothing, as closing a message box did
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


		private readonly MainWindowViewModel _viewModel;
		private readonly MainView _view;
		private readonly StartOptions _options;
		private WindowPlacement? _lastNormal;
		private bool _closing;
		private bool _mayClose;
		private bool _fitted;
	}
}
