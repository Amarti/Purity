// Builds the kit for trying reminders with the real Google Calendar: the desktop app with this
// project's Google client ID and secret built in, a made-up history whose reminders fall in the
// coming weeks, and the scripts and checklist in README.md. Run it in the repo, on Windows:
//
//   dotnet run tools/google-check/build-kit.cs                      # Windows, Intel Mac and Linux x64
//   dotnet run tools/google-check/build-kit.cs -- windows mac-arm   # or any of: windows mac mac-arm linux linux-arm
//
// It asks for the ID and secret, or takes them from PURITY_GOOGLE_CLIENT_ID and
// PURITY_GOOGLE_CLIENT_SECRET. The repo is public, so they must never be committed: they go only
// into the apps in out/google-check, which git ignores, and the build's own files, which hold them
// in plain text, are deleted afterwards.

#:project ../../src/Purity.Core/Purity.Core.csproj

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Purity.Core;

var root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ThisFile())!, "..", ".."));
var here = Path.Combine(root, "tools", "google-check");
var output = Path.Combine(root, "out", "google-check");
var build = Path.Combine(output, ".build");

var targets = new Dictionary<string, (string Rid, string Kit)>
{
	["windows"] = ("win-x64", "purity-google-windows"),
	["mac"] = ("osx-x64", "purity-google-mac-intel"),
	["mac-arm"] = ("osx-arm64", "purity-google-mac-apple-silicon"),
	["linux"] = ("linux-x64", "purity-google-linux-x64"),
	["linux-arm"] = ("linux-arm64", "purity-google-linux-arm64"),
};
var platforms = args.Length > 0 ? args : new[] { "windows", "mac", "linux" };
if (platforms.FirstOrDefault(el => !targets.ContainsKey(el)) is { } unknown)
{
	Console.Error.WriteLine($"Unknown platform '{unknown}': use any of {string.Join(", ", targets.Keys)}");
	return 2;
}

Console.WriteLine("The client ID and secret are on the Desktop client under Google Auth Platform > Clients.");
var clientId = Ask("PURITY_GOOGLE_CLIENT_ID", "Client ID", hidden: false);
if (!clientId.EndsWith(".apps.googleusercontent.com", StringComparison.Ordinal) || !Plain(clientId))
{
	Console.Error.WriteLine("That isn't a client ID: one ends in .apps.googleusercontent.com.");
	return 1;
}
var secret = Ask("PURITY_GOOGLE_CLIENT_SECRET", "Client secret (not shown as you type or paste)", hidden: true);
if (secret.Length == 0 || !Plain(secret))
{
	Console.Error.WriteLine("That isn't a client secret. A Desktop client has one, usually starting with GOCSPX-.");
	return 1;
}

var version = Regex.Match(File.ReadAllText(Path.Combine(root, "src", "Purity.Desktop", "Purity.Desktop.csproj")), "<Version>(.*?)</Version>").Groups[1].Value;
var commit = Run("git", new[] { "-C", root, "describe", "--always", "--dirty" }).Trim();
Console.WriteLine($"Purity {version} ({commit})");

try
{
	if (Directory.Exists(build))
		Directory.Delete(build, recursive: true);
	Directory.CreateDirectory(build);
	var history = Path.Combine(build, "history.json");
	WriteHistory(history, DateTime.Today);

	foreach (var platform in platforms)
	{
		var (rid, name) = targets[platform];
		var kit = Path.Combine(output, name);
		Console.WriteLine($"Building {name}");
		try
		{
			if (Directory.Exists(kit))
				Directory.Delete(kit, recursive: true);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			Console.Error.WriteLine($"  {kit} is in use. Close the Purity it opened, then build again.");
			return 1;
		}

		if (rid.StartsWith("osx-", StringComparison.Ordinal))
		{
			var app = Path.Combine(kit, "Purity.app", "Contents");
			Publish(rid, Path.Combine(app, "MacOS"));
			Directory.CreateDirectory(Path.Combine(app, "Resources"));
			var plist = File.ReadAllText(Path.Combine(root, "src", "Purity.Desktop", "Packaging", "macOS", "Info.plist")).Replace("@VERSION@", version);
			WriteText(Path.Combine(app, "Info.plist"), plist, "\n");
			File.Copy(Path.Combine(root, "src", "Purity.Desktop", "Packaging", "macOS", "Purity.icns"), Path.Combine(app, "Resources", "Purity.icns"));
		}
		else
			Publish(rid, rid.StartsWith("win-", StringComparison.Ordinal) ? kit : Path.Combine(kit, "app"));

		File.Copy(history, Path.Combine(kit, "history.json"));
		if (rid.StartsWith("win-", StringComparison.Ordinal))
		{
			var opener = File.ReadAllText(Path.Combine(here, "open-purity.cmd"));
			foreach (var computer in new[] { "first computer", "second computer" })
				WriteText(Path.Combine(kit, computer + ".cmd"), opener.Replace("@COMPUTER@", computer), "\r\n");
		}
		else
			WriteText(Path.Combine(kit, "open-purity.command"), File.ReadAllText(Path.Combine(here, "open-purity.command")), "\n");
		WriteText(Path.Combine(kit, "README.md"), File.ReadAllText(Path.Combine(here, "README.md")), rid.StartsWith("win-", StringComparison.Ordinal) ? "\r\n" : "\n");
		WriteText(Path.Combine(kit, "kit-info.txt"), $"version: {version}\ncommit: {commit}\nrid: {rid}\nbuilt: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n", "\n");

		if (!rid.StartsWith("win-", StringComparison.Ordinal))
		{
			Run("dotnet", new[] { "run", Path.Combine(root, "tools", "zip-folder.cs"), "--", kit, kit + ".zip" });
			Console.WriteLine($"  {name}.zip, to copy to that computer");
		}
		else
			Console.WriteLine($"  {name}, to use where it is");
	}
}
catch (InvalidOperationException e)
{
	Console.Error.WriteLine(e.Message);
	return 1;
}
finally
{
	if (Directory.Exists(build))
		Directory.Delete(build, recursive: true);
}

