using System;
using System.Collections.ObjectModel;
using MvvmCross.Commands;
using MvvmCross.ViewModels;


namespace Purity
{
	public class PurityPeriodViewModel : MvxViewModel, IDisposable
	{
		public PurityPeriodViewModel(PurityPeriod period, MainViewModel owner)
		{
			_period = period;
			_owner = owner;
			SubEvents = new ObservableCollection<PurityEvent>(_period.SubEvents);

			ClosePeriodCommand = new MvxCommand(ClosePeriod);
			RemovePeriodCommand = new MvxCommand(DeletePeriod);
		}


		public void Dispose()
		{}


		public IMvxCommand RemovePeriodCommand { get; private set; }
		internal void DeletePeriod()
		{
			_owner.RemovePeriod(_period);
		}
		public IMvxCommand ClosePeriodCommand { get; private set; }
		internal void ClosePeriod()
		{
			_owner.ClosePeriod(_period);
			SubEvents.Clear();
			foreach (var p in _period.SubEvents)
				SubEvents.Add(p);
		}


		public DateTime SelectedBeginDate
		{
			get
			{
				return _period.BeginDate;
			}
			set
			{
				_period.BeginDate = value;
				RaisePropertyChanged(() => SelectedBeginDate);
			}
		}
		public bool SelectedBeginDateIsDarkHalfDay
		{
			get
			{
				return PurityEvent.IsAfterDark(_period.BeginDate);
			}
			set
			{
				if (value)
				{
					if (!PurityEvent.IsAfterDark(_period.BeginDate))
						_period.BeginDate = _period.BeginDate.AddHours(12);
				}
				else
				{
					if (PurityEvent.IsAfterDark(_period.BeginDate))
						_period.BeginDate = _period.BeginDate.AddHours(-12);
				}
				RaisePropertyChanged(() => SelectedBeginDateIsDarkHalfDay);
			}
		}
		public DateTime SelectedEndDate
		{
			get
			{
				return _period.EndDate;
			}
			set
			{
				_period.EndDate = value;
				RaisePropertyChanged(() => SelectedEndDate);
			}
		}
		public bool SelectedEndDateIsDarkHalfDay
		{
			get
			{
				return PurityEvent.IsAfterDark(_period.EndDate);
			}
			set
			{
				if (value)
				{
					if (!PurityEvent.IsAfterDark(_period.EndDate))
						_period.EndDate = _period.EndDate.AddHours(12);
				}
				else
				{
					if (PurityEvent.IsAfterDark(_period.EndDate))
						_period.EndDate = _period.EndDate.AddHours(-12);
				}
				RaisePropertyChanged(() => SelectedEndDateIsDarkHalfDay);
			}
		}
		public ObservableCollection<PurityEvent> SubEvents { get; private set; }

		private readonly PurityPeriod _period;
		private readonly MainViewModel _owner;
	}
}
