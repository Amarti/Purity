using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Purity.Core;


namespace Purity.Storage
{
	public enum SessionState
	{
		/// <summary>Not opened yet</summary>
		Closed,
		Ready,
		/// <summary>The stored file can't be read. Nothing may be saved over it</summary>
		Unreadable,
		/// <summary>The two sides disagree, and nothing is saved until a person chooses</summary>
		NeedsResolution,
	}


	public enum SaveStatus
	{
		Saved,
		/// <summary>The stored file already holds this history</summary>
		Unchanged,
		/// <summary>The stored file changed in a way only a person can settle</summary>
		NeedsResolution,
		/// <summary>The stored file can't be read, or was never opened. Nothing was written</summary>
		Refused,
		/// <summary>The stored file kept changing during the save. Nothing was lost, so try again</summary>
		TryAgain,
	}


	/// <summary>
	/// One open history and its stored file. This is the only way an app should read or write the
	/// data file, because it holds the rules that keep the history safe:
	/// <list type="bullet">
	/// <item>A file that failed to load is never saved over.</item>
	/// <item>A file that changed since it was read is merged, never overwritten.</item>
	/// <item>A closed period that changed or went missing in the file needs a person's decision.</item>
	/// <item>Every replaced version stays in the local backups.</item>
	/// <item>An entry that couldn't be saved stays in <see cref="Ledger"/>, and in the vault in case the app closes.</item>
	/// </list>
	/// Calls run one at a time. Change <see cref="Ledger"/> on the thread that awaits them: the
	/// session reads it again after each wait, so entries made meanwhile are kept.
	/// </summary>
	public sealed class DataSession
	{
		public DataSession(IDataStore store, LocalVault vault)
		{
			_store = store;
			_vault = vault;
		}


		/// <summary>
		/// Opens the history, starting from the last copy this device knew. Check
		/// <see cref="State"/> afterwards, as a bad file doesn't throw.
		/// </summary>
		public Task OpenAsync(CancellationToken cancellation = default) =>
			OneAtATime(() => OpenCoreAsync(cancellation), cancellation);

		/// <summary>
		/// Writes the history, merging first if the stored file changed. Call it after each
		/// accepted entry, so nothing depends on the app closing cleanly.
		/// </summary>
		/// <exception cref="IOException">The file couldn't be written. It is unchanged, the entries stay in <see cref="Ledger"/>, and saving again is safe</exception>
		public Task<SaveStatus> SaveAsync(CancellationToken cancellation = default) =>
			OneAtATime(() => SaveCoreAsync(cancellation), cancellation);

		/// <summary>
		/// Takes in a change made elsewhere, then writes anything still waiting. Also the way back
		/// once a damaged file has been repaired, for example when its sync finishes.
		/// </summary>
		/// <exception cref="IOException">A waiting entry couldn't be written. It stays in <see cref="Ledger"/></exception>
		public Task RefreshAsync(CancellationToken cancellation = default) =>
			OneAtATime(() => RefreshCoreAsync(cancellation), cancellation);

		/// <summary>
		/// Settles a disagreement and saves the result. If <see cref="Ledger"/> changed after the
		/// question was raised, the answer no longer fits: nothing is applied, the comparison runs
		/// again, and any new question is left in <see cref="PendingMerge"/>.
		/// </summary>
		/// <exception cref="IOException">The file couldn't be written. The question returns on the next launch</exception>
		public Task<SaveStatus> ResolveAsync(Func<MergeConflict, MergeSide> choose, CancellationToken cancellation = default) =>
			OneAtATime(() => ResolveCoreAsync(choose, cancellation), cancellation);

		/// <summary>
		/// Replaces the stored file with a backup. What it replaces becomes a backup too, and
		/// entries not saved yet join the restored history.
		/// </summary>
		/// <exception cref="InvalidDataException">The backup itself can't be read. Nothing was changed</exception>
		public Task<SaveStatus> RestoreAsync(Backup backup, CancellationToken cancellation = default) =>
			OneAtATime(() => RestoreCoreAsync(backup, cancellation), cancellation);

