﻿using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using ReactiveUI;


namespace Purity.Avalonia.ViewModels
{
	public class PurityPeriodViewModel : ViewModelBase
	{
		public PurityPeriodViewModel(PurityPeriod period, MainWindowViewModel owner)
		{
			_period = period;
			_owner = owner;
			UpdateFullPeriodLength();
			SubEvents = new ObservableCollection<PurityEvent>(_period.SubEvents);

			SelectedBeginDateHalfDayCommand = ReactiveCommand.Create(SelectedBeginDateHalfDay);
			SelectedEndDateHalfDayCommand = ReactiveCommand.Create(SelectedEndDateHalfDay);
			AcceptPeriodCommand = ReactiveCommand.Create(AcceptPeriod);
			RemovePeriodCommand = ReactiveCommand.Create(RemovePeriod);
		}


		private void UpdateFullPeriodLength()
		{
			var idx = _owner.Data.IndexOf(_period);
			_periodFullLength = idx > 0 ? PurityPeriod.GetFullPeriodLength(_owner.Data[idx - 1], _period) : 0;
			this.RaisePropertyChanged(nameof(SkipPeriodLength));
		}

		public void Refresh()
		{
			this.RaisePropertyChanged(nameof(SkipPeriodLength));
			this.RaisePropertyChanged(nameof(IsClosed));
			this.RaisePropertyChanged(nameof(IsLast));

			SubEvents.Clear();
			foreach (var p in _period.SubEvents)
				SubEvents.Add(p);
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
			_owner.AcceptPeriod(_period);
			UpdateFullPeriodLength();
			Refresh();
		}
		public ICommand RemovePeriodCommand { get; }
		internal void RemovePeriod()
		{
			_owner.RemovePeriod(_period);
		}


		public DateTimeOffset SelectedBeginDate
		{
			get => new(_period.Begin);
			set
			{
				_period.Begin = new DateTime(value.Ticks);
				UpdateFullPeriodLength();
				this.RaisePropertyChanged(nameof(SelectedBeginDate));
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
				this.RaisePropertyChanged(nameof(SelectedBeginDateIsAfterDusk));
			}
		}
		public DateTimeOffset SelectedEndDate
		{
			get => new(_period.End);
			set
			{
				_period.End = new DateTime(value.Ticks);
				this.RaisePropertyChanged(nameof(SelectedEndDate));
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
				this.RaisePropertyChanged(nameof(SelectedEndDateIsAfterDusk));
			}
		}
		public bool SkipStreak
		{
			get => _period.SkipStreak;
			set
			{
				_period.SkipStreak = value;
				this.RaisePropertyChanged(nameof(SkipStreak));
			}
		}
		public string SkipPeriodLength => $"Skip" + (_periodFullLength > 0 ? $" ({_periodFullLength})" : string.Empty);
		public bool IsClosed => _period.Closed;
		public bool IsLast => _owner.Data.Count > 0 && _owner.Data[^1] == _period;
		public ObservableCollection<PurityEvent> SubEvents { get; private set; }

		private readonly PurityPeriod _period;
		private readonly MainWindowViewModel _owner;
		private int _periodFullLength;
	}
}