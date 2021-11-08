using System.Windows;


namespace Purity
{
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();

			_vm = new MainViewModel();

			var rawData = DataSerializer.Deserialize();
			_vm.BakeData(rawData);

			DataContext = _vm;
		}

		private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			DataSerializer.Serialize(_vm.Data);
		}

		private readonly MainViewModel _vm;
	}
}