		/// <summary>
		/// Brings in what a conflicted copy holds. Periods only it has are added, and a period that
		/// differs needs a person's decision. A copy can't remove anything, so it can't undo work.
		/// </summary>
		/// <param name="copy">The content of the copy, which is left for the app to deal with</param>
		/// <param name="cancellation">Cancels the merge</param>
		/// <exception cref="InvalidDataException">The copy can't be read. Nothing was changed</exception>
		public Task<SaveStatus> MergeCopyAsync(byte[] copy, CancellationToken cancellation = default) =>
			OneAtATime(() => MergeCopyCoreAsync(copy, cancellation), cancellation);


		private async Task<T> OneAtATime<T>(Func<Task<T>> action, CancellationToken cancellation)
		{
			await _gate.WaitAsync(cancellation);
			try
			{
				return await action();
			}
			finally
			{
				_gate.Release();
			}
		}

		private async Task<SessionState> OpenCoreAsync(CancellationToken cancellation)
		{
			_lastSyncedLost = false;
			var known = ReadFromVault(vault => vault.ReadLastSynced()) ?? new List<PurityPeriod>();
			var held = ReadFromVault(vault => vault.ReadHeld());

			_baseline = Records(known);
			Replace(held ?? known);
			_synced = null;
			PendingMerge = null;
			State = SessionState.Closed;

			await SyncWithStoreAsync(cancellation);
			if (held != null)
				await TrySaveHeldAsync(cancellation);

			return State;
		}

		/// <summary>Failing here isn't a failed opening: the entries just stay held</summary>
		private async Task TrySaveHeldAsync(CancellationToken cancellation)
		{
			try
			{
				await SaveCoreAsync(cancellation);
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				// HasUnsavedChanges reports it
			}
		}

		private async Task<SaveStatus> RefreshCoreAsync(CancellationToken cancellation)
		{
			if (State == SessionState.Closed)
				await OpenCoreAsync(cancellation);
			else
				await SyncWithStoreAsync(cancellation);

			return await SaveCoreAsync(cancellation);
		}

		private async Task<SaveStatus> ResolveCoreAsync(Func<MergeConflict, MergeSide> choose, CancellationToken cancellation)
		{
			if (State != SessionState.NeedsResolution)
				return SaveStatus.Refused;

			if (!HistoryMerge.Same(_pendingLocal, Records(Ledger.Data)))
			{
				await SyncWithStoreAsync(cancellation);
				return await SaveCoreAsync(cancellation);
			}

			Apply(PendingMerge!.Resolve(choose));
			_baseline = _pendingRemote;
			PendingMerge = null;
			State = SessionState.Ready;

			return await SaveSettledAsync(cancellation);
		}

		private async Task<SaveStatus> RestoreCoreAsync(Backup backup, CancellationToken cancellation)
		{
			var periods = ParseHistory(_vault.ReadBackup(backup));
			var current = await _store.LoadAsync(cancellation);
			if (current != null)
				UseVault(vault => vault.AddBackup(current.Content));

			var restored = Records(periods);
			var held = Records(Ledger.Data).Where(el => !_baseline.Any(el.SameAs)).ToList();
			var merge = HistoryMerge.Merge(Array.Empty<PeriodRecord>(), held, restored);

			_synced = current;
			LoadError = null;
			if (merge.HasConflicts)
			{
				Ask(merge, Records(Ledger.Data), restored);
				return SaveStatus.NeedsResolution;
			}

			if (held.Count == 0)
				Replace(periods);				// the backup's events stay exactly as they are
			else
				Apply(merge.Merged);

			_baseline = restored;
			PendingMerge = null;
			State = SessionState.Ready;

			return await SaveSettledAsync(cancellation);
		}

		private async Task<SaveStatus> MergeCopyCoreAsync(byte[] copy, CancellationToken cancellation)
		{
			var periods = ParseHistory(copy);
			if (State != SessionState.Ready)
				return SaveStatus.Refused;

			var local = Records(Ledger.Data);
			var merge = HistoryMerge.Merge(Array.Empty<PeriodRecord>(), local, Records(periods));
			if (merge.HasConflicts)
			{
				Ask(merge, local, _baseline);
				return SaveStatus.NeedsResolution;
			}

			if (!HistoryMerge.Same(merge.Merged, local))
				Apply(merge.Merged);

			return await SaveCoreAsync(cancellation);
		}

