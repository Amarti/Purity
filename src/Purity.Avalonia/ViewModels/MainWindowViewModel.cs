using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Controls;
using Purity.Avalonia.Views;
using Purity.ViewModels;
using ReactiveUI;


namespace Purity.Avalonia.ViewModels
{
	public class MainWindowViewModel : ViewModelBase, IMainWindowViewModel, IDisposable
	{
		public MainWindowViewModel(Window ownerWindow, Settings settings)
		{
			_ownerWindow = ownerWindow;
			_impl = new MainWindowViewModelImpl<PurityPeriodViewModel>(settings, (period, owner) => new PurityPeriodViewModel(period, owner, _ownerWindow));

			OpenSettingsCommand = ReactiveCommand.Create(OpenSettings);
			AddPeriodCommand = ReactiveCommand.Create(_impl.AddPeriod);
			RecalculateCommand = ReactiveCommand.Create(_impl.Recalculate);
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
		private async void OpenSettings()
		{
			var settingsCopy = new Settings(Settings);
			var w = new SettingsWindow();
			w.InitDataContext(settingsCopy, () => DataSerializer.ExportPeriodsLengthsReport(Data), path => DataSerializer.SerializeData(Data, path));
			if (await w.ShowDialog<bool?>(_ownerWindow) == true)
				_impl.SetSettings(settingsCopy);
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
		private readonly Window _ownerWindow;
		private readonly MainWindowViewModelImpl<PurityPeriodViewModel> _impl;
	}
}
