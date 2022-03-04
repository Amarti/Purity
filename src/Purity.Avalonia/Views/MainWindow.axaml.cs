using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Purity.Avalonia.ViewModels;


namespace Purity.Avalonia.Views
{
	public partial class MainWindow : Window
	{
		public MainWindow()
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

		//		private void PurityPeriodsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		//		{
		//			//if (VisualTreeHelper.GetChildrenCount(Periods) > 0)
		//			//{
		//			//	var border = (Border)VisualTreeHelper.GetChild(Periods, 0);
		//			//	var scrollViewer = (ScrollViewer)VisualTreeHelper.GetChild(border, 0);
		//			//	scrollViewer.ScrollToBottom();
		//			//}
		//		}

		private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
		{
#if !DEBUG
			DataSerializer.Serialize(_vm.Data);
#endif
		}

		//		private readonly MainWindowViewModel _vm;
	}
}
