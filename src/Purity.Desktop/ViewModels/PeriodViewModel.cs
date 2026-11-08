using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Purity.Core;


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
		}


		/// <summary>Shows the period's current events and values</summary>
		public void Refresh()
		{
			SubEvents.Clear();
			foreach (var item in Period.SubEvents)
				SubEvents.Add(item);
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

		public string SkipLabel => _owner.Ledger.SkipLabel(Period);
		public bool IsClosed => Period.Closed;
		public bool IsLast => _owner.IsLast(Period);
		public ObservableCollection<PurityEvent> SubEvents { get; }
		public PurityPeriod Period { get; }

		[RelayCommand]
		private Task Accept() => _owner.AcceptAsync(this);

		[RelayCommand]
		private Task Remove() => _owner.RemoveAsync(this);


		/// <summary>The WPF picker showed the UTC date. For the +00:00 stamps the app writes, that is the stamp's own date.</summary>
		private static DateTime ShownDate(DateTimeOffset stamp) => stamp.UtcDateTime.Date;

		/// <summary>Moves between the day and night onah of the same date, as the WPF buttons do</summary>
		private static DateTimeOffset Toggled(DateTimeOffset stamp) =>
			PurityEvent.IsDateAfterDusk(stamp) ? stamp.AddHours(-12) : stamp.AddHours(12);


		private readonly MainWindowViewModel _owner;
	}
}
