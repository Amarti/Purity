using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Purity.WPF.ViewModels;


namespace Purity.WPF
{
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();

			((INotifyCollectionChanged)Periods.Items).CollectionChanged += PurityPeriodsCollectionChanged;

			var vm = new MainWindowViewModel();
			var rawData = DataSerializer.Deserialize();
			vm.InitData(rawData);

			DataContext = vm;
		}

		private void PurityPeriodsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (VisualTreeHelper.GetChildrenCount(Periods) > 0)
			{
				var border = (Border)VisualTreeHelper.GetChild(Periods, 0);
				var scrollViewer = (ScrollViewer)VisualTreeHelper.GetChild(border, 0);
				scrollViewer.ScrollToBottom();
			}
		}

		private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
		{
#if !DEBUG
			DataSerializer.Serialize(((MainWindowViewModel)DataContext).Data);
#endif
		}
	}
}
