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
				BeginChanged();
			}
		}

		/// <summary>
		/// The Hebrew date the start belongs to, given as the Gregorian day holding its daytime.
		/// Picking one keeps the day or night choice, and a date nobody changes is left alone.
		/// </summary>
		public DateTime? BeginHebrewDay
		{
			get => HebrewDay(Period.Begin);
			set
			{
				if (value is not { } day || day == BeginHebrewDay)
					return;

				Period.Begin = Stamps.FromHebrewDaytime(day, BeginIsAfterDusk);
				BeginChanged();
			}
		}

		/// <summary>The day the start's date field holds, in the calendar on show</summary>
		public DateTime? BeginDay
		{
			get => UsesHebrewCalendar ? BeginHebrewDay : BeginDate;
			set
			{
				if (UsesHebrewCalendar)
					BeginHebrewDay = value;
				else
					BeginDate = value;
			}
		}

		public bool BeginIsAfterDusk => PurityEvent.IsDateAfterDusk(Period.Begin);

		[RelayCommand]
		private void ToggleBeginNight()
		{
			Period.Begin = Toggled(Period.Begin);
			BeginChanged();
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
				EndChanged();
			}
		}

		/// <inheritdoc cref="BeginHebrewDay"/>
		public DateTime? EndHebrewDay
		{
			get => HebrewDay(Period.End);
			set
			{
				if (value is not { } day || day == EndHebrewDay)
					return;

				Period.End = Stamps.FromHebrewDaytime(day, EndIsAfterDusk);
				EndChanged();
			}
		}

		/// <inheritdoc cref="BeginDay"/>
		public DateTime? EndDay
		{
			get => UsesHebrewCalendar ? EndHebrewDay : EndDate;
			set
			{
				if (UsesHebrewCalendar)
					EndHebrewDay = value;
				else
					EndDate = value;
			}
		}

		public bool EndIsAfterDusk => PurityEvent.IsDateAfterDusk(Period.End);

		[RelayCommand]
		private void ToggleEndNight()
		{
			Period.End = Toggled(Period.End);
			EndChanged();
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

		/// <summary>The same onah in the other calendar: a tooltip on desktop, and a line under the field on a touch screen</summary>
		public string? BeginInOtherCalendar => InOtherCalendar(Period.Begin);

		/// <inheritdoc cref="BeginInOtherCalendar"/>
		public string? EndInOtherCalendar => InOtherCalendar(Period.End);

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
			var text = Loc.T.TodayLine(Loc.Instance.LongDay(today));
			EventRows = SubEvents.Select((el, i) => new EventRow(el, EventTiming.IsOver(el, today), i == line ? text : null)).ToList();
		}

		/// <summary>The text a date field shows, in the language being shown. It never comes from the OS's culture data, so no machine setting can change it.</summary>
		private static string LongDate(DateTimeOffset stamp) => Loc.Instance.LongDate(stamp);

		/// <summary>The same onah in the calendar not on show, without the year, or null for a date the Hebrew calendar doesn't reach</summary>
		private string? InOtherCalendar(DateTimeOffset stamp)
		{
			if (!Stamps.IsInHebrewCalendar(stamp))
				return null;

			var night = PurityEvent.IsDateAfterDusk(stamp);
			if (!UsesHebrewCalendar)
				return Loc.T.HebrewDateTip(Loc.Instance.HebrewDate(stamp), night);

			var day = ShownDate(stamp);
			return night ? Loc.T.GregorianNightTip(day, day.AddDays(1)) : day.ToString("dddd, d MMMM", Loc.Instance.DateCulture);
		}

		private bool UsesHebrewCalendar => _owner.UsesHebrewCalendar;

		private static DateTime? HebrewDay(DateTimeOffset stamp) => Stamps.IsInHebrewCalendar(stamp) ? Stamps.HebrewDaytime(stamp) : null;

		private void BeginChanged()
		{
			OnPropertyChanged(nameof(BeginDay));
			OnPropertyChanged(nameof(BeginDate));
			OnPropertyChanged(nameof(BeginHebrewDay));
			OnPropertyChanged(nameof(BeginIsAfterDusk));
			OnPropertyChanged(nameof(BeginText));
			OnPropertyChanged(nameof(BeginInOtherCalendar));
			OnPropertyChanged(nameof(SkipLabel));
		}

		private void EndChanged()
		{
			OnPropertyChanged(nameof(EndDay));
			OnPropertyChanged(nameof(EndDate));
			OnPropertyChanged(nameof(EndHebrewDay));
			OnPropertyChanged(nameof(EndIsAfterDusk));
			OnPropertyChanged(nameof(EndText));
			OnPropertyChanged(nameof(EndInOtherCalendar));
		}

		/// <summary>
		/// Moves between the day and night onah of the same date, as the WPF buttons do. A Gregorian
		/// date's night comes after its day and a Hebrew date's before it, so the same press moves the
		/// stored date opposite ways, and the date on show stays put either way.
		/// </summary>
		private DateTimeOffset Toggled(DateTimeOffset stamp)
		{
			var night = PurityEvent.IsDateAfterDusk(stamp);
			return stamp.AddHours(UsesHebrewCalendar == night ? 12 : -12);
		}


		private readonly MainWindowViewModel _owner;
	}
}
