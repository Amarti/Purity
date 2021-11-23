﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using MvvmCross.Commands;
using MvvmCross.ViewModels;


namespace Purity
{
	public class MainViewModel : MvxViewModel, IDisposable
	{
		public MainViewModel()
		{
			AddPeriodCommand = new MvxCommand(AddPeriod);
			RecalculateCommand = new MvxCommand(Recalculate);
			SaveCommand = new MvxCommand(Save);

			PurityPeriods = new ObservableCollection<PurityPeriodViewModel>();
		}


		public void Dispose()
		{}


		/// <summary>
		/// Initializes with raw data set and shows UI
		/// </summary>
		/// <param name="rawData">Raw data set</param>
		public void InitData(List<PurityPeriod> rawData)
		{
			Data = rawData.OrderBy(el => el.Begin).ToList();
			BakeData(false);
			foreach (var period in Data)
				PurityPeriods.Add(new PurityPeriodViewModel(period, this));
		}
		/// <summary>
		/// Performs consistency calculations on whole data set
		/// </summary>
		public void BakeData(bool full = true)
		{
			_recentPeriodsStreak.Clear();

			PurityPeriod lastPeriod = null;
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
		private void UpdateRecentPeriodsStreak(PurityPeriod a, PurityPeriod b)
		{
			if (a == null || b == null)
				return;

			var l = PurityPeriod.GetFullPeriodLength(a, b);
			if (l == 0)
				return;

			if (!_recentPeriodsStreak.Any() || _recentPeriodsStreak.Last() < l)
			{
				_recentPeriodsStreak.Clear();
				Trace.WriteLine($"{nameof(UpdateRecentPeriodsStreak)}: cleared recent period streaks");
			}
			_recentPeriodsStreak.Add(l);
			Trace.WriteLine($"{nameof(UpdateRecentPeriodsStreak)}: added recent period streak in {l} half-calendar days");
		}


		public IMvxCommand AddPeriodCommand { get; private set; }
		private void AddPeriod()
		{
			AddPeriod(DateTime.Today, DateTime.Today.AddDays(7));
		}
		public void AddPeriod(DateTime beg, DateTime end)
		{
			if (Data.Any(el => el.Begin == beg))
				return;

			var period = new PurityPeriod(beg, end);
			Data.Add(period);
			PurityPeriods.Add(new PurityPeriodViewModel(period, this));
			RefreshItems();
		}

		public IMvxCommand RecalculateCommand { get; private set; }
		private void Recalculate()
		{
			BakeData();
			RefreshItems();
		}
		public IMvxCommand SaveCommand { get; private set; }
		private void Save()
		{
			DataSerializer.Serialize(Data);
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
			PurityPeriods.Remove(PurityPeriods.First(el => el.SelectedBeginDate == period.Begin));
			if (_recentPeriodsStreak.Count != 0 && !period.SkipStreak)
				_recentPeriodsStreak.RemoveAt(_recentPeriodsStreak.Count - 1);
			if (_recentPeriodsStreak.Count == 0)
				BakeData(false);	// need to recalculate _recentPeriodsStreak

			RefreshItems();
		}
		private void RefreshItems()
		{
			foreach (var vm in PurityPeriods)
				vm.Refresh();
		}


		public ObservableCollection<PurityPeriodViewModel> PurityPeriods { get; set; }
		public bool IsSaveVisible
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

		public List<PurityPeriod> Data;
		private readonly List<int> _recentPeriodsStreak = new List<int>();
	}
}
