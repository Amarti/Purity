using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using Purity.Avalonia.ViewModels;


namespace Purity.Avalonia.Views
{
	public partial class SettingsWindow : ReactiveWindow<SettingsWindowViewModel>
	{
		public SettingsWindow()
		{
			InitializeComponent();
#if DEBUG
			this.AttachDevTools();
#endif
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}

		public void InitDataContext(Settings settings, Action reportSaver, Action<string> dataSaver)
		{
			DataContext = new SettingsWindowViewModel(this, settings, reportSaver, dataSaver);
		}
	}
}
