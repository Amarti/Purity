using System;
using System.Collections.ObjectModel;
using System.Windows;
using MvvmCross.Commands;
using MvvmCross.ViewModels;


namespace Purity.WPF.ViewModels
{
	public class PurityPeriodViewModel : MvxViewModel, IDisposable
	{
		public PurityPeriodViewModel(PurityPeriod period, MainWindowViewModel ownerVM)
		{
			_period = period;
			_ownerVM = ownerVM;
			UpdateFullPeriodLength();
			SubEvents = new ObservableCollection<PurityEvent>(_period.SubEvents);

			SelectedBeginDateHalfDayCommand = new MvxCommand(SelectedBeginDateHalfDay);
			SelectedEndDateHalfDayCommand = new MvxCommand(SelectedEndDateHalfDay);
			AcceptPeriodCommand = new MvxCommand(AcceptPeriod);
			RemovePeriodCommand = new MvxCommand(RemovePeriod);
		}


		public void Dispose()
		{}


		private void UpdateFullPeriodLength()
		{
			var idx = _ownerVM.Data.IndexOf(_period);
			_periodFullLength = idx > 0 ? PurityPeriod.GetFullPeriodLength(_ownerVM.Data[idx - 1], _period) : 0;
			RaisePropertyChanged(() => SkipPeriodLength);
		}

		public void Refresh()
		{
			RaiseAllPropertiesChanged();
			SubEvents.Clear();
			foreach (var p in _period.SubEvents)
				SubEvents.Add(p);
		}

		public IMvxCommand SelectedBeginDateHalfDayCommand { get; }
		internal void SelectedBeginDateHalfDay()
		{
			SelectedBeginDateIsAfterDusk = !SelectedBeginDateIsAfterDusk;
		}
		public IMvxCommand SelectedEndDateHalfDayCommand { get; }
		internal void SelectedEndDateHalfDay()
		{
			SelectedEndDateIsAfterDusk = !SelectedEndDateIsAfterDusk;
		}
		public IMvxCommand AcceptPeriodCommand { get; }
		internal void AcceptPeriod()
		{
			_ownerVM.AcceptPeriod(_period);
			UpdateFullPeriodLength();
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
			get => _period.Begin;
			set
			{
				_period.Begin = value;
				UpdateFullPeriodLength();
				RaisePropertyChanged(() => SelectedBeginDate);
			}
		}
		public bool SelectedBeginDateIsAfterDusk
		{
			get => PurityEvent.IsDateAfterDusk(_period.Begin);
			set
			{
				if (value)
				{
					if (!PurityEvent.IsDateAfterDusk(_period.Begin))
					{
						_period.Begin = _period.Begin.AddHours(12);
						UpdateFullPeriodLength();
					}
				}
				else
				{
					if (PurityEvent.IsDateAfterDusk(_period.Begin))
					{
						_period.Begin = _period.Begin.AddHours(-12);
						UpdateFullPeriodLength();
					}
				}
				RaisePropertyChanged(() => SelectedBeginDateIsAfterDusk);
			}
		}
		public DateTime SelectedEndDate
		{
			get => _period.End;
			set
			{
				_period.End = value;
				RaisePropertyChanged(() => SelectedEndDate);
			}
		}
		public bool SelectedEndDateIsAfterDusk
		{
			get => PurityEvent.IsDateAfterDusk(_period.End);
			set
			{
				if (value)
				{
					if (!PurityEvent.IsDateAfterDusk(_period.End))
						_period.End = _period.End.AddHours(12);
				}
				else
				{
					if (PurityEvent.IsDateAfterDusk(_period.End))
						_period.End = _period.End.AddHours(-12);
				}
				RaisePropertyChanged(() => SelectedEndDateIsAfterDusk);
			}
		}
		public bool SkipStreak
		{
			get => _period.SkipStreak;
			set
			{
				_period.SkipStreak = value;
				RaisePropertyChanged(() => SkipStreak);
			}
		}
		public string SkipPeriodLength => $"Skip" + (_periodFullLength > 0 ? $" ({_periodFullLength})" : string.Empty);
		public bool IsClosed => _period.Closed;
		public bool IsLast => _ownerVM.Data.Count > 0 && _ownerVM.Data[^1] == _period;
		public ObservableCollection<PurityEvent> SubEvents { get; private set; }

		private readonly PurityPeriod _period;
		private readonly MainWindowViewModel _ownerVM;
		private int _periodFullLength;
	}
}
