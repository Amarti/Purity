using CommunityToolkit.Mvvm.ComponentModel;
using Purity.Core;


namespace Purity.Desktop.ViewModels
{
	/// <summary>What's coming: the Next strip (T1), and today for greying the dates already past (T5)</summary>
	public sealed partial class MainWindowViewModel
	{
		/// <summary>The earliest date still to come in any period, or null when none is</summary>
		[ObservableProperty]
		public partial PurityEvent? NextEvent { get; private set; }

		/// <summary>How far off the next date is</summary>
		[ObservableProperty]
		public partial string NextIn { get; private set; } = string.Empty;

		/// <summary>The strip gives way while a period is open, and returns when it's closed or removed</summary>
		public bool ShowsNextStrip => NextEvent != null && !HasOpenPeriod;

		/// <summary>The date on this computer's calendar, as of the last time it was looked at</summary>
		internal DateTime Today { get; private set; }

		/// <summary>Moves on to a new day if there is one, for a window left open past midnight</summary>
		public void NoticeNewDay()
		{
			var today = _clock.GetLocalNow().Date;
			if (today == Today)
				return;

			Today = today;
			RefreshAll();
		}


		private void UpdateNext()
		{
			NextEvent = Periods.SelectMany(el => el.SubEvents)
				.Where(el => !EventTiming.IsOver(el, Today))
				.OrderBy(EventTiming.Begins)
				.FirstOrDefault();
			NextIn = NextEvent == null ? string.Empty : EventTiming.DaysAway(NextEvent, Today);
			OnPropertyChanged(nameof(ShowsNextStrip));
		}
	}
}
