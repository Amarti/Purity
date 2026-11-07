using System.Text;
using Xunit;
using static Purity.Storage.Tests.History;


namespace Purity.Storage.Tests
{
	public class DataSessionTests : IDisposable
	{
		private readonly TestFolder _folder = new();
		private readonly FileDataStore _store;
		private readonly LocalVault _vault;
		private readonly DataSession _session;


		public DataSessionTests()
		{
			_store = new FileDataStore(_folder.DataFile);
			_vault = new LocalVault(_folder.VaultFolder);
			_session = new DataSession(_store, _vault);
		}

		public void Dispose()
		{
			_store.Dispose();
			_folder.Dispose();
		}


		private byte[] Stored() => File.ReadAllBytes(_folder.DataFile);

		private void Store(params PeriodRecord[] records) => File.WriteAllBytes(_folder.DataFile, FileFor(records));

		private async Task<DataSession> OpenedWith(params PeriodRecord[] records)
		{
			Store(records);
			await _session.OpenAsync();
			Assert.Equal(SessionState.Ready, _session.State);
			return _session;
		}

		/// <summary>Adds a period the way the app does</summary>
		private static void Enter(DataSession session, PeriodRecord record)
		{
			var period = session.Ledger.AddPeriod(record.Begin, record.End);
			session.Ledger.AcceptPeriod(period);
		}


		[Fact]
		public async Task ASessionThatWasNeverOpenedSavesNothing()
		{
			Assert.Equal(SessionState.Closed, _session.State);
			Assert.Equal(SaveStatus.Refused, await _session.SaveAsync());
			Assert.False(File.Exists(_folder.DataFile));
		}

		[Fact]
		public async Task AFirstRunStartsEmptyAndWritesNothing()
		{
			await _session.OpenAsync();

			Assert.Equal(SessionState.Ready, _session.State);
			Assert.Empty(_session.Ledger.Data);
			Assert.Equal(SaveStatus.Unchanged, await _session.SaveAsync());
			Assert.False(File.Exists(_folder.DataFile));
		}

		[Fact]
		public async Task AFirstEntryCreatesTheFile()
		{
			await _session.OpenAsync();
			Enter(_session, Cycle(0));

			Assert.Equal(SaveStatus.Saved, await _session.SaveAsync());
			Assert.Equal(FileFor(Cycle(0)), Stored());
		}

		[Fact]
		public async Task OpeningLoadsTheHistoryWithItsStoredEvents()
		{
			var session = await OpenedWith(Cycles(3));

			Assert.True(SameInputs(Cycles(3), session.Ledger.Inputs()), "inputs");
			Assert.Equal(FileFor(Cycles(3)), Encoding.UTF8.GetBytes(Core.PurityFile.Format(session.Ledger.Data)));
		}

		[Fact]
		public async Task AFileWithAByteOrderMarkOpens()
		{
			File.WriteAllBytes(_folder.DataFile, [0xEF, 0xBB, 0xBF, .. FileFor(Cycles(2))]);

			await _session.OpenAsync();

			Assert.Equal(SessionState.Ready, _session.State);
			Assert.Equal(2, _session.Ledger.Data.Count);
		}

		[Fact]
		public async Task PeriodsOutOfOrderInTheFileOpenSortedWithoutRecalculating()
		{
			var ordered = FileFor(Cycles(3));
			var ledger = new Core.PeriodLedger();
			ledger.Load(Core.PurityFile.Parse(Encoding.UTF8.GetString(ordered)));
			ledger.Data[0].SubEvents[0].Note = "as stored";
			ledger.Data.Reverse();
			File.WriteAllText(_folder.DataFile, Core.PurityFile.Format(ledger.Data));

			await _session.OpenAsync();

			Assert.True(SameInputs(Cycles(3), _session.Ledger.Inputs()), "inputs");
			Assert.Equal("as stored", _session.Ledger.Data[0].SubEvents[0].Note);
		}

		[Fact]
		public async Task SavingWithoutChangesWritesNothing()
		{
			var session = await OpenedWith(Cycles(3));
			var written = File.GetLastWriteTimeUtc(_folder.DataFile);

			Assert.Equal(SaveStatus.Unchanged, await session.SaveAsync());
			Assert.Equal(written, File.GetLastWriteTimeUtc(_folder.DataFile));
		}

		[Fact]
		public async Task AnEntryIsWrittenWithRecalculatedEvents()
		{
			var session = await OpenedWith(Cycles(3));
			Enter(session, Cycle(3));

			Assert.Equal(SaveStatus.Saved, await session.SaveAsync());
			Assert.Equal(FileFor(Cycles(4)), Stored());
		}

