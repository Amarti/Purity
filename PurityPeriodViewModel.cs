﻿using System;
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
			UpdateFullPeriodLength();
			SubEvents = new ObservableCollection<PurityEvent>(_period.SubEvents);

			ClosePeriodCommand = new MvxCommand(ClosePeriod);
			RemovePeriodCommand = new MvxCommand(DeletePeriod);
		}


		public void Dispose()
		{}


		private void UpdateFullPeriodLength()
		{
			var idx = _owner.Data.IndexOf(_period);
			_periodFullLength = idx > 0 ? PurityPeriod.GetFullPeriodLength(_owner.Data[idx - 1], _period) : 0;
			RaisePropertyChanged(() => SkipPeriodLength);
		}

		public void Refresh()
		{
			RaiseAllPropertiesChanged();
			SubEvents.Clear();
			foreach (var p in _period.SubEvents)
				SubEvents.Add(p);
		}

		public IMvxCommand RemovePeriodCommand { get; private set; }
		internal void DeletePeriod()
		{
			_owner.RemovePeriod(_period);
		}
		public IMvxCommand ClosePeriodCommand { get; private set; }
		internal void ClosePeriod()
		{
			_owner.ClosePeriod(_period);
			Refresh();
		}


		public DateTime SelectedBeginDate
		{
			get
			{
				return _period.Begin;
			}
			set
			{
				_period.Begin = value;
				UpdateFullPeriodLength();
				RaisePropertyChanged(() => SelectedBeginDate);
			}
		}
		public bool SelectedBeginDateIsDarkHalfDay
		{
			get
			{
				return PurityEvent.IsDateAfterDark(_period.Begin);
			}
			set
			{
				if (value)
				{
					if (!PurityEvent.IsDateAfterDark(_period.Begin))
						_period.Begin = _period.Begin.AddHours(12);
				}
				else
				{
					if (PurityEvent.IsDateAfterDark(_period.Begin))
					{
						_period.Begin = _period.Begin.AddHours(-12);
						UpdateFullPeriodLength();
					}
				}
				RaisePropertyChanged(() => SelectedBeginDateIsDarkHalfDay);
			}
		}
		public DateTime SelectedEndDate
		{
			get
			{
				return _period.End;
			}
			set
			{
				_period.End = value;
				RaisePropertyChanged(() => SelectedEndDate);
			}
		}
		public bool SelectedEndDateIsDarkHalfDay
		{
			get
			{
				return PurityEvent.IsDateAfterDark(_period.End);
			}
			set
			{
				if (value)
				{
					if (!PurityEvent.IsDateAfterDark(_period.End))
						_period.End = _period.End.AddHours(12);
				}
				else
				{
					if (PurityEvent.IsDateAfterDark(_period.End))
						_period.End = _period.End.AddHours(-12);
				}
				RaisePropertyChanged(() => SelectedEndDateIsDarkHalfDay);
			}
		}
		public bool SkipStreak
		{
			get
			{
				return _period.SkipStreak;
			}
			set
			{
				_period.SkipStreak = value;
				RaisePropertyChanged(() => SkipStreak);
			}
		}
		public string SkipPeriodLength => $"Skip" + (_periodFullLength > 0 ? $" ({_periodFullLength})" : string.Empty);
		public bool IsClosed => _period.Closed;
		public bool IsLast => _owner.Data.Count > 0 && _owner.Data[^1] == _period;
		public ObservableCollection<PurityEvent> SubEvents { get; private set; }

		private readonly PurityPeriod _period;
		private readonly MainViewModel _owner;
		private int _periodFullLength;
	}
}
