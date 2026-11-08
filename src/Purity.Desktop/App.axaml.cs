using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Purity.Desktop.Views;


namespace Purity.Desktop
{
	public partial class App : Application
	{
		public override void Initialize()
		{
			AvaloniaXamlLoader.Load(this);
			Resources[AppFont.ResourceKey] = AppFont.Choose();
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
