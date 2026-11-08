using System;
using System.IO;
using System.Threading;


namespace Purity.Storage
{
	/// <summary>
	/// The few file operations storage needs, overridable so tests can make each one fail the way
	/// a full disk, a locked file or a power cut would.
	/// </summary>
	internal class FileOperations
	{
		/// <summary>The file's content, or null when it doesn't exist</summary>
		public virtual byte[]? Read(string path)
		{
			try
			{
				using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
				using var content = new MemoryStream();
				stream.CopyTo(content);
				return content.ToArray();
			}
			catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
			{
				return null;
			}
		}

		/// <summary>Creates a new file and returns only once its content has reached the disk</summary>
		/// <param name="path">The file to create</param>
		/// <param name="content">The content</param>
		/// <param name="mode">Unix permissions to apply, or null to keep the file private to its owner</param>
		public virtual void WriteDurably(string path, ReadOnlyMemory<byte> content, UnixFileMode? mode)
		{
			var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
			if (!OperatingSystem.IsWindows())
				options.UnixCreateMode = PrivateFile;

			using var stream = new FileStream(path, options);
			if (mode != null && !OperatingSystem.IsWindows())
				File.SetUnixFileMode(stream.SafeFileHandle, mode.Value);

			stream.Write(content.Span);
			stream.Flush(flushToDisk: true);
		}

		/// <summary>Unix permissions of an existing file, or null where there are none to keep</summary>
		public virtual UnixFileMode? UnixMode(string path) =>
			!OperatingSystem.IsWindows() && File.Exists(path) ? File.GetUnixFileMode(path) : null;

		/// <summary>Renames a file over the target in one step, so readers see one or the other</summary>
		public virtual void Move(string source, string target) => File.Move(source, target, overwrite: true);

		public virtual void Delete(string path) => File.Delete(path);

		public virtual void Pause(TimeSpan time) => Thread.Sleep(time);


		private const UnixFileMode PrivateFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
	}
}
