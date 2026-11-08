using System.Text;
using Xunit;
using static Purity.Storage.Tests.History;


namespace Purity.Storage.Tests
{
	/// <summary>
	/// Each way the old app can lose the history, made to happen on purpose. The rule in every test:
	/// the stored file ends up as it was, or as a complete newer version, and no entry disappears.
	/// </summary>
	public class FailureInjectionTests : IDisposable
	{
		private readonly TestFolder _folder = new();
		private readonly InjectedFiles _files = new();
		private readonly FileDataStore _store;
		private readonly LocalVault _vault;
		private readonly DataSession _session;


		public FailureInjectionTests()
		{
			_store = new FileDataStore(_folder.DataFile, _files, TimeSpan.Zero);
			_vault = new LocalVault(_folder.VaultFolder);
			_session = new DataSession(_store, _vault);
		}

		public void Dispose()
		{
			_store.Dispose();
			_folder.Dispose();
		}


		private byte[] Stored() => _folder.ReadData();

		private void Store(byte[] content) => _folder.WriteData(content);

		private async Task OpenWith(params PeriodRecord[] records)
		{
			Store(FileFor(records));
			await _session.OpenAsync();
			Assert.Equal(SessionState.Ready, _session.State);
		}

		private void Enter(PeriodRecord record) => _session.Ledger.AcceptPeriod(_session.Ledger.AddPeriod(record.Begin, record.End));


		// ---- A damaged file must never be saved over ----

		/// <summary>The failure that can erase the history in the old app: load fails, save writes an empty list.</summary>
		[Fact]
		public async Task AFileCutAtAnyByteIsNeverSavedOver()
		{
			var whole = FileFor(Cycles(4));
			for (var length = 0; length < whole.Length; length++)
			{
				var damaged = whole[..length];
				Store(damaged);
				var session = new DataSession(_store, new LocalVault(Path.Combine(_folder.Root, "cut")));

				await session.OpenAsync();

				Assert.True(session.State == SessionState.Unreadable, $"cut at {length} wasn't noticed");
				Assert.Equal(SaveStatus.Refused, await session.SaveAsync());
				Assert.Equal(damaged, Stored());
			}
		}

		[Theory]
		[InlineData("")]
		[InlineData("   ")]
		[InlineData("{}")]
		[InlineData("[1, 2]")]
		[InlineData("{\"Begin\": \"2033-01-01T00:00:00+00:00\"}")]
		[InlineData("[{\"Begin\": \"not a date\"}]")]
		[InlineData("[{\"Begin\": \"2033-13-45T00:00:00+00:00\"}]")]
		[InlineData("<html>Sign in to continue</html>")]
		public async Task ContentThatIsNotAHistoryIsNeverSavedOver(string content)
		{
			var bytes = Encoding.UTF8.GetBytes(content);
			Store(bytes);

			await _session.OpenAsync();

			Assert.Equal(SessionState.Unreadable, _session.State);
			Assert.NotNull(_session.LoadError);
			Assert.Equal(SaveStatus.Refused, await _session.SaveAsync());
			Assert.Equal(bytes, Stored());
		}

		[Fact]
		public async Task BytesThatAreNotTextAreNeverSavedOver()
		{
			byte[] junk = [0x5B, 0xFF, 0xFE, 0x00, 0xC0, 0x80, 0x5D];
			Store(junk);

			await _session.OpenAsync();

			Assert.Equal(SessionState.Unreadable, _session.State);
			Assert.Equal(SaveStatus.Refused, await _session.SaveAsync());
			Assert.Equal(junk, Stored());
		}

		/// <summary>Valid JSON around a bad byte: a lenient read would swap the byte for a placeholder and save that.</summary>
		[Fact]
		public async Task AnInvalidByteInsideValidJsonIsNeverSavedOver()
		{
			var text = Encoding.UTF8.GetBytes("[{\"Begin\": \"2033-01-01T00:00:00+00:00\", \"End\": \"2033-01-05T00:00:00+00:00\", \"Closed\": true, \"SkipStreak\": false, \"SubEvents\": [{\"Type\": 1, \"Stamp\": \"2033-01-13T12:00:00+00:00\", \"Note\": \"#\"}]}]");
			text[Array.IndexOf(text, (byte)'#')] = 0xFF;
			Store(text);

			await _session.OpenAsync();

			Assert.Equal(SessionState.Unreadable, _session.State);
			Assert.Equal(text, Stored());
		}

