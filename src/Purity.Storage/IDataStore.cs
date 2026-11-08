using System;
using System.Threading;
using System.Threading.Tasks;


namespace Purity.Storage
{
	/// <summary>
	/// Where the data file lives: a local or synced folder today, a cloud API later. Stores pass
	/// raw bytes, so none of them can change the file format.
	/// </summary>
	public interface IDataStore : IDisposable
	{
		/// <summary>Reads the current content, or returns null when nothing is stored yet</summary>
		Task<StoredData?> LoadAsync(CancellationToken cancellation = default);

		/// <summary>
		/// Replaces the content, unless it is no longer the version the caller last saw. A failed
		/// save leaves the stored content as it was.
		/// </summary>
		/// <param name="content">The new content</param>
		/// <param name="expectedVersion">The version the caller last saw, or null if it saw nothing stored</param>
		/// <param name="cancellation">Cancels the save</param>
		Task<SaveResult> SaveAsync(ReadOnlyMemory<byte> content, string? expectedVersion, CancellationToken cancellation = default);

		/// <summary>Raised when the content may have been changed from outside this store</summary>
		event EventHandler? Changed;
	}


	/// <param name="Content">The stored bytes</param>
	/// <param name="Version">A content hash for files, a rev or ETag for cloud APIs</param>
	public sealed record StoredData(byte[] Content, string Version);


	/// <param name="Version">The new version, or null when a newer version was found and nothing was saved</param>
	public sealed record SaveResult(string? Version)
	{
		public bool IsConflict => Version == null;

		public static readonly SaveResult Conflict = new((string?)null);
	}
}
