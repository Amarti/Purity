using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;


namespace Purity
{
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();

			((INotifyCollectionChanged)Periods.Items).CollectionChanged += PurityPeriodsCollectionChanged;

			_vm = new MainViewModel();
			var rawData = DataSerializer.Deserialize();
			_vm.InitData(rawData);

			DataContext = _vm;
		}

		private void PurityPeriodsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
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
			DataSerializer.Serialize(_vm.Data);
#endif
		}

		private readonly MainViewModel _vm;
	}
}
