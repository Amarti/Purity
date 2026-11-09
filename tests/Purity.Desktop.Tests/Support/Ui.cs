using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;


namespace Purity.Desktop.Tests
{
	/// <summary>Runs test code on the UI thread of Avalonia's invisible test screen, with the app's own styles</summary>
	internal static class Ui
	{
		/// <summary>
		/// Waits for the whole test. Only the session's overload for a task with a result keeps the UI
		/// thread running until the test is done; the others return at the test's first await.
		/// </summary>
		public static Task Run(Func<Task> test) => Session.Dispatch(async () =>
		{
			await test();
			return true;
		}, CancellationToken.None);

		public static Task Run(Action test) => Session.Dispatch(test, CancellationToken.None);

		/// <summary>Lets the UI thread work until the condition holds, as file loading finishes in the background</summary>
		public static async Task WaitUntil(Func<bool> condition)
		{
			var deadline = DateTime.UtcNow.AddSeconds(10);
			while (!condition())
			{
				if (DateTime.UtcNow > deadline)
					throw new TimeoutException("The window didn't get there in time");

				Dispatcher.UIThread.RunJobs();
				await Task.Delay(10);
			}
			Dispatcher.UIThread.RunJobs();
		}


		private static readonly HeadlessUnitTestSession Session = HeadlessUnitTestSession.StartNew(typeof(TestApp));
	}


	public sealed class TestApp
	{
		public static AppBuilder BuildAvaloniaApp() =>
			AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
	}
}