		[Fact]
		public async Task SavingTwiceWritesOnce()
		{
			var session = await OpenedWith(Cycles(3));
			Enter(session, Cycle(3));
			await session.SaveAsync();

			Assert.Equal(SaveStatus.Unchanged, await session.SaveAsync());
		}

		[Fact]
		public async Task TheReplacedVersionBecomesABackup()
		{
			var session = await OpenedWith(Cycles(3));
			Enter(session, Cycle(3));

			await session.SaveAsync();

			Assert.Equal(FileFor(Cycles(3)), _vault.ReadBackup(Assert.Single(_vault.ListBackups())));
		}

		[Fact]
		public async Task AVersionReplacedByAnotherDeviceBecomesABackupToo()
		{
			var session = await OpenedWith(Cycles(3));
			Store(Cycles(4));

			await session.RefreshAsync();

			Assert.Equal(FileFor(Cycles(3)), _vault.ReadBackup(Assert.Single(_vault.ListBackups())));
		}

		[Fact]
		public async Task ReopeningWithoutChangesAddsNoNewBackups()
		{
			await OpenedWith(Cycles(3));
			await _session.OpenAsync();
			var before = _vault.ListBackups().Count;

			await _session.OpenAsync();
			await _session.OpenAsync();

			Assert.Equal(before, _vault.ListBackups().Count);
		}

		[Fact]
		public async Task RefreshTakesInAnotherDevicesEntryWithStoredEvents()
		{
			var session = await OpenedWith(Cycles(3));
			Store(Cycles(4));

			await session.RefreshAsync();

			Assert.Equal(SessionState.Ready, session.State);
			Assert.True(SameInputs(Cycles(4), session.Ledger.Inputs()), "inputs");
			Assert.Equal(SaveStatus.Unchanged, await session.SaveAsync());
		}

		[Fact]
		public async Task RefreshWithoutAnyChangeKeepsTheHistory()
		{
			var session = await OpenedWith(Cycles(3));

			await session.RefreshAsync();

			Assert.Equal(SessionState.Ready, session.State);
			Assert.True(SameInputs(Cycles(3), session.Ledger.Inputs()), "inputs");
		}

		[Fact]
		public async Task RefreshKeepsAnEntryNotSavedYet()
		{
			var session = await OpenedWith(Cycles(3));
			Enter(session, Cycle(4));
			Store(Cycles(4));

			await session.RefreshAsync();

			Assert.True(SameInputs(Cycles(5), session.Ledger.Inputs()), "both entries must be there");
			Assert.Equal(FileFor(Cycles(5)), Stored());
			Assert.False(session.HasUnsavedChanges);
		}

		[Fact]
		public async Task ARefreshWithNothingNewKeepsThePeriodObjects()
		{
			var session = await OpenedWith(Cycles(3));
			var held = session.Ledger.Data[^1];
			var replaced = 0;
			session.HistoryReplaced += (_, _) => replaced++;

			await session.RefreshAsync();

			Assert.Same(held, session.Ledger.Data[^1]);
			Assert.Equal(0, replaced);
		}

		[Fact]
		public async Task ARewriteThatChangesNoInputsKeepsAnEntryInProgress()
		{
			var session = await OpenedWith(Cycles(3));
			var inProgress = session.Ledger.AddPeriod(Cycle(5).Begin, Cycle(5).End);
			File.WriteAllText(_folder.DataFile, Encoding.UTF8.GetString(FileFor(Cycles(3))).Replace("\r\n", "\n"));

			await session.RefreshAsync();

			Assert.Same(inProgress, session.Ledger.Data[^1]);
		}

		[Fact]
		public async Task AnAppIsToldWhenThePeriodObjectsAreReplaced()
		{
			var session = await OpenedWith(Cycles(3));
			var replaced = 0;
			session.HistoryReplaced += (_, _) => replaced++;
			Store(Cycles(4));

			await session.RefreshAsync();

			Assert.Equal(1, replaced);
		}

		[Fact]
		public async Task AnAppIsToldWhenAMergeReplacesThePeriodObjects()
		{
			var session = await OpenedWith(Cycles(3));
			Enter(session, Cycle(4));
			var replaced = 0;
			session.HistoryReplaced += (_, _) => replaced++;
			Store(Cycles(4));

			await session.RefreshAsync();

			Assert.Equal(1, replaced);
		}

		[Fact]
		public async Task RefreshingBeforeOpeningOpens()
		{
			Store(Cycles(3));

			await _session.RefreshAsync();

			Assert.Equal(SessionState.Ready, _session.State);
			Assert.Equal(3, _session.Ledger.Data.Count);
		}

