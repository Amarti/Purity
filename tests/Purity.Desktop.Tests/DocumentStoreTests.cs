using System.Text;
using Purity.Storage;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>The history as a document a phone's picker gave, and how such a place is told from a path</summary>
	public sealed class DocumentStoreTests
	{
		[Fact]
		public async Task ANewDocumentHoldsNothingAndTakesTheFirstSave()
		{
			var file = new MemoryDocument("pureData.json");
			using var store = new DocumentDataStore(file);

			Assert.Null(await store.LoadAsync());
			var saved = await store.SaveAsync(Bytes("[1]"), null);

			Assert.False(saved.IsConflict);
			Assert.Equal("[1]", Text(file.Content));
			Assert.Equal(saved.Version, (await store.LoadAsync())!.Version);
		}

		[Fact]
		public async Task ASaveOverAVersionItDidntSeeIsRefused()
		{
			var file = new MemoryDocument("pureData.json") { Content = Bytes("[1]") };
			using var store = new DocumentDataStore(file);
			var seen = (await store.LoadAsync())!.Version;
			file.Content = Bytes("[1,2]");						// another device saved meanwhile

			Assert.True((await store.SaveAsync(Bytes("[3]"), seen)).IsConflict);
			Assert.Equal("[1,2]", Text(file.Content));
		}

		[Fact]
		public async Task AShorterHistoryLeavesNothingOfTheLongerOne()
		{
			var file = new MemoryDocument("pureData.json") { Content = Bytes("[1,2,3,4]") };
			using var store = new DocumentDataStore(file);

			await store.SaveAsync(Bytes("[1]"), (await store.LoadAsync())!.Version);

			Assert.Equal("[1]", Text(file.Content));
		}

		[Fact]
		public async Task AProvidersOwnFailureIsOneTheSessionKnows()
		{
			var file = new MemoryDocument("pureData.json") { FailWith = new InvalidOperationException("The provider went away") };
			using var store = new DocumentDataStore(file);

			var failure = await Assert.ThrowsAsync<IOException>(() => store.LoadAsync());

			Assert.Contains("provider", failure.Message);
		}

		[Fact]
		public async Task AWriteThatDidntLandWholeIsAFailureNotASave()
		{
			var file = new MemoryDocument("pureData.json") { Keeps = written => written[..1] };
			using var store = new DocumentDataStore(file);

			await Assert.ThrowsAsync<IOException>(() => store.SaveAsync(Bytes("[1,2]"), null));
		}

		[Fact]
		public async Task ComingBackRaisesAChangeToLookFor()
		{
			using var store = new DocumentDataStore(new MemoryDocument("pureData.json"));
			var changes = 0;
			store.Changed += (_, _) => changes++;

			store.NoticeOutsideChange();

			Assert.Equal(1, changes);
			await Task.CompletedTask;
		}

		[Theory]
		[InlineData("C:\\Users\\someone\\Dropbox\\pureData.json", false)]
		[InlineData("/home/someone/pureData.json", false)]
		[InlineData("relative/pureData.json", false)]
		[InlineData("content://com.google.android.apps.docs.storage/document/x", false)]		// only Purity's own mark makes one
		[InlineData("document:YXZhLmFuZHJvaWQ=", true)]
		public void ADocumentIsToldFromAPathByPuritysOwnMark(string location, bool document) =>
			Assert.Equal(document, DataLocation.IsDocument(location));

		[Fact]
		public void ADocumentsBookmarkComesBackAsGiven()
		{
			var location = DataLocation.OfDocument("YXZh/bG90+dXM=");

			Assert.Equal("YXZh/bG90+dXM=", DataLocation.BookmarkOf(location));
			Assert.True(DataLocation.Same(location, DataLocation.OfDocument("YXZh/bG90+dXM=")));
			Assert.False(DataLocation.Same(location, DataLocation.OfDocument("other")));
		}


		private static byte[] Bytes(string text) => new UTF8Encoding(false).GetBytes(text);

		private static string Text(byte[] content) => new UTF8Encoding(false).GetString(content);
	}
}
