using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
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
			_periods = this.FindControl<ListBox>("Periods")!;

			_viewModel = new MainWindowViewModel(this, options.DataFile, TimeProvider.System, work => Dispatcher.UIThread.Post(() => _ = work()));
			DataContext = _viewModel;

			// Like the WPF app, show the newest periods whenever the list changes
			_viewModel.Periods.CollectionChanged += (_, _) => Dispatcher.UIThread.Post(ScrollToEnd, DispatcherPriority.Background);
			Opened += async (_, _) =>
			{
				FitToScreen();
				await _viewModel.StartAsync();
				if (options.PictureFile is { } picture)
					DispatcherTimer.RunOnce(() => TakePictureAndClose(picture), TimeSpan.FromSeconds(1.5));
			};
			Closing += OnClosing;
		}


		public Task<int?> AskAsync(string text, params string[] buttons) => MessageWindow.ShowAsync(this, text, buttons);

		public Task<bool> ShowSettingsAsync(SettingsWindowViewModel settings) => new SettingsWindow(settings).ShowDialog<bool>(this);


		private void ScrollToEnd()
		{
			if (_periods.Scroll is ScrollViewer viewer)
				viewer.ScrollToEnd();
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
				("screen for windows", screen == null ? "unknown" : $"{screen.WorkingArea.Width / screen.Scaling:0} x {screen.WorkingArea.Height / screen.Scaling:0}"),
				("window", $"{ClientSize.Width:0} x {ClientSize.Height:0}{(_fitted ? ", made shorter to fit" : "")}"),
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