		/// <summary>Parses without error, and saving would then erase every date in it.</summary>
		[Theory]
		[InlineData("[{\"BeginDate\": \"2033-01-01T00:00:00\", \"EndDate\": \"2033-01-05T00:00:00\", \"SubEvents\": []}]")]
		[InlineData("[{\"Begin\": {\"Date\": \"2033-01-01\"}, \"End\": {\"Date\": \"2033-01-05\"}, \"SubEvents\": []}]")]
		[InlineData("[{}]")]
		public async Task AFormatFromBeforeTheseFieldsIsNeverSavedOver(string content)
		{
			var bytes = Encoding.UTF8.GetBytes(content);
			Store(bytes);

			await _session.OpenAsync();

			Assert.Equal(SessionState.Unreadable, _session.State);
			Assert.Equal(SaveStatus.Refused, await _session.SaveAsync());
			Assert.Equal(bytes, Stored());
		}

		[Fact]
		public async Task AFileTheSystemWontReadIsTreatedAsUnreadable()
		{
			var store = new ScriptedStore { LoadFailure = new UnauthorizedAccessException("Access denied") };
			var session = new DataSession(store, _vault);

			await session.OpenAsync();

			Assert.Equal(SessionState.Unreadable, session.State);
			Assert.Equal(SaveStatus.Refused, await session.SaveAsync());
			Assert.Equal(0, store.Saves);
		}

		[Fact]
		public async Task ACancelledOpenLeavesNothingSavable()
		{
			using var cancelled = new CancellationTokenSource();
			await cancelled.CancelAsync();
			Store(FileFor(Cycles(3)));

			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _session.OpenAsync(cancelled.Token));

			Assert.Equal(SaveStatus.Refused, await _session.SaveAsync());
		}

		[Fact]
		public async Task AHalfSyncedFileRecoversOnceTheSyncFinishes()
		{
			var whole = FileFor(Cycles(4));
			Store(whole[..(whole.Length / 2)]);
			await _session.OpenAsync();
			Assert.Equal(SessionState.Unreadable, _session.State);

			Store(whole);
			await _session.RefreshAsync();

			Assert.Equal(SessionState.Ready, _session.State);
			Assert.Null(_session.LoadError);
			Assert.True(SameInputs(Cycles(4), _session.Ledger.Inputs()), "inputs");
		}

		[Fact]
		public async Task AnEntryHeldDuringDamageReachesTheFileAfterRecovery()
		{
			await OpenWith(Cycles(3));
			Enter(Cycle(3));
			var whole = FileFor(Cycles(3));
			Store(whole[..(whole.Length / 2)]);

			Assert.Equal(SaveStatus.Refused, await _session.SaveAsync());
			Assert.Equal(SessionState.Unreadable, _session.State);
			Assert.True(SameInputs(Cycles(4), _session.Ledger.Inputs()), "the entry must stay in memory");
			Assert.True(_session.HasUnsavedChanges);

			Store(whole);
			await _session.RefreshAsync();

			Assert.Equal(FileFor(Cycles(4)), Stored());
			Assert.False(_session.HasUnsavedChanges);
		}

		[Fact]
		public async Task ADamagedFileStillShowsTheLastKnownHistory()
		{
			await OpenWith(Cycles(3));
			Store(Encoding.UTF8.GetBytes("[{"));

			var restarted = new DataSession(_store, _vault);
			await restarted.OpenAsync();

			Assert.Equal(SessionState.Unreadable, restarted.State);
			Assert.True(SameInputs(Cycles(3), restarted.Ledger.Inputs()), "inputs");
			Assert.Equal(SaveStatus.Refused, await restarted.SaveAsync());
		}

		[Fact]
		public async Task AnEntryMadeAfterAStartupWithADamagedFileIsKept()
		{
			await OpenWith(Cycles(3));
			var whole = Stored();
			Store(whole[..(whole.Length / 2)]);
			var restarted = new DataSession(_store, _vault);
			await restarted.OpenAsync();
			var entry = Cycle(3);
			restarted.Ledger.AcceptPeriod(restarted.Ledger.AddPeriod(entry.Begin, entry.End));

			Store(whole);
			await restarted.RefreshAsync();

			Assert.Equal(FileFor(Cycles(4)), Stored());
		}

		[Fact]
		public async Task RestoringKeepsAnEntryThatWasNeverSaved()
		{
			await OpenWith(Cycles(3));
			Enter(Cycle(3));
			await _session.SaveAsync();
			var backup = _vault.ListBackups()[0];
			Enter(Cycle(5));
			Store(Encoding.UTF8.GetBytes("[{"));
			Assert.Equal(SaveStatus.Refused, await _session.SaveAsync());

			Assert.Equal(SaveStatus.Saved, await _session.RestoreAsync(backup));

			Assert.Equal(FileFor(Cycles(3).Append(Cycle(5)).ToArray()), Stored());
		}

