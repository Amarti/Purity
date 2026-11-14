using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;


namespace Purity.UI
{
	/// <summary>The look every Purity app shares. Each app's own subclass opens its first window or view.</summary>
	public partial class App : Application
	{
		public override void Initialize()
		{
			AvaloniaXamlLoader.Load(this);
			Resources[AppFont.ResourceKey] = AppFont.Family;
			if (AppPlatform.IsMobile)
				ShowTheDeviceForTheSystemsLook();
		}

		/// <summary>"The same as the system" pictures the device in hand: a phone or a tablet, rather than the desktop's screen</summary>
		private void ShowTheDeviceForTheSystemsLook()
		{
			var device = AppPlatform.IsPhone ? "SameAsPhoneIconDrawingImage" : "SameAsTabletIconDrawingImage";
			if (this.TryGetResource(device, null, out var picture))
				Resources["SameAsSystemIconDrawingImage"] = picture;
		}

		/// <summary>Light, dark, or whatever the system uses (tweak T7). Every window follows at once.</summary>
		public static void Apply(Appearance appearance)
		{
			if (Current is { } app)
				app.RequestedThemeVariant = appearance switch
				{
					Appearance.Light => ThemeVariant.Light,
					Appearance.Dark => ThemeVariant.Dark,
					_ => ThemeVariant.Default,
				};
		}
	}


	/// <summary>Command line options</summary>
	/// <param name="DataFile">A data file to open instead of the one in the settings, for this run only</param>
	/// <param name="PictureFile">
	/// For checks on machines CI can't reach: save a picture of the window here, with a report
	/// beside it, then quit. Only allowed together with <paramref name="DataFile"/>.
	/// </param>
	/// <param name="GoogleStandIn">Debug builds only: use the test stand-in for Google at this address</param>
	public sealed record StartOptions(string? DataFile, string? PictureFile = null, Uri? GoogleStandIn = null)
	{
		/// <summary>Reads <c>--data &lt;file&gt;</c> and <c>--picture &lt;file&gt;</c>, and ignores anything else, such as the arguments older macOS adds</summary>
		public static StartOptions Parse(IReadOnlyList<string> args)
		{
			string? dataFile = null, pictureFile = null;
			Uri? googleStandIn = null;
			for (var i = 0; i < args.Count - 1; i++)
			{
				if (args[i] == "--data")
					dataFile = args[i + 1];
				else if (args[i] == "--picture")
					pictureFile = args[i + 1];
#if DEBUG
				else if (args[i] == "--google-stand-in")
					googleStandIn = new Uri(args[i + 1]);
#endif
			}

			return new StartOptions(dataFile, pictureFile, googleStandIn);
		}
	}
}
