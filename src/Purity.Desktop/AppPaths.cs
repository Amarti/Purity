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
		/// <summary>Relative paths given at the start mean this folder, even after a file dialog changes the current one</summary>
		internal static readonly string StartFolder = Environment.CurrentDirectory;

		public static string Folder
		{
			get
			{
				var custom = Environment.GetEnvironmentVariable("PURITY_APP_DATA");
				if (!string.IsNullOrWhiteSpace(custom))
					return Path.GetFullPath(custom, StartFolder);

				var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create);
				return Path.Combine(local, "Purity");
			}
		}

		public static string SettingsFile => Path.Combine(Folder, "settings.json");

		/// <summary>Where a new history goes when the person starts one on the first run: on this computer, never synced</summary>
		public static string DefaultDataFile => Path.Combine(Folder, "pureData.json");

		/// <summary>One vault per data file, as Purity.Storage requires</summary>
		public static string VaultFor(string dataFile)
		{
			var path = Path.GetFullPath(dataFile);
			if (!OperatingSystem.IsLinux())
				path = path.ToUpperInvariant();			// Windows and macOS ignore case in paths

			var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path)))[..16];
			return Path.Combine(VaultsFolder, hash);
		}

		/// <summary>Every data file's vault: its backups and local copies</summary>
		public static string VaultsFolder => Path.Combine(Folder, "vaults");

		/// <summary>Where Windows keeps the Google sign-in, encrypted for this user. Macs and Linux use the system's own store.</summary>
		public static string GoogleSignInFile => Path.Combine(Folder, "google.sign-in");

		/// <summary>Where Windows keeps the iCloud sign-in, likewise</summary>
		public static string ICloudSignInFile => Path.Combine(Folder, "icloud.sign-in");

		/// <summary>Where Windows keeps the sign-in to another CalDAV server, likewise</summary>
		public static string CalDavSignInFile => Path.Combine(Folder, "caldav.sign-in");

		/// <summary>Every sign-in file Windows can keep</summary>
		public static IReadOnlyList<string> SignInFiles => new[] { GoogleSignInFile, ICloudSignInFile, CalDavSignInFile };
	}
}