		[Fact]
		public async Task RestoringWithAnEntryForTheSameCycleAsks()
		{
			await OpenWith(Cycles(3));
			Enter(Cycle(3));
			await _session.SaveAsync();
			Enter(Cycle(4));
			await _session.SaveAsync();
			var backup = _vault.ListBackups()[0];
			var edited = Cycle(3, length: 7);
			_session.Ledger.Data[3].End = edited.End;

			Assert.Equal(SaveStatus.NeedsResolution, await _session.RestoreAsync(backup));

			Assert.Equal(SaveStatus.Saved, await _session.ResolveAsync(_ => MergeSide.Local));
			Assert.Equal(FileFor(Cycles(3).Append(edited).ToArray()), Stored());
		}

		[Fact]
		public async Task RestoringWhatTheFileAlreadyHoldsSettlesTheLastCopyToo()
		{
			await OpenWith(Cycles(4));
			Enter(Cycle(4));
			await _session.SaveAsync();
			var backup = _vault.ListBackups()[0];
			Store(FileFor(Cycles(4)));
			var restarted = new DataSession(_store, _vault);
			await restarted.OpenAsync();
			Assert.Equal(SessionState.NeedsResolution, restarted.State);

			Assert.Equal(SaveStatus.Unchanged, await restarted.RestoreAsync(backup));

			var again = new DataSession(_store, _vault);
			await again.OpenAsync();
			Assert.Equal(SessionState.Ready, again.State);
		}

		[Fact]
		public async Task ADamagedFileCanBeReplacedWithABackupAndIsKeptItself()
		{
			await OpenWith(Cycles(3));
			Enter(Cycle(3));
			await _session.SaveAsync();
			var damaged = Encoding.UTF8.GetBytes("[{ damaged");
			Store(damaged);
			var session = new DataSession(_store, _vault);
			await session.OpenAsync();
			Assert.Equal(SessionState.Unreadable, session.State);

			var status = await session.RestoreAsync(_vault.ListBackups()[0]);

			Assert.Equal(SaveStatus.Saved, status);
			Assert.Equal(SessionState.Ready, session.State);
			Assert.Equal(FileFor(Cycles(3)), Stored());
			Assert.Contains(_vault.ListBackups(), el => _vault.ReadBackup(el).AsSpan().SequenceEqual(damaged));
		}


		// ---- A wiped or missing file must not take the history with it ----

		/// <summary>The old app saves an empty list over the file, and the new app sees a valid file.</summary>
		[Fact]
		public async Task AWipedFileNeedsADecisionAndCanBeUndone()
		{
			await OpenWith(Cycles(4));
			Store(FileFor());

			var restarted = new DataSession(_store, _vault);
			await restarted.OpenAsync();

			Assert.Equal(SessionState.NeedsResolution, restarted.State);
			Assert.Equal(SaveStatus.NeedsResolution, await restarted.SaveAsync());
			Assert.Equal(FileFor(), Stored());

			Assert.Equal(SaveStatus.Saved, await restarted.ResolveAsync(_ => MergeSide.Local));

			Assert.Equal(FileFor(Cycles(4)), Stored());
		}

		/// <summary>The answer is only safe once it reaches the file, so a failure must raise the question again.</summary>
		[Fact]
		public async Task AnAnswerThatCouldNotBeSavedIsAskedAgainAfterARestart()
		{
			await OpenWith(Cycles(4));
			Store(FileFor(Cycles(3)));
			var restarted = new DataSession(_store, _vault);
			await restarted.OpenAsync();
			_files.DiskFull = true;

			await Assert.ThrowsAsync<IOException>(() => restarted.ResolveAsync(_ => MergeSide.Local));
			_files.DiskFull = false;

			var again = new DataSession(_store, _vault);
			await again.OpenAsync();
			Assert.Equal(SessionState.NeedsResolution, again.State);
			Assert.Equal(4, again.Ledger.Data.Count);
		}

		[Fact]
		public async Task AnEntryMadeDuringAStartupQuestionIsKept()
		{
			await OpenWith(Cycles(4));
			Store(FileFor(Cycles(3)));
			var restarted = new DataSession(_store, _vault);
			await restarted.OpenAsync();
			var entry = Cycle(5);
			restarted.Ledger.AcceptPeriod(restarted.Ledger.AddPeriod(entry.Begin, entry.End));

			Assert.Equal(SaveStatus.NeedsResolution, await restarted.ResolveAsync(_ => MergeSide.Local));
			Assert.Equal(SaveStatus.Saved, await restarted.ResolveAsync(_ => MergeSide.Local));

			Assert.Equal(FileFor(Cycles(4).Append(entry).ToArray()), Stored());
		}

