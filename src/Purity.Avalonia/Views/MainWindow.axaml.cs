using System.Collections.Specialized;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using Purity.Avalonia.ViewModels;
using ReactiveUI;


namespace Purity.Avalonia.Views
{
	public partial class MainWindow : /*ReactiveWindow<MainWindowViewModel>*/Window
	{
		public MainWindow()
		{
			//this.WhenActivated(d =>
			//{
			//	this.OneWayBind(DataContext as MainWindowViewModel,
			//	   vm => vm.PurityPeriods,
			//	   view => view.Periods.Items).DisposeWith(d);
			//});

			InitializeComponent();
#if DEBUG
			this.AttachDevTools();
#endif
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}

		private void PurityPeriodsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			//if (VisualTreeHelper.GetChildrenCount(Periods) > 0)
			//{
			//	var border = (Border)VisualTreeHelper.GetChild(Periods, 0);
			//	var scrollViewer = (ScrollViewer)VisualTreeHelper.GetChild(border, 0);
			//	scrollViewer.ScrollToBottom();
			//}
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

		//public ListBox Periods => this.FindControl<ListBox>("Periods");
		//		private readonly MainWindowViewModel _vm;
	}
}
