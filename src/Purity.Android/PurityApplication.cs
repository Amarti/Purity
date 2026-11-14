using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Avalonia.Media;
using Purity.UI;


namespace Purity.Android
{
	/// <summary>
	/// The Android app. It's left out of Android's own backups on purpose: they would copy the history's
	/// backups and the sign-ins to the cloud, where Purity can't remove them.
	/// </summary>
	[Application(Label = "Purity", Icon = "@mipmap/ic_launcher", RoundIcon = "@mipmap/ic_launcher_round", SupportsRtl = true, AllowBackup = false)]
	public sealed class PurityApplication : AvaloniaAndroidApplication<PhoneApp>
	{
		public PurityApplication(IntPtr handle, JniHandleOwnership ownership)
			: base(handle, ownership)
		{}


		public override void OnCreate()
		{
			// Before Avalonia starts, since the look depends on it: under 600 dp at its narrowest is a phone
			var narrowest = Resources?.Configuration?.SmallestScreenWidthDp ?? 0;
			AppPlatform.Device = narrowest < 600 ? DeviceKind.Phone : DeviceKind.Tablet;
			SecretStores.ForThisPlatform = file => new AndroidSecretStore(file);
			StorageDocuments.ForThisPlatform = file => new AndroidDocumentFile(file);
			base.OnCreate();
		}


		protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
			base.CustomizeAppBuilder(builder)
				.With(new FontManagerOptions { DefaultFamilyName = AppFont.Source });		// for what sits outside the pages: drop-downs and the like
	}
}
