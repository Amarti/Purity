using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Purity.Desktop.Localization;
using Purity.Desktop.ViewModels;


namespace Purity.Desktop.Views
{
	public partial class SettingsWindow : Window
	{
		/// <summary>For the XAML previewer</summary>
		public SettingsWindow()
			: this(new SettingsWindowViewModel(string.Empty, null))
		{}

		public SettingsWindow(SettingsWindowViewModel viewModel)
		{
			AvaloniaXamlLoader.Load(this);
			DataContext = _viewModel = viewModel;
		}


		private async void PickFile(object? sender, RoutedEventArgs e)
		{
			if (await DataFilePicker.OpenAsync(StorageProvider) is { } path)
				_viewModel.DataFilePath = path;
		}

		/// <summary>Not in the WPF app. Closes the settings unchanged; the main window then moves the history</summary>
		private async void Move(object? sender, RoutedEventArgs e)
		{
			var name = Path.GetFileName(_viewModel.DataFilePath.Trim());
			if (await DataFilePicker.ChooseNewPlaceAsync(StorageProvider, string.IsNullOrEmpty(name) ? "pureData.json" : name) is not { } path)
				return;

			_viewModel.RequestMove(path);
			Close(false);
		}

		/// <summary>Not in the WPF app. Opens over the settings and keeps its own choices, whichever way the settings then close</summary>
		private async void OpenReminders(object? sender, RoutedEventArgs e)
		{
			if (_viewModel.NewReminders() is { } reminders)
				await new RemindersWindow(reminders).ShowDialog(this);
		}

		/// <summary>The WPF app wrote the report beside itself without asking. Here the person chooses where.</summary>
		private async void CreateReport(object? sender, RoutedEventArgs e)
		{
			if (_viewModel.BuildReport() is not { } report)
				return;

			var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
			{
				Title = Loc.T.ReportTitle,
				SuggestedFileName = "periodsLengths.csv",
				DefaultExtension = "csv",
			});
			if (file?.TryGetLocalPath() is not { } path)
				return;

			try
			{
				await File.WriteAllTextAsync(path, report, new UTF8Encoding(false));
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				this.FindControl<MessageLayer>("Messages")!.Tell(Loc.T.ReportFailed + "\n\n" + ex.Message, MessageKind.Problem);
			}
		}

		/// <summary>Closes the settings unchanged; the main window then lists the earlier versions</summary>
		private void Restore(object? sender, RoutedEventArgs e)
		{
			_viewModel.RequestRestore();
			Close(false);
		}

		private void Ok(object? sender, RoutedEventArgs e) => Close(true);

		private void Cancel(object? sender, RoutedEventArgs e) => Close(false);


		private readonly SettingsWindowViewModel _viewModel;
	}
}
