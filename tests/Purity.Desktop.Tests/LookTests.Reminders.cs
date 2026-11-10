using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
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

			Assert.Equal(new object?[] { "Google", "iCloud", "Outlook", UiText.English.RemindersOther, UiText.English.RemindersFile }, places.Select(el => el.Content));
			Assert.All(places.Take(4), el => Assert.False(el.IsEnabled));
			Assert.Equal(UiText.English.RemindersGoogleNotInBuild, ToolTip.GetTip(places[0]));
			Assert.All(places.Skip(1).Take(3), el => Assert.Equal(UiText.English.RemindersLater, ToolTip.GetTip(el)));
			Assert.True(places[4].IsEnabled);
			Assert.True(places[4].IsChecked);
			window.Close();
		});

		[Fact]
		public Task ChoosingGoogleSwapsTheFilePartForTheConnectionPart() => Ui.Run(() =>
		{
			var (window, reminders) = ShowReminders(new MemoryConnection { IsConnected = false });
			var google = window.FindControl<StackPanel>("GooglePanel")!;
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
			Assert.Equal(1, PlacesOf(window)[4].Opacity);
			window.Close();
		});


		private static (RemindersWindow Window, RemindersWindowViewModel Reminders) ShowReminders(IReminderConnection? connection = null)
		{
			var period = new PurityPeriod(new DateTimeOffset(2031, 2, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2031, 2, 6, 0, 0, 0, TimeSpan.Zero)) { Closed = true };
			period.SubEvents.Add(new PurityEvent(PurityEventType.Mikveh, new DateTimeOffset(2031, 3, 2, 12, 0, 0, TimeSpan.Zero)));
			var reminders = new RemindersWindowViewModel(new ReminderSetup(), new[] { period }, new EarlierClock(), null, connection);
			var window = new RemindersWindow(reminders);
			window.Show();
			Dispatcher.UIThread.RunJobs();
			return (window, reminders);
		}

		private static RadioButton[] PlacesOf(Window window) =>
			window.GetVisualDescendants().OfType<RadioButton>().Where(el => el.GroupName == "Where").ToArray();


		private sealed class EarlierClock : TimeProvider
		{
			public override DateTimeOffset GetUtcNow() => new(2031, 1, 1, 9, 0, 0, TimeSpan.Zero);

			public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
		}
	}
}
