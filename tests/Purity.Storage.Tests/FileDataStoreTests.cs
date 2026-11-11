using System.Runtime.Versioning;
using System.Text;
using Xunit;


namespace Purity.Storage.Tests
{
	public class FileDataStoreTests : IDisposable
	{
		private static readonly byte[] First = Encoding.UTF8.GetBytes("[first]");
		private static readonly byte[] Second = Encoding.UTF8.GetBytes("[second]");

		private readonly TestFolder _folder = new();
		private readonly FileDataStore _store;


		public FileDataStoreTests()
		{
			_store = new FileDataStore(_folder.DataFile);
		}

		public void Dispose()
		{
			_store.Dispose();
			_folder.Dispose();
		}


		[Fact]
		public async Task AMissingFileLoadsAsNothing()
		{
			Assert.Null(await _store.LoadAsync());
		}

		[Fact]
		public async Task AMissingFolderLoadsAsNothing()
		{
			using var store = new FileDataStore(Path.Combine(_folder.Root, "nowhere", "pureData.json"));

			Assert.Null(await store.LoadAsync());
		}

		[Fact]
		public async Task SavingCreatesTheFile()
		{
			var result = await _store.SaveAsync(First, null);

			Assert.False(result.IsConflict);
			Assert.Equal(First, await _folder.ReadDataAsync());
		}

		[Fact]
		public async Task LoadReturnsTheBytesAndTheVersionTheSaveReported()
		{
			var result = await _store.SaveAsync(First, null);

			var loaded = await _store.LoadAsync();

			Assert.Equal(First, loaded!.Content);
			Assert.Equal(result.Version, loaded.Version);
		}

		[Fact]
		public async Task DifferentContentHasADifferentVersion()
		{
			var first = await _store.SaveAsync(First, null);
			var second = await _store.SaveAsync(Second, first.Version);

			Assert.NotEqual(first.Version, second.Version);
		}

		[Fact]
		public async Task SavingOverTheExpectedVersionReplacesTheFile()
		{
			var first = await _store.SaveAsync(First, null);

			var second = await _store.SaveAsync(Second, first.Version);

			Assert.False(second.IsConflict);
			Assert.Equal(Second, await _folder.ReadDataAsync());
		}

		[Fact]
		public async Task SavingOverANewerVersionChangesNothing()
		{
			var first = await _store.SaveAsync(First, null);
			await _folder.WriteDataAsync(Second);

			var result = await _store.SaveAsync(First, first.Version);

			Assert.True(result.IsConflict);
			Assert.Equal(Second, await _folder.ReadDataAsync());
		}

		[Fact]
		public async Task CreatingAFileThatAppearedMeanwhileChangesNothing()
		{
			await _folder.WriteDataAsync(Second);

			var result = await _store.SaveAsync(First, null);

			Assert.True(result.IsConflict);
			Assert.Equal(Second, await _folder.ReadDataAsync());
		}

		[Fact]
		public async Task SavingOverAFileThatVanishedChangesNothing()
		{
			var first = await _store.SaveAsync(First, null);
			File.Delete(_folder.DataFile);

			var result = await _store.SaveAsync(Second, first.Version);

			Assert.True(result.IsConflict);
			Assert.False(File.Exists(_folder.DataFile));
		}

		[Fact]
		public async Task NoTemporaryFileStaysBehind()
		{
			var first = await _store.SaveAsync(First, null);
			await _store.SaveAsync(Second, first.Version);
			await _folder.WriteDataAsync(First);
			await _store.SaveAsync(Second, "stale");

			Assert.Empty(_folder.LeftoverTemporaryFiles());
		}

		[Fact]
		public async Task ACancelledSaveChangesNothing()
		{
			var first = await _store.SaveAsync(First, null);
			using var cancelled = new CancellationTokenSource();
			await cancelled.CancelAsync();

			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _store.SaveAsync(Second, first.Version, cancelled.Token));

