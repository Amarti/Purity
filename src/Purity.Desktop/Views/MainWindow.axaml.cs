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
			Opened += async (_, _) => await _viewModel.StartAsync();
			Closing += OnClosing;
		}


		public Task<int?> AskAsync(string text, params string[] buttons) => MessageWindow.ShowAsync(this, text, buttons);

		public Task<bool> ShowSettingsAsync(SettingsWindowViewModel settings) => new SettingsWindow(settings).ShowDialog<bool>(this);


		private void ScrollToEnd()
		{
			if (_periods.Scroll is ScrollViewer viewer)
				viewer.ScrollToEnd();
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
	}
}
