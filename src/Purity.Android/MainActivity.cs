using Android.App;
using Android.Content.PM;
using Android.OS;
using Avalonia.Android;
using Purity.UI;


namespace Purity.Android
{
	/// <summary>
	/// Purity's one activity. A phone keeps it upright, as approved on 2026-11-14; a tablet turns it
	/// as it's held. It stays put through turning, resizing and a change of the system's look, which
	/// Avalonia lays out again by itself.
	/// </summary>
	[Activity(
		Label = "Purity",
		Theme = "@style/Purity.Theme",
		Icon = "@mipmap/ic_launcher",
		MainLauncher = true,
		LaunchMode = LaunchMode.SingleTask,
		ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize
			| ConfigChanges.UiMode | ConfigChanges.Density | ConfigChanges.Locale | ConfigChanges.LayoutDirection | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden)]
	public sealed class MainActivity : AvaloniaMainActivity
	{
		/// <summary>The activity on screen, which closing the app finishes</summary>
		internal static MainActivity? Current { get; private set; }


		protected override void OnCreate(Bundle? savedInstanceState)
		{
			RequestedOrientation = AppPlatform.IsPhone ? ScreenOrientation.Portrait : ScreenOrientation.FullUser;
			Current = this;
			base.OnCreate(savedInstanceState);
		}

		protected override void OnDestroy()
		{
			if (Current == this)
				Current = null;
			base.OnDestroy();
		}
	}
}
