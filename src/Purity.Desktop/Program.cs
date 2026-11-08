using Avalonia;


namespace Purity.Desktop
{
	internal static class Program
	{
		[STAThread]
		public static int Main(string[] args)
		{
			var options = StartOptions.Parse(args);
			if (options.PictureFile != null && options.DataFile == null)
			{
				Console.Error.WriteLine("--picture needs --data with a made-up history, so a picture can never show the real one");
				return 2;
			}

			var code = BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
			return code != 0 ? code : ExitCode;
		}

		/// <summary>Also used by the XAML previewer and the UI tests</summary>
		public static AppBuilder BuildAvaloniaApp() =>
			AppBuilder.Configure<App>()
				.UsePlatformDetect()
				.LogToTrace();

		/// <summary>Set when a check run fails, as closing the window gives no other way to say so</summary>
		internal static int ExitCode;
	}
}
