using System.Runtime.Versioning;
using System.Text;
using Xunit;


namespace Purity.Storage.Tests
{
	public class LocalVaultTests : IDisposable
	{
		private readonly TestFolder _folder = new();
		private readonly SteppingClock _clock = new();
		private readonly LocalVault _vault;


		public LocalVaultTests()
		{
			_vault = new LocalVault(_folder.VaultFolder, new FileOperations(), _clock);
		}

		public void Dispose() => _folder.Dispose();


		private static byte[] Content(int number) => Encoding.UTF8.GetBytes($"[{number}]");


		[Fact]
		public void AnEmptyVaultHoldsNothing()
		{
			Assert.Null(_vault.ReadLastSynced());
			Assert.Empty(_vault.ListBackups());
		}

		[Fact]
		public void LastSyncedComesBackAsWritten()
		{
			_vault.WriteLastSynced(Content(1));
			_vault.WriteLastSynced(Content(2));

			Assert.Equal(Content(2), _vault.ReadLastSynced());
		}

		[Fact]
		public void ABackupComesBackAsWritten()
		{
			_vault.AddBackup(Content(1));

			var backup = Assert.Single(_vault.ListBackups());

			Assert.Equal(Content(1), _vault.ReadBackup(backup));
		}

		[Fact]
		public void ABackupRecordsWhenItWasTaken()
		{
			_vault.AddBackup(Content(1));

			Assert.Equal(_clock.Latest, Assert.Single(_vault.ListBackups()).TakenAt);
		}

		[Fact]
		public void BackupsAreListedNewestFirst()
		{
			foreach (var number in new[] { 1, 2, 3 })
				_vault.AddBackup(Content(number));

			Assert.Equal([Content(3), Content(2), Content(1)], _vault.ListBackups().Select(_vault.ReadBackup));
		}

		[Fact]
		public void TheSameContentTwiceInARowIsKeptOnce()
		{
			_vault.AddBackup(Content(1));
			_vault.AddBackup(Content(1));

			Assert.Single(_vault.ListBackups());
		}

		[Fact]
		public void ContentThatReturnsLaterIsKeptAgain()
		{
			_vault.AddBackup(Content(1));
			_vault.AddBackup(Content(2));
			_vault.AddBackup(Content(1));

			Assert.Equal(3, _vault.ListBackups().Count);
		}

		[Fact]
		public void OnlyTheNewestBackupsAreKept()
		{
			for (var number = 1; number <= LocalVault.MaxBackups + 3; number++)
				_vault.AddBackup(Content(number));

			var backups = _vault.ListBackups();

			Assert.Equal(LocalVault.MaxBackups, backups.Count);
			Assert.Equal(Content(LocalVault.MaxBackups + 3), _vault.ReadBackup(backups[0]));
			Assert.Equal(Content(4), _vault.ReadBackup(backups[^1]));
		}

		[Fact]
		public void ALeftoverTemporaryFileIsNotTakenForABackup()
		{
			_vault.AddBackup(Content(1));
			File.WriteAllBytes(Path.Combine(_folder.VaultFolder, "backups", "~pureData-x.json.abc.tmp"), Content(2));

			Assert.Single(_vault.ListBackups());
		}

		[Fact]
		public void ReadingAVanishedBackupFailsClearly()
		{
			_vault.AddBackup(Content(1));
			var backup = Assert.Single(_vault.ListBackups());
			File.Delete(backup.Path);

			Assert.Throws<FileNotFoundException>(() => _vault.ReadBackup(backup));
		}

		[Fact]
		public void NoTemporaryFileStaysBehind()
		{
			_vault.WriteLastSynced(Content(1));
			_vault.AddBackup(Content(1));

			Assert.Empty(Directory.GetFiles(_folder.VaultFolder, "*.tmp", SearchOption.AllDirectories));
		}

		[Fact]
		[UnsupportedOSPlatform("windows")]
		public void TheVaultIsPrivateToItsOwner()
		{
			if (OperatingSystem.IsWindows())
				Assert.Skip("Windows has no file modes");

			_vault.AddBackup(Content(1));

			const UnixFileMode owner = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
			Assert.Equal(owner, File.GetUnixFileMode(_folder.VaultFolder));
			Assert.Equal(owner, File.GetUnixFileMode(Path.Combine(_folder.VaultFolder, "backups")));
			Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Assert.Single(_vault.ListBackups()).Path));
		}


		private sealed class SteppingClock : TimeProvider
		{
			public override DateTimeOffset GetUtcNow()
			{
				Latest = Latest.AddSeconds(1);
				return Latest;
			}


			public DateTimeOffset Latest { get; private set; } = new(2033, 1, 1, 0, 0, 0, TimeSpan.Zero);
		}
	}
}
