using Avalonia.Media;


namespace Purity.Desktop
{
	/// <summary>
	/// The app's font. WPF uses Segoe UI, which every Windows has, and macOS keeps its own system
	/// font. Linux usually defaults to DejaVu Sans, about a sixth wider, which cuts off the long
	/// dates in the date boxes. Liberation Sans is within a few percent of Segoe UI, so Linux uses
	/// it whenever it's installed; the Linux package asks for it.
	/// </summary>
	internal static class AppFont
	{
		public const string ResourceKey = "AppFontFamily";
		public const string LinuxFamily = "Liberation Sans";

		public static FontFamily Choose()
		{
			if (OperatingSystem.IsLinux() && FontManager.Current.SystemFonts.Any(el => el.Name == LinuxFamily))
				return new FontFamily(LinuxFamily);

			return FontFamily.Default;
		}
	}
}