		[Fact]
		public async Task AnAnswerWhenTheFileHasSinceBeenDamagedIsRefused()
		{
			await OpenWith(Cycles(4));
			Store(FileFor(Cycles(3)));
			await _session.RefreshAsync();
			Enter(Cycle(5));
			var damaged = Encoding.UTF8.GetBytes("[{");
			Store(damaged);

			Assert.Equal(SaveStatus.Refused, await _session.ResolveAsync(_ => MergeSide.Local));

			Assert.Equal(damaged, Stored());
			Assert.Equal(SessionState.Unreadable, _session.State);
		}

		/// <summary>The old app reverting a correction: it read the file before the End was fixed.</summary>
		[Fact]
		public async Task AStaleCopyRevertingAClosedPeriodNeedsADecision()
		{
			var corrected = Cycles(3)[..2].Append(Cycle(2, length: 7)).ToArray();
			await OpenWith(corrected);
			Store(FileFor(Cycles(3)));

			await _session.RefreshAsync();

			Assert.Equal(SessionState.NeedsResolution, _session.State);
			Assert.True(SameInputs(corrected, _session.Ledger.Inputs()), "the correction must stay");
		}

		[Fact]
		public async Task AnAcceptedWipeIsNotAskedAgainAndStaysInTheBackups()
		{
			await OpenWith(Cycles(4));
			Store(FileFor());
			var restarted = new DataSession(_store, _vault);
			await restarted.OpenAsync();

			Assert.Equal(SaveStatus.Unchanged, await restarted.ResolveAsync(_ => MergeSide.Remote));

			var again = new DataSession(_store, _vault);
			await again.OpenAsync();
			Assert.Equal(SessionState.Ready, again.State);
			Assert.Contains(_vault.ListBackups(), el => _vault.ReadBackup(el).AsSpan().SequenceEqual(FileFor(Cycles(4))));
		}

		/// <summary>
		/// The laptop left open, for real: the old app read the file before the newest entry was
		/// made, and writes its stale copy back when it closes.
		/// </summary>
		[Fact]
		public async Task AStaleCopyWrittenOverTheNewestEntryDoesNotLoseIt()
		{
			await OpenWith(Cycles(4));
			Store(FileFor(Cycles(3)));

			await _session.RefreshAsync();

			Assert.Equal(SessionState.NeedsResolution, _session.State);
			Assert.Equal(4, _session.Ledger.Data.Count);

			Assert.Equal(SaveStatus.Saved, await _session.ResolveAsync(_ => MergeSide.Local));
			Assert.Equal(FileFor(Cycles(4)), Stored());
		}

		[Fact]
		public async Task AStaleCopySeenAtStartupDoesNotLoseTheNewestEntry()
		{
			await OpenWith(Cycles(4));
			Store(FileFor(Cycles(3)));

			var restarted = new DataSession(_store, _vault);
			await restarted.OpenAsync();

			Assert.Equal(SessionState.NeedsResolution, restarted.State);
			Assert.Equal(SaveStatus.Saved, await restarted.ResolveAsync(_ => MergeSide.Local));
			Assert.Equal(FileFor(Cycles(4)), Stored());
		}

		[Fact]
		public async Task ARemovalOnThisDeviceIsSavedWithoutAQuestion()
		{
			await OpenWith(Cycles(4));

			Assert.True(_session.Ledger.RemovePeriod(_session.Ledger.Data[^1]));

			Assert.Equal(SaveStatus.Saved, await _session.SaveAsync());
			Assert.Equal(3, Core.PurityFile.Parse(Encoding.UTF8.GetString(Stored())).Count);
		}

		[Fact]
		public async Task AFileWipedWhileRunningIsNotTakenIn()
		{
			await OpenWith(Cycles(4));
			Store(FileFor());

			await _session.RefreshAsync();

			Assert.Equal(SessionState.NeedsResolution, _session.State);
			Assert.Equal(4, _session.Ledger.Data.Count);
		}

		[Fact]
		public async Task AFileDeletedWhileRunningIsNotRecreatedWithoutADecision()
		{
			await OpenWith(Cycles(4));
			Enter(Cycle(4));
			File.Delete(_folder.DataFile);

			Assert.Equal(SaveStatus.NeedsResolution, await _session.SaveAsync());
			Assert.False(File.Exists(_folder.DataFile));

			Assert.Equal(SaveStatus.Saved, await _session.ResolveAsync(_ => MergeSide.Local));

			Assert.Equal(FileFor(Cycles(5)), Stored());
		}

		[Fact]
		public async Task AFileMissingAtStartupIsNotTakenForAFreshStart()
		{
			await OpenWith(Cycles(4));
			File.Delete(_folder.DataFile);

			var restarted = new DataSession(_store, _vault);
			await restarted.OpenAsync();

			Assert.Equal(SessionState.NeedsResolution, restarted.State);
		}

