using Avalonia.Media;


namespace Purity.Desktop
{
	/// <summary>
	/// The app's font: Heebo, whose Hebrew and Latin letters are designed to match. It's built into
	/// the app, so Windows, macOS and Linux all show the same letters in the same widths, whatever
	/// fonts the computer has.
	/// </summary>
	internal static class AppFont
	{
		public const string ResourceKey = "AppFontFamily";
		public const string Source = "avares://Purity/Assets/Fonts#Heebo";

		public static FontFamily Family { get; } = new(Source);
	}
}
