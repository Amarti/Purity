using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using Purity.Avalonia.ViewModels;
using ReactiveUI;


namespace Purity.Avalonia.Views
{
	public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
	{
		public MainWindow()
		{
			InitializeComponent();

#if DEBUG
			this.AttachDevTools();
#endif

			//this.WhenActivated(d =>
			//{
			//	this.OneWayBind(DataContext as MainWindowViewModel,
			//	   vm => vm.PurityPeriods,
			//	   view => view.Periods.Items).DisposeWith(d);
			//});
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}


		private void WindowActivated(object sender, EventArgs e)
		{
			if (_periods == null)
			{
				_periods = this.FindControl<ListBox>("Periods");
				((INotifyCollectionChanged)_periods.Items).CollectionChanged += PurityPeriodsCollectionChanged;
				ScrollDown();
			}
		}
		private void PurityPeriodsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			ScrollDown();
		}
		private void ScrollDown()
		{
			_periods?.ScrollIntoView(_periods.ItemCount - 1);
		}

		private void WindowClosing(object sender, CancelEventArgs e)
		{
			if (ViewModel != null)
			{
				DataSerializer.SerializeSettings(ViewModel.Settings);
#if !DEBUG
				DataSerializer.SerializeData(ViewModel.Data, ViewModel.Settings.DataFilePath);
#endif
			}
		}


		private ListBox? _periods;
	}
}
