using Purity.Storage;


namespace Purity.Desktop.Tests
{
	/// <summary>Documents as a phone's picker gives them, held in memory, as a file in Google Drive would be</summary>
	internal sealed class MemoryDocuments : IDocuments
	{
		/// <summary>The documents by their name, which is also their bookmark</summary>
		public Dictionary<string, MemoryDocument> Files { get; } = new();

		public MemoryDocument Add(string name, byte[] content)
		{
			var file = new MemoryDocument(name) { Content = content };
			Files[name] = file;
			return file;
		}

		public static string LocationOf(string name) => DataLocation.OfDocument(name);

		public Task<IDataStore?> OpenAsync(string location) =>
			Task.FromResult<IDataStore?>(Files.TryGetValue(DataLocation.BookmarkOf(location), out var file) && !file.Deleted ? new DocumentDataStore(file) : null);

		public Task<bool> DeleteAsync(string location)
		{
			if (!Files.TryGetValue(DataLocation.BookmarkOf(location), out var file) || file.FailWith != null)
				return Task.FromResult(false);

			file.Delete();
			return Task.FromResult(true);
		}

		public Task<string?> NameAsync(string location) =>
			Task.FromResult(Files.TryGetValue(DataLocation.BookmarkOf(location), out var file) ? file.Name : null);
	}


	/// <summary>One document. What's written lands when the stream closes, as a provider uploads it then.</summary>
	internal sealed class MemoryDocument(string name) : IDocumentFile
	{
		public string Name => name;

		public byte[] Content { get; set; } = Array.Empty<byte>();

		/// <summary>A provider's own failure, thrown by every call while set, as Android's are Java's</summary>
		public Exception? FailWith { get; set; }

		/// <summary>What the provider keeps of a write, to stand for one that loses some of it</summary>
		public Func<byte[], byte[]>? Keeps { get; set; }

		public bool Deleted { get; private set; }

		public void Delete()
		{
			Deleted = true;
			Content = Array.Empty<byte>();
		}

		public Task<Stream> OpenReadAsync()
		{
			Fail();
			return Task.FromResult<Stream>(new MemoryStream(Content, writable: false));
		}

		public Task<Stream> OpenWriteAsync()
		{
			Fail();
			return Task.FromResult<Stream>(new WriteBack(this));
		}


		private void Fail()
		{
			if (FailWith != null)
				throw FailWith;
		}


		private sealed class WriteBack(MemoryDocument file) : MemoryStream
		{
			protected override void Dispose(bool disposing)
			{
				if (disposing)
				{
					var written = ToArray();
					file.Content = file.Keeps?.Invoke(written) ?? written;
				}
				base.Dispose(disposing);
			}
		}
	}
}
