using System;
using MvvmCross.Commands;
using MvvmCross.ViewModels;


namespace Purity.WPF.ViewModels
{
	public class SettingsWindowViewModel : MvxViewModel, IDisposable
	{
		public SettingsWindowViewModel(Settings settings, Action<string> dataSaver)
		{
			_settings = settings;
			_dataSaver = dataSaver;
			SaveDataCommand = new MvxCommand(SaveData);
		}


		public void Dispose()
		{}


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
		private readonly Action<string> _dataSaver;
	}
}
