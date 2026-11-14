using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Purity.Storage;
using Purity.UI.Localization;
using Purity.UI.ViewModels;


namespace Purity.UI.Views
{
	/// <summary>
	/// The whole app on a phone or a tablet, which have no windows: the history, and Settings and the
	/// like over it, as pages on a phone or as sheets on a tablet. The system's back gesture goes back
	/// from them as Cancel does. Coming back to Purity reads the history again, and leaving saves it.
	/// </summary>
	public sealed class AppShell : UserControl, IAppDialogs, IDialogHost
	{
		/// <param name="viewModel">Makes the history's view model, which talks through this shell and keeps the history in documents a picker gives</param>
		/// <param name="closeApp">Ends the app, as only the system can on a phone</param>
		public AppShell(Func<IAppDialogs, IDocuments, MainWindowViewModel> viewModel, Action closeApp)
		{
			_closeApp = closeApp;
			Main = new MainView { IsCompact = AppPlatform.IsPhone };
			Content = new Panel { Children = { Main, _pages, _messages } };
			_direction = Loc.Instance.FollowFlowDirection(this);

			ViewModel = viewModel(this, new StorageDocuments(() => TopLevel.GetTopLevel(this)?.StorageProvider));
			Main.DataContext = ViewModel;
			App.Apply(ViewModel.Appearance);
			ViewModel.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName == nameof(MainWindowViewModel.Appearance))
					App.Apply(ViewModel.Appearance);
			};
		}


		public MainWindowViewModel ViewModel { get; }

		public MainView Main { get; }

		/// <summary>The pages or sheets shown over the history, the top one last</summary>
		public IEnumerable<DialogView> Pages => _pages.Children.OfType<HostedPage>().Select(el => el.View);


		/// <summary>The back gesture: the top page goes back as Cancel does</summary>
		/// <returns>False when there's no page to go back from, so the system may leave the app</returns>
		public bool GoBack()
		{
			if (_pages.Children.LastOrDefault() is not HostedPage page)
				return false;

			page.View.Cancel();
			return true;
		}

		public void Tell(string text, MessageKind kind) => _messages.Tell(text, kind);

		public Task<int?> AskAsync(Question question, params string[] buttons) => _messages.AskAsync(question, buttons);

		public async Task<bool> ShowSettingsAsync(SettingsWindowViewModel settings) => await ShowAsync(new SettingsView(settings)) is true;

		public async Task<Backup?> ChooseBackupAsync(IReadOnlyList<BackupChoice> choices) => await ShowAsync(new BackupsView(choices)) as Backup;

		public async Task<string?> PickDataFileAsync() =>
			TopLevel.GetTopLevel(this) is { } top ? await DataFilePicker.OpenAsync(top.StorageProvider) : null;

		public void CloseApp() => _closeApp();

		public Task<object?> ShowAsync(DialogView view)
		{
			var page = new HostedPage(view, AppPlatform.IsPhone);
			var done = new TaskCompletionSource<object?>();
			view.Finished += (_, result) =>
			{
				_pages.Children.Remove(page);
				done.TrySetResult(result);
			};
			_pages.Children.Add(page);
			return done.Task;
		}


		protected override async void OnLoaded(RoutedEventArgs e)
		{
			base.OnLoaded(e);
			if (TopLevel.GetTopLevel(this) is { } top)
				top.BackRequested += OnBackRequested;
			if (Application.Current?.TryGetFeature<IActivatableLifetime>() is { } life)
			{
				life.Activated += OnActivated;
				life.Deactivated += OnDeactivated;
			}

			if (_started)
				return;

			_started = true;
			if (!await ViewModel.StartAsync())
				_closeApp();								// the first question was closed: nothing was set, so it comes back next time
		}

		protected override void OnUnloaded(RoutedEventArgs e)
		{
			base.OnUnloaded(e);
			if (TopLevel.GetTopLevel(this) is { } top)
				top.BackRequested -= OnBackRequested;
			if (Application.Current?.TryGetFeature<IActivatableLifetime>() is { } life)
			{
				life.Activated -= OnActivated;
				life.Deactivated -= OnDeactivated;
			}
		}


		private void OnBackRequested(object? sender, RoutedEventArgs e)
		{
			if (GoBack())
				e.Handled = true;
		}

		private async void OnActivated(object? sender, ActivatedEventArgs e) => await ViewModel.ComeBackAsync();

		private async void OnDeactivated(object? sender, ActivatedEventArgs e) => await ViewModel.SaveNowAsync();


		private readonly Action _closeApp;
		private readonly Panel _pages = new();
		private readonly MessageLayer _messages = new();
		private readonly IDisposable _direction;
		private bool _started;
	}


	/// <summary>One view over the rest: a page with a back arrow and its title on a phone, or a sheet in the middle of a tablet</summary>
	internal sealed class HostedPage : Panel
	{
		public HostedPage(DialogView view, bool phone)
		{
			View = view;
			var back = new Button
			{
				Width = 48,
				Height = 48,
				Background = Brushes.Transparent,
				BorderThickness = new Thickness(0),
				Content = new Avalonia.Controls.Shapes.Path
				{
					Data = Geometry.Parse("M14,5 L7,12 L14,19"),
					StrokeThickness = 2,
					StrokeLineCap = PenLineCap.Round,
					StrokeJoin = PenLineJoin.Round,
					Width = 22,
					Height = 22,
					Stretch = Stretch.None,
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center,
				},
			};
			((Avalonia.Controls.Shapes.Path)back.Content).Bind(Avalonia.Controls.Shapes.Shape.StrokeProperty, back.GetResourceObservable("IconBrush"));
			AutomationProperties.SetName(back, Loc.T.Back);
			back.Click += (_, _) => view.Cancel();

			var title = new TextBlock { FontSize = 18, FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 12, 0) };
			title.Bind(TextBlock.TextProperty, view.GetObservable(DialogView.TitleProperty));
			var bar = new DockPanel { Height = 52, Margin = new Thickness(4, 0) };
			DockPanel.SetDock(back, Dock.Left);
			bar.Children.Add(back);
			bar.Children.Add(title);

			var scroller = new ScrollViewer { Content = view, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
			var page = new DockPanel();
			DockPanel.SetDock(bar, Dock.Top);
			page.Children.Add(bar);
			page.Children.Add(scroller);

			if (phone)
			{
				Bind(BackgroundProperty, this.GetResourceObservable("WindowBackgroundBrush"));
				Children.Add(page);
				return;
			}

			// On a tablet, a sheet at the width its desktop window has, over the history dimmed
			Background = new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0));
			var sheet = new Border
			{
				Width = view.DesktopWidth,
				Margin = new Thickness(24),
				CornerRadius = new CornerRadius(10),
				ClipToBounds = true,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				Child = page,
			};
			sheet.Bind(Border.BackgroundProperty, sheet.GetResourceObservable("WindowBackgroundBrush"));
			Children.Add(sheet);
		}


		public DialogView View { get; }
	}
}
