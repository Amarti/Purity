using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Purity.Reminders;


namespace Purity.Desktop
{
	/// <summary>The system's secure store on each platform, for what must not sit in a plain file</summary>
	internal static class SecretStores
	{
		/// <summary>The Google sign-in, under the names it has always had</summary>
		public static ISecretStore ForGoogle() => ForThisComputer(AppPaths.GoogleSignInFile, "Purity calendar sign-in", "purity-calendar");

		public static ISecretStore ForICloud() => ForThisComputer(AppPaths.ICloudSignInFile, "Purity iCloud sign-in", "purity-icloud");

		/// <summary>The sign-in to another CalDAV server</summary>
		public static ISecretStore ForCalDav() => ForThisComputer(AppPaths.CalDavSignInFile, "Purity CalDAV sign-in", "purity-caldav");


		/// <param name="windowsFile">Where Windows keeps it, encrypted for this user</param>
		/// <param name="name">Its name in a Mac's keychain and a Linux keyring</param>
		/// <param name="application">Its key in a Linux keyring</param>
		private static ISecretStore ForThisComputer(string windowsFile, string name, string application)
		{
			if (OperatingSystem.IsWindows())
				return new WindowsSecretStore(windowsFile);
			return OperatingSystem.IsMacOS() ? new MacSecretStore(name) : new LinuxSecretStore(name, application);
		}


		/// <summary>Runs one of the system's own tools. A missing tool is an answer, not a crash.</summary>
		/// <param name="input">What to give the tool on its input, which unlike its arguments other programs can't see</param>
		internal static (bool Worked, string Output) Run(string tool, IEnumerable<string> arguments, string? input = null)
		{
			var start = new ProcessStartInfo(tool)
			{
				RedirectStandardInput = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			};
			foreach (var argument in arguments)
				start.ArgumentList.Add(argument);

			try
			{
				using var process = Process.Start(start);
				if (process == null)
					return (false, string.Empty);

				process.StandardInput.Write(input ?? string.Empty);
				process.StandardInput.Close();
				var output = process.StandardOutput.ReadToEnd();
				process.WaitForExit();
				return (process.ExitCode == 0, output.TrimEnd('\r', '\n'));
			}
			catch (Exception e) when (e is Win32Exception or IOException or InvalidOperationException)
			{
				return (false, string.Empty);
			}
		}
	}


	/// <summary>Encrypted so that only this Windows user can read it</summary>
	[SupportedOSPlatform("windows")]
	internal sealed class WindowsSecretStore(string file) : ISecretStore
	{
		public string? Read()
		{
			try
			{
				return File.Exists(file) ? Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(file), null, DataProtectionScope.CurrentUser)) : null;
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException or CryptographicException)
			{
				return null;
			}
		}

		public bool Write(string secret)
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(file)!);
				var temp = file + ".tmp";
				File.WriteAllBytes(temp, ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), null, DataProtectionScope.CurrentUser));
				File.Move(temp, file, overwrite: true);
				return true;
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException or CryptographicException)
			{
				return false;
			}
		}

		public void Delete()
		{
			try
			{
				File.Delete(file);
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				// nothing more can be done, and a sign-in that's been ended at Google is useless anyway
			}
		}
	}


	/// <summary>The login keychain, through the tool that comes with every Mac</summary>
	internal sealed class MacSecretStore(string service) : ISecretStore
	{
		public string? Read()
		{
			var (worked, output) = SecretStores.Run(Tool, new[] { "find-generic-password", "-a", Account, "-s", service, "-w" });
			return worked && output.Length > 0 ? output : null;
		}

		/// <summary>
		/// Through the tool's own command line, so the secret never appears among a program's arguments.
		/// It can't hold quotes or backslashes, and none does: Google's tokens have none, and a CalDAV
		/// sign-in is kept in base64.
		/// </summary>
		public bool Write(string secret) =>
			SecretStores.Run(Tool, new[] { "-i" }, $"add-generic-password -U -a {Account} -s \"{service}\" -w \"{secret}\"\n").Worked;

		public void Delete() => SecretStores.Run(Tool, new[] { "delete-generic-password", "-a", Account, "-s", service });


		private const string Tool = "/usr/bin/security";
		private const string Account = "purity";
	}


	/// <summary>The desktop's keyring, where there is one. Without one, a sign-in lasts until the app closes.</summary>
	internal sealed class LinuxSecretStore(string label, string application) : ISecretStore
	{
		public string? Read()
		{
			var (worked, output) = SecretStores.Run(Tool, new[] { "lookup", "application", application });
			return worked && output.Length > 0 ? output : null;
		}

		public bool Write(string secret) =>
			SecretStores.Run(Tool, new[] { "store", "--label=" + label, "application", application }, secret).Worked;

		public void Delete() => SecretStores.Run(Tool, new[] { "clear", "application", application });


		private const string Tool = "secret-tool";
	}
}
