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
		/// <param name="selfTest">The check of this machine's date calculations, run before anything else</param>
		public MainWindowViewModel(IAppDialogs dialogs, string? dataFileOverride, TimeProvider clock, Action<Func<Task>> runOnUiThread, Func<SelfTestResult>? selfTest = null)
		{
			_dialogs = dialogs;
			_dataFileOverride = dataFileOverride == null ? null : Path.GetFullPath(dataFileOverride, AppPaths.StartFolder);
			_clock = clock;
			_runOnUiThread = runOnUiThread;
			_selfTest = selfTest ?? SelfTest.Run;
			_settings = DesktopSettings.Load();
		}


		/// <summary>
		/// Checks the date calculations, then opens the data file, first asking where to keep the
		/// history if this computer has no data file yet. If the check fails, nothing is opened: no
		/// dates are shown and nothing can be saved.
		/// </summary>
		/// <returns>False when the person closed the first question, so the app should close too</returns>
		public async Task<bool> StartAsync()
		{
			SelfTestResult = _selfTest();
			if (!SelfTestResult.Passed)
			{
				UpdateWarning();
				await _dialogs.AskAsync(SelfTestFailedMessage + SelfTestResult, "OK");
				return true;
			}

			if (!string.IsNullOrWhiteSpace(DataFilePath))
				await OpenDataFileAsync();
			else if (await ChooseFirstDataFileAsync() is { } chosen)
				await UseDataFileAsync(chosen);
			else
				return false;
			return true;
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
			var settings = new SettingsWindowViewModel(DataFilePath, Ledger.Data.Count > 0 ? BuildReport : null, HasBackups(), CanMove, Appearance);
			var ok = await _dialogs.ShowSettingsAsync(settings);
			if (settings.RestoreRequested)
			{
				await ChooseAndRestoreAsync();
				return;
			}
			if (settings.MoveTo is { } target)
			{
				await MoveDataFileAsync(target);
				return;
			}
			if (!ok)
			{
				if (_session == null)
					UpdateWarning();
				return;
			}

			await SetAppearanceAsync(settings.Appearance);
			var chosen = settings.DataFilePath.Trim();
			if (_session != null && SamePath(chosen, DataFilePath))
				return;

			if (_session != null && _session.HasUnsavedChanges)
				await SaveAsync();

			await UseDataFileAsync(chosen);
		}

		private string? BuildReport() => Ledger.Data.Count < 1 ? null : PurityFile.PeriodLengthsReport(Ledger.Data);

		/// <summary>
		/// Adds an open period starting today, as the WPF app does. "Today" is the UTC date there too.
		/// Unlike WPF, only while no other period is open: that one must be accepted or removed first.
		/// </summary>
		[RelayCommand(CanExecute = nameof(CanAddPeriod))]
		private void AddPeriod() => TryAddPeriod();

		/// <returns>False when a period already begins at that moment, which the ledger refuses</returns>
		private bool TryAddPeriod()
		{
			if (!CanAddPeriod)
				return false;

			var today = new DateTimeOffset(_clock.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero);
			var period = Ledger.AddPeriod(today, today.AddDays(7));
			if (period == null)
				return false;

			if (Periods.Count > 0)
				Periods[0].IsFolded = true;				// only the first period shows in full, and that's the new one now
			Periods.Insert(0, new PeriodViewModel(period, this));
			RefreshAll();
			UpdateCommands();
			return true;
		}

		/// <summary>
		/// The wide button: adds a period, or closes the one that's open. Since one button does both, a
		/// second click right after the first is taken for a double click and does nothing, or a double
		/// click would add a period and at once save it with the dates it was given.
		/// </summary>
		[RelayCommand(CanExecute = nameof(HasDataFile))]
		private async Task AddOrClosePeriod()
		{
			var now = _clock.GetUtcNow();
			if (now - _lastAddOrClose < DoubleClickTime)
				return;

			_lastAddOrClose = now;
			if (Periods.FirstOrDefault(el => !el.IsClosed) is not { } open)
				await AddOrExplainAsync();
			else if (await MayCloseAsync(open))
				await AcceptAsync(open);
		}

		/// <summary>The wide button would otherwise look broken, doing nothing and saying nothing</summary>
		private async Task AddOrExplainAsync()
		{
			if (!TryAddPeriod())
				await _dialogs.AskAsync("A period beginning today is already in the history, and two periods can't begin at the same time.\n\nTo add another, first change that period's start date.", "OK");
		}

		/// <summary>
		/// Asks first when the end date hasn't come yet, as it is when it's still the week-ahead date a
		/// new period starts with, since the wide button is easy to press by accident.
		/// </summary>
		private async Task<bool> MayCloseAsync(PeriodViewModel open)
		{
			if (open.EndDate <= _clock.GetLocalNow().Date)
				return true;

			return await _dialogs.AskAsync($"This period's end date, {open.EndText}, hasn't come yet.\n\nClose the period anyway?", "Close period", "Keep open") == 0;
		}

		/// <summary>
		/// Recalculates every period. A question about two versions of the history comes first, and
		/// copies left by a sync app are offered again, even ones put off before.
		/// </summary>
		[RelayCommand(CanExecute = nameof(HasDataFile))]
		private async Task Recalculate()
		{
			if (_session?.State == SessionState.NeedsResolution)
			{
				await ResolvePendingAsync();
				return;
			}

			await BringInCopiesAsync(askAgain: true);
			Ledger.Recalculate();
			RefreshAll();
		}

		internal async Task AcceptAsync(PeriodViewModel period)
		{
			Ledger.AcceptPeriod(period.Period);

			var ordered = Periods.OrderByDescending(el => el.Period.Begin).ToList();
			Periods.Clear();
			foreach (var item in ordered)
				Periods.Add(item);
			KeepFirstPeriodOpen();
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
			KeepFirstPeriodOpen();
			RefreshAll();
			UpdateCommands();

			await SaveAsync();
		}

		internal bool IsLast(PurityPeriod period) => Ledger.Data.Count > 0 && Ledger.Data[^1] == period;


		private async Task OpenDataFileAsync()
		{
			CloseDataFile();
			if (SelfTestResult is { Passed: false } || string.IsNullOrWhiteSpace(DataFilePath))
			{
				UpdateWarning();
				return;
			}

			var path = Path.GetFullPath(DataFilePath);
			_store = new FileDataStore(path);
			_vault = new LocalVault(AppPaths.VaultFor(path));
			_session = new DataSession(_store, _vault);
			_session.HistoryReplaced += OnHistoryReplaced;
			UpdateCommands();

			await _session.OpenAsync();
			Rebuild();
			_store.Changed += OnFileChanged;			// starts watching for changes made elsewhere

			await AfterSyncAsync();
			await CheckForCopiesAsync();
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
			_vault = null;
			_saveFailed = false;
			_toldUnreadable = false;
			_restoringFrom = null;
			ForgetCopies();
			Periods.Clear();
			UpdateCommands();
		}

		private void UpdateCommands()
		{
			AddPeriodCommand.NotifyCanExecuteChanged();
			AddOrClosePeriodCommand.NotifyCanExecuteChanged();
			RecalculateCommand.NotifyCanExecuteChanged();
			OnPropertyChanged(nameof(HasOpenPeriod));
			OnPropertyChanged(nameof(AddOrCloseLabel));
			OnPropertyChanged(nameof(ShowsEmptyHint));
			Show(OpenPeriods, Periods.Where(el => !el.IsClosed));
			Show(ClosedPeriods, Periods.Where(el => el.IsClosed));
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
			await CheckForCopiesAsync();				// a sync app often leaves a copy as it replaces the file
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
				await ResolvePendingAsync();
			}

			if (_session?.State == SessionState.Ready)
				_toldUnreadable = false;
			UpdateWarning();
		}

		/// <summary>
		/// Asks about each period both sides changed, then saves the answer. The other side is the data
		/// file, or a copy being brought in.
		/// </summary>
		/// <returns>
		/// Whether the first question, the one waiting when this was called, is answered, applied and
		/// saved. Only then may a copy that raised it be set aside.
		/// </returns>
		private async Task<bool> ResolveAsync()
		{
			// A change from the sync app arriving while a question is on screen must not open a second
			// one on top: the question being answered notices the change and asks afresh
			if (_resolving)
				return false;

			_resolving = true;
			try
			{
				var first = true;
				var applied = false;
				for (var round = 0; round < 3 && _session?.State == SessionState.NeedsResolution; round++)
				{
					UpdateWarning();
					var (other, keepOther) = (_resolvingCopy, _restoringFrom) switch
					{
						({ } copy, _) => ($"In the copy \"{copy.Name}\"", "Keep the copy's"),
						(null, { } when) => ($"In the backup from {when}", "Keep the backup's"),
						_ => ("In the data file", "Keep the file's"),
					};
					var question = _session.PendingMerge!;
					var answers = new Dictionary<MergeConflict, MergeSide>();
					foreach (var conflict in question.Conflicts)
					{
						var answer = await _dialogs.AskAsync(ConflictText(conflict, other), "Keep this computer's", keepOther);
						if (answer == null)
							return false;			// asked again on Refresh; nothing is saved until then

						answers[conflict] = answer == 0 ? MergeSide.Local : MergeSide.Remote;
					}

					// The change may have settled or replaced the question meanwhile. Answers to a
					// question that's gone must not be applied, and a new question is about the file.
					if (_session?.PendingMerge != question)
					{
						_resolvingCopy = null;
						_restoringFrom = null;
						first = false;
						continue;
					}

					var expected = question.Resolve(conflict => answers[conflict]);
					try
					{
						var status = await _session.ResolveAsync(conflict => answers[conflict]);
						_saveFailed = status == SaveStatus.TryAgain;
					}
					catch (Exception e) when (e is IOException or UnauthorizedAccessException)
					{
						_saveFailed = true;
					}

					// If the history changed some other way, the session compared again with the data
					// file instead of applying the answers
					applied = first && _session.State == SessionState.Ready && !_session.HasUnsavedChanges && Holds(expected);
					if (!applied)
					{
						_resolvingCopy = null;
						_restoringFrom = null;
						first = false;
					}
				}
				return applied;
			}
			finally
			{
				_resolving = false;
			}
		}

		private bool Holds(IReadOnlyList<PeriodRecord> expected) =>
			expected.Count == Ledger.Data.Count && expected.Zip(Ledger.Data).All(el => el.First.SameAs(PeriodRecord.From(el.Second)));

		private void UpdateWarning()
		{
			if (_session?.State != SessionState.NeedsResolution)
				_resolvingCopy = null;				// the question it raised is gone
				_restoringFrom = null;

			Warning = _session?.State switch
			{
				_ when SelfTestResult is { Passed: false } => "Purity's check of its date calculations failed on this computer, so it shows no dates. Don't rely on this copy of the app.",
				null => "Choose your data file in Settings.",
				SessionState.Unreadable => "The data file can't be read. Nothing is written to it until it can be; your entries are kept on this computer.",
				SessionState.NeedsResolution => "Two versions of your history disagree. Press Refresh to choose which to keep. Nothing is saved until then.",
				_ when _saveFailed => "The data file couldn't be written. Your entries are kept on this computer and saved on the next change.",
				_ when _copiesWaiting > 0 && !_checkingCopies => (_copiesWaiting == 1
					? "A copy of the data file left by your sync app is beside it and not brought in yet. Press Refresh to bring it in."
					: $"{_copiesWaiting} copies of the data file left by your sync app are beside it and not brought in yet. Press Refresh to bring them in."),
				_ when _session.VaultFailed => "This computer's backup copy couldn't be written or read. Saving to the data file still works.",
				_ => null,
			};
			OnPropertyChanged(nameof(ShowsEmptyHint));
		}

		private void Rebuild()
		{
			Periods.Clear();
			var first = Ledger.Data.LastOrDefault();
			foreach (var period in Enumerable.Reverse(Ledger.Data))
				Periods.Add(new PeriodViewModel(period, this) { IsFolded = period.Closed && period != first });
			UpdateCommands();
		}

		/// <summary>Touches the list only when it's different, so adding an open period doesn't disturb it</summary>
		private static void Show(ObservableCollection<PeriodViewModel> shown, IEnumerable<PeriodViewModel> periods)
		{
			var wanted = periods.ToList();
			if (shown.SequenceEqual(wanted))
				return;

			shown.Clear();
			foreach (var item in wanted)
				shown.Add(item);
		}

		/// <summary>The first period in the list always shows in full, whichever one that has become</summary>
		private void KeepFirstPeriodOpen()
		{
			if (Periods.Count > 0)
				Periods[0].IsFolded = false;
		}

		private void RefreshAll()
		{
			foreach (var item in Periods)
				item.Refresh();
		}

		/// <param name="conflict">The periods the two sides disagree on</param>
		/// <param name="other">Names the other side: the data file or a copy</param>
		private static string ConflictText(MergeConflict conflict, string other)
		{
			var text = new StringBuilder("Your history was changed in two places, and the two versions disagree here.\n\nOn this computer:\n");
			Describe(text, conflict.Local);
			text.Append('\n').Append(other).Append(":\n");
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


		/// <summary>The periods as the list shows them, newest first. <see cref="Ledger"/> keeps them oldest first, as the data file does.</summary>
		public ObservableCollection<PeriodViewModel> Periods { get; } = new();

		/// <summary>Those still open, which stay in view above the list</summary>
		public ObservableCollection<PeriodViewModel> OpenPeriods { get; } = new();

		/// <summary>What the list shows: the accepted ones, newest first</summary>
		public ObservableCollection<PeriodViewModel> ClosedPeriods { get; } = new();

		/// <summary>A problem with the data file the person should know about, or null</summary>
		[ObservableProperty]
		public partial string? Warning { get; private set; }

		public string DataFilePath => _dataFileOverride ?? _settings.DataFilePath;

		/// <summary>How the check of this machine's date calculations went, once it has run</summary>
		public SelfTestResult? SelfTestResult { get; private set; }

		internal PeriodLedger Ledger => _session?.Ledger ?? NoLedger;

		private bool HasDataFile => _session != null;

		/// <summary>A period that is still open, with the pink frame, waiting to be accepted or removed</summary>
		public bool HasOpenPeriod => Ledger.Data.Any(el => !el.Closed);

		private bool CanAddPeriod => HasDataFile && !HasOpenPeriod;

		/// <summary>What the wide button does now, for its tooltip and screen readers</summary>
		public string AddOrCloseLabel => HasOpenPeriod ? ClosePeriodLabel : NewPeriodLabel;

		/// <summary>An empty history that opened properly, so the list can say what to do first</summary>
		public bool ShowsEmptyHint => _session?.State == SessionState.Ready && Periods.Count == 0;


		public const string NewPeriodLabel = "New period";
		public const string ClosePeriodLabel = "Close period";

		private const string UnreadableMessage =
			"The data file couldn't be read, so you're seeing the copy this computer kept last time, if it has one. Recent entries may be missing.\n\n" +
			"Nothing will be written to the data file until it can be read. Anything you enter now is kept on this computer and added once it can be read, for example when your sync finishes.";

		private const string SelfTestFailedMessage =
			"Purity checks its date calculations every time it starts, and on this computer they came out different from the approved ones. " +
			"So it shows no dates, and it hasn't opened your data file, so nothing in it can change.\n\n" +
			"Please don't rely on this copy of the app, and let the developer know what the check found:\n\n";

		private const string UnsavedOnCloseMessage =
			"Some entries couldn't be saved to the data file yet.\n\nThey're kept on this computer, and Purity saves them the next time it opens. Close anyway?";

		private static readonly PeriodLedger NoLedger = new();
		private static readonly TimeSpan DoubleClickTime = TimeSpan.FromMilliseconds(600);

		private readonly IAppDialogs _dialogs;
		private readonly TimeProvider _clock;
		private readonly Action<Func<Task>> _runOnUiThread;
		private readonly Func<SelfTestResult> _selfTest;
		private readonly DesktopSettings _settings;
		private string? _dataFileOverride;
		private FileDataStore? _store;
		private LocalVault? _vault;
		private DataSession? _session;
		private bool _saveFailed;
		private bool _toldUnreadable;
		private bool _resolving;
		private DateTimeOffset _lastAddOrClose = DateTimeOffset.MinValue;
	}
}
