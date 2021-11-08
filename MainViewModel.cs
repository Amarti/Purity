using System;
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

			LoadData();

			// TDOO: tests -> hebrew day starts aligned with greg day from 00:00, so no relation to evening -> if i want to make next hebrew date, i need to add +1 day if time is 12:00

			var culture = CultureInfo.CreateSpecificCulture("he-IL");
			culture.DateTimeFormat.Calendar = _hec;
			//Thread.CurrentThread.CurrentCulture = culture;
			var tm = DateTime.Now;
			Trace.WriteLine($"{_hec.GetMonth(tm)}, {_hec.GetDayOfMonth(tm)}");
			tm = tm.AddHours(12);
			Trace.WriteLine($"{_hec.GetMonth(tm)}, {_hec.GetDayOfMonth(tm)}");

			//if (tm.Hour != 0)    // TODO: check
			//	tm.AddDays(1);
			tm = _hec.AddMonths(tm, 1);   // adding full hebrew month
			Trace.WriteLine(string.Format("{0,12}{1,15:MMM}", _hec.GetMonth(tm), tm));

			AddPeriodCommand = new MvxCommand(AddPeriod);

			PurityPeriods = new ObservableCollection<PurityPeriodViewModel>();
			// for testing purposes only
			//PurityPeriods.Add(new PurityPeriodViewModel(new PurityPeriod(), this));
		}


		public void Dispose()
		{}


		private void LoadData()
		{
			var rawData = DataSerializer.Deserialize();
			BakeData(rawData);
		}
		/// <summary>
		/// Performs consistency calculations on whole raw data set
		/// </summary>
		/// <param name="rawData">Raw data set</param>
		private void BakeData(List<PurityPeriod> rawData)
		{
			_data = rawData.OrderBy(el => el.Begin.Stamp).ToList();

			PurityPeriod lastPeriod = null;

			foreach (var period in rawData)
			{
				UpdateRecentPeriodsStreak(lastPeriod, period);
				if (period.End.Stamp != DateTime.MinValue)
				{
					period.ClosePeriod(lastPeriod, _hec, _recentPeriodsStreak);
					//item.AddVesetHodesh(lastPeriod, _hebrewCalendar);
					//item.AddVesetAflaga(_recentPeriodsStreak);
					//item.AddTkufaBeinonit();
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
				_recentPeriodsStreak.Clear();
			_recentPeriodsStreak.Add(l);
		}


		public IMvxCommand AddPeriodCommand { get; private set; }
		internal void AddPeriod()
		{
			AddPeriod(DateTime.Now, DateTime.Now.AddDays(7));
		}

		public void AddPeriod(DateTime beg, DateTime end)
		{
			var period = new PurityPeriod(beg, end);
			_data.Add(period);
			PurityPeriods.Add(new PurityPeriodViewModel(period, this));
		}
		public void RemovePeriod(PurityPeriod period/*DateTime beg*/)
		{
			if (_data.Count == 0)
				return;

			//var l = _data.Last();
			//if (l.Begin.Stamp == beg)
			if (period == _data.Last())
			{
				_data.Remove(period);
				PurityPeriods.Remove(PurityPeriods.First(el => el.SelectedBeginDate == period.Begin.Stamp));
				if (_recentPeriodsStreak.Count != 0)
					_recentPeriodsStreak.RemoveAt(_recentPeriodsStreak.Count - 1);
				if (_recentPeriodsStreak.Count == 0)
					BakeData(_data);   // need to recalculate _recentPeriodsStreak
			}
		}
		public void ClosePeriod(PurityPeriod period/*DateTime beg*/)
		{
			//var item = _data.FirstOrDefault(el => el.Begin.Stamp == beg);
			if (period != null && period.End.Stamp != DateTime.MinValue)
			{
				var idx = _data.IndexOf(period);
				var lastPeriod = idx > 0 ? _data[idx - 1] : null;
				period.ClosePeriod(lastPeriod, _hec, _recentPeriodsStreak);
				//period.AddVesetHodesh(lastPeriod, _hebrewCalendar);
				//period.AddVesetAflaga(_recentPeriodsStreak);
				//period.AddTkufaBeinonit();
				//// todo: calc mikveh const
			}
		}


		public ObservableCollection<PurityPeriodViewModel> PurityPeriods { get; set; }

		private List<PurityPeriod> _data;
		private readonly HebrewCalendar _hec;
		private readonly List<int> _recentPeriodsStreak = new List<int>();
	}
}
