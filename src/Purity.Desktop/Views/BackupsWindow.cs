using Avalonia;
using Avalonia.Controls;
using Purity.Desktop.Localization;
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
			Title = Loc.T.EarlierVersions;
			FlowDirection = Loc.Instance.FlowDirection;
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
			var restore = new Button { Content = Loc.T.RestoreEllipsis, MinWidth = 90, Padding = new Thickness(10, 3), IsEnabled = false, IsDefault = true };
			var cancel = new Button { Content = Loc.T.Cancel, MinWidth = 90, Padding = new Thickness(10, 3), IsCancel = true };
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
					? Loc.T.NoBackupsYet
					: Loc.T.ChooseBackup,
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
