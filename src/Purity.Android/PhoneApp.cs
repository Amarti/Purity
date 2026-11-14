using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Purity.Reminders;
using Purity.UI;
using Purity.UI.ViewModels;
using Purity.UI.Views;


namespace Purity.Android
{
	/// <summary>The shared look and pages, on a phone or a tablet: the history first, with Settings and the like over it</summary>
	public sealed class PhoneApp : App
	{
		public override void OnFrameworkInitializationCompleted()
		{
			if (ApplicationLifetime is ISingleViewApplicationLifetime single)
			{
				// Held from here, on the UI thread: notices come from the file watcher's own thread
				var ui = Dispatcher.UIThread;
				single.MainView = new AppShell(
					(dialogs, documents) => new MainWindowViewModel(dialogs, null, TimeProvider.System, work => ui.Post(() => _ = work()), reminderConnections: Connections, documents: documents),
					() => MainActivity.Current?.FinishAndRemoveTask());
			}

			base.OnFrameworkInitializationCompleted();
		}


		/// <summary>Google and other CalDAV servers, as decided on 2026-11-14. No calendar file and no iCloud on Android.</summary>
		private static ReminderConnections Connections(DesktopSettings settings) => new(new Dictionary<ReminderPlace, IReminderConnection>
		{
			[ReminderPlace.Google] = AndroidGoogleConnection.For(settings),
			[ReminderPlace.CalDav] = CalDavConnection.ForOther(settings),
		});
	}
}
