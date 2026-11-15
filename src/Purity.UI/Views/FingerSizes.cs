using Avalonia.Controls;
using Avalonia.LogicalTree;


namespace Purity.UI.Views
{
	/// <summary>
	/// On a phone, the desktop's controls, sized for a mouse, grow tall enough for a finger, and the
	/// fixed rows that hold them grow with them. Only heights change, so the layout stays the one
	/// approved for the desktop.
	/// </summary>
	internal static class FingerSizes
	{
		public static void Apply(Control root)
		{
			foreach (var control in root.GetLogicalDescendants().OfType<Control>().Prepend(root))
			{
				if (control.Height == MouseField)
				{
					control.Height = FingerField;
					if (control is TextBox box)
						box.VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center;		// text at the top suits only the desktop's one-line height
				}
				else if (control is Button && control.Height == MouseButton)
					control.Height = FingerButton;

				if (control is Grid grid)
					foreach (var row in grid.RowDefinitions)
						if (row.Height.IsAbsolute && row.Height.Value >= MouseButton)
							row.Height = new GridLength(row.Height.Value + FingerButton - MouseButton);
			}
		}


		private const double MouseField = 23;
		private const double FingerField = 32;
		private const double MouseButton = 30;
		private const double FingerButton = 40;
	}
}
