// Builds the desktop app for Windows, macOS and Linux, each self-contained so the computer it goes
// to needs nothing installed. It needs only the .NET 10 SDK, and runs from any console: Command
// Prompt, PowerShell, or a Mac or Linux terminal. No git, no Git Bash.
//
//   dotnet run tools/build-apps.cs                        # all three
//   dotnet run tools/build-apps.cs -- windows mac         # or any of: windows mac linux
//
// Writes out/<version>/. The version comes from <Version> in src/Purity.Desktop/Purity.Desktop.csproj.
// The Linux packages are made by dpkg-deb, which only Linux has: on Windows that step runs in WSL,
// and a Mac can't build them. Reminders through Google need the project's client ID and secret in
// PURITY_GOOGLE_CLIENT_ID and PURITY_GOOGLE_CLIENT_SECRET when building; without them the app
// simply doesn't offer Google.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

var root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ThisFile())!, ".."));
var project = Path.Combine(root, "src", "Purity.Desktop", "Purity.Desktop.csproj");
var version = Regex.Match(File.ReadAllText(project), "<Version>(.*?)</Version>").Groups[1].Value;
var output = Path.Combine(root, "out", version);
var stage = Path.Combine(output, ".stage");

string[] known = ["windows", "mac", "linux"];
var platforms = args.Length > 0 ? args : known;
if (platforms.FirstOrDefault(el => !known.Contains(el)) is { } unknown)
{
	Console.Error.WriteLine($"Unknown platform '{unknown}': use windows, mac or linux");
	return 2;
}

try
{
	Reset(stage);
	Console.WriteLine($"Purity {version}");
	foreach (var platform in platforms)
	{
		switch (platform)
		{
			case "windows":
				foreach (var arch in new[] { "x64", "arm64" })
				{
					var folder = Publish($"win-{arch}", Path.Combine(stage, $"win-{arch}"));	// a single Purity.exe, as the project file sets for Windows
					Keep(Path.Combine(folder, "Purity.exe"), $"Purity-{version}-windows-{arch}.exe");
				}
				break;

			case "mac":
				foreach (var (arch, name) in new[] { ("x64", "intel"), ("arm64", "apple-silicon") })
				{
					var app = Path.Combine(stage, $"osx-{arch}", "Purity.app");
					Publish($"osx-{arch}", Path.Combine(app, "Contents", "MacOS"));
					var packaging = Path.Combine(root, "src", "Purity.Desktop", "Packaging", "macOS");
					Directory.CreateDirectory(Path.Combine(app, "Contents", "Resources"));
					var plist = File.ReadAllText(Path.Combine(packaging, "Info.plist")).Replace("\r", string.Empty).Replace("@VERSION@", version);
					File.WriteAllText(Path.Combine(app, "Contents", "Info.plist"), plist, new UTF8Encoding(false));
					File.Copy(Path.Combine(packaging, "Purity.icns"), Path.Combine(app, "Contents", "Resources", "Purity.icns"));

					// zipped so the programs inside keep their executable mark, even when built on Windows
					var zip = Path.Combine(output, $"Purity-{version}-macos-{name}.zip");
					Run("dotnet", ["run", Path.Combine(root, "tools", "zip-folder.cs"), "--", app, zip]);
					Report(zip);
				}
				break;

			case "linux":
				foreach (var (rid, arch) in new[] { ("linux-x64", "amd64"), ("linux-arm64", "arm64") })
				{
					var folder = Publish(rid, Path.Combine(stage, rid));
					var deb = Path.Combine(output, $"purity_{version}_{arch}.deb");
					File.Delete(deb);
					MakeDeb(folder, arch, deb);
					Report(deb);
				}
				break;
		}
	}

	Console.WriteLine($"Ready in {Path.GetRelativePath(root, output)}");
	return 0;
}
catch (BuildException e)
{
	Console.Error.WriteLine(e.Message);
	return 1;
}
finally
{
	if (Directory.Exists(stage))
		Directory.Delete(stage, recursive: true);
}


string Publish(string rid, string folder)
{
	Run("dotnet", ["publish", project, "-c", "Release", "-r", rid, "--self-contained", "-p:DebugType=none", "-nologo", "-v", "quiet", "-o", folder]);
	foreach (var symbols in Directory.EnumerateFiles(folder, "*.pdb", SearchOption.AllDirectories))
		File.Delete(symbols);										// such as those SkiaSharp ships for Windows
	return folder;
}

void Keep(string built, string name)
{
	var kept = Path.Combine(output, name);
	File.Copy(built, kept, overwrite: true);
	Report(kept);
}

void Report(string file) => Console.WriteLine($"  {Path.GetFileName(file)}, {new FileInfo(file).Length / 1048576.0:0.0} MB");

/// <summary>
/// The packaging step of tools/package-linux.sh, so the package is laid out in one place. It runs
/// on Linux, or in WSL on Windows, where Linux's own file system keeps the permissions it sets.
/// </summary>
void MakeDeb(string folder, string arch, string deb)
{
	var script = Path.Combine(root, "tools", "package-linux.sh");
	if (OperatingSystem.IsWindows())
	{
		try
		{
			Run("wsl.exe", ["-e", "bash", Wsl(script), "--deb", Wsl(folder), arch, version, Wsl(deb)]);
		}
		catch (Win32Exception)
		{
			throw new BuildException("The Linux packages need WSL on Windows (wsl --install), or build them on Linux.");
		}
	}
	else if (OperatingSystem.IsLinux())
		Run("bash", [script, "--deb", folder, arch, version, deb]);
	else
		throw new BuildException("The Linux packages need dpkg-deb, which only Linux has: build them on Linux, or on Windows with WSL.");
}

static void Reset(string folder)
{
	if (Directory.Exists(folder))
		Directory.Delete(folder, recursive: true);
	Directory.CreateDirectory(folder);
}

/// <summary>A Windows path as WSL sees it: C:\x becomes /mnt/c/x</summary>
static string Wsl(string path) => $"/mnt/{char.ToLowerInvariant(path[0])}{path[2..].Replace('\\', '/')}";

/// <summary>Runs a program quietly, showing what it said only when it fails</summary>
static void Run(string program, IEnumerable<string> arguments)
{
	var start = new ProcessStartInfo(program) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
	foreach (var argument in arguments)
		start.ArgumentList.Add(argument);

	using var process = Process.Start(start)!;
	var said = process.StandardOutput.ReadToEndAsync();
	var complained = process.StandardError.ReadToEndAsync();
	process.WaitForExit();
	if (process.ExitCode != 0)
		throw new BuildException($"{program} {string.Join(' ', start.ArgumentList.Take(2))} failed:\n{said.Result}{complained.Result}");
}

static string ThisFile([CallerFilePath] string path = "") => path;


sealed class BuildException(string message) : Exception(message);
