using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Purity.Core;
using Purity.UI.Localization;
using Purity.UI.ViewModels;


namespace Purity.UI.Views
{
	public partial class RemindersView : DialogView
	{
		/// <summary>For the XAML previewer</summary>
		public RemindersView()
			: this(new RemindersWindowViewModel(new ReminderSetup(), Array.Empty<PurityPeriod>(), TimeProvider.System))
		{}

		public RemindersView(RemindersWindowViewModel viewModel)
		{
			InitializeComponent();
			DataContext = _viewModel = viewModel;
			_messages = Messages;
			DesktopWidth = 480;
			if (AppPlatform.IsMobile)
				KeepOnlyThePlacesOffered();
			if (AppPlatform.IsPhone)
			{
				FitAPhone();
				FingerSizes.Apply(this);
			}
		}


		/// <summary>A phone saves no calendar file, and Android has no iCloud, so the switch holds only what's offered, its ends still rounded</summary>
		private void KeepOnlyThePlacesOffered()
		{
			var offered = new List<RadioButton> { GooglePlace };
			if (_viewModel.ShowsICloud)
				offered.Add(ICloudPlace);
			offered.Add(OtherPlace);
			if (_viewModel.ShowsFile)
				offered.Add(FilePlace);

			Places.Children.RemoveAll(Places.Children.Except(offered).ToList());
			Places.ColumnDefinitions.Clear();
			for (var i = 0; i < offered.Count; i++)
			{
				Places.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
				Grid.SetColumn(offered[i], i);
				offered[i].CornerRadius = new Avalonia.CornerRadius(i == 0 ? 4 : 0, i == offered.Count - 1 ? 4 : 0, i == offered.Count - 1 ? 4 : 0, i == 0 ? 4 : 0);
			}
		}

		/// <summary>Narrower labels, so three choices fit beside them, and the two alert times one under the other</summary>
		private void FitAPhone()
		{
			foreach (var row in Body.Children.OfType<Grid>())
				if (row.ColumnDefinitions.Count == 2 && row.ColumnDefinitions[0].Width.Value == DesktopLabel)
					row.ColumnDefinitions[0].Width = new GridLength(PhoneLabel);
			Kinds.Margin = new Avalonia.Thickness(PhoneLabel, -4, 0, 0);

			var parts = Times.Children.ToList();				// a label and its choice, twice
			Times.ColumnDefinitions.Clear();
			Times.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(150)));
			Times.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
			Times.RowDefinitions.Clear();
			Times.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
			Times.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
			Times.RowSpacing = 8;
			for (var i = 0; i < parts.Count; i++)
			{
				Grid.SetRow(parts[i], i / 2);
				Grid.SetColumn(parts[i], i % 2);
			}
		}


		private async void SaveFile(object? sender, RoutedEventArgs e)
		{
			var file = await TopLevel.GetTopLevel(this)!.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
			{
				Title = Loc.T.RemindersFileTitle,
				SuggestedFileName = "reminders.ics",
				DefaultExtension = "ics",
				FileTypeChoices = new[] { new FilePickerFileType(Loc.T.RemindersFileType) { Patterns = new[] { "*.ics" } } },
			});
			if (file == null)
				return;

			try
			{
				await using (var stream = await file.OpenWriteAsync())
				await using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
					await writer.WriteAsync(_viewModel.BuildFile());		// written in full before it's said to be
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
			Finish(true);
		}

		private void Cancel(object? sender, RoutedEventArgs e) => Finish(false);

		/// <summary>Going away, however it went, gives up any sign-in still under way</summary>
		protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
		{
			base.OnDetachedFromVisualTree(e);
			_viewModel.CancelConnecting();
		}


		private const double DesktopLabel = 100;
		private const double PhoneLabel = 80;

		private readonly RemindersWindowViewModel _viewModel;
		private readonly MessageLayer _messages;
	}
}
