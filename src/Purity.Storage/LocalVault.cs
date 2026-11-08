using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;


namespace Purity.Storage
{
	/// <param name="Path">Where the backup is kept</param>
	/// <param name="TakenAt">When the content was replaced</param>
	public sealed record Backup(string Path, DateTimeOffset TakenAt);


	/// <summary>
	/// This device's private copies, kept in its app data folder and never synced: the last version
	/// it agreed with the stored file on, entries still waiting to reach that file, and rolling
	/// backups. Use one vault per data file.
	/// </summary>
	public sealed class LocalVault
	{
		/// <summary>How many backups are kept. New ones appear only when the content changes</summary>
		public const int MaxBackups = 50;


		public LocalVault(string folder)
			: this(folder, new FileOperations(), TimeProvider.System)
		{}
		internal LocalVault(string folder, FileOperations files, TimeProvider clock)
		{
			_folder = Path.GetFullPath(folder);
			_backups = Path.Combine(_folder, "backups");
			_files = files;
			_clock = clock;
		}


		/// <summary>The content last agreed with the stored file, or null if there is none</summary>
		public byte[]? ReadLastSynced() => AtomicFile.Retrying(_files, () => _files.Read(LastSyncedPath));

		public void WriteLastSynced(ReadOnlyMemory<byte> content)
		{
			CreatePrivateFolder(_folder);
			AtomicFile.Replace(_files, LastSyncedPath, content, () => true);
		}

		/// <summary>The entries that couldn't reach the stored file, or null if there are none</summary>
		public byte[]? ReadHeld() => AtomicFile.Retrying(_files, () => _files.Read(HeldPath));

		public void WriteHeld(ReadOnlyMemory<byte> content)
		{
			CreatePrivateFolder(_folder);
			AtomicFile.Replace(_files, HeldPath, content, () => true);
		}

		public void ClearHeld()
		{
			if (File.Exists(HeldPath))
				_files.Delete(HeldPath);
		}

		/// <summary>Keeps a copy of the content, unless the newest backup already holds it</summary>
		public void AddBackup(ReadOnlyMemory<byte> content)
		{
			CreatePrivateFolder(_folder);
			CreatePrivateFolder(_backups);

			var hash = Convert.ToHexString(SHA256.HashData(content.Span))[..HashLength];
			var newest = ListBackups().FirstOrDefault();
			if (newest != null && HashOf(newest.Path) == hash)
				return;

			var stamp = _clock.GetUtcNow().ToString(StampFormat, CultureInfo.InvariantCulture);
			AtomicFile.Replace(_files, Path.Combine(_backups, $"{Prefix}{stamp}-{hash}.json"), content, () => true);

			foreach (var old in ListBackups().Skip(MaxBackups))
				_files.Delete(old.Path);
		}

		/// <summary>All backups, newest first</summary>
		public IReadOnlyList<Backup> ListBackups()
		{
			if (!Directory.Exists(_backups))
				return Array.Empty<Backup>();

			return Directory.EnumerateFiles(_backups, $"{Prefix}*.json")
				.Order(StringComparer.Ordinal)
				.Reverse()
				.Select(el => new Backup(el, TakenAt(el)))
				.ToList();
		}

		public byte[] ReadBackup(Backup backup) =>
			AtomicFile.Retrying(_files, () => _files.Read(backup.Path)) ?? throw new FileNotFoundException("The backup no longer exists", backup.Path);


		private static string HashOf(string path)
		{
			var name = Path.GetFileNameWithoutExtension(path);
			return name[^HashLength..];
		}

		private static DateTimeOffset TakenAt(string path)
		{
			var stamp = Path.GetFileNameWithoutExtension(path)[Prefix.Length..^(HashLength + 1)];
			return DateTimeOffset.ParseExact(stamp, StampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
		}

		private static void CreatePrivateFolder(string path)
		{
			if (OperatingSystem.IsWindows())
				Directory.CreateDirectory(path);
			else
				Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
		}


		private string LastSyncedPath => Path.Combine(_folder, "last-synced.json");
		private string HeldPath => Path.Combine(_folder, "held.json");

		private const string Prefix = "pureData-";
		private const string StampFormat = "yyyyMMdd'T'HHmmssfff'Z'";
		private const int HashLength = 8;

		private readonly string _folder;
		private readonly string _backups;
		private readonly FileOperations _files;
		private readonly TimeProvider _clock;
	}
}
