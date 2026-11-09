using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Purity.Desktop.Localization;


namespace Purity.Desktop.Views
{
	/// <summary>A plain message box, in place of the WPF app's MessageBox</summary>
	public sealed class MessageWindow : Window
	{
		private MessageWindow(string text, IReadOnlyList<string> buttons)
		{
			Title = "Purity";
			FlowDirection = Loc.Instance.FlowDirection;
			SizeToContent = SizeToContent.WidthAndHeight;
			CanResize = false;
			WindowStartupLocation = WindowStartupLocation.CenterOwner;
			ShowInTaskbar = false;

			var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
			for (var i = 0; i < buttons.Count; i++)
			{
				var index = i;
				var button = new Button { Content = buttons[i], MinWidth = 75, Padding = new Thickness(10, 3), IsDefault = i == 0, IsCancel = i == buttons.Count - 1 };
				button.Click += (_, _) => Close(index);
				row.Children.Add(button);
			}

			var panel = new StackPanel { Margin = new Thickness(20), Spacing = 20 };
			panel.Children.Add(new TextBlock { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 460 });
			panel.Children.Add(row);
			Content = panel;
		}


		/// <returns>The index of the button pressed, or null when the window was closed without one</returns>
		public static async Task<int?> ShowAsync(Window owner, string text, params string[] buttons) =>
			await new MessageWindow(text, buttons).ShowDialog<int?>(owner);
	}
}
