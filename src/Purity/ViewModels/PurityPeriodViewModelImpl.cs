using System;
using System.Collections.ObjectModel;


namespace Purity.ViewModels
{
	public class PurityPeriodViewModelImpl : IPurityPeriodViewModel, IDisposable
	{
		public PurityPeriodViewModelImpl(PurityPeriod period, IMainWindowViewModel ownerVM)
		{
			_period = period;
			_ownerVM = ownerVM;
			SubEvents = new ObservableCollection<PurityEvent>(_period.SubEvents);
		}


		public void Dispose()
		{}


		public void UpdatePeriodLength()
		{
			var idx = _ownerVM.Data.IndexOf(_period);
			_periodFullLength = idx > 0 ? PurityPeriod.GetPeriodLength(_ownerVM.Data[idx - 1], _period) : 0;
		}

		public void Refresh()
		{
			SubEvents.Clear();
			foreach (var p in _period.SubEvents)
				SubEvents.Add(p);
		}


		public DateTime SelectedBeginDate
		{
			get => _period.Begin;
			set => _period.Begin = value;
		}
		public bool SelectedBeginDateIsAfterDusk
		{
			get => PurityEvent.IsDateAfterDusk(_period.Begin);
			set
			{
				if (value)
				{
					if (!PurityEvent.IsDateAfterDusk(_period.Begin))
						_period.Begin = _period.Begin.AddHours(12);
				}
				else
				{
					if (PurityEvent.IsDateAfterDusk(_period.Begin))
						_period.Begin = _period.Begin.AddHours(-12);
				}
			}
		}
		public DateTime SelectedEndDate
		{
			get => _period.End;
			set => _period.End = value;
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
			}
		}
		public bool SkipStreak
		{
			get => _period.SkipStreak;
			set => _period.SkipStreak = value;
		}
		public string SkipPeriodLength => $"Skip" + (_periodFullLength > 0 ? $" ({_periodFullLength})" : string.Empty);
		public bool IsClosed => _period.Closed;
		public bool IsLast => _ownerVM.Data.Count > 0 && _ownerVM.Data[^1] == _period;
		public ObservableCollection<PurityEvent> SubEvents { get; private set; }

		private readonly PurityPeriod _period;
		private readonly IMainWindowViewModel _ownerVM;
		private int _periodFullLength;
	}
}
