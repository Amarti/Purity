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

			_vm = new MainViewModel();

			var rawData = DataSerializer.Deserialize();
			//_vm.PurityPeriods.CollectionChanged += PurityPeriodsCollectionChanged;

			_vm.InitData(rawData);

			DataContext = _vm;

		}

		private void PurityPeriodsCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			Periods.SelectedIndex = Periods.Items.Count - 1;
			Periods.ScrollIntoView(Periods.SelectedItem);
			//if (VisualTreeHelper.GetChildrenCount(Periods) > 0)
			//{
			//	var border = (Border)VisualTreeHelper.GetChild(Periods, 0);
			//	var scrollViewer = (ScrollViewer)VisualTreeHelper.GetChild(border, 0);
			//	scrollViewer.ScrollToBottom();
			//}
		}

		private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			DataSerializer.Serialize(_vm.Data);
		}

		private readonly MainViewModel _vm;
	}
}
