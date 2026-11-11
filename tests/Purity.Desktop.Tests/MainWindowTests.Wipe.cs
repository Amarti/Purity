using Purity.Desktop.Localization;
using Purity.Desktop.ViewModels;
using Purity.Reminders;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>Removing all history from Settings. Every date is made up.</summary>
	public sealed partial class MainWindowTests
	{
		[Fact]
		public async Task RemovingAllHistoryAsksOnceThenLeavesNoHistoryBehind()
		{
			var calendar = new MemoryConnection();
			calendar.Entries.Add(new CalendarEntry("kept", ReminderTag.Test, Today.AddDays(30), "Already there"));
			var window = await OpenWithSettingsAsync(calendar);
			var copy = Path.Combine(_root, "pureData (Someone's conflicted copy 2031-06-01).json");
			var leftover = Path.Combine(_root, $"~pureData.json.{Guid.NewGuid():N}.tmp");
			var earlier = AppPaths.DefaultDataFile;
			File.WriteAllText(copy, History());
			File.WriteAllText(leftover, "half a save");
			File.WriteAllText(earlier, History());
			File.WriteAllText(AppPaths.GoogleSignInFile, "a sign-in");
			Assert.True(Directory.Exists(AppPaths.VaultsFolder));
			var offered = false;
			_dialogs.InSettings = settings =>
			{
				offered = settings.CanWipe;
				settings.RequestWipe();
				return false;
			};
			_dialogs.Answers.Enqueue(0);						// Remove for good
			_dialogs.Answers.Enqueue(0);						// Close Purity

			await window.OpenSettingsCommand.ExecuteAsync(null);

			Assert.True(offered);
			Assert.Equal(2, _dialogs.Shown.Count);
			Assert.Contains(UiText.English.WipeBody, _dialogs.Shown[0]);
			Assert.EndsWith(UiText.English.WipeSure, _dialogs.Shown[0]);
			Assert.DoesNotContain(_dataFile, _dialogs.Shown[0]);				// it says "your history and all its backups", not what each one is
			Assert.Contains(UiText.English.Wiped, _dialogs.Shown[1]);
			foreach (var gone in new[] { _dataFile, copy, leftover, earlier, AppPaths.GoogleSignInFile })
				Assert.False(File.Exists(gone), gone);
			Assert.False(Directory.Exists(AppPaths.VaultsFolder));
			Assert.False(calendar.IsConnected);
			Assert.Empty(calendar.Entries);
			Assert.Equal(1, _dialogs.Closed);

			var kept = DesktopSettings.Load();							// the settings stay, but don't lead back to the history
			Assert.Equal(Appearance.Dark, kept.Appearance);
			Assert.Equal(string.Empty, kept.DataFilePath);
			Assert.Null(kept.GoogleCalendarId);
			Assert.True(await window.PrepareToCloseAsync());
			Assert.False(File.Exists(_dataFile));
		}

		[Fact]
		public async Task CancellingRemovesNothing()
		{
			var calendar = new MemoryConnection();
			var window = await OpenWithSettingsAsync(calendar);
			var data = File.ReadAllBytes(_dataFile);
			_dialogs.InSettings = settings =>
			{
				settings.RequestWipe();
				return false;
			};
			_dialogs.Answers.Enqueue(1);

			await window.OpenSettingsCommand.ExecuteAsync(null);

			Assert.Single(_dialogs.Shown);
			Assert.Equal(data, File.ReadAllBytes(_dataFile));
			Assert.Equal(_dataFile, DesktopSettings.Load().DataFilePath);
			Assert.True(Directory.Exists(AppPaths.VaultsFolder));
			Assert.True(calendar.IsConnected);
			Assert.Equal(0, _dialogs.Closed);
			Assert.Equal(3, window.Periods.Count);
		}

		[Fact]
		public async Task WithoutAConnectionTheCalendarStaysButTheSignInIsForgotten()
		{
			var calendar = new MemoryConnection();
			var window = await OpenWithSettingsAsync(calendar);
			calendar.FailWith = new CalendarServiceException(CalendarProblem.NoConnection, "offline");
			_dialogs.InSettings = settings =>
			{
				settings.RequestWipe();
				return false;
			};
			_dialogs.Answers.Enqueue(0);
			_dialogs.Answers.Enqueue(0);

			await window.OpenSettingsCommand.ExecuteAsync(null);

			Assert.Equal(1, calendar.Forgotten);
			Assert.False(calendar.IsConnected);
			Assert.Contains(UiText.English.WipeCalendarLeft, _dialogs.Shown[1]);
			Assert.False(File.Exists(_dataFile));
			Assert.Equal(1, _dialogs.Closed);
		}


		/// <summary>The made-up history, chosen in the settings as on a computer that has used Purity before</summary>
		private async Task<MainWindowViewModel> OpenWithSettingsAsync(MemoryConnection calendar)
		{
			WriteHistory();
			new DesktopSettings { DataFilePath = _dataFile, Appearance = Appearance.Dark, GoogleCalendarId = "made-up" }.Save();
			_viewModel = new MainWindowViewModel(_dialogs, null, new FixedClock(), _pending.Enqueue, reminderConnection: _ => calendar);
			await _viewModel.StartAsync();
			Assert.Empty(_dialogs.Shown);
			return _viewModel;
		}
	}
}