			Assert.Equal(First, await _folder.ReadDataAsync());
		}

		[Fact]
		public async Task CancellingDuringTheSaveChangesNothing()
		{
			using var cancelled = new CancellationTokenSource();
			var files = new InjectedFiles();
			using var store = new FileDataStore(_folder.DataFile, files, TimeSpan.Zero);
			var first = await store.SaveAsync(First, null);
			files.AfterWrite = cancelled.Cancel;

			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(Second, first.Version, cancelled.Token));

			Assert.Equal(First, await _folder.ReadDataAsync());
			Assert.Empty(_folder.LeftoverTemporaryFiles());
		}

		[Fact]
		public async Task AFailedCleanupDoesNotChangeTheOutcome()
		{
			var files = new InjectedFiles { LockedDeletes = true };
			using var store = new FileDataStore(_folder.DataFile, files, TimeSpan.Zero);

			var result = await store.SaveAsync(First, null);

			Assert.False(result.IsConflict);
			Assert.Equal(First, await _folder.ReadDataAsync());
		}

		/// <summary>A sync client or an editor may hold the file open. On Windows that takes the right sharing.</summary>
		[Fact]
		public async Task AFileAnotherProgramHasOpenStillLoads()
		{
			await _store.SaveAsync(First, null);
			using var other = new FileStream(_folder.DataFile, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

			var loaded = await _store.LoadAsync();

			Assert.Equal(First, loaded!.Content);
		}

		[Fact]
		[UnsupportedOSPlatform("windows")]
		public async Task ANewFileIsPrivateToItsOwner()
		{
			if (OperatingSystem.IsWindows())
				Assert.Skip("Windows has no file modes");

			await _store.SaveAsync(First, null);

			Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(_folder.DataFile));
		}

		[Fact]
		[UnsupportedOSPlatform("windows")]
		public async Task ReplacingAFileKeepsItsPermissions()
		{
			if (OperatingSystem.IsWindows())
				Assert.Skip("Windows has no file modes");

			var first = await _store.SaveAsync(First, null);
			const UnixFileMode shared = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead;
			File.SetUnixFileMode(_folder.DataFile, shared);

			await _store.SaveAsync(Second, first.Version);

			Assert.Equal(shared, File.GetUnixFileMode(_folder.DataFile));
		}

		[Fact]
		public async Task AnOutsideChangeRaisesChanged()
		{
			using var store = new FileDataStore(_folder.DataFile, new FileOperations(), TimeSpan.FromMilliseconds(50));
			var changed = new TaskCompletionSource();
			store.Changed += (_, _) => changed.TrySetResult();
			await store.SaveAsync(First, null);

			await _folder.WriteDataAsync(Second);

			await changed.Task.WaitAsync(TimeSpan.FromSeconds(30));
		}

		[Fact]
		public async Task AnOutsideDeletionRaisesChanged()
		{
			using var store = new FileDataStore(_folder.DataFile, new FileOperations(), TimeSpan.FromMilliseconds(50));
			await store.SaveAsync(First, null);
			var changed = new TaskCompletionSource();
			store.Changed += (_, _) => changed.TrySetResult();

			File.Delete(_folder.DataFile);

			await changed.Task.WaitAsync(TimeSpan.FromSeconds(30));
		}

		/// <summary>The way sync clients deliver a file: written elsewhere, then renamed into place.</summary>
		[Fact]
		public async Task AFileRenamedIntoPlaceRaisesChanged()
		{
			using var store = new FileDataStore(_folder.DataFile, new FileOperations(), TimeSpan.FromMilliseconds(50));
			await store.SaveAsync(First, null);
			var changed = new TaskCompletionSource();
			store.Changed += (_, _) => changed.TrySetResult();
			var delivered = Path.Combine(_folder.Root, "delivered");
			await File.WriteAllBytesAsync(delivered, Second);

			File.Move(delivered, _folder.DataFile, overwrite: true);

			await changed.Task.WaitAsync(TimeSpan.FromSeconds(30));
		}

		/// <summary>Sync clients often keep downloads in a cache folder and move them in. Linux reports that as a new file, not a rename.</summary>
		[Fact]
		public async Task AFileMovedInFromAnotherFolderRaisesChanged()
		{
			using var store = new FileDataStore(_folder.DataFile, new FileOperations(), TimeSpan.FromMilliseconds(50));
			await store.SaveAsync(First, null);
			var changed = new TaskCompletionSource();
			store.Changed += (_, _) => changed.TrySetResult();
			var cache = Directory.CreateDirectory(Path.Combine(_folder.Root, ".cache")).FullName;
			var delivered = Path.Combine(cache, "delivered");
			await File.WriteAllBytesAsync(delivered, Second);

			File.Move(delivered, _folder.DataFile, overwrite: true);

			await changed.Task.WaitAsync(TimeSpan.FromSeconds(30));
		}

		[Fact]
		public async Task AnUnsubscribedHandlerHearsNothing()
		{
			using var store = new FileDataStore(_folder.DataFile, new FileOperations(), TimeSpan.FromMilliseconds(20));
			var raised = 0;
			EventHandler handler = (_, _) => Interlocked.Increment(ref raised);
			store.Changed += handler;
			store.Changed -= handler;
			await store.SaveAsync(First, null);

			await _folder.WriteDataAsync(Second);
			await Task.Delay(TimeSpan.FromSeconds(1));

			Assert.Equal(0, raised);
		}

		[Fact]
		public async Task ALockDuringTheCheckDelaysChangedUntilItEnds()
		{
			var files = new InjectedFiles();
			using var store = new FileDataStore(_folder.DataFile, files, TimeSpan.FromMilliseconds(20));
			await store.SaveAsync(First, null);
			var changed = new TaskCompletionSource();
			store.Changed += (_, _) => changed.TrySetResult();
			files.LockedReads = int.MaxValue;

			await _folder.WriteDataAsync(Second);
			await Task.Delay(TimeSpan.FromMilliseconds(300));
			Assert.False(changed.Task.IsCompleted);

			files.LockedReads = 0;

			await changed.Task.WaitAsync(TimeSpan.FromSeconds(30));
		}

		/// <summary>The first load may hit a lock, and no further file event may ever come.</summary>
		[Fact]
		public async Task ChangedRepeatsUntilTheChangeIsLoaded()
		{
			using var store = new FileDataStore(_folder.DataFile, new FileOperations(), TimeSpan.FromMilliseconds(20));
			var raised = 0;
			var repeated = new TaskCompletionSource();
			store.Changed += (_, _) =>
			{
				if (Interlocked.Increment(ref raised) == 3)
					repeated.TrySetResult();
			};
			await store.SaveAsync(First, null);

			await _folder.WriteDataAsync(Second);
			await repeated.Task.WaitAsync(TimeSpan.FromSeconds(30));

			await store.LoadAsync();
			await Task.Delay(TimeSpan.FromMilliseconds(500));
			var settled = Volatile.Read(ref raised);
			await Task.Delay(TimeSpan.FromMilliseconds(500));

			Assert.Equal(settled, Volatile.Read(ref raised));
		}

		/// <summary>While nobody loads a change it keeps being reported, but now and then, not in a tight loop</summary>
		[Fact]
		public async Task AnUnloadedChangeIsRepeatedOnlyNowAndThen()
		{
			using var store = new FileDataStore(_folder.DataFile, new FileOperations(), TimeSpan.FromMilliseconds(20));
			var raised = 0;
			var first = new TaskCompletionSource();
			store.Changed += (_, _) =>
			{
				Interlocked.Increment(ref raised);
				first.TrySetResult();
			};
			await store.SaveAsync(First, null);

			await _folder.WriteDataAsync(Second);
			await first.Task.WaitAsync(TimeSpan.FromSeconds(30));
			await Task.Delay(TimeSpan.FromMilliseconds(300));

			// one repeat is due in this time; the rest allows for late file events
			var count = Volatile.Read(ref raised);
			Assert.True(count <= 4, $"reported {count} times in 300 ms");
		}

		[Fact]
		public async Task DisposingWhileAChangeIsSettlingIsSafe()
		{
			var store = new FileDataStore(_folder.DataFile, new FileOperations(), TimeSpan.FromMilliseconds(1));
			store.Changed += (_, _) => { };
			await store.SaveAsync(First, null);

			await _folder.WriteDataAsync(Second);
			store.Dispose();

			await Task.Delay(TimeSpan.FromMilliseconds(200));
		}

		[Fact]
		public async Task OurOwnSaveDoesNotRaiseChanged()
		{
			using var store = new FileDataStore(_folder.DataFile, new FileOperations(), TimeSpan.FromMilliseconds(50));
			var raised = 0;
			store.Changed += (_, _) => Interlocked.Increment(ref raised);

			var first = await store.SaveAsync(First, null);
			await store.SaveAsync(Second, first.Version);
			await Task.Delay(TimeSpan.FromSeconds(1));

			Assert.Equal(0, raised);
		}

		/// <summary>
		/// A busy machine can stall a save between putting the file in place and returning, longer
		/// than the watcher waits. CI did once, on Linux with tests running side by side.
		/// </summary>
		[Fact]
		public async Task OurOwnSaveDoesNotRaiseChangedEvenWhenItIsSlowToFinish()
		{
			var files = new InjectedFiles { AfterMove = () => Thread.Sleep(400) };
			using var store = new FileDataStore(_folder.DataFile, files, TimeSpan.FromMilliseconds(50));
			var raised = 0;
			store.Changed += (_, _) => Interlocked.Increment(ref raised);

			var first = await store.SaveAsync(First, null);
			await store.SaveAsync(Second, first.Version);
			await Task.Delay(TimeSpan.FromSeconds(1));

			Assert.Equal(2, files.Moves);
			Assert.Equal(0, raised);
		}

		[Fact]
		public void WatchingAMissingFolderDoesNothing()
		{
			using var store = new FileDataStore(Path.Combine(_folder.Root, "nowhere", "pureData.json"));

			store.Changed += (_, _) => { };
		}

		[Theory]
		[InlineData("pureData (Someone's conflicted copy 2033-01-02).json")]
		[InlineData("pureData (Case Conflict).json")]
		[InlineData("pureData.sync-conflict-20330102-120000-ABCDEFG.json")]
		[InlineData("pureData (1).json")]
		[InlineData("pureData 2.json")]
		public void FindsCopiesLeftBySyncClients(string name)
		{
			File.WriteAllBytes(Path.Combine(_folder.Root, name), First);

			var found = Assert.Single(_store.FindConflictedCopies());

			Assert.Equal(name, Path.GetFileName(found));
		}

		[Theory]
		[InlineData("pureData.json")]
		[InlineData("pureData.1.json")]
		[InlineData("pureData.json.bck")]
		[InlineData("pureDataOld.json")]
		[InlineData("pureSettings.json")]
		[InlineData("other (conflicted copy).json")]
		public void IgnoresOtherFiles(string name)
		{
			File.WriteAllBytes(Path.Combine(_folder.Root, name), First);

			Assert.Empty(_store.FindConflictedCopies());
		}

		[Fact]
		public void ConflictedCopiesComeInAStableOrder()
		{
			File.WriteAllBytes(Path.Combine(_folder.Root, "pureData (2).json"), First);
			File.WriteAllBytes(Path.Combine(_folder.Root, "pureData (1).json"), First);

			Assert.Equal(["pureData (1).json", "pureData (2).json"], _store.FindConflictedCopies().Select(Path.GetFileName));
		}
	}
}