		[Fact]
		public async Task UnsavedChangesAreReported()
		{
			Assert.False(_session.HasUnsavedChanges);

			var session = await OpenedWith(Cycles(3));
			Assert.False(session.HasUnsavedChanges);

			Enter(session, Cycle(3));
			Assert.True(session.HasUnsavedChanges);

			await session.SaveAsync();
			Assert.False(session.HasUnsavedChanges);
		}

		[Fact]
		public async Task AnEntryMadeDuringARefreshIsKept()
		{
			var store = new ScriptedStore { Stored = new StoredData(FileFor(Cycles(3)), "v1") };
			var session = new DataSession(store, _vault);
			await session.OpenAsync();
			store.Stored = new StoredData(FileFor(Cycles(4)), "v2");
			store.HoldLoad = new TaskCompletionSource();

			var refreshing = session.RefreshAsync();
			Enter(session, Cycle(5));
			store.HoldLoad.SetResult();
			await refreshing;

			var expected = Cycles(4).Append(Cycle(5)).ToArray();
			Assert.True(SameInputs(expected, session.Ledger.Inputs()), "the entry must stay");
			Assert.Equal(FileFor(expected), store.Stored.Content);
		}

		[Fact]
		public async Task AnEntryMadeDuringASaveIsStillUnsavedAfterIt()
		{
			var store = new ScriptedStore { Stored = new StoredData(FileFor(Cycles(3)), "v1") };
			var session = new DataSession(store, _vault);
			await session.OpenAsync();
			Enter(session, Cycle(3));
			store.HoldSave = new TaskCompletionSource();

			var saving = session.SaveAsync();
			Enter(session, Cycle(4));
			store.HoldSave.SetResult();
			await saving;

			Assert.True(session.HasUnsavedChanges);
			Assert.Equal(SaveStatus.Saved, await session.SaveAsync());
			Assert.Equal(FileFor(Cycles(5)), store.Stored.Content);
		}

		/// <summary>If the save counted that entry as stored, its absence from the file would look like a loss.</summary>
		[Fact]
		public async Task AnEntryMadeDuringASaveRaisesNoFalseQuestionLater()
		{
			var store = new ScriptedStore { Stored = new StoredData(FileFor(Cycles(3)), "v1") };
			var session = new DataSession(store, _vault);
			await session.OpenAsync();
			Enter(session, Cycle(3));
			store.HoldSave = new TaskCompletionSource();
			var saving = session.SaveAsync();
			Enter(session, Cycle(4));
			store.HoldSave.SetResult();
			await saving;
			store.HoldSave = null;

			store.Stored = new StoredData(FileFor(Cycles(4).Append(Cycle(6)).ToArray()), "another device");
			await session.RefreshAsync();

			Assert.Equal(SessionState.Ready, session.State);
			Assert.Equal(FileFor(Cycles(5).Append(Cycle(6)).ToArray()), store.Stored.Content);
		}

		[Fact]
		public async Task CallsWaitForEachOther()
		{
			var store = new ScriptedStore { Stored = new StoredData(FileFor(Cycles(3)), "v1") };
			var session = new DataSession(store, _vault);
			await session.OpenAsync();
			store.HoldLoad = new TaskCompletionSource();

			var refreshing = session.RefreshAsync();
			var saving = session.SaveAsync();

			Assert.False(saving.IsCompleted);
			store.HoldLoad.SetResult();
			await Task.WhenAll(refreshing, saving);
		}

		[Fact]
		public async Task AWaitingCallCanBeCancelled()
		{
			var store = new ScriptedStore { Stored = new StoredData(FileFor(Cycles(3)), "v1") };
			var session = new DataSession(store, _vault);
			await session.OpenAsync();
			store.HoldLoad = new TaskCompletionSource();
			var refreshing = session.RefreshAsync();
			using var cancelled = new CancellationTokenSource();
			await cancelled.CancelAsync();

			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.SaveAsync(cancelled.Token));

			store.HoldLoad.SetResult();
			await refreshing;
			Assert.Equal(SaveStatus.Unchanged, await session.SaveAsync());
		}

		[Fact]
		public async Task ADisagreementIsSettledByThePersonAndSaved()
		{
			var session = await OpenedWith(Cycles(3));
			session.Ledger.Data[^1].End = Cycle(2, length: 6).End;
			session.Ledger.AcceptPeriod(session.Ledger.Data[^1]);
			Store(Cycles(3)[..2].Append(Cycle(2, length: 7)).ToArray());

			Assert.Equal(SaveStatus.NeedsResolution, await session.SaveAsync());
			Assert.Equal(SessionState.NeedsResolution, session.State);
			Assert.Single(session.PendingMerge!.Conflicts);

			Assert.Equal(SaveStatus.Saved, await session.ResolveAsync(_ => MergeSide.Local));

			Assert.Equal(SessionState.Ready, session.State);
			Assert.Null(session.PendingMerge);
			Assert.Equal(FileFor(Cycles(3)[..2].Append(Cycle(2, length: 6)).ToArray()), Stored());
		}

