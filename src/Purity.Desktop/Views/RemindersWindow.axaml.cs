using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Purity.Core;
using Purity.Desktop.Localization;
using Purity.Desktop.ViewModels;


namespace Purity.Desktop.Views
{
	public partial class RemindersWindow : Window
	{
		/// <summary>For the XAML previewer</summary>
		public RemindersWindow()
			: this(new RemindersWindowViewModel(new ReminderSetup(), Array.Empty<PurityPeriod>(), TimeProvider.System))
		{}

		public RemindersWindow(RemindersWindowViewModel viewModel)
		{
			AvaloniaXamlLoader.Load(this);
			DataContext = _viewModel = viewModel;
		}


		private async void SaveFile(object? sender, RoutedEventArgs e)
		{
			var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
			{
				Title = Loc.T.RemindersFileTitle,
				SuggestedFileName = "reminders.ics",
				DefaultExtension = "ics",
				FileTypeChoices = new[] { new FilePickerFileType(Loc.T.RemindersFileType) { Patterns = new[] { "*.ics" } } },
			});
			if (file?.TryGetLocalPath() is not { } path)
				return;

			var messages = this.FindControl<MessageLayer>("Messages")!;
			try
			{
				await File.WriteAllTextAsync(path, _viewModel.BuildFile(), new UTF8Encoding(false));
				messages.Tell(Loc.T.RemindersSaved, MessageKind.Done);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				messages.Tell(Loc.T.RemindersFileFailed + "\n\n" + ex.Message, MessageKind.Problem);
			}
		}

		private void Ok(object? sender, RoutedEventArgs e)
		{
			_viewModel.Accept();
			Close();
		}

		private void Cancel(object? sender, RoutedEventArgs e) => Close();


		private readonly RemindersWindowViewModel _viewModel;
	}
}
