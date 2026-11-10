using Avalonia.Platform.Storage;
using Purity.Desktop.Localization;


namespace Purity.Desktop.Views
{
	/// <summary>The system's file dialogs, for choosing a data file or a new place for one</summary>
	internal static class DataFilePicker
	{
		/// <returns>The path of a data file the person already has, or null</returns>
		public static async Task<string?> OpenAsync(IStorageProvider storage)
		{
			var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
			{
				Title = Loc.T.DataFileTitle,
				FileTypeFilter = new[] { DataFiles(), FilePickerFileTypes.All },
			});
			return files.Count > 0 ? files[0].TryGetLocalPath() : null;
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
			return file?.TryGetLocalPath();
		}


		private static FilePickerFileType DataFiles() => new(Loc.T.DataFileType) { Patterns = new[] { "*.json" } };
	}
}
