using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
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

			// Like the WPF app, show the newest periods whenever the list changes
			_viewModel.Periods.CollectionChanged += (_, _) => Dispatcher.UIThread.Post(ScrollToEnd, DispatcherPriority.Background);
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


		private void ScrollToEnd()
		{
			if (_periods.Scroll is ScrollViewer viewer)
				viewer.ScrollToEnd();
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
			if ((Screens.ScreenFromWindow(this) ?? Screens.Primary) is not { } screen)
				return;

			var frame = FrameSize is { } outer ? Math.Max(0, outer.Height - ClientSize.Height) : 0;
			var height = WindowFit.Height(Height, frame, screen.WorkingArea.Height / screen.Scaling);
			if (height >= Height)
				return;

			Height = height;
			Position = new PixelPoint(Position.X, screen.WorkingArea.Y);
			_fitted = true;
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
				_viewModel.Dispose();
				Close();
			}
			finally
			{
				_closing = false;
			}
		}


		private readonly MainWindowViewModel _viewModel;
		private readonly ListBox _periods;
		private bool _closing;
		private bool _mayClose;
		private bool _fitted;
	}
}
