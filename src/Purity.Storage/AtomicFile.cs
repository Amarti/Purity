using System;
using System.IO;


namespace Purity.Storage
{
	internal static class AtomicFile
	{
		/// <summary>
		/// Replaces a file so that a crash or a full disk at any moment leaves either the old
		/// content or the new, never a mix.
		/// </summary>
		/// <param name="files">File operations to use</param>
		/// <param name="path">The file to replace or create</param>
		/// <param name="content">The new content</param>
		/// <param name="mayReplace">Last check, made after the slow part and right before every rename attempt</param>
		/// <returns>Whether the file was replaced</returns>
		public static bool Replace(FileOperations files, string path, ReadOnlyMemory<byte> content, Func<bool> mayReplace)
		{
			// beside the target, because a rename is only atomic within one volume
			var temp = Path.Combine(Path.GetDirectoryName(path)!, $"~{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
			try
			{
				files.WriteDurably(temp, content, files.UnixMode(path));

				// checked on every attempt: whoever held the file may have been changing it
				var replaced = Retrying(files, () =>
				{
					if (!mayReplace())
						return false;

					files.Move(temp, path);
					return true;
				});

				if (replaced)
					RemoveLeftovers(files, path);
				return replaced;
			}
			finally
			{
				TryDelete(files, temp);
			}
		}

		/// <summary>
		/// Runs a read or a rename, giving a sync client or a virus scanner a moment to let go of
		/// the file. Anything still failing after that reaches the caller.
		/// </summary>
		public static T Retrying<T>(FileOperations files, Func<T> action)
		{
			for (var attempt = 0; ; attempt++)
			{
				try
				{
					return action();
				}
				catch (Exception e) when (attempt < RetryDelays.Length && e is IOException or UnauthorizedAccessException)
				{
					files.Pause(TimeSpan.FromMilliseconds(RetryDelays[attempt]));
				}
			}
		}


		/// <summary>
		/// A crash leaves its temporary file behind, and that holds a whole copy of the history.
		/// Only old ones go, because a fresh one may be another program's save in progress.
		/// </summary>
		private static void RemoveLeftovers(FileOperations files, string path)
		{
			var pattern = $"~{Path.GetFileName(path)}.*.tmp";
			var cutoff = DateTime.UtcNow - LeftoverAge;
			try
			{
				foreach (var leftover in Directory.GetFiles(Path.GetDirectoryName(path)!, pattern))
				{
					if (File.GetLastWriteTimeUtc(leftover) < cutoff)
						TryDelete(files, leftover);
				}
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				// tidying up must never fail a save that already succeeded
			}
		}

		private static void TryDelete(FileOperations files, string path)
		{
			try
			{
				files.Delete(path);
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				// a leftover temporary file is harmless, and must not hide the real outcome
			}
		}


		/// <summary>
		/// In milliseconds, about 2.6 seconds in all. Windows refuses to replace a file while anyone
		/// has it open, but most such locks last only milliseconds, so the first waits are short.
		/// </summary>
		private static readonly int[] RetryDelays = { 20, 40, 80, 160, 320, 500, 500, 500, 500 };

		private static readonly TimeSpan LeftoverAge = TimeSpan.FromDays(1);
	}
}
