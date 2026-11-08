using System.Security.Cryptography;
using System.Text;


namespace Purity.Desktop
{
	/// <summary>
	/// Where this device keeps its own files: settings and, per data file, the local copies and
	/// backups that Purity.Storage manages. These are never synced. PURITY_APP_DATA points them
	/// elsewhere, so a test run can't touch the real ones.
	/// </summary>
	public static class AppPaths
	{
		public static string Folder
		{
			get
			{
				var custom = Environment.GetEnvironmentVariable("PURITY_APP_DATA");
				if (!string.IsNullOrWhiteSpace(custom))
					return Path.GetFullPath(custom);

				var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create);
				return Path.Combine(local, "Purity");
			}
		}

		public static string SettingsFile => Path.Combine(Folder, "settings.json");

		/// <summary>One vault per data file, as Purity.Storage requires</summary>
		public static string VaultFor(string dataFile)
		{
			var path = Path.GetFullPath(dataFile);
			if (!OperatingSystem.IsLinux())
				path = path.ToUpperInvariant();			// Windows and macOS ignore case in paths

			var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path)))[..16];
			return Path.Combine(Folder, "vaults", hash);
		}
	}
}