Console.WriteLine($"Ready in {output}. Next: README.md in any of the kits.");
return 0;


// Publishes the app for one platform. The ID and secret reach the build only through its
// environment, never its command line, and no build server outlives it holding them.
void Publish(string rid, string folder)
{
	var arguments = new[]
	{
		"publish", Path.Combine(root, "src", "Purity.Desktop", "Purity.Desktop.csproj"), "-c", "Release", "-r", rid, "--self-contained",
		"-p:DebugType=none", "--artifacts-path", build, "--disable-build-servers", "-nologo", "-v", "quiet", "-o", folder,
	};
	Run("dotnet", arguments, new Dictionary<string, string> { ["PURITY_GOOGLE_CLIENT_ID"] = clientId, ["PURITY_GOOGLE_CLIENT_SECRET"] = secret });
	foreach (var symbols in Directory.EnumerateFiles(folder, "*.pdb", SearchOption.AllDirectories))
		File.Delete(symbols);			// debug symbols, such as those SkiaSharp ships for Windows

	// The app offers Google only when the build took the ID, so a build that didn't would look like a broken app
	var id = Encoding.UTF8.GetBytes(clientId);
	var built = Directory.EnumerateFiles(build, "Purity.dll", SearchOption.AllDirectories)
		.Where(el => el.Contains(rid, StringComparison.OrdinalIgnoreCase))
		.Any(el => File.ReadAllBytes(el).AsSpan().IndexOf(id) >= 0);
	if (!built)
		throw new InvalidOperationException($"The build for {rid} didn't take the client ID");
}

static string Run(string program, IEnumerable<string> arguments, IReadOnlyDictionary<string, string>? environment = null)
{
	var start = new ProcessStartInfo(program) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
	foreach (var argument in arguments)
		start.ArgumentList.Add(argument);
	foreach (var (name, value) in environment ?? new Dictionary<string, string>())
		start.Environment[name] = value;

	using var process = Process.Start(start)!;
	var said = process.StandardOutput.ReadToEndAsync();
	var complained = process.StandardError.ReadToEndAsync();
	process.WaitForExit();
	if (process.ExitCode != 0)
		throw new InvalidOperationException($"{program} {string.Join(' ', start.ArgumentList.Take(2))} failed:\n{said.Result}{complained.Result}");
	return said.Result;
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
	if (!hidden || Console.IsInputRedirected)
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

// A made-up history: six closed periods, the newest beginning a week ago and ending two days ago,
// so its mikveh is five days away and its other dates three to four weeks away. One onset is
// after dusk and the gaps vary, so Veset Haflaga has intervals to track.
static void WriteHistory(string file, DateTime today)
{
	int[] gaps = { 31, 27, 30, 28, 29 };			// days from one period's start to the next, oldest first
	var ledger = new PeriodLedger();
	var begin = today.AddDays(-7 - gaps.Sum());
	for (var i = 0; i <= gaps.Length; i++)
	{
		var period = ledger.AddPeriod(Stamps.FromDate(begin, i == 2), Stamps.FromDate(begin.AddDays(5), false))!;
		ledger.AcceptPeriod(period);
		if (i < gaps.Length)
			begin = begin.AddDays(gaps[i]);
	}
	File.WriteAllText(file, PurityFile.Format(ledger.Data), new UTF8Encoding(false));
}

static void WriteText(string file, string text, string newline)
{
	Directory.CreateDirectory(Path.GetDirectoryName(file)!);
	File.WriteAllText(file, text.ReplaceLineEndings(newline), new UTF8Encoding(false));
}

static string ThisFile([CallerFilePath] string path = "") => path;
