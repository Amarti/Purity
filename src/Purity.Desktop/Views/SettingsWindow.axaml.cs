using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Purity.Desktop.ViewModels;


namespace Purity.Desktop.Views
{
	public partial class SettingsWindow : Window
	{
		/// <summary>For the XAML previewer</summary>
		public SettingsWindow()
			: this(new SettingsWindowViewModel(string.Empty, () => null))
		{}

		public SettingsWindow(SettingsWindowViewModel viewModel)
		{
			AvaloniaXamlLoader.Load(this);
			DataContext = _viewModel = viewModel;
		}


		private async void PickFile(object? sender, RoutedEventArgs e)
		{
			var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
			{
				Title = "Data file",
				FileTypeFilter = new[] { new FilePickerFileType("Purity data") { Patterns = new[] { "*.json" } }, FilePickerFileTypes.All },
			});
			if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
				_viewModel.DataFilePath = path;
		}

		/// <summary>The WPF app wrote the report beside itself without asking. Here the person chooses where.</summary>
		private async void CreateReport(object? sender, RoutedEventArgs e)
		{
			if (_viewModel.BuildReport() is not { } report)
				return;

			var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
			{
				Title = "Periods lengths report",
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
				await MessageWindow.ShowAsync(this, "The report couldn't be written.\n\n" + ex.Message, "OK");
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
