using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Purity.Core;
using Purity.Desktop.Localization;


namespace Purity.Desktop.ViewModels
{
	/// <summary>One period's frame in the list, behaving as the WPF app's does</summary>
	public sealed partial class PeriodViewModel : ObservableObject
	{
		public PeriodViewModel(PurityPeriod period, MainWindowViewModel owner)
		{
			Period = period;
			_owner = owner;
			SubEvents = new ObservableCollection<PurityEvent>(period.SubEvents);
			BuildRows();
		}


		/// <summary>Shows the period's current events and values</summary>
		public void Refresh()
		{
			SubEvents.Clear();
			foreach (var item in Period.SubEvents)
				SubEvents.Add(item);
			BuildRows();
			OnPropertyChanged(string.Empty);
		}


		/// <summary>
		/// The date the picker shows. Picking a date keeps the day or night choice, where the WPF
		/// app reset it to day, which made the order of the two clicks matter. The owner approved
		/// this difference from WPF in November 2026; a test pins it.
		/// </summary>
		public DateTime? BeginDate
		{
			get => ShownDate(Period.Begin);
			set
			{
				if (value is not { } date || date == BeginDate)
					return;

				Period.Begin = Stamps.FromDate(date, BeginIsAfterDusk);
				OnPropertyChanged();
				OnPropertyChanged(nameof(SkipLabel));
			}
		}

		public bool BeginIsAfterDusk => PurityEvent.IsDateAfterDusk(Period.Begin);

		[RelayCommand]
		private void ToggleBeginNight()
		{
			Period.Begin = Toggled(Period.Begin);
			OnPropertyChanged(nameof(BeginIsAfterDusk));
			OnPropertyChanged(nameof(SkipLabel));
		}

		/// <inheritdoc cref="BeginDate"/>
		public DateTime? EndDate
		{
			get => ShownDate(Period.End);
			set
			{
				if (value is not { } date || date == EndDate)
					return;

				Period.End = Stamps.FromDate(date, EndIsAfterDusk);
				OnPropertyChanged();
			}
		}

		public bool EndIsAfterDusk => PurityEvent.IsDateAfterDusk(Period.End);

		[RelayCommand]
		private void ToggleEndNight()
		{
			Period.End = Toggled(Period.End);
			OnPropertyChanged(nameof(EndIsAfterDusk));
		}

		public bool SkipStreak
		{
			get => Period.SkipStreak;
			set
			{
				if (Period.SkipStreak == value)
					return;

				Period.SkipStreak = value;
				OnPropertyChanged();
			}
		}

		/// <summary>In English exactly the engine's own label</summary>
		public string SkipLabel
		{
			get
			{
				var length = _owner.Ledger.PeriodLength(Period);
				return length > 0 ? string.Create(CultureInfo.InvariantCulture, $"{Loc.T.Skip} ({length})") : Loc.T.Skip;
			}
		}

		/// <summary>Whether the period shows as a single line (T2). Only a matter of what's on screen, so nothing about it is saved.</summary>
		[ObservableProperty]
		public partial bool IsFolded { get; set; }

		/// <summary>The first period in the list always shows in full, and one still open is waiting to be accepted or removed</summary>
		public bool CanFold => IsClosed && !IsLast;

		[RelayCommand]
		private void ToggleFold()
		{
			if (IsFolded || CanFold)
				IsFolded = !IsFolded;
		}

		public string BeginText => LongDate(Period.Begin);
		public string EndText => LongDate(Period.End);

		/// <summary>What a folded line's button does, for screen readers</summary>
		public string OpenLabel => Loc.T.OpenPeriodFrom(BeginText);

		/// <summary>The end of a folded line: that the period is skipped, or else the interval before it</summary>
		public string FoldedNote
		{
			get
			{
				if (Period.SkipStreak)
					return Loc.T.Skipped;

				var length = _owner.Ledger.PeriodLength(Period);
				return length > 0 ? string.Create(CultureInfo.InvariantCulture, $"({length})") : string.Empty;
			}
		}

		public bool IsClosed => Period.Closed;
		public bool IsLast => _owner.IsLast(Period);
		public ObservableCollection<PurityEvent> SubEvents { get; }

		/// <summary>The same dates, each with how it stands against today</summary>
		public IReadOnlyList<EventRow> EventRows { get; private set; } = Array.Empty<EventRow>();

		public bool HasTodayLine => EventRows.Any(el => el.TodayText != null);
		public PurityPeriod Period { get; }

		[RelayCommand]
		private Task Accept() => _owner.AcceptAsync(this);

		[RelayCommand]
		private Task Remove() => _owner.RemoveAsync(this);


		/// <summary>The WPF picker showed the UTC date. For the +00:00 stamps the app writes, that is the stamp's own date.</summary>
		private static DateTime ShownDate(DateTimeOffset stamp) => stamp.UtcDateTime.Date;

		/// <summary>The line marking today goes only between dates that have passed and dates to come, where it tells something</summary>
		private void BuildRows()
		{
			var today = _owner.Today;
			var passed = SubEvents.TakeWhile(el => EventTiming.IsOver(el, today)).Count();
			var line = passed > 0 && passed < SubEvents.Count ? passed : -1;
			var text = Loc.T.TodayLine(today.ToString("dddd, d MMMM yyyy", Loc.Instance.DateCulture));
			EventRows = SubEvents.Select((el, i) => new EventRow(el, EventTiming.IsOver(el, today), i == line ? text : null)).ToList();
		}

		/// <summary>The text a date field shows, in the language being shown. It never comes from the OS's culture data, so no machine setting can change it.</summary>
		private static string LongDate(DateTimeOffset stamp) => ShownDate(stamp).ToString("dddd, d MMMM yyyy", Loc.Instance.DateCulture);

		/// <summary>Moves between the day and night onah of the same date, as the WPF buttons do</summary>
		private static DateTimeOffset Toggled(DateTimeOffset stamp) =>
			PurityEvent.IsDateAfterDusk(stamp) ? stamp.AddHours(-12) : stamp.AddHours(12);


		private readonly MainWindowViewModel _owner;
	}
}
