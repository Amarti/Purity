using System.Diagnostics;
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

		/// <summary>
		/// The real case is two processes, and on Linux and macOS both the lock and the pipe work
		/// differently from Windows, so this runs a second copy of the test runner that holds the
		/// folder as an open Purity would. Then it kills that copy, as a crash would, and checks that
		/// neither the lock nor the pipe's socket file is left behind in the way.
		/// </summary>
		[Fact]
		public async Task AnotherProcessIsKeptOutAndAnswers()
		{
			using var holder = StartHolder();
			try
			{
				await WaitForFile("ready", holder);
				Assert.Null(SingleInstance.TryAcquire(_folder));
				Assert.True(SingleInstance.AskRunningToShow(_folder));
				await WaitForFile("shown", holder);
			}
			finally
			{
				holder.Kill(entireProcessTree: true);
				await holder.WaitForExitAsync();
			}

			using var next = SingleInstance.TryAcquire(_folder);
			Assert.NotNull(next);
			next!.Listen();
			var shown = new TaskCompletionSource();
			next.Show = () => shown.TrySetResult();
			Assert.True(SingleInstance.AskRunningToShow(_folder));
			await shown.Task.WaitAsync(TimeSpan.FromSeconds(10));
		}

		/// <summary>
		/// Not a test on its own. Started in a second process by <see cref="AnotherProcessIsKeptOutAndAnswers"/>,
		/// it holds the folder named in the environment until killed. Otherwise it does nothing.
		/// </summary>
		[Fact]
		public async Task HoldForAnotherProcess()
		{
			var folder = Environment.GetEnvironmentVariable(HolderVariable);
			if (folder == null)
				return;

			using var hold = SingleInstance.TryAcquire(folder) ?? throw new InvalidOperationException("The folder was already held");
			hold.Listen();
			hold.Show = () => File.WriteAllText(Path.Combine(folder, "shown"), "");
			File.WriteAllText(Path.Combine(folder, "ready"), "");
			await Task.Delay(TimeSpan.FromSeconds(60));
		}


		private Process StartHolder()
		{
			var runner = Environment.ProcessPath!;
			var start = new ProcessStartInfo(runner) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
			if (Path.GetFileNameWithoutExtension(runner) == "dotnet")
				start.ArgumentList.Add(typeof(SingleInstanceTests).Assembly.Location);
			start.ArgumentList.Add("-method");
			start.ArgumentList.Add($"{typeof(SingleInstanceTests).FullName}.{nameof(HoldForAnotherProcess)}");
			start.ArgumentList.Add("-noColor");
			start.Environment[HolderVariable] = _folder;

			var process = Process.Start(start)!;
			_ = process.StandardOutput.ReadToEndAsync();
			_ = process.StandardError.ReadToEndAsync();
			return process;
		}

		private async Task WaitForFile(string name, Process holder)
		{
			var deadline = DateTime.UtcNow.AddSeconds(30);
			while (!File.Exists(Path.Combine(_folder, name)))
			{
				if (holder.HasExited)
					throw new InvalidOperationException($"The other process ended early, with exit code {holder.ExitCode}");
				if (DateTime.UtcNow > deadline)
					throw new TimeoutException($"The other process never got to '{name}'");
				await Task.Delay(50);
			}
		}


		private const string HolderVariable = "PURITY_TEST_HOLD_FOLDER";


		private readonly string _folder = Path.Combine(Path.GetTempPath(), $"purity-instance-{Guid.NewGuid():N}");
	}
}
