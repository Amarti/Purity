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
		public static ISecretStore ForThisComputer()
		{
			if (OperatingSystem.IsWindows())
				return new WindowsSecretStore(AppPaths.GoogleSignInFile);
			return OperatingSystem.IsMacOS() ? new MacSecretStore() : new LinuxSecretStore();
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
	internal sealed class MacSecretStore : ISecretStore
	{
		public string? Read()
		{
			var (worked, output) = SecretStores.Run(Tool, new[] { "find-generic-password", "-a", Account, "-s", Service, "-w" });
			return worked && output.Length > 0 ? output : null;
		}

		/// <summary>Through the tool's own command line, so the secret never appears among a program's arguments</summary>
		public bool Write(string secret) =>
			SecretStores.Run(Tool, new[] { "-i" }, $"add-generic-password -U -a {Account} -s \"{Service}\" -w \"{secret}\"\n").Worked;

		public void Delete() => SecretStores.Run(Tool, new[] { "delete-generic-password", "-a", Account, "-s", Service });


		private const string Tool = "/usr/bin/security";
		private const string Account = "purity";
		private const string Service = "Purity calendar sign-in";
	}


	/// <summary>The desktop's keyring, where there is one. Without one, a sign-in lasts until the app closes.</summary>
	internal sealed class LinuxSecretStore : ISecretStore
	{
		public string? Read()
		{
			var (worked, output) = SecretStores.Run(Tool, new[] { "lookup", "application", Application });
			return worked && output.Length > 0 ? output : null;
		}

		public bool Write(string secret) =>
			SecretStores.Run(Tool, new[] { "store", "--label=Purity calendar sign-in", "application", Application }, secret).Worked;

		public void Delete() => SecretStores.Run(Tool, new[] { "clear", "application", Application });


		private const string Tool = "secret-tool";
		private const string Application = "purity-calendar";
	}
}
