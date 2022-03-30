using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using NLog;


namespace Purity.ViewModels
{
	public class MainWindowViewModelImpl<T> : IMainWindowViewModel, IDisposable where T : class, IPurityPeriodViewModel
	{
		public MainWindowViewModelImpl(Settings settings, Func<PurityPeriod, IMainWindowViewModel, T> periodVMCreator)
		{
			Settings = settings;
			_periodVMCreator = periodVMCreator;
			Data = new List<PurityPeriod>();
			PurityPeriods = new ObservableCollection<T>();
		}


		public void Dispose()
		{}


		/// <summary>
		/// Initializes with raw data set
		/// </summary>
		public void InitData()
		{
			var rawData = DataSerializer.DeserializeData(Settings.DataFilePath);
			Data = rawData.OrderBy(el => el.Begin).ToList();
			BakeData(false);

			PurityPeriods.Clear();
			foreach (var period in Data)
				PurityPeriods.Add(_periodVMCreator.Invoke(period, this));
		}
		/// <summary>
		/// Performs consistency calculations on whole data set
		/// </summary>
		private void BakeData(bool full = true)
		{
			_recentPeriodsStreak.Clear();

			PurityPeriod? lastPeriod = null;
			foreach (var period in Data)
			{
				UpdateRecentPeriodsStreak(lastPeriod, period);
				if (full)
				{
					period.SubEvents.Clear();
					if (period.End != DateTime.MinValue && period.Closed)
						period.Commit(CultureHolder.Instance.HebrewCalendar, _recentPeriodsStreak);
				}
				lastPeriod = period;
			}
		}
		private void UpdateRecentPeriodsStreak(PurityPeriod? a, PurityPeriod b)
		{
			if (a == null || b == null)
				return;

			var l = PurityPeriod.GetPeriodLength(a, b);
			if (l == 0)
				return;

			if (!_recentPeriodsStreak.Any() || _recentPeriodsStreak.Last() < l)
			{
				_recentPeriodsStreak.Clear();
				Logger.Debug($"{nameof(UpdateRecentPeriodsStreak)}: cleared recent period streaks");
			}
			_recentPeriodsStreak.Add(l);
			Logger.Debug($"{nameof(UpdateRecentPeriodsStreak)}: added recent period streak in {l} half-calendar days");
		}


		public void AddPeriod()
		{
			AddPeriod(DateTime.Today, DateTime.Today.AddDays(7));
		}
		private void AddPeriod(DateTime beg, DateTime end)
		{
			if (Data.Any(el => el.Begin == beg))
				return;

			var period = new PurityPeriod(beg, end);
			Data.Add(period);
			PurityPeriods.Add(_periodVMCreator.Invoke(period, this));
			RefreshItems();
		}

		public void Recalculate()
		{
			BakeData();
			RefreshItems();
		}

		public void AcceptPeriod(PurityPeriod period)
		{
			if (period == null || period.End == DateTime.MinValue)
				return;

			period.Closed = true;

			Data = Data.OrderBy(el => el.Begin).ToList();
			BakeData();

			var lvm = PurityPeriods.OrderBy(vm => vm.SelectedBeginDate).ToArray();
			PurityPeriods.Clear();
			foreach (var vm in lvm)
				PurityPeriods.Add(vm);
		}
		public void RemovePeriod(PurityPeriod period)
		{
			if (Data.Count == 0 || period != Data.Last())
				return;

			Data.Remove(period);
			if (_recentPeriodsStreak.Count != 0 && !period.SkipStreak)
				_recentPeriodsStreak.RemoveAt(_recentPeriodsStreak.Count - 1);
			if (_recentPeriodsStreak.Count == 0)
				BakeData(false);	// need to recalculate _recentPeriodsStreak

			PurityPeriods.Remove(PurityPeriods.First(el => el.SelectedBeginDate == period.Begin));
			RefreshItems();
		}
		private void RefreshItems()
		{
			foreach (var vm in PurityPeriods)
				vm.Refresh();
		}

		public void SetSettings(Settings settings)
		{
			Settings = settings;
			InitData();
		}
		public void SaveState()
		{
			DataSerializer.SerializeSettings(Settings);
#if !DEBUG
			DataSerializer.SerializeData(Data, Settings.DataFilePath);
#endif
		}


		public ObservableCollection<T> PurityPeriods { get; set; }

		public Settings Settings { get; private set; }
		private readonly Func<PurityPeriod, IMainWindowViewModel, T> _periodVMCreator;

		public List<PurityPeriod> Data { get; private set; }
		private readonly List<int> _recentPeriodsStreak = new();

		private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
	}
}
