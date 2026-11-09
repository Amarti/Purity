using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using Purity.Desktop.Localization;
using Purity.Desktop.Views;


namespace Purity.Desktop
{
	public partial class App : Application
	{
		public override void Initialize()
		{
			Loc.Instance.TakeOverCulture();
			AvaloniaXamlLoader.Load(this);
			Resources[AppFont.ResourceKey] = AppFont.Family;
		}

		public override void OnFrameworkInitializationCompleted()
		{
			// Program.Main has already refused --picture without --data
			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
				var window = new MainWindow(StartOptions.Parse(desktop.Args ?? Array.Empty<string>()));
				desktop.MainWindow = window;
				if (Program.Instance is { } instance)
					instance.Show = () => Dispatcher.UIThread.Post(window.BringToFront);
			}

			base.OnFrameworkInitializationCompleted();
		}

		/// <summary>Light, dark, or whatever the system uses (tweak T7). Every window follows at once.</summary>
		public static void Apply(Appearance appearance)
		{
			if (Current is { } app)
				app.RequestedThemeVariant = appearance switch
				{
					Appearance.Light => ThemeVariant.Light,
					Appearance.Dark => ThemeVariant.Dark,
					_ => ThemeVariant.Default,
				};
		}
	}


	/// <summary>Command line options</summary>
	/// <param name="DataFile">A data file to open instead of the one in the settings, for this run only</param>
	/// <param name="PictureFile">
	/// For checks on machines CI can't reach: save a picture of the window here, with a report
	/// beside it, then quit. Only allowed together with <paramref name="DataFile"/>.
	/// </param>
	public sealed record StartOptions(string? DataFile, string? PictureFile = null)
	{
		/// <summary>Reads <c>--data &lt;file&gt;</c> and <c>--picture &lt;file&gt;</c>, and ignores anything else, such as the arguments older macOS adds</summary>
		public static StartOptions Parse(IReadOnlyList<string> args)
		{
			string? dataFile = null, pictureFile = null;
			for (var i = 0; i < args.Count - 1; i++)
			{
				if (args[i] == "--data")
					dataFile = args[i + 1];
				else if (args[i] == "--picture")
					pictureFile = args[i + 1];
			}

			return new StartOptions(dataFile, pictureFile);
		}
	}
}
