using System.Security.Cryptography;
using Avalonia.Platform.Storage;
using Purity.Storage;


namespace Purity.UI
{
	/// <summary>Documents a picker gave, kept by a lasting permission: on Android, files in Google Drive, Dropbox or on the phone</summary>
	public interface IDocuments
	{
		/// <summary>The store for one, or null when it can't be reached any more, as when its permission was taken back</summary>
		Task<IDataStore?> OpenAsync(string location);

		/// <returns>False when it couldn't be deleted, or reached</returns>
		Task<bool> DeleteAsync(string location);

		/// <summary>Its name, as the person saw it in the picker, or null when it can't be reached</summary>
		Task<string?> NameAsync(string location);
	}


	/// <summary>Documents through Avalonia's storage, which on Android is the system's picker and its lasting permissions</summary>
	/// <param name="storage">The window's storage, or null before there is one</param>
	public sealed class StorageDocuments(Func<IStorageProvider?> storage) : IDocuments
	{
		/// <summary>
		/// How a platform reads and writes a document, where it knows better than Avalonia: Android
		/// empties a document before writing it only when asked to
		/// </summary>
		public static Func<IStorageFile, IDocumentFile>? ForThisPlatform { get; set; }


		public async Task<IDataStore?> OpenAsync(string location) =>
			await FileAsync(location) is { } file ? new DocumentDataStore(ForThisPlatform?.Invoke(file) ?? new StorageDocumentFile(file)) : null;

		public async Task<bool> DeleteAsync(string location)
		{
			try
			{
				if (await FileAsync(location) is not { } file)
					return false;

				await file.DeleteAsync();
				if (file is IStorageBookmarkItem kept)
					await kept.ReleaseBookmarkAsync();
				return true;
			}
			catch (Exception e) when (e is not OperationCanceledException)
			{
				return false;							// a provider can fail in ways of its own, and all of them mean it's still there
			}
		}


		public async Task<string?> NameAsync(string location) => (await FileAsync(location))?.Name;


		private async Task<IStorageFile?> FileAsync(string location)
		{
			try
			{
				return storage() is { } provider ? await provider.OpenFileBookmarkAsync(DataLocation.BookmarkOf(location)) : null;
			}
			catch (Exception e) when (e is not OperationCanceledException)
			{
				return null;
			}
		}
	}


	/// <summary>A document as the store needs it: its two streams. Avalonia keeps its own storage types for itself.</summary>
	public interface IDocumentFile
	{
		Task<Stream> OpenReadAsync();

		/// <summary>A stream that replaces what the document holds</summary>
		Task<Stream> OpenWriteAsync();
	}


	/// <summary>A document Avalonia opened, as the store needs it</summary>
	internal sealed class StorageDocumentFile(IStorageFile file) : IDocumentFile
	{
		public Task<Stream> OpenReadAsync() => file.OpenReadAsync();

		public Task<Stream> OpenWriteAsync() => file.OpenWriteAsync();
	}


	/// <summary>
	/// The data file as a document a picker gave. Nothing can be swapped in at once there, so a save
	/// writes over it in place, and reads it back to be sure; the backups in Purity's own storage cover
	/// a write cut short. Changes made elsewhere can't be watched, so they're looked for when Purity
	/// comes back to the front. The version is a hash of the content, as a file's is.
	/// </summary>
	public sealed class DocumentDataStore(IDocumentFile file) : IDataStore
	{
		public event EventHandler? Changed;


		/// <summary>The document may have changed elsewhere, as it can whenever Purity was in the background</summary>
		public void NoticeOutsideChange() => Changed?.Invoke(this, EventArgs.Empty);

		/// <summary>An empty document holds nothing yet, as a new one from the picker doesn't</summary>
		public async Task<StoredData?> LoadAsync(CancellationToken cancellation = default)
		{
			var content = await ReadAsync(cancellation);
			return content.Length == 0 ? null : new StoredData(content, VersionOf(content));
		}

		public async Task<SaveResult> SaveAsync(ReadOnlyMemory<byte> content, string? expectedVersion, CancellationToken cancellation = default)
		{
			var now = await ReadAsync(cancellation);
			if ((now.Length == 0 ? null : VersionOf(now)) != expectedVersion)
				return SaveResult.Conflict;

			await Translated(async () =>
			{
				await using var stream = await file.OpenWriteAsync();
				if (stream.CanSeek)
					stream.SetLength(0);					// a shorter history mustn't leave the end of the longer one behind
				await stream.WriteAsync(content, cancellation);
				await stream.FlushAsync(cancellation);
				return true;
			});

			if (!(await ReadAsync(cancellation)).AsSpan().SequenceEqual(content.Span))
				throw new IOException("The document holds something other than what was just written to it");
			return new SaveResult(VersionOf(content.Span));
		}

		public void Dispose()
		{}


		private static string VersionOf(ReadOnlySpan<byte> content) => Convert.ToHexString(SHA256.HashData(content));

		private Task<byte[]> ReadAsync(CancellationToken cancellation) => Translated(async () =>
		{
			await using var stream = await file.OpenReadAsync();
			using var copy = new MemoryStream();
			await stream.CopyToAsync(copy, cancellation);
			return copy.ToArray();
		});

		/// <summary>A provider's own failures, which on Android are Java's, as the one kind the session knows means "couldn't"</summary>
		private static async Task<T> Translated<T>(Func<Task<T>> work)
		{
			try
			{
				return await work();
			}
			catch (Exception e) when (e is not (OperationCanceledException or IOException or UnauthorizedAccessException))
			{
				throw new IOException(e.Message, e);
			}
		}
	}
}
