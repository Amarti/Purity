using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;


namespace Purity.Storage
{
	/// <summary>
	/// The data file in a local or synced folder. The version is a hash of the content.
	/// <para/>Plain files have no compare-and-replace, so a change landing between the last check
	/// and the rename goes unnoticed. That window is microseconds wide. Only an API store with
	/// real revisions can close it.
	/// </summary>
	public sealed class FileDataStore : IDataStore
	{
		public FileDataStore(string path)
			: this(path, new FileOperations(), TimeSpan.FromMilliseconds(500))
		{}
		internal FileDataStore(string path, FileOperations files, TimeSpan settleTime)
		{
			_path = Path.GetFullPath(path);
			_folder = Path.GetDirectoryName(_path)!;
			_files = files;
			_settleTime = settleTime;
		}


		public Task<StoredData?> LoadAsync(CancellationToken cancellation = default) => Task.Run(Load, cancellation);

		public Task<SaveResult> SaveAsync(ReadOnlyMemory<byte> content, string? expectedVersion, CancellationToken cancellation = default) =>
			Task.Run(() => Save(content, expectedVersion, cancellation), cancellation);

		/// <summary>
		/// Finds the copies a sync client left beside the data file when two devices clashed.
		/// Their entries are missing from the data file until someone merges them.
		/// </summary>
		public IReadOnlyList<string> FindConflictedCopies()
		{
			var name = Path.GetFileNameWithoutExtension(_path);
			var extension = Path.GetExtension(_path);
			var pattern = $"^{Regex.Escape(name)}( \\(\\d+\\)| \\d+){Regex.Escape(extension)}$";
			var numbered = new Regex(pattern, RegexOptions.CultureInvariant);

			return Directory.EnumerateFiles(_folder, $"{name}*{extension}")
				.Where(el => IsConflictedCopy(Path.GetFileName(el), numbered))
				.Order(StringComparer.Ordinal)
				.ToList();
		}

		public void Dispose()
		{
			_watcher?.Dispose();
			_settle?.Dispose();
		}


		private static bool IsConflictedCopy(string name, Regex numbered) =>
			name.Contains("conflict", StringComparison.OrdinalIgnoreCase) || numbered.IsMatch(name);

		private static string VersionOf(ReadOnlySpan<byte> content) => Convert.ToHexString(SHA256.HashData(content));

		private StoredData? Load()
		{
			var content = AtomicFile.Retrying(_files, () => _files.Read(_path));
			var stored = content == null ? null : new StoredData(content, VersionOf(content));
			_lastSeen = stored?.Version;
			return stored;
		}

		private SaveResult Save(ReadOnlyMemory<byte> content, string? expectedVersion, CancellationToken cancellation)
		{
			if (CurrentVersion() != expectedVersion)
				return SaveResult.Conflict;

			// Known before the file is in place: its file event may be checked before Replace returns,
			// which on a busy machine can take longer than the watcher waits
			var version = VersionOf(content.Span);
			_saving = version;
			try
			{
				if (!AtomicFile.Replace(_files, _path, content, () => !cancellation.IsCancellationRequested && VersionNow() == expectedVersion))
				{
					cancellation.ThrowIfCancellationRequested();
					return SaveResult.Conflict;
				}

				_lastSeen = version;			// before the save stops counting as in progress, so no check falls between
				return new SaveResult(version);
			}
			finally
			{
				_saving = null;
			}
		}

		private string? CurrentVersion() => AtomicFile.Retrying(_files, VersionNow);

		private string? VersionNow()
		{
			var content = _files.Read(_path);
			return content == null ? null : VersionOf(content);
		}

		private void StartWatching()
		{
			if (_watcher != null || !Directory.Exists(_folder))
				return;

			_settle = new Timer(_ => ReportChange());
			_watcher = new FileSystemWatcher(_folder, Path.GetFileName(_path))
			{
				NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
			};
			_watcher.Changed += (_, _) => Settle();
			_watcher.Created += (_, _) => Settle();
			_watcher.Deleted += (_, _) => Settle();
			_watcher.Renamed += (_, _) => Settle();
			_watcher.Error += (_, _) => Settle();
			_watcher.EnableRaisingEvents = true;
		}

		/// <summary>A sync client touches a file several times in a row, so wait for it to go quiet</summary>
		private void Settle() => CheckAfter(_settleTime);

		private void CheckAfter(TimeSpan time)
		{
			try
			{
				_settle?.Change(time, Timeout.InfiniteTimeSpan);
			}
			catch (ObjectDisposedException)
			{
				// a late file event, after this store was disposed
			}
		}

		/// <summary>
		/// Keeps reporting until someone manages to load the change. If that load hits a lock,
		/// no further file event may ever come, and the session would stay stuck.
		/// </summary>
		private void ReportChange()
		{
			try
			{
				// The save in progress first: it records what it wrote as seen before it stops
				// counting as in progress, so this order never misses a save that just finished
				var now = CurrentVersion();
				var saving = _saving;
				if ((saving != null && now == saving) || now == _lastSeen)
					return;
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				Settle();
				return;
			}

			_changed?.Invoke(this, EventArgs.Empty);
			CheckAfter(_settleTime * RecheckFactor);
		}


		public event EventHandler? Changed
		{
			add
			{
				_changed += value;
				StartWatching();
			}
			remove => _changed -= value;
		}

		private const int RecheckFactor = 10;

		private readonly string _path;
		private readonly string _folder;
		private readonly FileOperations _files;
		private readonly TimeSpan _settleTime;

		private EventHandler? _changed;
		private FileSystemWatcher? _watcher;
		private Timer? _settle;
		private volatile string? _lastSeen;
		/// <summary>The version a save is putting in place, until it's recorded as seen</summary>
		private volatile string? _saving;
	}
}
