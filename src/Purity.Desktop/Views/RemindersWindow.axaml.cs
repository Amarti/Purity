using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
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
			_messages = this.FindControl<MessageLayer>("Messages")!;
			Closing += (_, _) => _viewModel.CancelConnecting();
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

			try
			{
				await File.WriteAllTextAsync(path, _viewModel.BuildFile(), new UTF8Encoding(false));
				_messages.Tell(Loc.T.RemindersSaved, MessageKind.Done);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				_messages.Tell(Loc.T.RemindersFileFailed + "\n\n" + ex.Message, MessageKind.Problem);
			}
		}

		private async void Connect(object? sender, RoutedEventArgs e) => Say(await _viewModel.ConnectAsync());

		/// <summary>Enter in a sign-in field connects, rather than pressing OK and closing the window</summary>
		private async void ConnectOnEnter(object? sender, KeyEventArgs e)
		{
			if (e.Key != Key.Enter)
				return;

			e.Handled = true;
			Say(await _viewModel.ConnectAsync());
		}

		private void CancelConnecting(object? sender, RoutedEventArgs e) => _viewModel.CancelConnecting();

		private async void SendTest(object? sender, RoutedEventArgs e) => Say(await _viewModel.SendTestAsync());

		/// <summary>Deletes a calendar at the service, so it asks first</summary>
		private async void Disconnect(object? sender, RoutedEventArgs e)
		{
			var answer = await _messages.AskAsync(new Question(_viewModel.DisconnectQuestion, _viewModel.DisconnectBody), new[] { Loc.T.RemindersDisconnectConfirm, Loc.T.Cancel });
			if (answer == 0)
				Say(await _viewModel.DisconnectAsync());
		}

		private void Say(ReminderNotice? notice)
		{
			if (notice != null)
				_messages.Tell(notice.Text, notice.Kind);
		}

		private void Ok(object? sender, RoutedEventArgs e)
		{
			_viewModel.Accept();
			Close();
		}

		private void Cancel(object? sender, RoutedEventArgs e) => Close();


		private readonly RemindersWindowViewModel _viewModel;
		private readonly MessageLayer _messages;
	}
}
