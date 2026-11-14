namespace Purity.UI
{
	public enum DeviceKind
	{
		Desktop,

		/// <summary>Under 600 dp at its narrowest: the compact layout, upright only</summary>
		Phone,

		/// <summary>The desktop's layout, turned either way, with Settings and the like as sheets</summary>
		Tablet,
	}


	/// <summary>What Purity runs on, as each app says when it starts. It decides the layout, and the picture for "same as the system".</summary>
	public static class AppPlatform
	{
		public static DeviceKind Device { get; set; } = DeviceKind.Desktop;

		public static bool IsPhone => Device == DeviceKind.Phone;

		/// <summary>A phone or a tablet, which have no windows, files by path, or calendar files to save</summary>
		public static bool IsMobile => Device != DeviceKind.Desktop;
	}
}
