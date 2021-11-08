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
				return _period.Begin.Stamp;
			}
			set
			{
				_period.Begin.Stamp = value;
				RaisePropertyChanged(() => SelectedBeginDate);
			}
		}
		public bool SelectedBeginDateAfterNoon
		{
			get
			{
				return _period.Begin.Stamp.Hour == 12;
			}
			set
			{
				if (value)
				{
					if (_period.Begin.Stamp.Hour == 0)
						_period.Begin.Stamp = _period.Begin.Stamp.AddHours(12);
				}
				else
				{
					if (_period.Begin.Stamp.Hour == 12)
						_period.Begin.Stamp = _period.Begin.Stamp.AddHours(-12);
				}
				RaisePropertyChanged(() => SelectedBeginDateAfterNoon);
			}
		}
		public DateTime SelectedEndDate
		{
			get
			{
				return _period.End.Stamp;
			}
			set
			{
				_period.End.Stamp = value;
				RaisePropertyChanged(() => SelectedEndDate);
			}
		}
		public bool SelectedEndDateAfterNoon
		{
			get
			{
				return _period.End.Stamp.Hour == 12;
			}
			set
			{
				if (value)
				{
					if (_period.End.Stamp.Hour == 0)
						_period.End.Stamp = _period.End.Stamp.AddHours(12);
				}
				else
				{
					if (_period.End.Stamp.Hour == 12)
						_period.End.Stamp = _period.End.Stamp.AddHours(-12);
				}
				RaisePropertyChanged(() => SelectedEndDateAfterNoon);
			}
		}
		public ObservableCollection<PurityEvent> SubEvents { get; private set; }

		private readonly PurityPeriod _period;
		private readonly MainViewModel _owner;
	}
}
