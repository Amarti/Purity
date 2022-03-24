using System;
using MvvmCross.Commands;
using MvvmCross.ViewModels;


namespace Purity.WPF.ViewModels
{
	public class SettingsWindowViewModel : MvxViewModel, IDisposable
	{
		public SettingsWindowViewModel(Settings settings, Action reportSaver, Action<string> dataSaver)
		{
			_settings = settings;
			_reportSaver = reportSaver;
			_dataSaver = dataSaver;
			SaveReportCommand = new MvxCommand(SaveReport);
			SaveDataCommand = new MvxCommand(SaveData);
		}


		public void Dispose()
		{}


		public IMvxCommand SaveReportCommand { get; }
		private void SaveReport()
		{
			_reportSaver.Invoke();
		}
		public IMvxCommand SaveDataCommand { get; }
		private void SaveData()
		{
			_dataSaver.Invoke(DataFilePath);
		}


		public string DataFilePath
		{
			get => _settings.DataFilePath;
			set
			{
				_settings.DataFilePath = value;
				RaisePropertyChanged(() => DataFilePath);
			}
		}

		public static bool IsDebug
		{
			get
			{
#if DEBUG
				return true;
#else
				return false;
#endif
			}
		}

		private readonly Settings _settings;
		private readonly Action _reportSaver;
		private readonly Action<string> _dataSaver;
	}
}
