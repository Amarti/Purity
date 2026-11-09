using Avalonia;


namespace Purity.Desktop.Views
{
	/// <summary>
	/// The WPF window is taller than some laptop screens: a 15-inch MacBook Pro shows 900 points,
	/// less the menu bar and the Dock. There the window gets shorter to fit. Only the list of
	/// periods loses height, and it scrolls, so nothing goes out of reach. Since 2.2.0 the window
	/// can also be resized (T8), and it opens where it was, as long as that is still on a screen.
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
			Size(wanted, frame, available, MinimumHeight);

		/// <summary>The same for width, where the minimum is the window's own: its rows don't get narrower</summary>
		public static double Width(double wanted, double frame, double available, double minimum) =>
			Size(wanted, frame, available, minimum);

		/// <summary>
		/// Whether a window with its top-left corner here would still show its title bar on one of
		/// these screens. When a screen was taken away, the window goes back to the middle instead.
		/// </summary>
		/// <param name="corner">The window's saved top-left corner, in screen pixels</param>
		/// <param name="workingAreas">Each screen's area left for windows, in screen pixels</param>
		public static bool ShowsTitleBar(PixelPoint corner, IEnumerable<PixelRect> workingAreas)
		{
			var titleBar = new PixelPoint(corner.X + TitleBarReach, corner.Y + TitleBarReach / 4);
			return workingAreas.Any(el => el.Contains(titleBar));
		}


		private static double Size(double wanted, double frame, double available, double minimum) =>
			Math.Min(wanted, Math.Max(minimum, Math.Floor(available - frame)));

		/// <summary>How far into the window a point must be on a screen to grab the title bar with</summary>
		private const int TitleBarReach = 40;
	}
}
