using System.Reflection;
using Avalonia.Threading;
using Purity.Desktop.ViewModels;
using Purity.Desktop.Views;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>
	/// Each window test gets a fresh Avalonia, whose UI thread is whichever thread asks for it first.
	/// A notice from the file watcher that arrives after a test, as when its folder is deleted at the
	/// end, must not be what asks: if it lands as the next test starts, that test fails on the wrong
	/// thread, as CI found once on Linux.
	/// </summary>
	public sealed partial class LookTests
	{
		[Fact]
		public async Task AChangeNoticedAfterAWindowTestClaimsNoThread()
		{
			var dataFile = Path.Combine(_root, "pureData.json");
			File.WriteAllText(dataFile, History());
			await Ui.Run(async () =>
			{
				var window = new MainWindow(new StartOptions(dataFile));		// left open, still watching the file
				window.Show();
				var viewModel = (MainWindowViewModel)window.DataContext!;
				await Ui.WaitUntil(() => viewModel.Periods.Count == 4);
			});
			Assert.Null(UiThread());

			File.AppendAllText(dataFile, "\n");									// a change made elsewhere, noticed once it settles
			await Task.Delay(TimeSpan.FromSeconds(1.5));

			Assert.Null(UiThread());
		}


		/// <summary>Avalonia's UI thread if one has been claimed, without claiming it as asking for it would</summary>
		private static Dispatcher? UiThread() =>
			(Dispatcher?)typeof(Dispatcher).GetMethod("TryGetUIThread", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null);
	}
}