		[Fact]
		public async Task TakingTheStoredSideLeavesTheFileAlone()
		{
			var session = await OpenedWith(Cycles(3));
			session.Ledger.Data[^1].End = Cycle(2, length: 6).End;
			session.Ledger.AcceptPeriod(session.Ledger.Data[^1]);
			var remote = Cycles(3)[..2].Append(Cycle(2, length: 7)).ToArray();
			Store(remote);
			await session.SaveAsync();

			Assert.Equal(SaveStatus.Unchanged, await session.ResolveAsync(_ => MergeSide.Remote));

			Assert.Equal(FileFor(remote), Stored());
			Assert.True(SameInputs(remote, session.Ledger.Inputs()), "inputs");
		}

		[Fact]
		public async Task AnEntryMadeWhileAQuestionIsOpenIsNotDiscardedByTheAnswer()
		{
			var session = await OpenedWith(Cycles(3));
			session.Ledger.Data[^1].End = Cycle(2, length: 6).End;
			session.Ledger.AcceptPeriod(session.Ledger.Data[^1]);
			var remote = Cycles(3)[..2].Append(Cycle(2, length: 7)).ToArray();
			Store(remote);
			await session.SaveAsync();

			Enter(session, Cycle(5));

			Assert.Equal(SaveStatus.NeedsResolution, await session.ResolveAsync(_ => MergeSide.Remote));
			Assert.Contains(session.Ledger.Inputs(), el => el.SameAs(Cycle(5)));
			Assert.Equal(FileFor(remote), Stored());

			Assert.Equal(SaveStatus.Saved, await session.ResolveAsync(_ => MergeSide.Remote));
			Assert.Equal(FileFor(remote.Append(Cycle(5)).ToArray()), Stored());
		}

		[Fact]
		public async Task AnEntryMadeWhileAQuestionIsOpenIsSavedIfTheQuestionWentAway()
		{
			var session = await OpenedWith(Cycles(3));
			session.Ledger.Data[^1].End = Cycle(2, length: 6).End;
			session.Ledger.AcceptPeriod(session.Ledger.Data[^1]);
			Store(Cycles(3)[..2].Append(Cycle(2, length: 7)).ToArray());
			await session.SaveAsync();
			Store(Cycles(3));

			Enter(session, Cycle(5));

			Assert.Equal(SaveStatus.Saved, await session.ResolveAsync(_ => MergeSide.Remote));
			Assert.Equal(FileFor(Cycles(3)[..2].Append(Cycle(2, length: 6)).Append(Cycle(5)).ToArray()), Stored());
		}

		[Fact]
		public async Task ResolvingWithNothingPendingIsRefused()
		{
			var session = await OpenedWith(Cycles(2));

			Assert.Equal(SaveStatus.Refused, await session.ResolveAsync(_ => MergeSide.Local));
		}

		[Fact]
		public async Task RefreshDuringADisagreementComparesAgainTheLatestFile()
		{
			var session = await OpenedWith(Cycles(3));
			session.Ledger.Data[^1].End = Cycle(2, length: 6).End;
			session.Ledger.AcceptPeriod(session.Ledger.Data[^1]);
			Store(Cycles(3)[..2].Append(Cycle(2, length: 7)).ToArray());
			await session.SaveAsync();
			Store(Cycles(3));

			await session.RefreshAsync();

			Assert.Equal(SessionState.Ready, session.State);
			Assert.Null(session.PendingMerge);
		}

		[Fact]
		public async Task TheHistorySurvivesARestartThroughTheVault()
		{
			var session = await OpenedWith(Cycles(3));
			Enter(session, Cycle(3));
			await session.SaveAsync();

			var restarted = new DataSession(_store, _vault);
			await restarted.OpenAsync();

			Assert.Equal(SessionState.Ready, restarted.State);
			Assert.True(SameInputs(Cycles(4), restarted.Ledger.Inputs()), "inputs");
		}

		[Fact]
		public async Task ABackupCanBeRestoredOverAGoodFile()
		{
			var session = await OpenedWith(Cycles(3));
			Enter(session, Cycle(3));
			await session.SaveAsync();
			var backup = Assert.Single(_vault.ListBackups());

			Assert.Equal(SaveStatus.Saved, await session.RestoreAsync(backup));

			Assert.Equal(FileFor(Cycles(3)), Stored());
			Assert.True(SameInputs(Cycles(3), session.Ledger.Inputs()), "inputs");
		}

