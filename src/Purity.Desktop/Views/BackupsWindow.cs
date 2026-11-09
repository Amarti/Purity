using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Purity.Desktop.ViewModels;
using Purity.Storage;


namespace Purity.Desktop.Views
{
	/// <summary>The earlier versions of the history, newest first, to pick one to bring back</summary>
	public sealed class BackupsWindow : Window
	{
		private BackupsWindow(IReadOnlyList<BackupChoice> choices)
		{
			Title = "Earlier versions";
			Width = 520;
			Height = 380;
			CanResize = false;
			WindowStartupLocation = WindowStartupLocation.CenterOwner;
			ShowInTaskbar = false;

			var list = new ListBox
			{
				ItemsSource = choices,
				ItemTemplate = new FuncDataTemplate<BackupChoice>((choice, _) => new TextBlock { Text = choice.Label, TextWrapping = Avalonia.Media.TextWrapping.Wrap }),
				SelectionMode = SelectionMode.Single,
			};
			ScrollViewer.SetHorizontalScrollBarVisibility(list, Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled);
			var restore = new Button { Content = "Restore...", MinWidth = 90, Padding = new Thickness(10, 3), IsEnabled = false, IsDefault = true };
			var cancel = new Button { Content = "Cancel", MinWidth = 90, Padding = new Thickness(10, 3), IsCancel = true };
			list.SelectionChanged += (_, _) => restore.IsEnabled = list.SelectedItem != null;
			list.DoubleTapped += (_, _) => { if (list.SelectedItem is BackupChoice choice) Close(choice.Backup); };
			restore.Click += (_, _) => Close((list.SelectedItem as BackupChoice)?.Backup);
			cancel.Click += (_, _) => Close(null);

			var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
			buttons.Children.Add(restore);
			buttons.Children.Add(cancel);

			var intro = new TextBlock
			{
				Text = choices.Count == 0
					? "This computer has no earlier versions yet. One is kept each time your history changes."
					: "Each time your history changed, this computer kept the version it replaced. Choose the one to bring back:",
				TextWrapping = Avalonia.Media.TextWrapping.Wrap,
			};

			var layout = new DockPanel { Margin = new Thickness(20), LastChildFill = true };
			DockPanel.SetDock(intro, Dock.Top);
			DockPanel.SetDock(buttons, Dock.Bottom);
			intro.Margin = new Thickness(0, 0, 0, 10);
			buttons.Margin = new Thickness(0, 12, 0, 0);
			layout.Children.Add(intro);
			layout.Children.Add(buttons);
			layout.Children.Add(list);
			Content = layout;
		}


		/// <returns>The version picked, or null</returns>
		public static Task<Backup?> ShowAsync(Window owner, IReadOnlyList<BackupChoice> choices) =>
			new BackupsWindow(choices).ShowDialog<Backup?>(owner);
	}
}