		[Fact]
		public async Task AcceptingADeletedFileIsNotAskedAgain()
		{
			await OpenWith(Cycles(4));
			File.Delete(_folder.DataFile);
			var restarted = new DataSession(_store, _vault);
			await restarted.OpenAsync();

			await restarted.ResolveAsync(_ => MergeSide.Remote);

			var again = new DataSession(_store, _vault);
			await again.OpenAsync();
			Assert.Equal(SessionState.Ready, again.State);
			Assert.Empty(again.Ledger.Data);
		}


		// ---- A concurrent change must be merged, never overwritten ----

		/// <summary>The laptop left open that would wipe entries made on the phone.</summary>
		[Fact]
		public async Task AnotherDevicesEntryIsKeptWhenThisOneSaves()
		{
			await OpenWith(Cycles(3));
			Enter(Cycle(4));
			Store(FileFor(Cycles(4)));

			Assert.Equal(SaveStatus.Saved, await _session.SaveAsync());

			Assert.Equal(FileFor(Cycles(5)), Stored());
		}

		[Fact]
		public async Task AChangeDuringTheSaveIsMergedToo()
		{
			await OpenWith(Cycles(3));
			Enter(Cycle(4));
			_files.AfterWrite = () =>
			{
				_files.AfterWrite = null;
				Store(FileFor(Cycles(4)));
			};

			Assert.Equal(SaveStatus.Saved, await _session.SaveAsync());

			Assert.Equal(FileFor(Cycles(5)), Stored());
		}

		[Fact]
		public async Task TwoSessionsSavingInTurnKeepBothEntries()
		{
			Store(FileFor(Cycles(3)));
			using var otherStore = new FileDataStore(_folder.DataFile);
			var other = new DataSession(otherStore, new LocalVault(Path.Combine(_folder.Root, "other")));
			await _session.OpenAsync();
			await other.OpenAsync();

			Enter(Cycle(3));
			var entry = Cycle(4);
			other.Ledger.AcceptPeriod(other.Ledger.AddPeriod(entry.Begin, entry.End));

			Assert.Equal(SaveStatus.Saved, await _session.SaveAsync());
			Assert.Equal(SaveStatus.Saved, await other.SaveAsync());

			Assert.Equal(FileFor(Cycles(5)), Stored());
		}

		[Fact]
		public async Task TheSamePeriodEnteredOnBothDevicesIsWrittenOnce()
		{
			await OpenWith(Cycles(3));
			Enter(Cycle(3));
			Store(FileFor(Cycles(4)));

			Assert.Equal(SaveStatus.Unchanged, await _session.SaveAsync());

			Assert.Equal(FileFor(Cycles(4)), Stored());
		}

		/// <summary>Whoever held the file may have been writing it, so each retry checks again.</summary>
		[Fact]
		public async Task AChangeMadeWhileTheFileWasLockedIsMergedNotOverwritten()
		{
			await OpenWith(Cycles(3));
			Enter(Cycle(4));
			_files.LockedMoves = 1;
			_files.AfterLockedMove = () => Store(FileFor(Cycles(4)));

			Assert.Equal(SaveStatus.Saved, await _session.SaveAsync());

			Assert.Equal(FileFor(Cycles(5)), Stored());
		}

		/// <summary>The entry is only in memory when the save fails, so closing the app mustn't take it along.</summary>
		[Fact]
		public async Task AnEntryThatFailedToSaveSurvivesARestart()
		{
			await OpenWith(Cycles(3));
			Enter(Cycle(3));
			_files.DiskFull = true;
			await Assert.ThrowsAsync<IOException>(() => _session.SaveAsync());
			_files.DiskFull = false;

			var restarted = new DataSession(_store, _vault);
			await restarted.OpenAsync();

			Assert.Equal(FileFor(Cycles(4)), Stored());
			Assert.False(restarted.HasUnsavedChanges);
			Assert.Null(_vault.ReadHeld());
		}

		[Fact]
		public async Task AnEntryRefusedByADamagedFileSurvivesARestart()
		{
			await OpenWith(Cycles(3));
			Enter(Cycle(3));
			var whole = Stored();
			Store(whole[..(whole.Length / 2)]);
			Assert.Equal(SaveStatus.Refused, await _session.SaveAsync());

			var whileDamaged = new DataSession(_store, _vault);
			await whileDamaged.OpenAsync();
			Assert.Equal(SessionState.Unreadable, whileDamaged.State);
			Assert.True(SameInputs(Cycles(4), whileDamaged.Ledger.Inputs()), "the held entry must be shown");

			Store(whole);
			var repaired = new DataSession(_store, _vault);
			await repaired.OpenAsync();

			Assert.Equal(FileFor(Cycles(4)), Stored());
		}

