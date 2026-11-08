namespace Purity.Desktop.Views
{
	/// <summary>
	/// The WPF window is taller than some laptop screens: a 15-inch MacBook Pro shows 900 points,
	/// less the menu bar and the Dock. There the window gets shorter to fit. Only the list of
	/// periods loses height, and it scrolls, so nothing goes out of reach.
	/// </summary>
	public static class WindowFit
	{
		/// <summary>Never shorter than this, so a few periods always show</summary>
		public const double MinimumHeight = 400;

		/// <param name="wanted">The window's own height, inside its frame</param>
		/// <param name="frame">What the title bar and borders add</param>
		/// <param name="available">The screen's height left for windows</param>
		/// <returns>The height inside the frame that fits, or <paramref name="wanted"/> when it already fits</returns>
		public static double Height(double wanted, double frame, double available) =>
			Math.Min(wanted, Math.Max(MinimumHeight, Math.Floor(available - frame)));
	}
}