		/// <summary>Saves, and keeps a copy in the vault for as long as that doesn't work out</summary>
		private async Task<SaveStatus> SaveCoreAsync(CancellationToken cancellation)
		{
			try
			{
				var status = await WriteAsync(cancellation);
				if (status is SaveStatus.Saved or SaveStatus.Unchanged)
					ClearHeld();
				else
					HoldEntries();

				return status;
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				HoldEntries();
				throw;
			}
		}

		/// <summary>Usually there is nothing to clear, which says nothing about the vault working</summary>
		private void ClearHeld()
		{
			try
			{
				_vault.ClearHeld();
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				_vaultWriteFailed = true;
			}
		}

		private void HoldEntries()
		{
			if (HasUnsavedChanges)
				UseVault(vault => vault.WriteHeld(Encode(PurityFile.Format(Ledger.Data))));
		}

		private async Task<SaveStatus> WriteAsync(CancellationToken cancellation)
		{
			if (State != SessionState.Ready)
				return State == SessionState.NeedsResolution ? SaveStatus.NeedsResolution : SaveStatus.Refused;

			for (var attempt = 0; attempt < SaveAttempts; attempt++)
			{
				var saving = Records(Ledger.Data);
				var content = Encode(PurityFile.Format(Ledger.Data));
				if (IsStored(content))
					return SaveStatus.Unchanged;

				var result = await _store.SaveAsync(content, _synced?.Version, cancellation);
				if (!result.IsConflict)
				{
					_baseline = saving;
					Remember(new StoredData(content, result.Version!));
					return SaveStatus.Saved;
				}

				await SyncWithStoreAsync(cancellation);
				if (State == SessionState.NeedsResolution)
					return SaveStatus.NeedsResolution;
				if (State == SessionState.Unreadable)
					return SaveStatus.Refused;
			}

			return SaveStatus.TryAgain;
		}

		/// <summary>
		/// Saves a settled question. Until this succeeds the vault keeps the old copy, so a failed
		/// save or a crash raises the question again instead of losing the answer.
		/// </summary>
		private async Task<SaveStatus> SaveSettledAsync(CancellationToken cancellation)
		{
			var status = await SaveCoreAsync(cancellation);
			if (status == SaveStatus.Unchanged)
				Remember(_synced);

			return status;
		}

		/// <returns>The stored history, or null when it can't be read, which is then recorded</returns>
		private async Task<(StoredData? Stored, List<PurityPeriod> Periods)?> TryLoadAsync(CancellationToken cancellation)
		{
			StoredData? stored = null;
			try
			{
				stored = await _store.LoadAsync(cancellation);
				return (stored, stored == null ? new List<PurityPeriod>() : ParseHistory(stored.Content));
			}
			catch (Exception e) when (e is not OperationCanceledException)
			{
				_synced = stored;
				LoadError = e;
				State = SessionState.Unreadable;
				return null;
			}
		}

		private async Task SyncWithStoreAsync(CancellationToken cancellation)
		{
			var loaded = await TryLoadAsync(cancellation);
			if (loaded == null)
				return;

			var (stored, periods) = loaded.Value;
			LoadError = null;
			if (State == SessionState.Ready && stored?.Version == _synced?.Version)
				return;							// nothing new, so the app's period objects stay valid

			// read only now: an entry may have been made while the load was running
			var local = Records(Ledger.Data);
			var remote = Records(periods.OrderBy(el => el.Begin));
			var merge = HistoryMerge.Merge(_baseline, local, remote);

			_synced = stored;
			if (merge.HasConflicts)
			{
				Ask(merge, local, remote);
				return;
			}

			if (HistoryMerge.Same(merge.Merged, remote))
				Replace(periods);				// the stored events stay exactly as they are
			else if (!HistoryMerge.Same(merge.Merged, local))
				Apply(merge.Merged);

			_baseline = remote;
			PendingMerge = null;
			State = SessionState.Ready;
			Remember(stored);
		}

		private void Ask(MergeResult merge, IReadOnlyList<PeriodRecord> local, IReadOnlyList<PeriodRecord> remote)
		{
			_pendingLocal = local;
			_pendingRemote = remote;
			PendingMerge = merge;
			State = SessionState.NeedsResolution;
		}

