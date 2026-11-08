using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>One Purity per person on a computer, as two would share this computer's local copies of the history</summary>
	public sealed class SingleInstanceTests : IDisposable
	{
		public void Dispose()
		{
			try
			{
				Directory.Delete(_folder, true);
			}
			catch (IOException)
			{
				// temp gets cleaned anyway
			}
		}


		[Fact]
		public void OnlyOneHoldsTheFolderAtATime()
		{
			var first = SingleInstance.TryAcquire(_folder);
			Assert.NotNull(first);
			Assert.Null(SingleInstance.TryAcquire(_folder));

			var elsewhere = SingleInstance.TryAcquire(_folder + "-other");
			Assert.NotNull(elsewhere);						// a test run's own folder doesn't block the real app
			elsewhere!.Dispose();
			Directory.Delete(_folder + "-other", true);

			first!.Dispose();
			using var next = SingleInstance.TryAcquire(_folder);
			Assert.NotNull(next);							// released when the first one ends
		}

		[Fact]
		public async Task ASecondStartBringsTheOpenWindowForward()
		{
			using var running = SingleInstance.TryAcquire(_folder)!;
			running.Listen();
			var shown = new TaskCompletionSource();
			running.Show = () => shown.TrySetResult();

			Assert.True(SingleInstance.AskRunningToShow(_folder));
			await shown.Task.WaitAsync(TimeSpan.FromSeconds(10));

			shown = new TaskCompletionSource();
			running.Show = () => shown.TrySetResult();
			Assert.True(SingleInstance.AskRunningToShow(_folder));	// and again, as often as it's started
			await shown.Task.WaitAsync(TimeSpan.FromSeconds(10));
		}

		[Fact]
		public async Task AnEarlyRequestWaitsForTheWindow()
		{
			using var running = SingleInstance.TryAcquire(_folder)!;
			running.Listen();
			Assert.True(SingleInstance.AskRunningToShow(_folder));	// before the window exists

			var shown = new TaskCompletionSource();
			await Task.Delay(200);
			running.Show = () => shown.TrySetResult();
			await shown.Task.WaitAsync(TimeSpan.FromSeconds(10));
		}

		[Fact]
		public void NobodyAnswersWhenNothingRuns() =>
			Assert.False(SingleInstance.AskRunningToShow(_folder, TimeSpan.FromMilliseconds(300)));


		private readonly string _folder = Path.Combine(Path.GetTempPath(), $"purity-instance-{Guid.NewGuid():N}");
	}
}