		[Fact]
		public async Task AHeldEntryStaysHeldWhileSavingStillFails()
		{
			await OpenWith(Cycles(3));
			Enter(Cycle(3));
			_files.DiskFull = true;
			await Assert.ThrowsAsync<IOException>(() => _session.SaveAsync());

			var restarted = new DataSession(_store, _vault);
			await restarted.OpenAsync();

			Assert.Equal(SessionState.Ready, restarted.State);
			Assert.True(restarted.HasUnsavedChanges);
			Assert.NotNull(_vault.ReadHeld());
			Assert.True(SameInputs(Cycles(4), restarted.Ledger.Inputs()), "the held entry must be shown");
		}

		/// <summary>A cleanup tool can empty app data while the app runs, vault folder and all</summary>
		[Fact]
		public async Task AnEntryIsHeldEvenAfterTheVaultFolderWasRemoved()
		{
			await OpenWith(Cycles(3));
			Directory.Delete(_folder.VaultFolder, true);
			Enter(Cycle(3));
			_files.DiskFull = true;
			await Assert.ThrowsAsync<IOException>(() => _session.SaveAsync());

			Assert.False(_session.VaultFailed);
			var restarted = new DataSession(_store, _vault);
			await restarted.OpenAsync();
			Assert.True(SameInputs(Cycles(4), restarted.Ledger.Inputs()), "the held entry must be shown");
		}

		[Fact]
		public async Task AHeldEntryMeetsAnotherDevicesEntryWithoutLosingEither()
		{
			await OpenWith(Cycles(3));
			Enter(Cycle(4));
			_files.DiskFull = true;
			await Assert.ThrowsAsync<IOException>(() => _session.SaveAsync());
			_files.DiskFull = false;
			Store(FileFor(Cycles(4)));

			var restarted = new DataSession(_store, _vault);
			await restarted.OpenAsync();

			Assert.Equal(FileFor(Cycles(5)), Stored());
		}

		[Fact]
		public async Task AHeldEntryThatClashesRaisesTheQuestionAtStartup()
		{
			await OpenWith(Cycles(3));
			Enter(Cycle(3));
			_files.DiskFull = true;
			await Assert.ThrowsAsync<IOException>(() => _session.SaveAsync());
			_files.DiskFull = false;
			Store(FileFor([.. Cycles(3), Cycle(3, night: true)]));

			var restarted = new DataSession(_store, _vault);
			await restarted.OpenAsync();

			Assert.Equal(SessionState.NeedsResolution, restarted.State);
			Assert.Equal(SaveStatus.Saved, await restarted.ResolveAsync(_ => MergeSide.Local));
			Assert.Equal(FileFor(Cycles(4)), Stored());
			Assert.Null(_vault.ReadHeld());
		}

		[Fact]
		public async Task AnUnreadableHeldCopyIsReportedAndOpeningStillWorks()
		{
			await OpenWith(Cycles(3));
			_vault.WriteHeld(Encoding.UTF8.GetBytes("[{"));

			var restarted = new DataSession(_store, _vault);
			await restarted.OpenAsync();

			Assert.Equal(SessionState.Ready, restarted.State);
			Assert.True(restarted.VaultFailed);
			Assert.True(SameInputs(Cycles(3), restarted.Ledger.Inputs()), "inputs");
		}

		[Fact]
		public async Task ASuccessfulSaveLeavesNothingHeld()
		{
			await OpenWith(Cycles(3));
			Enter(Cycle(3));
			_files.DiskFull = true;
			await Assert.ThrowsAsync<IOException>(() => _session.SaveAsync());
			Assert.NotNull(_vault.ReadHeld());
			_files.DiskFull = false;

			await _session.SaveAsync();

			Assert.Null(_vault.ReadHeld());
		}

		/// <summary>A held copy left behind would come back at the next opening, so failing to remove it is reported</summary>
		[Fact]
		public async Task AHeldCopyThatCantBeRemovedIsReported()
		{
			var vaultFiles = new InjectedFiles();
			var vault = new LocalVault(_folder.VaultFolder, vaultFiles, TimeProvider.System);
			var session = new DataSession(_store, vault);
			Store(FileFor(Cycles(3)));
			await session.OpenAsync();
			session.Ledger.AcceptPeriod(session.Ledger.AddPeriod(Cycle(3).Begin, Cycle(3).End));
			_files.DiskFull = true;
			await Assert.ThrowsAsync<IOException>(() => session.SaveAsync());
			_files.DiskFull = false;
			vaultFiles.LockedDeletes = true;

			Assert.Equal(SaveStatus.Saved, await session.SaveAsync());

			Assert.True(session.VaultFailed);
			Assert.NotNull(vault.ReadHeld());
		}

