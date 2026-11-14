using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Purity.UI.Localization;
using Purity.UI.ViewModels;


namespace Purity.UI.Views
{
	/// <summary>The settings. They finish with true for OK, and false for anything else.</summary>
	public partial class SettingsView : DialogView
	{
		/// <summary>For the XAML previewer</summary>
		public SettingsView()
			: this(new SettingsWindowViewModel(string.Empty, null))
		{}

		public SettingsView(SettingsWindowViewModel viewModel)
		{
			InitializeComponent();
			DataContext = _viewModel = viewModel;
			DesktopWidth = 384;
			DesktopHeight = 459;
			if (AppPlatform.IsMobile)
			{
				DataFileBox.IsVisible = false;					// a document has no path to type, only a name to show
				DataFileNameBox.IsVisible = true;
			}
			if (AppPlatform.IsPhone)
				FingerSizes.Apply(this);
		}


		public override object? CancelResult => false;


		private IStorageProvider Storage => TopLevel.GetTopLevel(this)!.StorageProvider;

		private async void PickFile(object? sender, RoutedEventArgs e)
		{
			if (await DataFilePicker.OpenWithNameAsync(Storage) is not { } picked)
				return;

			_viewModel.DataFileName = picked.Name;
			_viewModel.DataFilePath = picked.Location;
		}

		/// <summary>Not in the WPF app. Closes the settings unchanged; the main window then moves the history</summary>
		private async void Move(object? sender, RoutedEventArgs e)
		{
			var name = Path.GetFileName(_viewModel.DataFilePath.Trim());
			if (await DataFilePicker.ChooseNewPlaceAsync(Storage, string.IsNullOrEmpty(name) ? "pureData.json" : name) is not { } path)
				return;

			_viewModel.RequestMove(path);
			Finish(false);
		}

		/// <summary>Not in the WPF app. Opens over the settings and keeps its own choices, whichever way the settings then close</summary>
		private async void OpenReminders(object? sender, RoutedEventArgs e)
		{
			if (_viewModel.NewReminders() is { } reminders)
				await DialogHost.ShowAsync(this, new RemindersView(reminders));
		}

		/// <summary>The WPF app wrote the report beside itself without asking. Here the person chooses where.</summary>
		private async void CreateReport(object? sender, RoutedEventArgs e)
		{
			if (_viewModel.BuildReport() is not { } report)
				return;

			var file = await Storage.SaveFilePickerAsync(new FilePickerSaveOptions
			{
				Title = Loc.T.ReportTitle,
				SuggestedFileName = "periodsLengths.csv",
				DefaultExtension = "csv",
			});
			if (file == null)
				return;

			try
			{
				// Through the file itself rather than a path, which a phone's picker doesn't give
				await using var stream = await file.OpenWriteAsync();
				await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
				await writer.WriteAsync(report);
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
			Finish(false);
		}

		/// <summary>Closes the settings unchanged; the main window then asks, twice, before removing anything</summary>
		private void WipeOut(object? sender, RoutedEventArgs e)
		{
			_viewModel.RequestWipe();
			Finish(false);
		}

		private void Ok(object? sender, RoutedEventArgs e) => Finish(true);

		private void Cancel(object? sender, RoutedEventArgs e) => Finish(false);


		private readonly SettingsWindowViewModel _viewModel;
	}
}
