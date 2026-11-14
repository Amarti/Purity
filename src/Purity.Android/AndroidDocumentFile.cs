using Avalonia.Platform.Storage;
using Purity.UI;


namespace Purity.Android
{
	/// <summary>
	/// A picked document, read and written through Android itself. Avalonia writes in Android's "w"
	/// mode, which on the phone's own storage writes over the start of a document and leaves the rest,
	/// so a shorter history would keep the end of the longer one. "wt" empties it first. A provider
	/// that doesn't know "wt" gets Avalonia's way, and the store reads back every save to be sure.
	/// </summary>
	internal sealed class AndroidDocumentFile(IStorageFile file) : IDocumentFile
	{
		public Task<Stream> OpenReadAsync() =>
			Task.FromResult(Resolver.OpenInputStream(Address) ?? throw new IOException("The document's provider gave nothing to read"));

		public Task<Stream> OpenWriteAsync()
		{
			try
			{
				return Task.FromResult(Resolver.OpenOutputStream(Address, "wt") ?? throw new IOException("The document's provider gave nowhere to write"));
			}
			catch (Java.Lang.Exception)
			{
				return file.OpenWriteAsync();
			}
		}


		private global::Android.Net.Uri Address => global::Android.Net.Uri.Parse(file.Path.AbsoluteUri)!;

		private static global::Android.Content.ContentResolver Resolver => global::Android.App.Application.Context.ContentResolver!;
	}
}
