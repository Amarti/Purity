using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Purity.Core;
using Purity.Desktop.Localization;
using Purity.Desktop.ViewModels;
using Purity.Desktop.Views;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>The Reminders window, on real windows. Every date is made up.</summary>
	public sealed partial class LookTests
	{
		[Fact]
		public Task SettingsOpensRemindersOverItself() => BackToEnglishAfter(async () =>
		{
			var window = await OpenPeriodsAsync();
			var (settings, closed) = await OpenSettingsAsync(window);
			var open = settings.GetVisualDescendants().OfType<Button>().Single(el => el.Content as string == UiText.English.RemindersOpen);

			Press(open, settings);
			await Ui.WaitUntil(() => settings.OwnedWindows.Count > 0);

			var reminders = Assert.IsType<RemindersWindow>(settings.OwnedWindows[0]);
			Assert.Equal(UiText.English.RemindersTitle, reminders.Title);
			Assert.True(settings.IsVisible);
			reminders.Close();
			settings.Close(false);
			await closed;
			window.Close();
		});

		[Fact]
		public Task WithoutGoogleInTheBuildOnlyTheFileCanBeChosen() => Ui.Run(() =>
		{
			var (window, _) = ShowReminders();
			var places = PlacesOf(window);

			Assert.Equal(new object?[] { "Google", "iCloud", UiText.English.RemindersOther, UiText.English.RemindersFile }, places.Select(el => el.Content));
			Assert.All(places.Take(3), el => Assert.False(el.IsEnabled));
			Assert.Equal(UiText.English.RemindersGoogleNotInBuild, ToolTip.GetTip(places[0]));
			Assert.Equal(new object?[] { UiText.English.RemindersICloudTip, UiText.English.RemindersCalDavTip }, places.Skip(1).Take(2).Select(ToolTip.GetTip));
			Assert.True(places[3].IsEnabled);
			Assert.True(places[3].IsChecked);
			window.Close();
		});

		[Fact]
		public Task ChoosingGoogleSwapsTheFilePartForTheConnectionPart() => Ui.Run(() =>
		{
			var (window, reminders) = ShowReminders(new MemoryConnection { IsConnected = false });
			var google = window.FindControl<StackPanel>("ServicePanel")!;
			Assert.False(google.IsVisible);
			Assert.True(PlacesOf(window)[0].IsEnabled);

			reminders.IsGoogle = true;
			Dispatcher.UIThread.RunJobs();

			Assert.True(google.IsVisible);
			Assert.True(window.FindControl<Button>("ConnectButton")!.IsVisible);
			Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), el => el.Content as string == UiText.English.RemindersSave && el.IsEffectivelyVisible);
			window.Close();
		});

		[Fact]
		public Task ChoosingICloudAsksForTheAppleAccountAndAnAppSpecificPassword() => Ui.Run(() =>
		{
			var (window, reminders) = ShowReminders(new MemoryConnection { IsConnected = false, TakesPassword = true }, ReminderPlace.ICloud);
			var fields = window.FindControl<StackPanel>("SignInFields")!;
			var connect = window.FindControl<Button>("ConnectButton")!;
			Assert.True(PlacesOf(window)[1].IsEnabled);

			reminders.IsICloud = true;
			Dispatcher.UIThread.RunJobs();

			Assert.True(fields.IsVisible);
			var links = LinksIn(window);
			Assert.Equal(new[] { "https://account.apple.com/", "https://support.apple.com/en-il/102654" }, links.Select(el => el.NavigateUri?.AbsoluteUri));
			Assert.Equal(new[] { "Apple Account manager", "this page" }, links.Select(AutomationProperties.GetName));
			Assert.All(links, el => Assert.True(el.IsEffectivelyVisible));
			var intro = window.FindControl<LinkText>("ServiceIntro")!;
			Assert.Single(intro.TextLayout.TextLines.Select(el => Math.Round(el.Height, 1)).Distinct());	// a line with a link is no taller than the rest
			var labels = fields.GetVisualDescendants().OfType<TextBlock>().Where(el => el.IsEffectivelyVisible && el.FindAncestorOfType<TextBox>() == null).Select(el => el.Text).ToArray();
			Assert.Contains(UiText.English.RemindersAppleAccount, labels);
			Assert.Contains(UiText.English.RemindersAppSpecificPassword, labels);
			Assert.DoesNotContain(UiText.English.RemindersServer, labels);		// iCloud's address is known
			Assert.Equal('•', window.FindControl<TextBox>("PasswordBox")!.PasswordChar);
			Assert.Equal(UiText.English.RemindersConnectICloud, connect.Content);
			Assert.False(connect.IsEnabled);

			reminders.SignInUser = "someone@example.com";
			reminders.SignInPassword = "abcd-efgh-ijkl-mnop";
			Dispatcher.UIThread.RunJobs();

			Assert.True(connect.IsEnabled);
			window.Close();
		});

		[Fact]
		public Task ABracketWithNoLinkForItStaysPlainText() => Ui.Run(() =>
		{
			var text = new LinkText { Marked = "Go to [one] or [two].", Links = new[] { new Uri("https://example.org/") } };

			Assert.Equal("https://example.org/", Assert.Single(text.GetLogicalDescendants().OfType<HyperlinkButton>()).NavigateUri?.AbsoluteUri);
			Assert.Equal(new[] { "Go to ", " or ", "two", "." }, text.Inlines!.OfType<Avalonia.Controls.Documents.Run>().Select(el => el.Text));
		});

		/// <summary>Avalonia draws text hidden by its own IsVisible, and a link laid out then, in the middle of drawing, crashed the app</summary>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public Task LinksInTextHiddenFromTheStartNeverCrashTheDrawing(bool inHiddenPanel) => Ui.Run(() =>
		{
			var text = new LinkText { Marked = "See [this page] and [that one].", Links = new[] { new Uri("https://example.com/"), new Uri("https://example.org/") } };
			var holder = new Border { Child = text };
			if (inHiddenPanel)
				holder.IsVisible = false;
			else
				text.IsVisible = false;
			var window = new Window { Content = new StackPanel { Children = { new TextBlock { Text = "Above" }, holder } } };
			window.Show();
			Dispatcher.UIThread.RunJobs();

			text.IsVisible = holder.IsVisible = true;
			Dispatcher.UIThread.RunJobs();

			var links = text.GetLogicalDescendants().OfType<HyperlinkButton>().ToArray();
			Assert.Equal(new[] { "https://example.com/", "https://example.org/" }, links.Select(el => el.NavigateUri?.AbsoluteUri));
			Assert.All(links, el => Assert.True(el.Bounds.Width > 0));
			Assert.Equal("See this page and that one.", string.Concat(text.Inlines!.Select(el => el is Avalonia.Controls.Documents.Run run ? run.Text : AutomationProperties.GetName(((Avalonia.Controls.Documents.InlineUIContainer)el).Child))));
			window.Close();
		});

		[Fact]
		public Task ChoosingOtherAsksForTheServerToo() => Ui.Run(() =>
		{
			var (window, reminders) = ShowReminders(new MemoryConnection { IsConnected = false, TakesPassword = true }, ReminderPlace.CalDav);

			reminders.IsCalDav = true;
			reminders.SignInUser = "someone";
			reminders.SignInPassword = "made up";
			Dispatcher.UIThread.RunJobs();

			var fields = window.FindControl<StackPanel>("SignInFields")!;
			var labels = fields.GetVisualDescendants().OfType<TextBlock>().Where(el => el.IsEffectivelyVisible && el.FindAncestorOfType<TextBox>() == null).Select(el => el.Text).ToArray();
			Assert.Equal(new[] { UiText.English.RemindersServer, UiText.English.RemindersUserName, UiText.English.RemindersPassword }, labels);
			Assert.False(window.FindControl<Button>("ConnectButton")!.IsEnabled);	// not without the server
			Assert.Empty(LinksIn(window));												// Apple's links are for iCloud alone
			Assert.Equal(UiText.English.RemindersCalDavIntro, window.FindControl<LinkText>("ServiceIntro")!.Inlines?.Text);

			reminders.SignInServer = "dav.example.org";
			Dispatcher.UIThread.RunJobs();
			Assert.True(window.FindControl<Button>("ConnectButton")!.IsEnabled);
			window.Close();
		});

		[Fact]
		public Task EnterInThePasswordConnectsRatherThanClosing() => Ui.Run(() =>
		{
			var connection = new MemoryConnection { IsConnected = false, TakesPassword = true };
			var (window, reminders) = ShowReminders(connection, ReminderPlace.ICloud);
			reminders.IsICloud = true;
			reminders.SignInUser = "someone@example.com";
			reminders.SignInPassword = "abcd-efgh-ijkl-mnop";
			Dispatcher.UIThread.RunJobs();

			window.FindControl<TextBox>("PasswordBox")!.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
			Dispatcher.UIThread.RunJobs();

			Assert.Equal("someone@example.com", connection.SignIn?.User);
			Assert.True(window.IsVisible);
			Assert.False(window.FindControl<StackPanel>("SignInFields")!.IsVisible);	// connected now
			window.Close();
		});

		[Fact]
		public Task WhenConnectedTheTestAndStopButtonsShow() => Ui.Run(() =>
		{
			var (window, reminders) = ShowReminders(new MemoryConnection());
			reminders.IsGoogle = true;
			Dispatcher.UIThread.RunJobs();

			Assert.False(window.FindControl<Button>("ConnectButton")!.IsVisible);
			Assert.Contains(window.GetVisualDescendants().OfType<Button>(), el => el.Content as string == UiText.English.RemindersSendTest && el.IsEffectivelyVisible);
			Assert.Contains(window.GetVisualDescendants().OfType<Button>(), el => el.Content as string == UiText.English.RemindersDisconnect && el.IsEffectivelyVisible);
			window.Close();
		});

		[Fact]
		public Task TheKindsAppearOnlyWhenChoosing() => Ui.Run(() =>
		{
			var (window, reminders) = ShowReminders();
			var kinds = window.FindControl<Grid>("Kinds")!;
			Assert.False(kinds.IsVisible);

			reminders.IsChosen = true;
			Dispatcher.UIThread.RunJobs();

			Assert.True(kinds.IsVisible);
			Assert.Equal(4, kinds.GetVisualDescendants().OfType<CheckBox>().Count());
			window.Close();
		});

		[Fact]
		public Task MoreOpensAndClosesAndTheWindowFits() => Ui.Run(() =>
		{
			var (window, _) = ShowReminders();
			var more = window.FindControl<StackPanel>("More")!;
			var before = window.Bounds.Height;
			Assert.False(more.IsVisible);

			Press(window.FindControl<Button>("MoreButton")!, window);
			window.UpdateLayout();
			Dispatcher.UIThread.RunJobs();

			Assert.True(more.IsVisible);
			Assert.True(window.Bounds.Height > before, "The window should grow to fit");
			Assert.Equal(2, more.GetVisualDescendants().OfType<ComboBox>().Count());
			window.Close();
		});

		[Fact]
		public Task ThePreviewListsTheNextReminders() => Ui.Run(() =>
		{
			var (window, reminders) = ShowReminders();

			Assert.Contains(window.FindControl<ItemsControl>("PreviewList")!.GetVisualDescendants().OfType<TextBlock>(), el => el.Text == "Appointment today");

			reminders.IsDetailed = true;
			Dispatcher.UIThread.RunJobs();

			Assert.Contains(window.FindControl<ItemsControl>("PreviewList")!.GetVisualDescendants().OfType<TextBlock>(), el => el.Text == "Mikveh today");
			window.Close();
		});

		[Fact]
		public Task TheWindowRunsRightToLeftInHebrew() => BackToEnglishAfter(() =>
		{
			Loc.Instance.Use(Language.Hebrew);

			var (window, _) = ShowReminders();

			Assert.Equal(Avalonia.Media.FlowDirection.RightToLeft, window.FlowDirection);
			Assert.Equal(UiText.Hebrew.RemindersTitle, window.Title);
			Assert.Contains(window.FindControl<ItemsControl>("PreviewList")!.GetVisualDescendants().OfType<TextBlock>(), el => el.Text == "תור הערב");
			window.Close();
			return Task.CompletedTask;
		});

		[Fact]
		public Task SwitchPartsThatCantBeChosenLookIt() => Ui.Run(() =>
		{
			var (window, _) = ShowReminders();

			Assert.True(PlacesOf(window)[0].Opacity < 1);
			Assert.Equal(1, PlacesOf(window)[3].Opacity);
			window.Close();
		});


		[Theory]
		[InlineData(ReminderPlace.ICloud, true)]
		[InlineData(ReminderPlace.ICloud, false)]
		[InlineData(ReminderPlace.CalDav, true)]
		[InlineData(ReminderPlace.Google, true)]
		public Task OpensWithAServiceAlreadyChosen(ReminderPlace place, bool connected) => Ui.Run(async () =>
		{
			// As after a restart: the text about the service is hidden from the start while connected,
			// and for iCloud it holds links. Opening this way once crashed the app on a Mac.
			var connection = new MemoryConnection { IsConnected = connected, TakesPassword = place != ReminderPlace.Google };
			var (window, reminders) = ShowReminders(connection, place, chosen: true);
			Assert.True(reminders.IsService);
			Assert.Equal(connected, reminders.IsConnected);

			await reminders.DisconnectAsync();
			Dispatcher.UIThread.RunJobs();

			var intro = window.FindControl<LinkText>("ServiceIntro")!;
			Assert.True(intro.IsEffectivelyVisible);
			Assert.True(intro.Bounds.Height > 0);
			var links = LinksIn(window);
			Assert.Equal(place == ReminderPlace.ICloud ? 2 : 0, links.Length);
			Assert.All(links, el => Assert.True(el.Bounds.Width > 0 && el.IsEffectivelyVisible));
			window.Close();
		});


		/// <param name="connection">The one service's connection, or none</param>
		/// <param name="place">Which service it is</param>
		/// <param name="chosen">Whether the service is already where reminders go, as after a restart</param>
		private static (RemindersWindow Window, RemindersWindowViewModel Reminders) ShowReminders(IReminderConnection? connection = null, ReminderPlace place = ReminderPlace.Google, bool chosen = false)
		{
			var period = new PurityPeriod(new DateTimeOffset(2031, 2, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2031, 2, 6, 0, 0, 0, TimeSpan.Zero)) { Closed = true };
			period.SubEvents.Add(new PurityEvent(PurityEventType.Mikveh, new DateTimeOffset(2031, 3, 2, 12, 0, 0, TimeSpan.Zero)));
			var setup = new ReminderSetup { Where = chosen ? place : ReminderPlace.File };
			var reminders = new RemindersWindowViewModel(setup, new[] { period }, new EarlierClock(), null, connection == null ? null : ReminderConnections.Of(place, connection));
			var window = new RemindersWindow(reminders);
			window.Show();
			Dispatcher.UIThread.RunJobs();
			return (window, reminders);
		}

		/// <summary>The links in the text about the service chosen</summary>
		private static HyperlinkButton[] LinksIn(Window window) =>
			window.FindControl<LinkText>("ServiceIntro")!.GetLogicalDescendants().OfType<HyperlinkButton>().ToArray();

		private static RadioButton[] PlacesOf(Window window) =>
			window.GetVisualDescendants().OfType<RadioButton>().Where(el => el.GroupName == "Where").ToArray();


		private sealed class EarlierClock : TimeProvider
		{
			public override DateTimeOffset GetUtcNow() => new(2031, 1, 1, 9, 0, 0, TimeSpan.Zero);

			public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
		}
	}
}
