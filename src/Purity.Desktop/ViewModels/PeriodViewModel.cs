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

				SetBegin(Stamps.FromDate(date, BeginIsAfterDusk));
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

				SetBegin(Stamps.FromHebrewDaytime(day, BeginIsAfterDusk));
			}
		}

		/// <summary>The day the start's date field holds, in the calendar on show</summary>
		public DateTime? BeginDay
		{
			get => DayOf(_asking is (true, var stamp) ? stamp : Period.Begin);
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
			SetBegin(Toggled(Period.Begin));
		}

		/// <inheritdoc cref="BeginDate"/>
		public DateTime? EndDate
		{
			get => ShownDate(Period.End);
			set
			{
				if (value is not { } date || date == EndDate)
					return;

				SetEnd(Stamps.FromDate(date, EndIsAfterDusk));
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

				SetEnd(Stamps.FromHebrewDaytime(day, EndIsAfterDusk));
			}
		}

		/// <inheritdoc cref="BeginDay"/>
		public DateTime? EndDay
		{
			get => DayOf(_asking is (false, var stamp) ? stamp : Period.End);
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
			SetEnd(Toggled(Period.End));
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

		private void SetBegin(DateTimeOffset stamp) => Change(stamp, true);

		private void SetEnd(DateTimeOffset stamp) => Change(stamp, false);

		/// <summary>
		/// A period still open takes a new date at once. A closed one takes it only once the change is
		/// confirmed: until then the field keeps showing the date it has, and goes back to it if not.
		/// </summary>
		private void Change(DateTimeOffset stamp, bool start)
		{
			if (IsClosed)
				_ = ConfirmChangeAsync(stamp, start);
			else
				Apply(stamp, start);
		}

		/// <summary>
		/// While asked, the field's day is the one picked. A two-way binding passes on only a value
		/// that differs from the last it saw, so going back to the date kept is a change it then hears
		/// of, and the field follows.
		/// </summary>
		private async Task ConfirmChangeAsync(DateTimeOffset stamp, bool start)
		{
			var was = start ? Period.Begin : Period.End;
			_asking = (start, stamp);
			Changed(start);
			bool confirmed;
			try
			{
				confirmed = await _owner.MayChangeClosedAsync(was, stamp, start);
			}
			finally
			{
				_asking = null;
			}

			if (confirmed)
				Apply(stamp, start);
			else
				Changed(start);
		}

		private void Apply(DateTimeOffset stamp, bool start)
		{
			if (start)
				Period.Begin = stamp;
			else
				Period.End = stamp;
			Changed(start);
		}

		private void Changed(bool start)
		{
			if (start)
				BeginChanged();
			else
				EndChanged();
		}

		/// <summary>The day a date field holds for a stamp, in the calendar on show</summary>
		private DateTime? DayOf(DateTimeOffset stamp) => UsesHebrewCalendar ? HebrewDay(stamp) : ShownDate(stamp);

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

		/// <summary>A closed period's date picked but not confirmed yet, and whether it's the start</summary>
		private (bool Start, DateTimeOffset Stamp)? _asking;
	}
}
