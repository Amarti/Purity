using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Purity.UI.Views;


namespace Purity.Desktop.Views
{
	/// <summary>
	/// A window around a view shown over another, such as Settings or Reminders: at the size its
	/// window always had, over its owner, finishing when the view does or the window is closed.
	/// </summary>
	public sealed class DialogWindow : Window, IDialogHost
	{
		private DialogWindow(DialogView view)
		{
			View = view;
			Content = view;
			Icon = AppIcon;
			CanResize = false;
			WindowStartupLocation = WindowStartupLocation.CenterOwner;
			Width = view.DesktopWidth;
			if (view.DesktopHeight is { } height)
				Height = height;
			else
				SizeToContent = SizeToContent.Height;
			Bind(TitleProperty, view.GetObservable(DialogView.TitleProperty));
			var direction = Loc.Instance.FollowFlowDirection(this);
			Closed += (_, _) => direction.Dispose();

			// The close box answers as Cancel does, and finishing the view closes the window
			view.Finished += (_, result) =>
			{
				if (!_closing)
					Close(result);
			};
			Closing += (_, _) =>
			{
				_closing = true;
				view.Cancel();
			};
		}


		/// <summary>The app's icon, for every window's title bar</summary>
		public static WindowIcon AppIcon => new(AssetLoader.Open(new Uri("avares://Purity.UI/Assets/Purity.ico")));

		public DialogView View { get; }


		/// <summary>A window for the view, to show over its owner</summary>
		public static DialogWindow For(DialogView view) => new(view);

		/// <summary>Another view over this one, as Reminders opens over Settings</summary>
		public Task<object?> ShowAsync(DialogView view) => For(view).ShowDialog<object?>(this);


		private bool _closing;
	}
}
