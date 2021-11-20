﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using MvvmCross.Commands;
using MvvmCross.ViewModels;


namespace Purity
{
	public class MainViewModel : MvxViewModel, IDisposable
	{
		public MainViewModel()
		{
			_hec = new HebrewCalendar();

			// TDOO: tests -> hebrew day starts aligned with greg day from 00:00, so no relation to evening -> if i want to make next hebrew date, i need to add +1 day if time is 12:00

			//var culture = CultureInfo.CreateSpecificCulture("he-IL");
			//culture.DateTimeFormat.Calendar = _hec;
			////Thread.CurrentThread.CurrentCulture = culture;
			//var tm = DateTime.Now;
			//Trace.WriteLine($"{_hec.GetMonth(tm)}, {_hec.GetDayOfMonth(tm)}");
			//tm = tm.AddHours(12);
			//Trace.WriteLine($"{_hec.GetMonth(tm)}, {_hec.GetDayOfMonth(tm)}");

			////if (tm.Hour != 0)    // TODO: check
			////	tm.AddDays(1);
			//tm = _hec.AddMonths(tm, 1);   // adding full hebrew month
			//Trace.WriteLine(string.Format("{0,12}{1,15:MMM}", _hec.GetMonth(tm), tm));

			AddPeriodCommand = new MvxCommand(AddPeriod);
			RecalculateCommand = new MvxCommand(Recalculate);
			SaveCommand = new MvxCommand(Save);

			PurityPeriods = new ObservableCollection<PurityPeriodViewModel>();
			// for testing purposes only
			//PurityPeriods.Add(new PurityPeriodViewModel(new PurityPeriod(), this));
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
						period.Commit(_hec, _recentPeriodsStreak);
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
			var period = new PurityPeriod(beg, end);
			Data.Add(period);
			PurityPeriods.Add(new PurityPeriodViewModel(period, this));
		}

		public IMvxCommand RecalculateCommand { get; private set; }
		private void Recalculate()
		{
			PurityPeriods.Clear();
			BakeData();
			foreach (var period in Data)
				PurityPeriods.Add(new PurityPeriodViewModel(period, this));
		}
		public IMvxCommand SaveCommand { get; private set; }
		private void Save()
		{
			DataSerializer.Serialize(Data);
		}

		public void RemovePeriod(PurityPeriod period)
		{
			if (Data.Count == 0)
				return;

			if (period == Data.Last())
			{
				Data.Remove(period);
				PurityPeriods.Remove(PurityPeriods.First(el => el.SelectedBeginDate == period.Begin));
				if (_recentPeriodsStreak.Count != 0)
					_recentPeriodsStreak.RemoveAt(_recentPeriodsStreak.Count - 1);
				if (_recentPeriodsStreak.Count == 0)
					BakeData(false);   // need to recalculate _recentPeriodsStreak
			}
		}
		public void ClosePeriod(PurityPeriod period)
		{
			if (period != null && period.End != DateTime.MinValue)
			{
				if (Data[^1] == period)
				{
					if (_recentPeriodsStreak.Count > 0)
						_recentPeriodsStreak.Remove(_recentPeriodsStreak[^1]);
					var lastPeriod = Data.Count > 1 ? Data[^2] : null;
					UpdateRecentPeriodsStreak(lastPeriod, period);
					period.Commit(_hec, _recentPeriodsStreak);
					period.Closed = true;

					// refreshing UI
					var vm = PurityPeriods.First(el => el.SelectedBeginDate == period.Begin);
					var id = PurityPeriods.IndexOf(vm);
					PurityPeriods.Remove(vm);
					PurityPeriods.Insert(id, new PurityPeriodViewModel(period, this));
				}
				else
					Recalculate();
			}
		}


		public ObservableCollection<PurityPeriodViewModel> PurityPeriods { get; set; }

		public List<PurityPeriod> Data;
		private readonly HebrewCalendar _hec;
		private readonly List<int> _recentPeriodsStreak = new List<int>();
	}
}
