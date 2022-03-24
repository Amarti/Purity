using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Purity.WPF.ViewModels;


namespace Purity.WPF.Views
{
	public partial class SettingsWindow : Window
	{
		public SettingsWindow(Settings settings, Action reportSaver, Action<string> dataSaver)
		{
			InitializeComponent();

			DataContext = new SettingsWindowViewModel(settings, reportSaver, dataSaver);
		}

		private void PickFile_OnClick(object sender, EventArgs e)
		{
			var dialog = new OpenFileDialog() { InitialDirectory = Environment.CurrentDirectory };
			if (dialog.ShowDialog() == true)
			{
				tbFilePath.Text = dialog.FileName;
				// Since setting the property explicitly bypasses the data binding, 
				// we must explicitly update it by calling BindingExpression.UpdateSource()
				tbFilePath.GetBindingExpression(TextBox.TextProperty).UpdateSource();
			}
		}
		private void Ok_OnClick(object sender, EventArgs e)
		{
			DialogResult = true;
			Close();
		}
		private void Cancel_OnClick(object sender, EventArgs e)
		{
			DialogResult = false;
			Close();
		}
	}
}
