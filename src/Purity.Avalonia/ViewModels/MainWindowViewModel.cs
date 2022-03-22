using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Avalonia.Controls;
using NLog;
using Purity.Avalonia.Views;
using ReactiveUI;


namespace Purity.Avalonia.ViewModels
{
	public class MainWindowViewModel : ViewModelBase
	{
		public MainWindowViewModel(Window owner, Settings settings)
		{
			_owner = owner;
			Settings = settings;
			Data = new List<PurityPeriod>();
			PurityPeriods = new ObservableCollection<PurityPeriodViewModel>();

			OpenSettingsCommand = ReactiveCommand.Create(OpenSettings);
			AddPeriodCommand = ReactiveCommand.Create(AddPeriod);
			RecalculateCommand = ReactiveCommand.Create(Recalculate);
		}


		/// <summary>
		/// Initializes with raw data set and shows UI
		/// </summary>
		public void InitData()
		{
			var rawData = DataSerializer.DeserializeData(Settings.DataFilePath);
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

			var l = PurityPeriod.GetFullPeriodLength(a, b);
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


		public ICommand OpenSettingsCommand { get; }
		private async void OpenSettings()
		{
			var settingsCopy = new Settings(Settings);
			var w = new SettingsWindow();
			w.InitDataContext(settingsCopy, path => DataSerializer.SerializeData(Data, path));
			if (await w.ShowDialog<bool?>(_owner) == true)
			{
				Settings = settingsCopy;
				InitData();
			}
		}

		public ICommand AddPeriodCommand { get; }
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

		public ICommand RecalculateCommand { get; }
		private void Recalculate()
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

		public void SaveState()
		{
			DataSerializer.SerializeSettings(Settings);
#if !DEBUG
			DataSerializer.SerializeData(Data, Settings.DataFilePath);
#endif
		}


		public ObservableCollection<PurityPeriodViewModel> PurityPeriods { get; set; }

		public Settings Settings;
		public List<PurityPeriod> Data;
		private readonly Window _owner;
		private readonly List<int> _recentPeriodsStreak = new();

		private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
	}
}
