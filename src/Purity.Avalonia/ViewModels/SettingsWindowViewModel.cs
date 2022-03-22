using System;
using System.Windows.Input;
using Avalonia.Controls;
using ReactiveUI;


namespace Purity.Avalonia.ViewModels
{
	public class SettingsWindowViewModel : ViewModelBase
	{
		public SettingsWindowViewModel(Window owner, Settings settings, Action<string> dataSaver)
		{
			_owner = owner;
			_settings = settings;
			_dataSaver = dataSaver;
			PickFileCommand = ReactiveCommand.Create(PickFile);
			SaveDataCommand = ReactiveCommand.Create(SaveData);
			OkCommand = ReactiveCommand.Create(Ok);
			CancelCommand = ReactiveCommand.Create(Cancel);
		}


		public void Dispose()
		{}


		public ICommand PickFileCommand { get; }
		private async void PickFile()
		{
			var dialog = new OpenFileDialog() { Directory = Environment.CurrentDirectory };
			var res = await dialog.ShowAsync(_owner);
			if (res != null && res.Length > 0)
				DataFilePath = res[0];
		}
		public ICommand SaveDataCommand { get; }
		private void SaveData()
		{
			_dataSaver.Invoke(DataFilePath);
		}
		public ICommand OkCommand { get; }
		private void Ok()
		{
			_owner.Close(true);
		}
		public ICommand CancelCommand { get; }
		private void Cancel()
		{
			_owner.Close(false);
		}


		public string DataFilePath
		{
			get => _settings.DataFilePath;
			set
			{
				_settings.DataFilePath = value;
				this.RaisePropertyChanged(nameof(DataFilePath));
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

		private readonly Window _owner;
		private readonly Settings _settings;
		private readonly Action<string> _dataSaver;
	}
}
