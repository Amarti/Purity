using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Purity.Storage;
using Purity.UI.Localization;
using Purity.UI.ViewModels;


namespace Purity.UI.Views
{
	/// <summary>The earlier versions of the history, newest first, to pick one to bring back. It finishes with the version picked, or null.</summary>
	public sealed class BackupsView : DialogView
	{
		public BackupsView(IReadOnlyList<BackupChoice> choices)
		{
			Title = Loc.T.EarlierVersions;
			DesktopWidth = 520;
			DesktopHeight = 380;

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
			list.DoubleTapped += (_, _) => { if (list.SelectedItem is BackupChoice choice) Finish(choice.Backup); };
			restore.Click += (_, _) => Finish((list.SelectedItem as BackupChoice)?.Backup);
			cancel.Click += (_, _) => Finish(null);

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
	}
}
