using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;


namespace Purity.UiProbe
{
	/// <summary>
	/// Checks that a UI stack works on this machine: the window opens, the engine's date text shows,
	/// and the date picker and file chooser respond. It shows made-up dates only.
	/// With --auto it saves a picture of the window and closes; otherwise it waits for the person checking.
	/// Writes what it found to &lt;out&gt;/&lt;variant&gt;.txt as it goes, so a crash keeps what came before.
	/// </summary>
	public static class Program
	{
		public static Report Report { get; private set; } = null!;

		public static string AvaloniaVersion { get; } =
			typeof(AvaloniaObject).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "unknown";


		[STAThread]
		public static int Main(string[] args)
		{
			var auto = args.Contains("--auto");
			var output = Argument(args, "--out") ?? Directory.GetCurrentDirectory();
			var variant = Argument(args, "--variant") ?? "probe";

			Report = new Report(Path.Combine(output, variant + ".txt"));
			Report.Set("variant", variant);
			Report.Set("mode", auto ? "auto" : "manual");
			Report.Set("os", RuntimeInformation.OSDescription);
			Report.Set("arch", $"{RuntimeInformation.ProcessArchitecture} process on {RuntimeInformation.OSArchitecture}");
			Report.Set("dotnet", RuntimeInformation.FrameworkDescription);
			Report.Set("avalonia", AvaloniaVersion);
			Report.Set("result", "starting");
			Report.Save();

			// A window that never appears must not hang the kit
			using var watchdog = auto ? new Timer(_ => GiveUp(), null, TimeSpan.FromSeconds(60), Timeout.InfiniteTimeSpan) : null;
			try
			{
				var code = AppBuilder.Configure(() => new ProbeApp(auto, output, variant))
					.UsePlatformDetect()
					.StartWithClassicDesktopLifetime(args);
				if (!auto && Report.Get("result") == "window shown")
					Report.Set("result", "closed");
				Report.Save();
				return Report.Get("result") is "passed" or "closed" ? code : 1;
			}
			catch (Exception e)
			{
				Report.Set("result", "failed");
				Report.Set("error", e.ToString());
				Report.Save();
				return 2;
			}
		}


		private static void GiveUp()
		{
			Report.Set("result", "failed");
			Report.Set("error", "the window didn't open and save its picture within 60 seconds");
			Report.Save();
			Environment.Exit(3);
		}

		private static string? Argument(string[] args, string name)
		{
			var at = Array.IndexOf(args, name);
			return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
		}
	}


	public sealed class ProbeApp : Application
	{
		private readonly bool _auto;
		private readonly string _output;
		private readonly string _variant;


		public ProbeApp(bool auto, string output, string variant) => (_auto, _output, _variant) = (auto, output, variant);


		public override void Initialize()
		{
			Name = "Purity check";
			RequestedThemeVariant = ThemeVariant.Light;
			Styles.Add(new FluentTheme());
		}

		public override void OnFrameworkInitializationCompleted()
		{
			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
				desktop.MainWindow = new ProbeWindow(_auto, _output, _variant);
			base.OnFrameworkInitializationCompleted();
		}
	}
}
