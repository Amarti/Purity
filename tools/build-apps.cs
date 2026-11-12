// Builds the desktop app for Windows, macOS and Linux, each self-contained so the computer it goes
// to needs nothing installed. It needs only the .NET 10 SDK, and runs from any console: Command
// Prompt, PowerShell, or a Mac or Linux terminal. No git, no Git Bash.
//
//   dotnet run tools/build-apps.cs                        # all three
//   dotnet run tools/build-apps.cs -- windows mac         # or any of: windows mac linux
//
// Writes out/<version>/. The version comes from <Version> in src/Purity.Desktop/Purity.Desktop.csproj.
// The Linux packages are made by dpkg-deb, which only Linux has: on Windows that step runs in WSL,
// and a Mac can't build them.
//
// Reminders through Google need the project's client ID and secret built in. It asks for them, or
// takes them from PURITY_GOOGLE_CLIENT_ID and PURITY_GOOGLE_CLIENT_SECRET, and pressing Enter at
// the ID builds without Google, as does a console it can't ask in. The repo is public, so they must
// never be committed: they reach the build only through its environment, never its command line,
// no build server outlives it holding them, and its working files, which hold them in plain text,
// stay in out/<version>/.stage and are deleted afterwards. A build stopped part way leaves them
// there until the next one.

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

(string Id, string Secret)? google = null;
try
{
	Console.WriteLine($"Purity {version}");
	google = Google();
	Console.WriteLine(google != null ? "With Google reminders" : "Without Google reminders, since no client ID was given");
	Reset(stage);
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

	Console.WriteLine($"Ready in {Path.GetRelativePath(root, output)}, " + (google != null ? "with Google reminders" : "without Google reminders"));
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


// The working files go in the stage rather than the project's obj folder, since with Google they
// hold its ID and secret in plain text. Without Google both are taken out of the build's
// environment, so a secret set on its own can't slip in.
string Publish(string rid, string folder)
{
	var build = Path.Combine(stage, "build");
	Run("dotnet", ["publish", project, "-c", "Release", "-r", rid, "--self-contained", "-p:DebugType=none", "--artifacts-path", build,
		"--disable-build-servers", "-nologo", "-v", "quiet", "-o", folder],
		new Dictionary<string, string?> { ["PURITY_GOOGLE_CLIENT_ID"] = google?.Id, ["PURITY_GOOGLE_CLIENT_SECRET"] = google?.Secret });
	foreach (var symbols in Directory.EnumerateFiles(folder, "*.pdb", SearchOption.AllDirectories))
		File.Delete(symbols);										// such as those SkiaSharp ships for Windows

	// The app offers Google only when the build took the ID, and a build that didn't would look like a broken app
	if (google is { } given)
	{
		var id = Encoding.UTF8.GetBytes(given.Id);
		var took = Directory.EnumerateFiles(build, "Purity.dll", SearchOption.AllDirectories)
			.Where(el => el.Contains(rid, StringComparison.OrdinalIgnoreCase))
			.Any(el => File.ReadAllBytes(el).AsSpan().IndexOf(id) >= 0);
		if (!took)
			throw new BuildException($"The build for {rid} didn't take Google's client ID.");
	}
	return folder;
}

// Google's client ID and secret, or null to build without them
static (string Id, string Secret)? Google()
{
	if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PURITY_GOOGLE_CLIENT_ID")))
	{
		if (Console.IsInputRedirected)
			return null;
		Console.WriteLine("Google reminders need the project's client ID and secret, on the Desktop client under Google Auth Platform > Clients.");
	}
	var id = Ask("PURITY_GOOGLE_CLIENT_ID", "Client ID (Enter to build without Google)", hidden: false);
	if (id.Length == 0)
		return null;
	if (!id.EndsWith(".apps.googleusercontent.com", StringComparison.Ordinal) || !Plain(id))
		throw new BuildException("That isn't a client ID: one ends in .apps.googleusercontent.com.");
	var secret = Ask("PURITY_GOOGLE_CLIENT_SECRET", "Client secret (not shown as you type or paste)", hidden: true);
	if (secret.Length == 0 || !Plain(secret))
		throw new BuildException("That isn't a client secret. A Desktop client has one, usually starting with GOCSPX-.");
	return (id, secret);
}

// Takes the value from the environment if it's there, or asks. A hidden answer isn't echoed, so
// it never shows on a screen or in a terminal's scrollback.
static string Ask(string variable, string question, bool hidden)
{
	if (Environment.GetEnvironmentVariable(variable) is { Length: > 0 } given)
	{
		Console.WriteLine($"{question.Split(" (")[0]}: taken from {variable}");
		return given.Trim();
	}

	Console.Write(question + ": ");
	if (Console.IsInputRedirected)
	{
		var read = (Console.ReadLine() ?? string.Empty).Trim();
		Console.WriteLine();			// nothing typed, so nothing ended the line
		return read;
	}
	if (!hidden)
		return (Console.ReadLine() ?? string.Empty).Trim();

	var typed = new StringBuilder();
	for (var key = Console.ReadKey(intercept: true); key.Key != ConsoleKey.Enter; key = Console.ReadKey(intercept: true))
	{
		if (key.Key == ConsoleKey.Backspace)
			typed.Length = Math.Max(0, typed.Length - 1);
		else if (!char.IsControl(key.KeyChar))
			typed.Append(key.KeyChar);
	}
	Console.WriteLine(typed.Length > 0 ? $"{typed.Length} characters" : string.Empty);
	return typed.ToString().Trim();
}

// Google's IDs and secrets use only these, and anything else could upset the build or a shell
static bool Plain(string value) => Regex.IsMatch(value, "^[A-Za-z0-9._-]+$");

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

/// <summary>Runs a program quietly, showing what it said only when it fails. A null value takes that variable out of its environment.</summary>
static void Run(string program, IEnumerable<string> arguments, IReadOnlyDictionary<string, string?>? environment = null)
{
	var start = new ProcessStartInfo(program) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
	foreach (var argument in arguments)
		start.ArgumentList.Add(argument);
	foreach (var (name, value) in environment ?? new Dictionary<string, string?>())
	{
		if (value == null)
			start.Environment.Remove(name);
		else
			start.Environment[name] = value;
	}

	using var process = Process.Start(start)!;
	var said = process.StandardOutput.ReadToEndAsync();
	var complained = process.StandardError.ReadToEndAsync();
	process.WaitForExit();
	if (process.ExitCode != 0)
		throw new BuildException($"{program} {string.Join(' ', start.ArgumentList.Take(2))} failed:\n{said.Result}{complained.Result}");
}

static string ThisFile([CallerFilePath] string path = "") => path;


sealed class BuildException(string message) : Exception(message);