		[Fact]
		public async Task RestoringADamagedBackupChangesNothing()
		{
			var session = await OpenedWith(Cycles(3));
			Enter(session, Cycle(3));
			await session.SaveAsync();
			var backup = Assert.Single(_vault.ListBackups());
			await File.WriteAllTextAsync(backup.Path, "[{");
			var before = Stored();

			await Assert.ThrowsAsync<InvalidDataException>(() => session.RestoreAsync(backup));

			Assert.Equal(before, Stored());
			Assert.Equal(SessionState.Ready, session.State);
		}

		[Fact]
		public async Task AFailingVaultDoesNotStopTheSaveButIsReported()
		{
			File.WriteAllText(_folder.VaultFolder, "a file where the vault folder should be");
			var session = await OpenedWith(Cycles(3));
			Enter(session, Cycle(3));

			Assert.Equal(SaveStatus.Saved, await session.SaveAsync());

			Assert.Equal(FileFor(Cycles(4)), Stored());
			Assert.True(session.VaultFailed);
		}

		[Fact]
		public async Task ADamagedLastSyncedCopyIsReportedAndOpeningStillWorks()
		{
			Directory.CreateDirectory(_folder.VaultFolder);
			File.WriteAllText(Path.Combine(_folder.VaultFolder, "last-synced.json"), "[{");
			Store(Cycles(3));

			await _session.OpenAsync();

			Assert.Equal(SessionState.Ready, _session.State);
			Assert.Equal(3, _session.Ledger.Data.Count);
			Assert.True(_session.VaultFailed);
		}

		/// <summary>A stale or wiped file couldn't be noticed at that opening, so a later success can't clear it.</summary>
		[Fact]
		public async Task AnOpeningWithoutItsLastCopyStaysReportedUntilTheNextOpening()
		{
			Directory.CreateDirectory(_folder.VaultFolder);
			File.WriteAllText(Path.Combine(_folder.VaultFolder, "last-synced.json"), "[{");
			Store(Cycles(3));
			await _session.OpenAsync();

			Enter(_session, Cycle(3));
			await _session.SaveAsync();
			Assert.True(_session.VaultFailed);

			await _session.OpenAsync();
			Assert.False(_session.VaultFailed);
		}

		[Fact]
		public async Task ALastCopyThatCantBeReadIsKeptAsABackup()
		{
			var damaged = Encoding.UTF8.GetBytes("[{");
			Directory.CreateDirectory(_folder.VaultFolder);
			File.WriteAllBytes(Path.Combine(_folder.VaultFolder, "last-synced.json"), damaged);
			Store(Cycles(3));

			await _session.OpenAsync();

			Assert.Equal(damaged, _vault.ReadBackup(Assert.Single(_vault.ListBackups())));
		}

		[Fact]
		public async Task ALastCopyBrieflyLockedStillGuardsTheOpening()
		{
			await OpenedWith(Cycles(4));
			Store(Cycles(3));
			var files = new InjectedFiles { LockedReads = 2 };

			var restarted = new DataSession(_store, new LocalVault(_folder.VaultFolder, files, TimeProvider.System));
			await restarted.OpenAsync();

			Assert.Equal(SessionState.NeedsResolution, restarted.State);
			Assert.False(restarted.VaultFailed);
		}

		[Fact]
		public async Task AVaultThatRecoversClearsTheReport()
		{
			File.WriteAllText(_folder.VaultFolder, "in the way");
			var session = await OpenedWith(Cycles(3));
			Assert.True(session.VaultFailed);
			File.Delete(_folder.VaultFolder);

			await session.OpenAsync();
			Enter(session, Cycle(3));
			await session.SaveAsync();

			Assert.False(session.VaultFailed);
		}

		[Fact]
		public async Task AFileThatKeepsChangingAsksToTryAgainAndLosesNothing()
		{
			var store = new ScriptedStore { Stored = new StoredData(FileFor(Cycles(2)), "v1"), AlwaysConflict = true };
			var session = new DataSession(store, _vault);
			await session.OpenAsync();
			Enter(session, Cycle(2));

			Assert.Equal(SaveStatus.TryAgain, await session.SaveAsync());

			Assert.Equal(3, store.Saves);
			Assert.Equal(SessionState.Ready, session.State);
			Assert.True(SameInputs(Cycles(3), session.Ledger.Inputs()), "the entry must stay");
		}
	}
}
