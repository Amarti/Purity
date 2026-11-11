using System.Text;
using Purity.Core;


namespace Purity.Storage.Tests
{
	/// <summary>A private folder per test. Tests never go near the real data file.</summary>
	internal sealed class TestFolder : IDisposable
	{
		public TestFolder()
		{
			Directory.CreateDirectory(Root);
			DataFile = Path.Combine(Root, "pureData.json");
			VaultFolder = Path.Combine(Root, "vault");
		}

		public void Dispose()
		{
			try
			{
				Directory.Delete(Root, true);
			}
			catch (IOException)
			{
				// a watcher may still hold the folder for a moment, and the system cleans temp anyway
			}
		}


		public string[] LeftoverTemporaryFiles() => Directory.GetFiles(Root, "*.tmp");

		public byte[] ReadData() => Patiently(() => File.ReadAllBytes(DataFile));

		public Task<byte[]> ReadDataAsync() => Task.Run(ReadData);

		public void WriteData(byte[] content) => Patiently(() =>
		{
			File.WriteAllBytes(DataFile, content);
			return true;
		});

		public Task WriteDataAsync(byte[] content) => Task.Run(() => WriteData(content));

		public void WriteText(string content) => WriteData(Encoding.UTF8.GetBytes(content));


		/// <summary>
		/// A virus scanner can grab a file right after a save. The app waits that out, so a test
		/// touching the data file directly has to as well, or it fails for no reason.
		/// </summary>
		private static T Patiently<T>(Func<T> action) => AtomicFile.Retrying(new FileOperations(), action);


		public string Root { get; } = Path.Combine(Path.GetTempPath(), $"purity-storage-{Guid.NewGuid():N}");
		public string DataFile { get; }
		public string VaultFolder { get; }
	}


	/// <summary>Synthetic histories, far from any real date</summary>
	internal static class History
	{
		public static PeriodRecord Cycle(int number, int length = 5, bool night = false)
		{
			var begin = Start.AddDays(30 * number).AddHours(night ? 12 : 0);
			return new PeriodRecord(begin, begin.AddDays(length), true, false);
		}

		public static PeriodRecord[] Cycles(int count) => Enumerable.Range(0, count).Select(el => Cycle(el)).ToArray();

		/// <summary>The file the engine writes for these inputs, events included</summary>
		public static byte[] FileFor(params PeriodRecord[] records)
		{
			var ledger = new PeriodLedger();
			ledger.Load(records.Select(el => el.ToPeriod()));
			ledger.Recalculate();
			return Encoding.UTF8.GetBytes(PurityFile.Format(ledger.Data));
		}

		public static bool SameInputs(IEnumerable<PeriodRecord> a, IEnumerable<PeriodRecord> b)
		{
			var (x, y) = (a.ToList(), b.ToList());
			return x.Count == y.Count && x.Zip(y).All(el => el.First.SameAs(el.Second));
		}

		public static IEnumerable<PeriodRecord> Inputs(this PeriodLedger ledger) => ledger.Data.Select(PeriodRecord.From);


		private static readonly DateTimeOffset Start = new(2033, 1, 1, 0, 0, 0, TimeSpan.Zero);
	}


	/// <summary>Real file operations, except where a test makes one fail</summary>
	internal class InjectedFiles : FileOperations
	{
		public override void WriteDurably(string path, ReadOnlyMemory<byte> content, UnixFileMode? mode)
		{
			Writes++;
			if (DiskFull)
			{
				base.WriteDurably(path, content[..(content.Length / 2)], mode);
				throw new IOException("There is not enough space on the disk");
			}

			base.WriteDurably(path, content, mode);
			AfterWrite?.Invoke();
		}

		public override void Move(string source, string target)
		{
			Moves++;
			if (PowerCut)
				throw Injected("Power lost");
			if (LockedMoves > 0)
			{
				LockedMoves--;
				AfterLockedMove?.Invoke();
				throw Injected(FileInUse);
			}

			base.Move(source, target);
			AfterMove?.Invoke();
		}

		public override void Delete(string path)
		{
			if (PowerCut || LockedDeletes)
				throw new IOException("Power lost");

			base.Delete(path);
		}

		public override byte[]? Read(string path)
		{
			if (TakeLockedRead())
				throw Injected(FileInUse);

			return base.Read(path);
		}

		/// <summary>Only a failure faked here can skip the wait. A real lock needs the time it gets in the app.</summary>
		public override void Pause(TimeSpan time)
		{
			Pauses++;
			if (!_injected)
				base.Pause(time);

			_injected = false;
		}


		private static IOException Injected(string message)
		{
			_injected = true;
			return new IOException(message);
		}

		/// <summary>Atomic, because a test can lift the lock while a watcher's timer is counting it down</summary>
		private bool TakeLockedRead()
		{
			lock (_readLocks)
			{
				if (_lockedReads == 0)
					return false;

				_lockedReads--;
				return true;
			}
		}


		public bool DiskFull { get; set; }
		public bool PowerCut { get; set; }
		public bool LockedDeletes { get; set; }
		public int LockedMoves { get; set; }
		public int LockedReads
		{
			get
			{
				lock (_readLocks)
					return _lockedReads;
			}
			set
			{
				lock (_readLocks)
					_lockedReads = value;
			}
		}
		public Action? AfterWrite { get; set; }
		public Action? AfterLockedMove { get; set; }
		/// <summary>Runs once a move has gone through</summary>
		public Action? AfterMove { get; set; }
		public int Writes { get; private set; }
		public int Moves { get; private set; }
		public int Pauses { get; private set; }


		private const string FileInUse = "The process cannot access the file because it is being used by another process";

		[ThreadStatic]
		private static bool _injected;

		private readonly object _readLocks = new();
		private int _lockedReads;
	}


	/// <summary>A store that plays back whatever a test sets up, for cases the disk can't produce on demand</summary>
	internal sealed class ScriptedStore : IDataStore
	{
		public async Task<StoredData?> LoadAsync(CancellationToken cancellation = default)
		{
			if (HoldLoad != null)
				await HoldLoad.Task;
			if (LoadFailure != null)
				throw LoadFailure;

			return Stored;
		}

		public async Task<SaveResult> SaveAsync(ReadOnlyMemory<byte> content, string? expectedVersion, CancellationToken cancellation = default)
		{
			if (HoldSave != null)
				await HoldSave.Task;

			Saves++;
			if (AlwaysConflict)
			{
				Stored = new StoredData(Stored!.Content, $"{Stored.Version}+");
				return SaveResult.Conflict;
			}

			Stored = new StoredData(content.ToArray(), $"saved {Saves}");
			return new SaveResult(Stored.Version);
		}

		public void Dispose()
		{}

		public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);


		public StoredData? Stored { get; set; }
		public Exception? LoadFailure { get; set; }
		public bool AlwaysConflict { get; set; }
		public TaskCompletionSource? HoldLoad { get; set; }
		public TaskCompletionSource? HoldSave { get; set; }
		public int Saves { get; private set; }

		public event EventHandler? Changed;
	}
}
