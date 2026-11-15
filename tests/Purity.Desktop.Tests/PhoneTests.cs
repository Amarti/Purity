using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Purity.Core;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>
	/// The Android app's pages on Avalonia's invisible test screen, at a phone's size and a tablet's:
	/// the layouts approved as sketched on 2026-11-14. Every date is made up.
	/// </summary>
	[Collection(AppDataCollection.Name)]
	public sealed class PhoneTests : IDisposable
	{
		public PhoneTests()
		{
			Directory.CreateDirectory(_root);
			Environment.SetEnvironmentVariable("PURITY_APP_DATA", Path.Combine(_root, "app data"));
			File.WriteAllText(_dataFile, History(), new UTF8Encoding(false));
		}

		public void Dispose()
		{
			AppPlatform.Device = DeviceKind.Desktop;
			Loc.Instance.Use(Language.English);
			Loc.Instance.UseCalendar(CalendarKind.Gregorian, false);
			Environment.SetEnvironmentVariable("PURITY_APP_DATA", null);
			try
			{
				Directory.Delete(_root, true);
			}
			catch (IOException)
			{
				// a watcher may hold the folder for a moment; temp gets cleaned anyway
			}
		}


		[Fact]
		public Task APhoneShowsTheHistoryInItsCompactLayout() => OnA(DeviceKind.Phone, 360, 740, (window, shell) =>
		{
			Assert.True(shell.Main.IsCompact);
			Assert.Same(shell.Main.Resources["CompactPeriodListTemplate"], shell.Main.FindNamed<ListBox>("Periods")!.ItemTemplate);
			Assert.True(shell.Main.FindNamed<EventChip>("CompactNextStrip")!.IsVisible);
			Assert.False(shell.Main.FindNamed<EventChip>("NextStrip")!.IsVisible);
			Assert.Equal(40, shell.Main.FindNamed<Grid>("TopRow")!.Children.OfType<Button>().First().Bounds.Height);
			Assert.Empty(Spilling(shell.Main, window.Bounds.Width));
		});

		[Fact]
		public Task SettingsOpenAsAPageThatTheBackGestureCloses() => OnA(DeviceKind.Phone, 360, 740, async (window, shell) =>
		{
			var opening = shell.ViewModel.OpenSettingsCommand.ExecuteAsync(null);
			Dispatcher.UIThread.RunJobs();

			var settings = Assert.IsType<SettingsView>(Assert.Single(shell.Pages));
			var page = settings.FindAncestorOfType<HostedPage>()!;
			Assert.Equal(window.Bounds.Size, page.Bounds.Size);
			Assert.True(Spilling(settings, window.Bounds.Width) is [], string.Join("\n", Spilling(settings, window.Bounds.Width)));

			Assert.True(shell.GoBack());
			Dispatcher.UIThread.RunJobs();
			Assert.Empty(shell.Pages);
			await opening;
			Assert.False(shell.GoBack());									// nothing left to go back from, so Android may leave the app
		});

		[Fact]
		public Task RemindersOpenOverSettingsAndGoBackOneAtATime() => OnA(DeviceKind.Phone, 360, 740, async (window, shell) =>
		{
			var opening = shell.ViewModel.OpenSettingsCommand.ExecuteAsync(null);
			Dispatcher.UIThread.RunJobs();
			var settings = (SettingsView)shell.Pages.Single();

			_ = DialogHost.ShowAsync(settings, new RemindersView(new RemindersWindowViewModel(new ReminderSetup(), Array.Empty<PurityPeriod>(), TimeProvider.System)));
			Dispatcher.UIThread.RunJobs();
			Assert.IsType<RemindersView>(shell.Pages.Last());

			shell.GoBack();
			Dispatcher.UIThread.RunJobs();
			Assert.Same(settings, Assert.Single(shell.Pages));
			shell.GoBack();
			await opening;
		});

		[Fact]
		public Task ATabletShowsTheDesktopColumnWithSettingsAsASheet() => OnA(DeviceKind.Tablet, 1280, 800, async (window, shell) =>
		{
			Assert.False(shell.Main.IsCompact);
			Assert.True(shell.Main.FindNamed<EventChip>("NextStrip")!.IsVisible);

			var opening = shell.ViewModel.OpenSettingsCommand.ExecuteAsync(null);
			Dispatcher.UIThread.RunJobs();
			var settings = (SettingsView)shell.Pages.Single();
			var sheet = settings.FindAncestorOfType<Border>(includeSelf: false)!;
			while (sheet.Parent is not HostedPage)
				sheet = sheet.FindAncestorOfType<Border>()!;
			Assert.Equal(384, sheet.Bounds.Width);
			Assert.Equal((window.Bounds.Width - 384) / 2, sheet.TranslatePoint(new Point(0, 0), window)!.Value.X, 0.5);

			shell.GoBack();
			await opening;
		});

		[Fact]
		public Task OnAndroidRemindersOfferGoogleAndOtherOnly() => OnA(DeviceKind.Phone, 360, 740, (window, _) =>
		{
			var connections = new ReminderConnections(new Dictionary<ReminderPlace, IReminderConnection>
			{
				[ReminderPlace.Google] = new MemoryConnection { IsConnected = false },
				[ReminderPlace.CalDav] = new MemoryConnection { IsConnected = false, TakesPassword = true },
			});
			var reminders = new RemindersWindowViewModel(new ReminderSetup(), Array.Empty<PurityPeriod>(), TimeProvider.System, connections: connections);
			var view = new RemindersView(reminders);
			window.Content = view;
			Dispatcher.UIThread.RunJobs();

			var places = view.FindNamed<Grid>("Places")!.Children.OfType<RadioButton>().ToArray();
			Assert.Equal(new object?[] { "Google", UiText.English.RemindersOther }, places.Select(el => el.Content));
			Assert.Equal(new CornerRadius(4, 0, 0, 4), places[0].CornerRadius);
			Assert.Equal(new CornerRadius(0, 4, 4, 0), places[1].CornerRadius);
			Assert.True(reminders.IsGoogle);								// no file to start with on a phone
			Assert.Equal(2, view.FindNamed<Grid>("Times")!.RowDefinitions.Count);
			Assert.Empty(Spilling(view, window.Bounds.Width));
		});

		[Fact]
		public Task SettingsShowADocumentByItsNameWithRoomForFingers() => OnA(DeviceKind.Phone, 360, 740, (window, _) =>
		{
			var settings = new SettingsWindowViewModel(MemoryDocuments.LocationOf("x"), null) { DataFileName = "pureData.json" };
			var view = new SettingsView(settings);
			window.Content = view;
			Dispatcher.UIThread.RunJobs();

			Assert.False(view.FindNamed<TextBox>("DataFileBox")!.IsVisible);
			var name = view.FindNamed<TextBox>("DataFileNameBox")!;
			Assert.True(name.IsVisible);
			Assert.Equal("pureData.json", name.Text);
			Assert.Equal(32, name.Bounds.Height);
			Assert.False(view.FindNamed<Button>("ReportButton")!.IsVisible);								// a periods report is for a computer
			Assert.All(view.GetVisualDescendants().OfType<Button>().Where(el => el.IsEffectivelyVisible && el.Content is string { Length: > 3 } && el is not Avalonia.Controls.Primitives.ToggleButton), el => Assert.Equal(40, el.Bounds.Height));
			Assert.Equal(32, view.GetVisualDescendants().OfType<Button>().Single(el => el.Content is "...").Bounds.Height);			// beside its field, at the field's height
		});

		[Fact]
		public void TheDesktopKeepsAllFourPlacesAndTheFile()
		{
			var reminders = new RemindersWindowViewModel(new ReminderSetup(), Array.Empty<PurityPeriod>(), TimeProvider.System);

			Assert.True(reminders.ShowsFile);
			Assert.True(reminders.ShowsICloud);
			Assert.True(reminders.IsFile);
		}


		/// <summary>The shell on a screen of this size, on this kind of device, with the history open</summary>
		private Task OnA(DeviceKind device, double width, double height, Func<Window, AppShell, Task> test) => Ui.Run(async () =>
		{
			AppPlatform.Device = device;
			var shell = new AppShell((dialogs, documents) => new MainWindowViewModel(dialogs, _dataFile, TimeProvider.System, work => Dispatcher.UIThread.Post(() => _ = work()), documents: documents), () => { });
			var window = new Window { Width = width, Height = height, Content = shell };
			window.Show();
			try
			{
				await Ui.WaitUntil(() => shell.ViewModel.Periods.Count == 3);
				await test(window, shell);
			}
			finally
			{
				window.Close();
				shell.ViewModel.Dispose();
				AppPlatform.Device = DeviceKind.Desktop;
			}
		});

		private Task OnA(DeviceKind device, double width, double height, Action<Window, AppShell> test) =>
			OnA(device, width, height, (window, shell) =>
			{
				test(window, shell);
				return Task.CompletedTask;
			});

		/// <summary>What shows past either edge of a screen this wide. A text box scrolls and clips its own text, so what it holds doesn't count.</summary>
		private static List<string> Spilling(Visual root, double width) =>
			root.GetVisualDescendants().OfType<Control>()
				.Where(el => el.IsEffectivelyVisible && el.Bounds.Width > 0 && el.FindAncestorOfType<TextBox>() == null && el.TranslatePoint(new Point(0, 0), TopLevel.GetTopLevel(root)!) is { } corner
					&& (corner.X < -0.5 || corner.X + el.Bounds.Width > width + 0.5))
				.Select(el => $"{el.GetType().Name} {el.Name} at {el.TranslatePoint(new Point(0, 0), TopLevel.GetTopLevel(root)!)} {el.Bounds.Width:0} wide")
				.ToList();

		/// <summary>Three accepted periods in 2031, so the next date is still to come</summary>
		private static string History()
		{
			var ledger = new PeriodLedger();
			foreach (var begin in new[] { new DateTime(2031, 1, 5), new DateTime(2031, 2, 3), new DateTime(2031, 3, 4) })
				ledger.AcceptPeriod(ledger.AddPeriod(Stamps.FromDate(begin, false), Stamps.FromDate(begin.AddDays(5), false))!);
			return PurityFile.Format(ledger.Data);
		}


		private readonly string _root = Path.Combine(Path.GetTempPath(), $"purity-phone-{Guid.NewGuid():N}");
		private string _dataFile => Path.Combine(_root, "pureData.json");
	}
}