		[Fact]
		public async Task NothingIsHeldWhenThereIsNothingToHold()
		{
			Store(Encoding.UTF8.GetBytes("[{"));
			Assert.Equal(SaveStatus.Refused, await _session.SaveAsync());

			Assert.Null(_vault.ReadHeld());
		}

		[Fact]
		public async Task AnEntryThatFailedToSaveIsWrittenByTheNextRefresh()
		{
			await OpenWith(Cycles(3));
			Enter(Cycle(3));
			_files.DiskFull = true;
			await Assert.ThrowsAsync<IOException>(() => _session.SaveAsync());
			_files.DiskFull = false;

			await _session.RefreshAsync();

			Assert.Equal(FileFor(Cycles(4)), Stored());
		}

		[Fact]
		public async Task AFileDamagedDuringTheSaveIsNotOverwrittenAndTheEntryStays()
		{
			await OpenWith(Cycles(3));
			Enter(Cycle(3));
			var damaged = Encoding.UTF8.GetBytes("[{");
			Store(damaged);

			Assert.Equal(SaveStatus.Refused, await _session.SaveAsync());

			Assert.Equal(damaged, Stored());
			Assert.True(SameInputs(Cycles(4), _session.Ledger.Inputs()), "the entry must stay");
		}


		// ---- A failed write must leave the old file whole ----

		[Fact]
		public async Task AFullDiskLeavesTheFileWholeAndTheEntryInMemory()
		{
			await OpenWith(Cycles(3));
			var before = Stored();
			Enter(Cycle(3));
			_files.DiskFull = true;

			await Assert.ThrowsAsync<IOException>(() => _session.SaveAsync());

			Assert.Equal(before, Stored());
			Assert.Empty(_folder.LeftoverTemporaryFiles());
			Assert.Equal(SessionState.Ready, _session.State);
			Assert.True(SameInputs(Cycles(4), _session.Ledger.Inputs()), "the entry must stay");
		}

		[Fact]
		public async Task SavingAgainAfterFreeingSpaceWorks()
		{
			await OpenWith(Cycles(3));
			Enter(Cycle(3));
			_files.DiskFull = true;
			await Assert.ThrowsAsync<IOException>(() => _session.SaveAsync());

			_files.DiskFull = false;

			Assert.Equal(SaveStatus.Saved, await _session.SaveAsync());
			Assert.Equal(FileFor(Cycles(4)), Stored());
		}

		[Fact]
		public async Task AFullDiskIsNotRetried()
		{
			await OpenWith(Cycles(3));
			Enter(Cycle(3));
			_files.DiskFull = true;

			await Assert.ThrowsAsync<IOException>(() => _session.SaveAsync());

			Assert.Equal(1, _files.Writes);
		}

		/// <summary>The new content is complete in its temporary file, so the old file was never touched.</summary>
		[Fact]
		public async Task APowerCutBeforeTheRenameLeavesTheOldFileWhole()
		{
			await OpenWith(Cycles(3));
			var before = Stored();
			Enter(Cycle(3));
			_files.PowerCut = true;

			await Assert.ThrowsAsync<IOException>(() => _session.SaveAsync());
			_files.PowerCut = false;

			Assert.Equal(before, Stored());
			Assert.Single(_folder.LeftoverTemporaryFiles());

			using var store = new FileDataStore(_folder.DataFile);
			var anotherDevice = new DataSession(store, new LocalVault(Path.Combine(_folder.Root, "other")));
			await anotherDevice.OpenAsync();
			Assert.Equal(SessionState.Ready, anotherDevice.State);
			Assert.True(SameInputs(Cycles(3), anotherDevice.Ledger.Inputs()), "inputs");
		}

		[Fact]
		public async Task ALeftoverTemporaryFileIsNotTakenForAConflictedCopy()
		{
			await OpenWith(Cycles(3));
			Enter(Cycle(3));
			_files.PowerCut = true;
			await Assert.ThrowsAsync<IOException>(() => _session.SaveAsync());
			_files.PowerCut = false;

			Assert.Empty(_store.FindConflictedCopies());
		}

		[Fact]
		public async Task AnOldLeftoverIsRemovedByTheNextSave()
		{
			await OpenWith(Cycles(3));
			var leftover = Path.Combine(_folder.Root, "~pureData.json.old.tmp");
			File.WriteAllBytes(leftover, Stored());
			File.SetLastWriteTimeUtc(leftover, DateTime.UtcNow.AddDays(-2));
			Enter(Cycle(3));

			await _session.SaveAsync();

			Assert.False(File.Exists(leftover));
		}

