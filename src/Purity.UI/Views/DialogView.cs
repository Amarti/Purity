using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;


namespace Purity.UI.Views
{
	/// <summary>
	/// A view shown over another until it finishes with a result, such as Settings or Reminders: a
	/// window of its own on the desktop, and a page or a sheet on Android.
	/// </summary>
	public class DialogView : UserControl
	{
		public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<DialogView, string?>(nameof(Title));


		/// <summary>What it's called, in the language on show</summary>
		public string? Title
		{
			get => GetValue(TitleProperty);
			set => SetValue(TitleProperty, value);
		}

		/// <summary>Raised once, when the view is done, with what it gives back</summary>
		public event EventHandler<object?>? Finished;

		/// <summary>Its width as a desktop window, as the window it began as had</summary>
		public double DesktopWidth { get; protected init; } = 400;

		/// <summary>Its height as a desktop window, or none for as tall as it needs</summary>
		public double? DesktopHeight { get; protected init; }


		/// <summary>
		/// What going back gives, as with a window's close box or Android's back gesture: the same
		/// as Cancel, unless the view says otherwise.
		/// </summary>
		public virtual object? CancelResult => null;

		/// <summary>Ends the view with this result</summary>
		protected void Finish(object? result)
		{
			if (_finished)
				return;

			_finished = true;
			Finished?.Invoke(this, result);
		}

		/// <summary>The person went back, or closed the window around the view</summary>
		public void Cancel() => Finish(CancelResult);


		private bool _finished;
	}


	/// <summary>Whatever can show a <see cref="DialogView"/> over itself: a desktop window, or the pages of the Android app</summary>
	public interface IDialogHost
	{
		/// <returns>What the view finished with</returns>
		Task<object?> ShowAsync(DialogView view);
	}


	public static class DialogHost
	{
		/// <summary>Shows a view over the one this control is in, through the nearest host above it</summary>
		public static Task<object?> ShowAsync(Visual from, DialogView view)
		{
			var host = from as IDialogHost ?? from.GetVisualAncestors().OfType<IDialogHost>().FirstOrDefault()
				?? throw new InvalidOperationException("Nothing above this control can show a view over it");
			return host.ShowAsync(view);
		}

		/// <inheritdoc cref="ShowAsync(Visual, DialogView)"/>
		public static async Task<T?> ShowAsync<T>(Visual from, DialogView view) => await ShowAsync(from, view) is T result ? result : default;
	}
}