		/// <summary>
		/// Records what this device now agrees with. The version it replaces becomes a backup,
		/// whether this device or another one replaced it.
		/// </summary>
		private void Remember(StoredData? stored)
		{
			_synced = stored;
			UseVault(vault =>
			{
				var next = stored?.Content ?? Encode(PurityFile.Format(new List<PurityPeriod>()));
				if (vault.ReadLastSynced() is { } previous && !previous.AsSpan().SequenceEqual(next))
					vault.AddBackup(previous);				// the same content again replaces nothing
				vault.WriteLastSynced(next);
			});
		}

		private void Apply(IEnumerable<PeriodRecord> records)
		{
			Ledger.Load(records.Select(el => el.ToPeriod()));
			Ledger.Recalculate();
			HistoryReplaced?.Invoke(this, EventArgs.Empty);
		}

		private void Replace(IEnumerable<PurityPeriod> periods)
		{
			Ledger.Load(periods);
			HistoryReplaced?.Invoke(this, EventArgs.Empty);
		}

		private bool IsStored(byte[] content) =>
			_synced == null ? Ledger.Data.Count == 0 : content.AsSpan().SequenceEqual(_synced.Content);

		private List<PurityPeriod>? ReadFromVault(Func<LocalVault, byte[]?> read)
		{
			try
			{
				var content = read(_vault);
				return content == null ? null : ParseHistory(content);
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
			{
				_lastSyncedLost = true;
				return null;
			}
		}

		/// <summary>A local copy failing must not stop the entry from reaching the data file</summary>
		private void UseVault(Action<LocalVault> action)
		{
			try
			{
				action(_vault);
				_vaultWriteFailed = false;
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				_vaultWriteFailed = true;
			}
		}


		/// <exception cref="InvalidDataException">The content isn't a history this app fully understands</exception>
		private static List<PurityPeriod> ParseHistory(byte[] content)
		{
			var periods = ParseText(content);

			// a format from before Begin was stored would parse, and saving would then erase its dates
			if (periods.Any(el => el.Begin == DateTimeOffset.MinValue))
				throw new InvalidDataException("The data file has periods without a begin date");

			return periods;
		}

		private static List<PurityPeriod> ParseText(byte[] content)
		{
			try
			{
				using var reader = new StreamReader(new MemoryStream(content), StrictUtf8, detectEncodingFromByteOrderMarks: true);
				return PurityFile.Parse(reader.ReadToEnd());
			}
			catch (Exception e) when (e is System.Text.Json.JsonException or DecoderFallbackException)
			{
				throw new InvalidDataException("The data file is damaged or incomplete", e);
			}
		}

		private static byte[] Encode(string text) => StrictUtf8.GetBytes(text);

		private static List<PeriodRecord> Records(IEnumerable<PurityPeriod> periods) => periods.Select(PeriodRecord.From).ToList();


		public PeriodLedger Ledger { get; } = new();
		public SessionState State { get; private set; } = SessionState.Closed;
		/// <summary>Why the stored file couldn't be read</summary>
		public Exception? LoadError { get; private set; }
		/// <summary>The disagreement waiting for <see cref="ResolveAsync"/></summary>
		public MergeResult? PendingMerge { get; private set; }
		/// <summary><see cref="Ledger"/> holds something the stored file doesn't. Warn before closing</summary>
		public bool HasUnsavedChanges => State != SessionState.Closed && !IsStored(Encode(PurityFile.Format(Ledger.Data)));
		/// <summary>
		/// A backup or local copy failed. Saving still works, but the safety net has a hole. When
		/// the last copy couldn't be read at opening, a stale or wiped file couldn't be noticed
		/// either, so that stays reported until the next opening.
		/// </summary>
		public bool VaultFailed => _lastSyncedLost || _vaultWriteFailed;

		/// <summary>The period objects in <see cref="Ledger"/> were replaced, so anything holding them must reload</summary>
		public event EventHandler? HistoryReplaced;

		private const int SaveAttempts = 3;

		private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

		private readonly IDataStore _store;
		private readonly LocalVault _vault;
		private readonly SemaphoreSlim _gate = new(1, 1);

		private StoredData? _synced;
		private IReadOnlyList<PeriodRecord> _baseline = Array.Empty<PeriodRecord>();
		private IReadOnlyList<PeriodRecord> _pendingLocal = Array.Empty<PeriodRecord>();
		private IReadOnlyList<PeriodRecord> _pendingRemote = Array.Empty<PeriodRecord>();
		private bool _lastSyncedLost;
		private bool _vaultWriteFailed;
	}
}
