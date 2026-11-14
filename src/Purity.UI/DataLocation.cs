namespace Purity.UI
{
	/// <summary>
	/// Where a history is kept. On a computer, a file's path. On a phone, a document the system's
	/// picker gave, such as a file in Google Drive or Dropbox, kept by a lasting permission: Avalonia
	/// gives a bookmark for it, which Purity keeps after "document:", so no path is ever taken for one.
	/// </summary>
	public static class DataLocation
	{
		public static bool IsDocument(string location) => location.StartsWith(DocumentPrefix, StringComparison.Ordinal);

		/// <summary>The location of a document, from the bookmark Avalonia gave for it</summary>
		public static string OfDocument(string bookmark) => DocumentPrefix + bookmark;

		/// <summary>The bookmark Avalonia opens a document by</summary>
		public static string BookmarkOf(string location) => location[DocumentPrefix.Length..];

		/// <summary>Whether two locations are the same history</summary>
		public static bool Same(string a, string b)
		{
			if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
				return false;
			if (IsDocument(a) || IsDocument(b))
				return string.Equals(a.Trim(), b.Trim(), StringComparison.Ordinal);

			var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
			return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), comparison);
		}


		private const string DocumentPrefix = "document:";
	}
}