		[Fact]
		public async Task AFreshTemporaryFileIsLeftAlone()
		{
			await OpenWith(Cycles(3));
			var inProgress = Path.Combine(_folder.Root, "~pureData.json.other.tmp");
			File.WriteAllBytes(inProgress, Stored());
			Enter(Cycle(3));

			await _session.SaveAsync();

			Assert.True(File.Exists(inProgress));
		}

		[Fact]
		public async Task ALeftoverOfAnotherFileIsLeftAlone()
		{
			await OpenWith(Cycles(3));
			var other = Path.Combine(_folder.Root, "~pureSettings.json.old.tmp");
			File.WriteAllBytes(other, [1]);
			File.SetLastWriteTimeUtc(other, DateTime.UtcNow.AddDays(-2));
			Enter(Cycle(3));

			await _session.SaveAsync();

			Assert.True(File.Exists(other));
		}

		[Fact]
		public async Task ALeftoverThatCantBeRemovedDoesNotFailTheSave()
		{
			await OpenWith(Cycles(3));
			var leftover = Path.Combine(_folder.Root, "~pureData.json.old.tmp");
			File.WriteAllBytes(leftover, Stored());
			File.SetLastWriteTimeUtc(leftover, DateTime.UtcNow.AddDays(-2));
			Enter(Cycle(3));
			_files.LockedDeletes = true;

			Assert.Equal(SaveStatus.Saved, await _session.SaveAsync());

			Assert.Equal(FileFor(Cycles(4)), Stored());
		}

		/// <summary>The real rename on this platform: anyone reading during a save gets a whole file.</summary>
		[Fact]
		public async Task AReaderNeverSeesAHalfWrittenFile()
		{
			var versions = new[] { FileFor(Cycles(60)), FileFor(Cycles(90)) };
			using var writer = new FileDataStore(_folder.DataFile);
			using var reader = new FileDataStore(_folder.DataFile);
			var version = (await writer.SaveAsync(versions[0], null)).Version;

			using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(2));
			var (reads, writes) = (0, 0);
			var reading = Task.Run(async () =>
			{
				while (!stop.IsCancellationRequested)
				{
					StoredData? seen;
					try
					{
						seen = await reader.LoadAsync();
					}
					catch (Exception e) when (IsLocked(e))
					{
						continue;
					}

					Assert.NotNull(seen);
					Assert.True(versions.Any(el => el.AsSpan().SequenceEqual(seen.Content)), "a read saw a partial file");
					reads++;
				}
			});

			for (var round = 1; !stop.IsCancellationRequested; round++)
			{
				try
				{
					version = (await writer.SaveAsync(versions[round % 2], version)).Version;
					writes++;
				}
				catch (Exception e) when (IsLocked(e))
				{
					// this reader never lets go, so a save may run out of retries: allowed, and nothing changed
				}
			}

			await reading;
			Assert.True(reads > 0, "nothing was read");
			Assert.True(writes > 0, "nothing was written");
		}

		private static bool IsLocked(Exception e) => e is IOException or UnauthorizedAccessException;

		[Fact]
		public async Task AFileBrieflyLockedBySyncIsSavedAfterRetrying()
		{
			await OpenWith(Cycles(3));
			Enter(Cycle(3));
			_files.LockedMoves = 3;

			Assert.Equal(SaveStatus.Saved, await _session.SaveAsync());

			Assert.Equal(FileFor(Cycles(4)), Stored());
			Assert.Equal(3, _files.Pauses);
		}

		[Fact]
		public async Task AFileLockedForGoodFailsAndIsLeftWhole()
		{
			await OpenWith(Cycles(3));
			var before = Stored();
			Enter(Cycle(3));
			_files.LockedMoves = int.MaxValue;

			await Assert.ThrowsAsync<IOException>(() => _session.SaveAsync());

			Assert.Equal(10, _files.Moves);
			Assert.Equal(before, Stored());
			Assert.Empty(_folder.LeftoverTemporaryFiles());
		}

		[Fact]
		public async Task AFileBrieflyLockedForReadingStillLoads()
		{
			Store(FileFor(Cycles(3)));
			_files.LockedReads = 2;

			await _session.OpenAsync();

			Assert.Equal(SessionState.Ready, _session.State);
			Assert.Equal(2, _files.Pauses);
		}

		[Fact]
		public async Task AFileThatStaysLockedIsUnreadableNotEmpty()
		{
			Store(FileFor(Cycles(3)));
			_files.LockedReads = int.MaxValue;

			await _session.OpenAsync();

			Assert.Equal(SessionState.Unreadable, _session.State);
			Assert.Equal(SaveStatus.Refused, await _session.SaveAsync());
		}
	}
}
