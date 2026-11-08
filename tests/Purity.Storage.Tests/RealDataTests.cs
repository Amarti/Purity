using Xunit;
using static Purity.Storage.Tests.History;


namespace Purity.Storage.Tests
{
	/// <summary>
	/// The storage layer on the real history, in a temporary copy. Actions logs on a public repo are
	/// public, so these tests only ever print counts: never compare periods or file content with
	/// Assert.Equal here, because a failure would print the values.
	/// </summary>
	public class RealDataTests : IDisposable
	{
		private readonly TestFolder _folder = new();


		public void Dispose() => _folder.Dispose();


		private static string RealFile
		{
			get
			{
				var configured = Environment.GetEnvironmentVariable("PURITY_PRIVATE_DATA");
				var folder = configured is { Length: > 0 } ? configured : Path.Combine(RepoRoot(), "tests", "private");
				return Path.Combine(folder, "pureData.json");
			}
		}

		private static string RepoRoot()
		{
			var folder = new DirectoryInfo(AppContext.BaseDirectory);
			while (folder != null && !File.Exists(Path.Combine(folder.FullName, "Purity.sln")))
				folder = folder.Parent;
			return folder?.FullName ?? throw new InvalidOperationException("Repository root not found");
		}

		private async Task<(DataSession Session, FileDataStore Store, byte[] Original)> OpenRealCopy()
		{
			if (!File.Exists(RealFile))
			{
				if (Environment.GetEnvironmentVariable("PURITY_REQUIRE_REAL_DATA") == "1")
					Assert.Fail("Real data is required for this run but wasn't found");
				Assert.Skip("Real data isn't available here");
			}

			File.Copy(RealFile, _folder.DataFile);
			var store = new FileDataStore(_folder.DataFile);
			var session = new DataSession(store, new LocalVault(_folder.VaultFolder));
			await session.OpenAsync();

			Assert.True(session.State == SessionState.Ready, "the real file didn't open");
			Assert.NotEmpty(session.Ledger.Data);
			return (session, store, _folder.ReadData());
		}

		private bool FileIs(byte[] expected) => _folder.ReadData().AsSpan().SequenceEqual(expected);

		private static PeriodRecord FarFuture(DataSession session, int cycles)
		{
			var begin = session.Ledger.Data[^1].Begin.AddDays(30 * cycles);
			return new PeriodRecord(begin, begin.AddDays(5), true, false);
		}


		[Fact]
		public async Task OpeningAndSavingLeavesTheFileUntouched()
		{
			var (session, store, original) = await OpenRealCopy();
			using var _ = store;

			Assert.True(await session.SaveAsync() == SaveStatus.Unchanged, "an unchanged history was written");
			Assert.True(FileIs(original), "the file changed");
		}

		[Fact]
		public async Task RecalculatingEverythingChangesNothingToSave()
		{
			var (session, store, original) = await OpenRealCopy();
			using var _ = store;

			session.Ledger.Recalculate();

			Assert.True(await session.SaveAsync() == SaveStatus.Unchanged, "recalculation changed the history");
			Assert.True(FileIs(original), "the file changed");
		}

		[Fact]
		public async Task TheRealHistoryMergesWithItself()
		{
			var (session, store, _) = await OpenRealCopy();
			using var __ = store;
			var history = session.Ledger.Inputs().ToList();

			var result = HistoryMerge.Merge(history, history, history);

			Assert.Empty(result.Conflicts.Select(el => el.Local.Count));
			Assert.True(SameInputs(history, result.Merged), "merging changed the history");
		}

		[Fact]
		public async Task EveryRealPeriodIsItsOwnCycle()
		{
			var (session, store, _) = await OpenRealCopy();
			using var __ = store;
			var history = session.Ledger.Inputs().ToList();

			for (var i = 1; i < history.Count; i++)
				Assert.True((history[i].Begin - history[i - 1].Begin).TotalDays >= HistoryMerge.SameCycleDays, $"period {i} shares a cycle with the one before it");
		}

		[Fact]
		public async Task EntriesFromTwoDevicesAddToTheRealHistoryWithoutTouchingIt()
		{
			var (session, store, _) = await OpenRealCopy();
			using var __ = store;
			var history = session.Ledger.Inputs().ToList();
			var (mine, theirs) = (FarFuture(session, 2), FarFuture(session, 1));
			var remote = history.Append(theirs).ToArray();

			session.Ledger.AcceptPeriod(session.Ledger.AddPeriod(mine.Begin, mine.End));
			_folder.WriteData(FileFor(remote));

			Assert.True(await session.SaveAsync() == SaveStatus.Saved, "the merge didn't save");

			var merged = session.Ledger.Inputs().ToList();
			Assert.Equal(history.Count + 2, merged.Count);
			Assert.True(SameInputs(history, merged.Take(history.Count)), "an existing period changed");
			Assert.True(FileIs(FileFor(merged.ToArray())), "the file isn't the engine's output for the merged history");
		}

		[Fact]
		public async Task AWipedRealHistoryCanBeBroughtBack()
		{
			var (_, store, original) = await OpenRealCopy();
			using var __ = store;
			_folder.WriteData(FileFor());

			var restarted = new DataSession(store, new LocalVault(_folder.VaultFolder));
			await restarted.OpenAsync();

			Assert.True(restarted.State == SessionState.NeedsResolution, "the wipe went unnoticed");

			await restarted.ResolveAsync(_ => MergeSide.Local);

			var recovered = _folder.ReadData();
			Assert.True(recovered.Length > original.Length / 2, "the history didn't come back");
			Assert.True(SameInputs(
				Core.PurityFile.Parse(System.Text.Encoding.UTF8.GetString(original)).Select(PeriodRecord.From),
				Core.PurityFile.Parse(System.Text.Encoding.UTF8.GetString(recovered)).Select(PeriodRecord.From)), "the recovered inputs differ");
		}
	}
}
