using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Purity.Core;
using Purity.Storage;


namespace Purity.Desktop.ViewModels
{
	/// <summary>
	/// The list of periods and the data file behind it. Calculations go through
	/// <see cref="PeriodLedger"/> exactly as the WPF app's did. Saving goes through
	/// <see cref="DataSession"/>, which merges instead of overwriting, never saves over a file it
	/// couldn't read, and keeps unsaved entries on this computer.
	/// </summary>
	public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
	{
		/// <param name="dialogs">The windows the view model talks through</param>
		/// <param name="dataFileOverride">A data file to use instead of the settings' one, for this run only</param>
		/// <param name="clock">Sets "today" for a new period</param>
		/// <param name="runOnUiThread">Queues work on the UI thread, for notices that arrive from a file watcher</param>
		public MainWindowViewModel(IAppDialogs dialogs, string? dataFileOverride, TimeProvider clock, Action<Func<Task>> runOnUiThread)
		{
			_dialogs = dialogs;
			_dataFileOverride = dataFileOverride;
			_clock = clock;
			_runOnUiThread = runOnUiThread;
			_settings = DesktopSettings.Load();
		}


		/// <summary>Opens the data file, first asking for one if none was chosen yet</summary>
		public async Task StartAsync()
		{
			if (string.IsNullOrWhiteSpace(DataFilePath))
				await OpenSettings();
			else
				await OpenDataFileAsync();
		}

		/// <summary>Saves what can be saved, and asks before closing with entries the data file doesn't have yet</summary>
		/// <returns>Whether the window may close</returns>
		public async Task<bool> PrepareToCloseAsync()
		{
			if (_session == null || !_session.HasUnsavedChanges)
				return true;

			await SaveAsync();
			if (!_session.HasUnsavedChanges)
				return true;

			return await _dialogs.AskAsync(UnsavedOnCloseMessage, "Close", "Keep open") == 0;
		}

		public void Dispose() => CloseDataFile();


		[RelayCommand]
		private async Task OpenSettings()
		{
			var settings = new SettingsWindowViewModel(DataFilePath, BuildReport);
			if (!await _dialogs.ShowSettingsAsync(settings))
			{
				if (_session == null)
					UpdateWarning();
				return;
			}

			var chosen = settings.DataFilePath.Trim();
			if (_session != null && SamePath(chosen, DataFilePath))
				return;

			if (_session != null && _session.HasUnsavedChanges)
				await SaveAsync();

			_dataFileOverride = null;
			_settings.DataFilePath = chosen;
			try
			{
				_settings.Save();
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				await _dialogs.AskAsync("The settings couldn't be saved, so Purity will ask for the data file again next time.\n\n" + e.Message, "OK");
			}
			await OpenDataFileAsync();
		}

		private string? BuildReport() => Ledger.Data.Count < 1 ? null : PurityFile.PeriodLengthsReport(Ledger.Data);

		/// <summary>
		/// Adds an open period starting today, as the WPF app does. "Today" is the UTC date there too.
		/// Unlike WPF, only while no other period is open: that one must be accepted or removed first.
		/// </summary>
		[RelayCommand(CanExecute = nameof(CanAddPeriod))]
		private void AddPeriod()
		{
			if (!CanAddPeriod)
				return;

			var today = new DateTimeOffset(_clock.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero);
			var period = Ledger.AddPeriod(today, today.AddDays(7));
			if (period == null)
				return;

			Periods.Add(new PeriodViewModel(period, this));
			RefreshAll();
			UpdateCommands();
		}

		/// <summary>Recalculates every period. A question about two versions of the history comes first.</summary>
		[RelayCommand(CanExecute = nameof(HasDataFile))]
		private async Task Recalculate()
		{
			if (_session?.State == SessionState.NeedsResolution)
			{
				await ResolveAsync();
				return;
			}

			Ledger.Recalculate();
			RefreshAll();
		}

		internal async Task AcceptAsync(PeriodViewModel period)
		{
			Ledger.AcceptPeriod(period.Period);

			var ordered = Periods.OrderBy(el => el.Period.Begin).ToList();
			Periods.Clear();
			foreach (var item in ordered)
				Periods.Add(item);
			RefreshAll();
			UpdateCommands();

			await SaveAsync();
		}

		internal async Task RemoveAsync(PeriodViewModel period)
		{
			if (period.IsClosed && await _dialogs.AskAsync("Are you sure you want to remove this period?", "Yes", "No") != 0)
				return;
			if (!Ledger.RemovePeriod(period.Period))
				return;

			Periods.Remove(period);
			RefreshAll();
			UpdateCommands();

			await SaveAsync();
		}

		internal bool IsLast(PurityPeriod period) => Ledger.Data.Count > 0 && Ledger.Data[^1] == period;


		private async Task OpenDataFileAsync()
		{
			CloseDataFile();
			if (string.IsNullOrWhiteSpace(DataFilePath))
			{
				UpdateWarning();
				return;
			}

			var path = Path.GetFullPath(DataFilePath);
			_store = new FileDataStore(path);
			_session = new DataSession(_store, new LocalVault(AppPaths.VaultFor(path)));
			_session.HistoryReplaced += OnHistoryReplaced;
			UpdateCommands();

			await _session.OpenAsync();
			Rebuild();
			_store.Changed += OnFileChanged;			// starts watching for changes made elsewhere

			await AfterSyncAsync();
		}

		private void CloseDataFile()
		{
			if (_session != null)
				_session.HistoryReplaced -= OnHistoryReplaced;
			if (_store != null)
			{
				_store.Changed -= OnFileChanged;
				_store.Dispose();
			}

			_session = null;
			_store = null;
			_saveFailed = false;
			_toldUnreadable = false;
			Periods.Clear();
			UpdateCommands();
		}

		private void UpdateCommands()
		{
			AddPeriodCommand.NotifyCanExecuteChanged();
			RecalculateCommand.NotifyCanExecuteChanged();
			OnPropertyChanged(nameof(AddPeriodTip));
		}

		private void OnHistoryReplaced(object? sender, EventArgs e) => Rebuild();

		/// <summary>Comes from a file watcher thread</summary>
		private void OnFileChanged(object? sender, EventArgs e) => _runOnUiThread(RefreshFromFileAsync);

		private async Task RefreshFromFileAsync()
		{
			if (_session == null)
				return;

			try
			{
				await _session.RefreshAsync();
				_saveFailed = false;
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				_saveFailed = true;
			}
			await AfterSyncAsync();
		}

		private async Task SaveAsync()
		{
			if (_session == null)
				return;

			try
			{
				var status = await _session.SaveAsync();
				_saveFailed = status == SaveStatus.TryAgain;
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				_saveFailed = true;
			}
			await AfterSyncAsync();
		}

		private async Task AfterSyncAsync()
		{
			if (_session?.State == SessionState.Unreadable && !_toldUnreadable)
			{
				_toldUnreadable = true;
				UpdateWarning();
				await _dialogs.AskAsync(UnreadableMessage, "OK");
			}
			else if (_session?.State == SessionState.NeedsResolution)
			{
				await ResolveAsync();
			}

			if (_session?.State == SessionState.Ready)
				_toldUnreadable = false;
			UpdateWarning();
		}

		/// <summary>Asks about each period both sides changed, then saves the answer</summary>
		private async Task ResolveAsync()
		{
			for (var round = 0; round < 3 && _session?.State == SessionState.NeedsResolution; round++)
			{
				UpdateWarning();
				var answers = new Dictionary<MergeConflict, MergeSide>();
				foreach (var conflict in _session.PendingMerge!.Conflicts)
				{
					var answer = await _dialogs.AskAsync(ConflictText(conflict), "Keep this computer's", "Keep the file's");
					if (answer == null)
						return;						// asked again on Refresh; nothing is saved until then

					answers[conflict] = answer == 0 ? MergeSide.Local : MergeSide.Remote;
				}

				try
				{
					var status = await _session.ResolveAsync(conflict => answers[conflict]);
					_saveFailed = status == SaveStatus.TryAgain;
				}
				catch (Exception e) when (e is IOException or UnauthorizedAccessException)
				{
					_saveFailed = true;
				}
			}
		}

		private void UpdateWarning()
		{
			Warning = _session?.State switch
			{
				null => "Choose your data file in Settings.",
				SessionState.Unreadable => "The data file can't be read. Nothing is written to it until it can be; your entries are kept on this computer.",
				SessionState.NeedsResolution => "Two versions of your history disagree. Press Refresh to choose which to keep. Nothing is saved until then.",
				_ when _saveFailed => "The data file couldn't be written. Your entries are kept on this computer and saved on the next change.",
				_ when _session.VaultFailed => "This computer's backup copy couldn't be written or read. Saving to the data file still works.",
				_ => null,
			};
		}

		private void Rebuild()
		{
			Periods.Clear();
			foreach (var period in Ledger.Data)
				Periods.Add(new PeriodViewModel(period, this));
			UpdateCommands();
		}

		private void RefreshAll()
		{
			foreach (var item in Periods)
				item.Refresh();
		}

		private static string ConflictText(MergeConflict conflict)
		{
			var text = new StringBuilder("Your history was changed both on this computer and in the data file, and the two versions disagree here.\n\nOn this computer:\n");
			Describe(text, conflict.Local);
			text.Append("\nIn the data file:\n");
			Describe(text, conflict.Remote);
			text.Append("\nWhich version do you want to keep?");
			return text.ToString();
		}

		private static void Describe(StringBuilder text, IReadOnlyList<PeriodRecord> periods)
		{
			if (periods.Count == 0)
				text.Append("    nothing\n");
			foreach (var period in periods)
				text.Append(CultureInfo.InvariantCulture, $"    {Describe(period.Begin)} to {Describe(period.End)}{(period.Closed ? "" : ", open")}{(period.SkipStreak ? ", skipped" : "")}\n");
		}

		private static string Describe(DateTimeOffset stamp) =>
			stamp.UtcDateTime.ToString("dddd, d MMMM yyyy", CultureInfo.InvariantCulture) + (PurityEvent.IsDateAfterDusk(stamp) ? " (night)" : " (day)");

		private static bool SamePath(string a, string b)
		{
			if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
				return false;

			var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
			return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), comparison);
		}


		public ObservableCollection<PeriodViewModel> Periods { get; } = new();

		/// <summary>A problem with the data file the person should know about, or null</summary>
		[ObservableProperty]
		public partial string? Warning { get; private set; }

		public string DataFilePath => _dataFileOverride ?? _settings.DataFilePath;

		internal PeriodLedger Ledger => _session?.Ledger ?? NoLedger;

		private bool HasDataFile => _session != null;

		/// <summary>A period that is still open, with the pink frame, waiting to be accepted or removed</summary>
		private bool HasOpenPeriod => Ledger.Data.Any(el => !el.Closed);

		private bool CanAddPeriod => HasDataFile && !HasOpenPeriod;

		/// <summary>Says why the add button is off, since nothing else on screen does</summary>
		public string AddPeriodTip => HasDataFile && HasOpenPeriod ? "Accept or remove the open period first" : "Add period";


		private const string UnreadableMessage =
			"The data file couldn't be read, so you're seeing the copy this computer kept last time, if it has one. Recent entries may be missing.\n\n" +
			"Nothing will be written to the data file until it can be read. Anything you enter now is kept on this computer and added once it can be read, for example when your sync finishes.";

		private const string UnsavedOnCloseMessage =
			"Some entries couldn't be saved to the data file yet.\n\nThey're kept on this computer, and Purity saves them the next time it opens. Close anyway?";

		private static readonly PeriodLedger NoLedger = new();

		private readonly IAppDialogs _dialogs;
		private readonly TimeProvider _clock;
		private readonly Action<Func<Task>> _runOnUiThread;
		private readonly DesktopSettings _settings;
		private string? _dataFileOverride;
		private FileDataStore? _store;
		private DataSession? _session;
		private bool _saveFailed;
		private bool _toldUnreadable;
	}
}
