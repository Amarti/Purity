using Purity.CalDavStandIn;
using Purity.Desktop.Localization;
using Purity.Desktop.ViewModels;
using Purity.Reminders;
using Purity.Reminders.CalDav;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>Reminders in iCloud and other CalDAV calendars, signed in with a password. Every date, name and password is made up.</summary>
	public sealed partial class MainWindowTests
	{
		[Theory]
		[InlineData(ReminderPlace.ICloud)]
		[InlineData(ReminderPlace.CalDav)]
		public async Task ClosingAPeriodPutsItsRemindersInTheCalendarChosen(ReminderPlace place)
		{
			var (window, calendar) = await OpenConnectedAsync(place);

			await CloseNewPeriodAsync(window);

			Assert.NotEmpty(calendar.Entries);
			Assert.Equal(1, calendar.ChoiceWrites);					// shared with the other computers, as with Google
			Assert.Empty(_dialogs.Shown);
		}

		[Theory]
		[InlineData(ReminderPlace.ICloud, CalendarProblem.NoConnection, nameof(UiText.RemindersICloudNoConnection))]
		[InlineData(ReminderPlace.ICloud, CalendarProblem.SignInNeeded, nameof(UiText.RemindersICloudSignInNeeded))]
		[InlineData(ReminderPlace.ICloud, CalendarProblem.Refused, nameof(UiText.RemindersICloudFailed))]
		[InlineData(ReminderPlace.CalDav, CalendarProblem.NoConnection, nameof(UiText.RemindersCalDavNoConnection))]
		[InlineData(ReminderPlace.CalDav, CalendarProblem.SignInNeeded, nameof(UiText.RemindersCalDavSignInNeeded))]
		[InlineData(ReminderPlace.CalDav, CalendarProblem.Refused, nameof(UiText.RemindersCalDavFailed))]
		[InlineData(ReminderPlace.Google, CalendarProblem.NoConnection, nameof(UiText.RemindersNoConnection))]
		public async Task AProblemNamesTheServiceItCameFrom(ReminderPlace place, CalendarProblem problem, string text)
		{
			var (window, calendar) = await OpenConnectedAsync(place);
			calendar.FailWith = new CalendarServiceException(problem, "made up");

			await window.RecalculateCommand.ExecuteAsync(null);
			await window.ReminderSyncFinished;

			Assert.Equal(typeof(UiText).GetProperty(text)!.GetValue(UiText.English), Assert.Single(_dialogs.Shown));
		}

		[Fact]
		public async Task OnlyTheServiceChosenIsUpdated()
		{
			WriteHistory();
			new DesktopSettings { DataFilePath = _dataFile, Reminders = new ReminderSetup { Where = ReminderPlace.ICloud, Scope = ReminderScope.AllDates } }.Save();
			var google = new MemoryConnection();
			var icloud = new MemoryConnection();
			_viewModel = new MainWindowViewModel(_dialogs, null, new FixedClock(), _pending.Enqueue, reminderConnections: _ => Both(google, icloud));
			await _viewModel.StartAsync();

			await CloseNewPeriodAsync(_viewModel);

			Assert.NotEmpty(icloud.Entries);
			Assert.Equal(0, google.Reads);
		}

		[Fact]
		public async Task RemovingAllHistoryRemovesEveryConnectedCalendar()
		{
			WriteHistory();
			new DesktopSettings { DataFilePath = _dataFile, GoogleCalendarId = "made-up", ICloudCalendar = "https://made.up/calendar/" }.Save();
			var google = new MemoryConnection();
			var icloud = new MemoryConnection { FailWith = new CalendarServiceException(CalendarProblem.NoConnection, "offline") };
			_viewModel = new MainWindowViewModel(_dialogs, null, new FixedClock(), _pending.Enqueue, reminderConnections: _ => Both(google, icloud));
			await _viewModel.StartAsync();
			_dialogs.InSettings = settings =>
			{
				settings.RequestWipe();
				return false;
			};
			_dialogs.Answers.Enqueue(0);
			_dialogs.Answers.Enqueue(0);

			await _viewModel.OpenSettingsCommand.ExecuteAsync(null);

			Assert.False(google.IsConnected);
			Assert.Equal(0, google.Forgotten);						// its calendar went too
			Assert.Equal(1, icloud.Forgotten);						// offline, so only the sign-in went
			Assert.Contains(UiText.English.WipeICloudCalendarLeft, _dialogs.Shown[1]);
			Assert.DoesNotContain(UiText.English.WipeCalendarLeft, _dialogs.Shown[1]);
			var saved = DesktopSettings.Load();
			Assert.Null(saved.GoogleCalendarId);
			Assert.Null(saved.ICloudCalendar);
		}


		[Fact]
		public async Task ConnectingToICloudTakesTheSignInTypedAndForgetsThePassword()
		{
			var connection = new MemoryConnection { IsConnected = false, TakesPassword = true };
			var reminders = new RemindersWindowViewModel(new ReminderSetup(), new[] { DatedPeriod() }, new ClockAt(new DateTime(2031, 1, 1)), null, ReminderConnections.Of(ReminderPlace.ICloud, connection));
			reminders.IsICloud = true;
			Assert.False(reminders.HasSignIn);
			Assert.Null(await reminders.ConnectAsync());				// nothing to sign in with yet
			Assert.Null(connection.SignIn);

			reminders.SignInUser = "someone@example.com";
			reminders.SignInPassword = "abcd-efgh-ijkl-mnop";
			Assert.Null(await reminders.ConnectAsync());

			Assert.Equal(("someone@example.com", "abcd-efgh-ijkl-mnop"), (connection.SignIn?.User, connection.SignIn?.Password));
			Assert.True(reminders.IsConnected);
			Assert.Empty(reminders.SignInPassword);
			Assert.False(reminders.ShowsSignIn);
		}

		[Fact]
		public async Task AWrongPasswordSaysWhatICloudNeeds()
		{
			var connection = new MemoryConnection { IsConnected = false, TakesPassword = true, FailWith = new CalendarServiceException(CalendarProblem.PasswordRefused, "made up") };
			var reminders = new RemindersWindowViewModel(new ReminderSetup(), new[] { DatedPeriod() }, new ClockAt(new DateTime(2031, 1, 1)), null, ReminderConnections.Of(ReminderPlace.ICloud, connection));
			reminders.IsICloud = true;
			reminders.SignInUser = "someone@example.com";
			reminders.SignInPassword = "the account's own password";

			var notice = await reminders.ConnectAsync();

			Assert.Equal(UiText.English.RemindersICloudPasswordRefused, notice?.Text);
			Assert.False(reminders.IsConnected);
			Assert.True(reminders.ShowsSignIn);
		}

		[Fact]
		public void EachServiceAsksItsOwnQuestionBeforeRemovingItsCalendar()
		{
			var reminders = new RemindersWindowViewModel(new ReminderSetup { Where = ReminderPlace.ICloud }, new[] { DatedPeriod() }, new ClockAt(new DateTime(2031, 1, 1)), null, ReminderConnections.Of(ReminderPlace.ICloud, new MemoryConnection { TakesPassword = true }));

			Assert.True(reminders.IsICloud);
			Assert.True(reminders.IsConnected);
			Assert.Equal(2, reminders.IntroLinks.Count);				// where to make an app-specific password, and Apple's page
			Assert.Equal(UiText.English.RemindersDisconnectICloudQuestion, reminders.DisconnectQuestion);

			reminders.IsGoogle = true;									// a build with no Google connection

			Assert.False(reminders.IsConnected);
			Assert.Equal(UiText.English.RemindersDisconnectQuestion, reminders.DisconnectQuestion);
			Assert.Equal(UiText.English.RemindersGoogleIntro, reminders.ServiceIntro);
			Assert.Empty(reminders.IntroLinks);
		}


		[Fact]
		public Task AppleHelpIsInTheLanguageOnShow() => BackToEnglishAfter(() =>
		{
			Loc.Instance.Use(Language.Hebrew);
			var reminders = new RemindersWindowViewModel(new ReminderSetup(), new[] { DatedPeriod() }, new ClockAt(new DateTime(2031, 1, 1)));

			Assert.Equal("https://support.apple.com/he-il/102654", reminders.AppPasswordHelp.AbsoluteUri);
			return Task.CompletedTask;
		});


		[Theory]
		[InlineData("dav.example.org", "https://dav.example.org/")]
		[InlineData("  https://dav.example.org/remote.php/dav  ", "https://dav.example.org/remote.php/dav")]
		[InlineData("http://127.0.0.1:5232/", "http://127.0.0.1:5232/")]
		public void AnAddressNeedntStartWithHttps(string typed, string address) =>
			Assert.Equal(address, CalDavConnection.Address(typed).AbsoluteUri);

		[Theory]
		[InlineData("ftp://dav.example.org/")]
		[InlineData("")]
		[InlineData("https://")]
		public void WhatIsntAnAddressSaysSo(string typed) =>
			Assert.Equal(CalendarProblem.NoCalendarService, Assert.Throws<CalendarServiceException>(() => CalDavConnection.Address(typed)).Problem);

		[Fact]
		public async Task AConnectionToAServerMakesItsCalendarAndRemovesIt()
		{
			await using var server = await CalDavServer.StartAsync();
			string? calendarAddress = "https://left.over/from/another/account/";
			var secrets = new MemorySecretStore();
			using var http = CalDavAccount.NewHttpClient();
			var connection = new CalDavConnection(null, secrets, () => calendarAddress, value => calendarAddress = value, new DesktopSettings(), http);

			await connection.ConnectAsync(new ReminderSignIn(server.BaseAddress.AbsoluteUri, " " + CalDavServer.User + " ", CalDavServer.Password), default);
			Assert.True(connection.IsConnected);
			Assert.Null(calendarAddress);								// looked for afresh in this account
			await connection.Calendar.AddAsync(ReminderPlanner.TestReminder(ReminderLanguage.English, new DateTime(2031, 1, 1, 9, 0, 0)), default);

			var made = Assert.Single(server.Calendars);
			Assert.Equal(ReminderSetup.DefaultCalendarName, made.Name);
			Assert.Single(made.Events);
			Assert.NotNull(calendarAddress);

			await connection.DisconnectAsync(default);

			Assert.Empty(server.Calendars);
			Assert.False(connection.IsConnected);
			Assert.Null(secrets.Value);
			Assert.Null(calendarAddress);
		}


		private static ReminderConnections Both(MemoryConnection google, MemoryConnection icloud) => new(new Dictionary<ReminderPlace, IReminderConnection>
		{
			[ReminderPlace.Google] = google,
			[ReminderPlace.ICloud] = icloud,
		});


		private sealed class MemorySecretStore : ISecretStore
		{
			public string? Value { get; private set; }

			public string? Read() => Value;

			public bool Write(string secret)
			{
				Value = secret;
				return true;
			}

			public void Delete() => Value = null;
		}
	}
}
