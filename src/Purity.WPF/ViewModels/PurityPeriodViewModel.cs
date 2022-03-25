using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using MvvmCross.Commands;
using MvvmCross.ViewModels;
using Purity.ViewModels;


namespace Purity.WPF.ViewModels
{
	public class PurityPeriodViewModel : MvxViewModel, IPurityPeriodViewModel, IDisposable
	{
		public PurityPeriodViewModel(PurityPeriod period, IMainWindowViewModel ownerVM)
		{
			_period = period;
			_ownerVM = ownerVM;
			_impl = new PurityPeriodViewModelImpl(_period, _ownerVM);
			UpdatePeriodLength();

			SelectedBeginDateHalfDayCommand = new MvxCommand(SelectedBeginDateHalfDay);
			SelectedEndDateHalfDayCommand = new MvxCommand(SelectedEndDateHalfDay);
			AcceptPeriodCommand = new MvxCommand(AcceptPeriod);
			RemovePeriodCommand = new MvxCommand(RemovePeriod);
		}


		public void Dispose()
		{
			_impl.Dispose();
		}


		private void UpdatePeriodLength()
		{
			_impl.UpdatePeriodLength();
			RaisePropertyChanged(() => SkipPeriodLength);
		}

		public void Refresh()
		{
			RaiseAllPropertiesChanged();
			_impl.Refresh();
		}

		public ICommand SelectedBeginDateHalfDayCommand { get; }
		internal void SelectedBeginDateHalfDay()
		{
			SelectedBeginDateIsAfterDusk = !SelectedBeginDateIsAfterDusk;
		}
		public ICommand SelectedEndDateHalfDayCommand { get; }
		internal void SelectedEndDateHalfDay()
		{
			SelectedEndDateIsAfterDusk = !SelectedEndDateIsAfterDusk;
		}
		public ICommand AcceptPeriodCommand { get; }
		internal void AcceptPeriod()
		{
			_ownerVM.AcceptPeriod(_period);
			UpdatePeriodLength();
			Refresh();
		}
		public IMvxCommand RemovePeriodCommand { get; private set; }
		internal void RemovePeriod()
		{
			if (!IsClosed
			||	 MessageBox.Show("Are you sure you want to remove this period?", LogEntry.ProductName,
								 MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
				_ownerVM.RemovePeriod(_period);
		}


		public DateTime SelectedBeginDate
		{
			get => _impl.SelectedBeginDate;
			set
			{
				_impl.SelectedBeginDate = value;
				UpdatePeriodLength();
				RaisePropertyChanged(() => SelectedBeginDate);
			}
		}
		public bool SelectedBeginDateIsAfterDusk
		{
			get => _impl.SelectedBeginDateIsAfterDusk;
			set
			{
				_impl.SelectedBeginDateIsAfterDusk = value;
				UpdatePeriodLength();
				RaisePropertyChanged(() => SelectedBeginDateIsAfterDusk);
			}
		}
		public DateTime SelectedEndDate
		{
			get => _impl.SelectedEndDate;
			set
			{
				_impl.SelectedEndDate = value;
				RaisePropertyChanged(() => SelectedEndDate);
			}
		}
		public bool SelectedEndDateIsAfterDusk
		{
			get => _impl.SelectedEndDateIsAfterDusk;
			set
			{
				_impl.SelectedEndDateIsAfterDusk = value;
				RaisePropertyChanged(() => SelectedEndDateIsAfterDusk);
			}
		}
		public bool SkipStreak
		{
			get => _impl.SkipStreak;
			set
			{
				_impl.SkipStreak = value;
				RaisePropertyChanged(() => SkipStreak);
			}
		}
		public string SkipPeriodLength => _impl.SkipPeriodLength;
		public bool IsClosed => _impl.IsClosed;
		public bool IsLast => _impl.IsLast;
		public ObservableCollection<PurityEvent> SubEvents => _impl.SubEvents;

		private readonly PurityPeriod _period;
		private readonly IMainWindowViewModel _ownerVM;
		private readonly PurityPeriodViewModelImpl _impl;
	}
}
