using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;


namespace Purity.Desktop
{
	/// <summary>
	/// Keeps Purity to one window per person on a computer. Two at once share this computer's local
	/// copies of the history, so each would act on the other's unsaved entries. A second start brings
	/// the open window to the front and quits.
	/// <para/>The hold is an exclusive lock on a file in the app's own folder, which the system
	/// releases even if the app crashes, so it can't go stale. Test runs with their own
	/// PURITY_APP_DATA folder don't get in each other's way, or in the way of the real app.
	/// </summary>
	public sealed class SingleInstance : IDisposable
	{
		private SingleInstance(FileStream lockFile, string pipeName)
		{
			_lockFile = lockFile;
			_pipeName = pipeName;
		}


		/// <summary>Takes the hold for this folder</summary>
		/// <returns>The hold, or null when another Purity has it</returns>
		public static SingleInstance? TryAcquire(string folder)
		{
			try
			{
				Directory.CreateDirectory(folder);
				var file = new FileStream(Path.Combine(folder, "running.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
				return new SingleInstance(file, PipeName(folder));
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				return null;
			}
		}

		/// <summary>Asks the Purity holding this folder to bring its window to the front</summary>
		/// <returns>Whether it answered. If not, the hold failed for some other reason, and starting anyway beats not starting.</returns>
		public static bool AskRunningToShow(string folder, TimeSpan? wait = null)
		{
			if (OperatingSystem.IsWindows())
				AllowSetForegroundWindow(AnyProcess);		// Windows only lets the window come forward if this start allows it

			// Only the open Purity's reply counts: the request getting through isn't enough to know. On
			// Linux and macOS the pipe is a socket, which takes a caller while the open Purity is still
			// finishing with the previous one and then drops it unanswered, so a hang-up means try again.
			using var timeout = new CancellationTokenSource(wait ?? TimeSpan.FromSeconds(5));
			while (true)
			{
				try
				{
					using var pipe = new NamedPipeClientStream(".", PipeName(folder), PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
					pipe.ConnectAsync(timeout.Token).GetAwaiter().GetResult();
					pipe.WriteAsync(new[] { ShowRequest }, timeout.Token).AsTask().GetAwaiter().GetResult();
					var reply = new byte[1];
					if (pipe.ReadAsync(reply, timeout.Token).AsTask().GetAwaiter().GetResult() == 1 && reply[0] == Showing)
						return true;
				}
				catch (IOException)
				{
					// dropped between callers
				}
				catch (Exception e) when (e is OperationCanceledException or TimeoutException or UnauthorizedAccessException)
				{
					return false;
				}

				if (timeout.IsCancellationRequested)
					return false;
				Thread.Sleep(50);
			}
		}

		/// <summary>
		/// Starts answering later starts. Call it right away, before the window exists: a request that
		/// arrives early waits until <see cref="Show"/> is set.
		/// </summary>
		public void Listen() => _listening = Task.Run(() => ListenAsync(_stop.Token));

		/// <summary>Brings the window to the front. Called on a pool thread.</summary>
		public Action? Show
		{
			set
			{
				bool pending;
				lock (_gate)
				{
					_show = value;
					pending = _pendingShow && value != null;
					_pendingShow = false;
				}
				if (pending)
					value!();
			}
		}

		public void Dispose()
		{
			_stop.Cancel();
			try
			{
				_listening?.Wait(TimeSpan.FromSeconds(2));
			}
			catch (AggregateException)
			{
				// it was stopping anyway
			}
			_lockFile.Dispose();
			_stop.Dispose();
		}


		private async Task ListenAsync(CancellationToken stop)
		{
			while (!stop.IsCancellationRequested)
			{
				try
				{
					await using var pipe = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
					await pipe.WaitForConnectionAsync(stop);
					var request = new byte[1];
					if (await pipe.ReadAsync(request, stop) != 1 || request[0] != ShowRequest)
						continue;

					await pipe.WriteAsync(new[] { Showing }, stop);
					RequestShow();

					// Hanging up at once could drop the reply before the caller reads it, so wait for it to hang up first
					using var linger = CancellationTokenSource.CreateLinkedTokenSource(stop);
					linger.CancelAfter(TimeSpan.FromSeconds(2));
					try
					{
						await pipe.ReadAtLeastAsync(request, 1, throwOnEndOfStream: false, linger.Token);
					}
					catch (OperationCanceledException) when (!stop.IsCancellationRequested)
					{
						// the caller is slow to hang up; it has the reply or never will
					}
				}
				catch (OperationCanceledException)
				{
					return;
				}
				catch (Exception e) when (e is IOException or UnauthorizedAccessException)
				{
					// a caller that gave up halfway, or the pipe busy for a moment; listen again
					try
					{
						await Task.Delay(200, stop);
					}
					catch (OperationCanceledException)
					{
						return;
					}
				}
			}
		}

		private void RequestShow()
		{
			Action? show;
			lock (_gate)
			{
				show = _show;
				if (show == null)
					_pendingShow = true;
			}
			show?.Invoke();
		}

		/// <summary>One pipe per app data folder, matching the lock</summary>
		private static string PipeName(string folder)
		{
			var path = Path.GetFullPath(folder);
			if (!OperatingSystem.IsLinux())
				path = path.ToUpperInvariant();			// Windows and macOS ignore case in paths

			return "purity-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path)))[..16];
		}

		[DllImport("user32.dll")]
		private static extern bool AllowSetForegroundWindow(int processId);


		private const int AnyProcess = -1;
		private const byte ShowRequest = 1;
		private const byte Showing = 2;

		private readonly FileStream _lockFile;
		private readonly string _pipeName;
		private readonly CancellationTokenSource _stop = new();
		private readonly object _gate = new();
		private Task? _listening;
		private Action? _show;
		private bool _pendingShow;
	}
}
