using Avalonia;


namespace Purity.Desktop
{
	internal static class Program
	{
		[STAThread]
		public static int Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

		/// <summary>Also used by the XAML previewer and the UI tests</summary>
		public static AppBuilder BuildAvaloniaApp() =>
			AppBuilder.Configure<App>()
				.UsePlatformDetect()
				.LogToTrace();
	}
}
