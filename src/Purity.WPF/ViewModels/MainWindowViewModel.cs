using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using MvvmCross.Commands;
using MvvmCross.ViewModels;
using Purity.ViewModels;
using Purity.WPF.Views;


namespace Purity.WPF.ViewModels
{
	public class MainWindowViewModel : MvxViewModel, IMainWindowViewModel, IDisposable
	{
		public MainWindowViewModel(Settings settings)
		{
			_impl = new MainWindowViewModelImpl<PurityPeriodViewModel>(settings, (period, owner) => new PurityPeriodViewModel(period, owner));

			OpenSettingsCommand = new MvxCommand(OpenSettings);
			AddPeriodCommand = new MvxCommand(_impl.AddPeriod);
			RecalculateCommand = new MvxCommand(_impl.Recalculate);
		}


		public void Dispose()
		{
			_impl.Dispose();
		}


		public void InitData()
		{
			_impl.InitData();
		}


		public ICommand OpenSettingsCommand { get; }
		private void OpenSettings()
		{
			var settingsCopy = new Settings(Settings);
			var w = new SettingsWindow(settingsCopy, () => DataSerializer.ExportPeriodsLengthsReport(Data), path => DataSerializer.SerializeData(Data, path));
			if (w.ShowDialog() == true)
				_impl.SetSetings(settingsCopy);
		}

		public ICommand AddPeriodCommand { get; }

		public ICommand RecalculateCommand { get; }

		public void AcceptPeriod(PurityPeriod period)
		{
			_impl.AcceptPeriod(period);
		}
		public void RemovePeriod(PurityPeriod period)
		{
			_impl.RemovePeriod(period);
		}

		public void SaveState()
		{
			_impl.SaveState();
		}


		public ObservableCollection<PurityPeriodViewModel> PurityPeriods => _impl.PurityPeriods;
		public Settings Settings => _impl.Settings;
		public List<PurityPeriod> Data => _impl.Data;
		private readonly MainWindowViewModelImpl<PurityPeriodViewModel> _impl;
	}
}
