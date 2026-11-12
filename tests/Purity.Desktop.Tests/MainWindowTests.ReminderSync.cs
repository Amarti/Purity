using Purity.Desktop.Localization;
using Purity.Desktop.ViewModels;
using Purity.Reminders;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>Keeping a connected calendar in line with the history, and the connection's part of the Reminders window. Every date is made up.</summary>
	public sealed partial class MainWindowTests
	{
		[Fact]
		public async Task ClosingAPeriodPutsItsRemindersInTheCalendar()
		{
			var (window, calendar) = await OpenConnectedAsync();

			await CloseNewPeriodAsync(window);

			Assert.Contains(calendar.Entries, el => el.Title == "Reminder H, tonight" || el.Title.StartsWith("Reminder H", StringComparison.Ordinal));
			Assert.All(calendar.Entries, el => Assert.StartsWith(ReminderTag.Prefix, el.Tag));
			Assert.Empty(_dialogs.Shown);
		}

		[Fact]
		public async Task ANewPeriodEmptiesTheCalendarAndRemovingItBringsThemBack()
		{
			var (window, calendar) = await OpenConnectedAsync();
			await CloseNewPeriodAsync(window, daysAgo: 2);
			var before = calendar.Entries.Select(el => el.Tag).OrderBy(el => el, StringComparer.Ordinal).ToArray();
			Assert.NotEmpty(before);

			window.AddPeriodCommand.Execute(null);
			await window.ReminderSyncFinished;
			Assert.Empty(calendar.Entries);

			await window.Periods[0].RemoveCommand.ExecuteAsync(null);
			await window.ReminderSyncFinished;
			Assert.Equal(before, calendar.Entries.Select(el => el.Tag).OrderBy(el => el, StringComparer.Ordinal));
		}

		[Fact]
		public async Task RunningAgainWritesNothing()
		{
			var (window, calendar) = await OpenConnectedAsync();
			await CloseNewPeriodAsync(window);
			var writes = calendar.Writes;

			await window.RecalculateCommand.ExecuteAsync(null);
			await window.ReminderSyncFinished;

			Assert.Equal(writes, calendar.Writes);
		}

		[Fact]
		public Task AChangeOfLanguageUpdatesTheEntriesInPlace() => Ui.Run(async () =>
		{
			try
			{
				var (window, calendar) = await OpenConnectedAsync();
				await CloseNewPeriodAsync(window);
				var tags = calendar.Entries.Select(el => el.Tag).OrderBy(el => el, StringComparer.Ordinal).ToArray();
				_dialogs.InSettings = settings =>
				{
					settings.IsHebrew = true;
					return true;
				};

				await window.OpenSettingsCommand.ExecuteAsync(null);
				await window.ReminderSyncFinished;

				Assert.Equal(tags, calendar.Entries.Select(el => el.Tag).OrderBy(el => el, StringComparer.Ordinal));
				Assert.Contains(calendar.Entries, el => el.Title.StartsWith("תזכורת", StringComparison.Ordinal));
				Assert.DoesNotContain(calendar.Entries, el => el.Title.StartsWith("Reminder", StringComparison.Ordinal));
			}
			finally
			{
				Loc.Instance.Use(Language.English);
			}
		});

		[Fact]
		public async Task ChangingTheChoicesUpdatesTheCalendarOnOk()
		{
			var (window, calendar) = await OpenConnectedAsync();
			await CloseNewPeriodAsync(window);
			_dialogs.InSettings = settings =>
			{
				var reminders = settings.NewReminders()!;
				reminders.IsDetailed = true;
				reminders.Accept();
				return false;
			};

			await window.OpenSettingsCommand.ExecuteAsync(null);
			await window.ReminderSyncFinished;

			Assert.Contains(calendar.Entries, el => el.Title.StartsWith("Veset Hachodesh", StringComparison.Ordinal));
		}

		[Fact]
		public async Task AFileMeansNoCalendarWork()
		{
			var (window, calendar) = await OpenConnectedAsync(ReminderPlace.File);

			await CloseNewPeriodAsync(window);

			Assert.Equal(0, calendar.Reads);
		}

		[Fact]
		public async Task NotConnectedMeansNoCalendarWork()
		{
			var (window, calendar) = await OpenConnectedAsync(connected: false);

			await CloseNewPeriodAsync(window);

			Assert.Equal(0, calendar.Reads);
			Assert.Empty(_dialogs.Shown);
		}

		[Fact]
		public async Task AnUnreadableHistoryNeverEmptiesTheCalendar()
		{
			File.WriteAllText(_dataFile, "not a history");
			new DesktopSettings { DataFilePath = _dataFile, Reminders = new ReminderSetup { Where = ReminderPlace.Google } }.Save();
			var calendar = new MemoryConnection();
			calendar.Entries.Add(new CalendarEntry("kept", ReminderTag.Test, Today.AddDays(30), "Already there"));
			_viewModel = new MainWindowViewModel(_dialogs, null, new FixedClock(), _pending.Enqueue, reminderConnections: _ => ReminderConnections.Of(ReminderPlace.Google, calendar));

			await _viewModel.StartAsync();
			await _viewModel.ReminderSyncFinished;

			Assert.Single(calendar.Entries);
			Assert.Equal(0, calendar.Reads);
		}

		[Fact]
		public async Task AProblemIsSaidOnceAndAgainOnlyAfterItCleared()
		{
			var (window, calendar) = await OpenConnectedAsync();
			calendar.FailWith = new CalendarServiceException(CalendarProblem.NoConnection, "gone");

			await window.RecalculateCommand.ExecuteAsync(null);
			await window.ReminderSyncFinished;
			await window.RecalculateCommand.ExecuteAsync(null);
			await window.ReminderSyncFinished;
			Assert.Equal(UiText.English.RemindersNoConnection, Assert.Single(_dialogs.Shown));

			calendar.FailWith = null;
			await window.RecalculateCommand.ExecuteAsync(null);
			await window.ReminderSyncFinished;
			calendar.FailWith = new CalendarServiceException(CalendarProblem.SignInNeeded, "gone");
			await window.RecalculateCommand.ExecuteAsync(null);
			await window.ReminderSyncFinished;

			Assert.Equal(new[] { UiText.English.RemindersNoConnection, UiText.English.RemindersSignInNeeded }, _dialogs.Shown);
		}


		[Fact]
		public void WithoutGoogleInTheBuildTheFileIsTheOnlyPlace()
		{
			var reminders = RemindersWith(new MemoryConnection { IsAvailable = false, IsConnected = false }, new ReminderSetup { Where = ReminderPlace.Google });

			Assert.False(reminders.CanUseGoogle);
			Assert.True(reminders.IsFile);
			Assert.Equal(UiText.English.RemindersGoogleNotInBuild, reminders.GoogleTip);
		}

		[Fact]
		public void TheChoiceOfPlaceComesBackAsSaved()
		{
			var reminders = RemindersWith(new MemoryConnection(), new ReminderSetup { Where = ReminderPlace.Google });

			Assert.True(reminders.IsGoogle);
			Assert.True(reminders.IsConnected);
			Assert.False(reminders.CanConnect);
		}

		[Fact]
		public async Task ConnectingSaysNothingWhenItWorks()
		{
			var connection = new MemoryConnection { IsConnected = false };
			var reminders = RemindersWith(connection);
			Assert.True(reminders.CanConnect);

			Assert.Null(await reminders.ConnectAsync());

			Assert.True(reminders.IsConnected);
			Assert.False(reminders.IsConnecting);
		}

		[Fact]
		public async Task ASignInThatCantBeKeptIsMentioned()
		{
			var reminders = RemindersWith(new MemoryConnection { IsConnected = false, RemembersSignIn = false });

			var notice = await reminders.ConnectAsync();

			Assert.Equal(UiText.English.RemindersNotRemembered, notice!.Text);
		}

		[Fact]
		public async Task ASignInThatFailsSaysSo()
		{
			var connection = new MemoryConnection { IsConnected = false, FailWith = new CalendarServiceException(CalendarProblem.SignInNotFinished, "no") };
			var reminders = RemindersWith(connection);

			var notice = await reminders.ConnectAsync();

			Assert.Equal(UiText.English.RemindersSignInNotFinished, notice!.Text);
			Assert.Equal(MessageKind.Problem, notice.Kind);
			Assert.False(reminders.IsConnected);
			Assert.True(reminders.CanConnect);
		}

		[Fact]
		public async Task GivingUpOnConnectingLeavesItAsItWas()
		{
			var reminders = RemindersWith(new MemoryConnection { IsConnected = false, SignInHangs = true });

			var waiting = reminders.ConnectAsync();
			Assert.True(reminders.IsConnecting);
			Assert.False(reminders.CanConnect);
			reminders.CancelConnecting();

			Assert.Null(await waiting);
			Assert.False(reminders.IsConnecting);
			Assert.True(reminders.CanConnect);
		}

		[Fact]
		public async Task ATestReminderGoesIntoTheCalendar()
		{
			var connection = new MemoryConnection();
			var reminders = RemindersWith(connection);

			var notice = await reminders.SendTestAsync();

			Assert.Equal(MessageKind.Done, notice.Kind);
			Assert.Equal(ReminderTag.Test, connection.Entries.Single().Tag);
			Assert.Equal("Test reminder", connection.Entries.Single().Title);
		}

		[Fact]
		public async Task StoppingRemovesEverythingAndOffersToConnectAgain()
		{
			var connection = new MemoryConnection();
			connection.Entries.Add(new CalendarEntry("a", "a", new DateTime(2031, 3, 2), "Title"));
			var reminders = RemindersWith(connection);

			Assert.Null(await reminders.DisconnectAsync());

			Assert.Empty(connection.Entries);
			Assert.False(reminders.IsConnected);
			Assert.True(reminders.CanConnect);
		}

		[Fact]
		public async Task StoppingWhileOfflineSaysSoAndStaysConnected()
		{
			var reminders = RemindersWith(new MemoryConnection { FailWith = new CalendarServiceException(CalendarProblem.NoConnection, "gone") });

			var notice = await reminders.DisconnectAsync();

			Assert.Equal(UiText.English.RemindersNoConnection, notice!.Text);
			Assert.True(reminders.IsConnected);
		}

		[Fact]
		public void TheChoiceOfPlaceIsKeptOnOk()
		{
			ReminderSetup? kept = null;
			var reminders = new RemindersWindowViewModel(new ReminderSetup(), new[] { DatedPeriod() }, new ClockAt(new DateTime(2031, 1, 1)), el => kept = el, ReminderConnections.Of(ReminderPlace.Google, new MemoryConnection()));

			reminders.IsGoogle = true;
			reminders.Accept();

			Assert.Equal(ReminderPlace.Google, kept!.Where);
		}

		[Fact]
		public void APlaceThisVersionDoesntKnowMeansTheFile() =>
			Assert.Equal(ReminderPlace.File, new ReminderSetup { WhereText = "Carrier pigeon" }.Where);


		/// <summary>The window with Google chosen, unless the setup says otherwise</summary>
		private RemindersWindowViewModel RemindersWith(MemoryConnection connection, ReminderSetup? setup = null) =>
			new(setup ?? new ReminderSetup { Where = ReminderPlace.Google }, new[] { DatedPeriod() }, new ClockAt(new DateTime(2031, 1, 1)), null, ReminderConnections.Of(ReminderPlace.Google, connection));

		private async Task<(MainWindowViewModel Window, MemoryConnection Calendar)> OpenConnectedAsync(ReminderPlace where = ReminderPlace.Google, bool connected = true)
		{
			WriteHistory();
			new DesktopSettings { DataFilePath = _dataFile, Reminders = new ReminderSetup { Where = where, Scope = ReminderScope.AllDates } }.Save();
			var calendar = new MemoryConnection { IsConnected = connected };
			_viewModel = new MainWindowViewModel(_dialogs, null, new FixedClock(), _pending.Enqueue, reminderConnections: _ => ReminderConnections.Of(where == ReminderPlace.File ? ReminderPlace.Google : where, calendar));
			await _viewModel.StartAsync();
			await _viewModel.ReminderSyncFinished;
			return (_viewModel, calendar);
		}

		private static async Task CloseNewPeriodAsync(MainWindowViewModel window, int daysAgo = 0)
		{
			window.AddPeriodCommand.Execute(null);
			window.Periods[0].BeginDate = Today.AddDays(-daysAgo);
			await window.Periods[0].AcceptCommand.ExecuteAsync(null);
			await window.ReminderSyncFinished;
		}
	}
}
