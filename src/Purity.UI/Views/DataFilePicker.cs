using Avalonia.Platform.Storage;
using Purity.UI.Localization;


namespace Purity.UI.Views
{
	/// <summary>
	/// The system's file dialogs, for choosing a data file or a new place for one. A computer's dialog
	/// gives a path. A phone's picker gives a document instead, such as a file in Google Drive, kept by
	/// a lasting permission whose address stands in for the path.
	/// </summary>
	internal static class DataFilePicker
	{
		/// <returns>Where a data file the person already has is, or null</returns>
		public static async Task<string?> OpenAsync(IStorageProvider storage) => (await OpenWithNameAsync(storage))?.Location;

		/// <returns>Where a data file the person already has is, with its name as the picker showed it, or null</returns>
		public static async Task<(string Location, string Name)?> OpenWithNameAsync(IStorageProvider storage)
		{
			var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
			{
				Title = Loc.T.DataFileTitle,
				FileTypeFilter = new[] { DataFiles(), FilePickerFileTypes.All },
			});
			return files.Count > 0 && await LocationOfAsync(files[0]) is { } location ? (location, files[0].Name) : null;
		}

		/// <param name="name">The data file's name, which the new place suggests too</param>
		/// <returns>Where the person wants the data file to go, or null</returns>
		public static async Task<string?> ChooseNewPlaceAsync(IStorageProvider storage, string name)
		{
			var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
			{
				Title = Loc.T.MoveToTitle,
				SuggestedFileName = name,
				DefaultExtension = "json",
				FileTypeChoices = new[] { DataFiles() },
			});
			return file == null ? null : await LocationOfAsync(file);
		}


		private static async Task<string?> LocationOfAsync(IStorageFile file) =>
			file.TryGetLocalPath() ?? (file.CanBookmark && await file.SaveBookmarkAsync() is { } bookmark ? DataLocation.OfDocument(bookmark) : null);

		private static FilePickerFileType DataFiles() => new(Loc.T.DataFileType) { Patterns = new[] { "*.json" } };
	}
}
