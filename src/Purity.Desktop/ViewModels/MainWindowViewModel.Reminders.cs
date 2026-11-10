using Purity.Reminders;
using Purity.Storage;
using static Purity.Desktop.Localization.Loc;


namespace Purity.Desktop.ViewModels
{
	/// <summary>Setting up reminders from the settings window, and keeping a connected calendar up to date</summary>
	public sealed partial class MainWindowViewModel
	{
		/// <summary>The latest run of updating the calendar, for whoever needs to wait for it</summary>
		internal Task ReminderSyncFinished { get; private set; } = Task.CompletedTask;


		private RemindersWindowViewModel NewReminders() => new(_settings.Reminders, Ledger.Data, _clock, SaveReminders, _reminderConnection);

		private void SaveReminders(ReminderSetup setup)
		{
			setup.Language = Instance.ReminderLanguage;		// what the preview showed is what the calendar gets
			setup.ChoicesUnshared = setup.Where == ReminderPlace.Google;
			_settings.Reminders = setup;
			try
			{
				_settings.Save();
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				_dialogs.Tell(T.RemindersNotSaved + e.Message, MessageKind.Problem);
			}
			RequestReminderSync();
		}

		/// <summary>The last computer to change its language sets the language of the reminders</summary>
		private void ShareLanguageChange()
		{
			if (_settings.Reminders.Where != ReminderPlace.Google)
				return;

			_settings.Reminders.Language = Instance.ReminderLanguage;
			_settings.Reminders.ChoicesUnshared = true;
			KeepSettingsQuietly();
			RequestReminderSync();
		}

		/// <summary>
		/// Brings the calendar in line, soon. Only from a history that's really been read: an empty
		/// or unreadable one would otherwise look like every reminder should go.
		/// </summary>
		private void RequestReminderSync()
		{
			if (_settings.Reminders.Where != ReminderPlace.Google || _reminderConnection is not { IsConnected: true } || _session?.State != SessionState.Ready)
				return;

			_reminderSyncWanted = true;
			if (_reminderSyncRunning)
				return;

			_reminderSyncRunning = true;
			ReminderSyncFinished = RunReminderSyncAsync();
		}

		private async Task RunReminderSyncAsync()
		{
			try
			{
				await Task.Yield();							// whatever asked for this finishes first
				while (_reminderSyncWanted)
				{
					_reminderSyncWanted = false;
					await SyncRemindersOnceAsync();
				}
			}
			finally
			{
				_reminderSyncRunning = false;
			}
		}

		private async Task SyncRemindersOnceAsync()
		{
			if (_session?.State != SessionState.Ready || _reminderConnection is not { IsConnected: true } connection)
				return;

			try
			{
				await ShareChoicesAsync(connection);
				var wanted = ReminderPlanner.PlanAll(Ledger.Data, _settings.Reminders.ToOptions(Instance.ReminderLanguage));
				await CalendarSync.ApplyAsync(wanted, _clock.GetLocalNow().DateTime, connection.Calendar, _reminderCancel.Token);
				_toldReminderProblem = false;
			}
			catch (CalendarServiceException e)
			{
				if (!_toldReminderProblem)
					_dialogs.Tell(ReminderProblems.Say(e.Problem), MessageKind.Problem);
				_toldReminderProblem = true;				// once is enough, however often it's tried
			}
			catch (OperationCanceledException)
			{
				// the app is closing, and the next run puts right whatever this one didn't reach
			}
		}

		/// <summary>
		/// Sends this computer's choices if the person changed them here, and otherwise takes up the
		/// calendar's. So a computer that's behind can't overwrite newer choices by simply running.
		/// </summary>
		private async Task ShareChoicesAsync(IReminderConnection connection)
		{
			var mine = _settings.Reminders;
			var shared = mine.ChoicesUnshared ? null : await connection.Choices.ReadChoicesAsync(_reminderCancel.Token);
			if (shared != null)
			{
				if (shared.Format() != mine.ToShared(Instance.ReminderLanguage).Format())
				{
					mine.Adopt(shared);
					KeepSettingsQuietly();
				}
				return;
			}

			await connection.Choices.WriteChoicesAsync(mine.ToShared(Instance.ReminderLanguage), _reminderCancel.Token);
			if (mine.ChoicesUnshared)
			{
				mine.ChoicesUnshared = false;
				KeepSettingsQuietly();
			}
		}

		private void KeepSettingsQuietly()
		{
			try
			{
				_settings.Save();
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				// Kept for this run. The calendar still holds the choices for the next one.
			}
		}


		private readonly CancellationTokenSource _reminderCancel = new();
		private bool _reminderSyncWanted;
		private bool _reminderSyncRunning;
		private bool _toldReminderProblem;
	}
}
