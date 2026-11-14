using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Purity.Desktop.Views;


namespace Purity.Desktop
{
	/// <summary>The desktop app: the shared look, opening the main window</summary>
	public sealed class DesktopApp : App
	{
		public override void OnFrameworkInitializationCompleted()
		{
			// Program.Main has already refused --picture without --data
			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
				var window = new MainWindow(StartOptions.Parse(desktop.Args ?? Array.Empty<string>()));
				desktop.MainWindow = window;
				var ui = Dispatcher.UIThread;			// held here: a second start is heard on another thread
				if (Program.Instance is { } instance)
					instance.Show = () => ui.Post(window.BringToFront);
			}

			base.OnFrameworkInitializationCompleted();
		}
	}
}
