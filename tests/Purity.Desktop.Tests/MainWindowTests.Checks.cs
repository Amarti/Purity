using System.Text;
using Purity.Core;
using Purity.Desktop.Localization;
using Purity.Desktop.ViewModels;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>Questions that guard against a slip: odd dates when a period is closed, and changing a closed period. Every date is made up.</summary>
	public sealed partial class MainWindowTests
	{
		[Fact]
		public async Task ClosingAPeriodThatEndsBeforeItBeginsAsksFirst()
		{
			var content = WriteHistory();
			var clock = new FixedClock();
			var window = await OpenAsync(clock);
			await window.AddOrClosePeriodCommand.ExecuteAsync(null);
			var open = window.Periods[0];
			open.EndDate = Today.AddDays(-3);
			clock.Elapsed += TimeSpan.FromSeconds(5);

			_dialogs.Answers.Enqueue(1);					// Keep open
			await window.AddOrClosePeriodCommand.ExecuteAsync(null);

			var asked = Assert.Single(_dialogs.Shown);
			Assert.Equal($"{UiText.English.OddDates}\n• {UiText.English.OddEndsFirst(open.EndText)}\n\n{UiText.English.OddKeepOpen}\n\n{UiText.English.ClosePeriodAnyway}", asked);
			Assert.False(open.IsClosed);
			Assert.Equal(content, File.ReadAllBytes(_dataFile));

			clock.Elapsed += TimeSpan.FromSeconds(5);
			_dialogs.Answers.Enqueue(0);					// Close period: it's only a warning
			await window.AddOrClosePeriodCommand.ExecuteAsync(null);
			Assert.True(open.IsClosed);
			Assert.NotEmpty(open.SubEvents);
		}

		[Fact]
		public async Task ALongPeriodWhoseEndHasntComeGivesBothReasons()
		{
			WriteHistory();
			var clock = new FixedClock();
			var window = await OpenAsync(clock);
			await window.AddOrClosePeriodCommand.ExecuteAsync(null);
			var open = window.Periods[0];
			open.BeginDate = Today.AddDays(-40);
			open.EndDate = Today.AddDays(2);
			clock.Elapsed += TimeSpan.FromSeconds(5);

			await window.AddOrClosePeriodCommand.ExecuteAsync(null);

			var asked = Assert.Single(_dialogs.Shown);
			Assert.StartsWith(UiText.English.EndNotYet(open.EndText) + "\n\n" + UiText.English.OddDates, asked);
			Assert.Contains(UiText.English.OddLong(42), asked);
			Assert.False(open.IsClosed);
		}

		[Fact]
		public async Task APeriodGoingInAmongTheEarlierOnesAsksFirst()
		{
			WriteHistory();
			var clock = new FixedClock();
			var window = await OpenAsync(clock);
			var newest = window.Periods[0].BeginText;
			await window.AddOrClosePeriodCommand.ExecuteAsync(null);
			var open = window.Periods[0];
			open.BeginDate = new DateTime(2031, 2, 10);
			open.EndDate = new DateTime(2031, 2, 15);
			clock.Elapsed += TimeSpan.FromSeconds(5);

			await window.AddOrClosePeriodCommand.ExecuteAsync(null);

			Assert.Contains("• " + UiText.English.OddBeforeNewest(newest), Assert.Single(_dialogs.Shown));
			Assert.False(open.IsClosed);
		}

		[Fact]
		public async Task RefreshingDoesntCheckTheHistory()
		{
			var ledger = new PeriodLedger();
			ledger.Load(PurityFile.Parse(History()));
			ledger.AcceptPeriod(ledger.AddPeriod(Stamps.FromDate(new DateTime(2031, 5, 1), false), Stamps.FromDate(new DateTime(2031, 4, 28), false)));
			File.WriteAllText(_dataFile, PurityFile.Format(ledger.Data), new UTF8Encoding(false));
			var window = await OpenAsync();
			Assert.NotEmpty(PeriodChecks.Check(ledger.Data, ledger.Data[^1]));		// closed with its end before its start

			await window.RecalculateCommand.ExecuteAsync(null);

			Assert.Empty(_dialogs.Shown);
		}

		[Fact]
		public async Task ChangingADateOfAClosedPeriodAsksFirst()
		{
			WriteHistory();
			var window = await OpenAsync();
			var closed = window.Periods[1];
			var (end, endText) = (closed.Period.End, closed.EndText);
			var notified = new List<string?>();
			closed.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

			_dialogs.Answers.Enqueue(1);					// Cancel
			closed.EndDate = closed.EndDate!.Value.AddDays(2);

			var asked = Assert.Single(_dialogs.Shown);
			var moved = Stamps.FromDate(end.UtcDateTime.Date.AddDays(2), false);
			Assert.Equal(
				$"{UiText.English.ChangeClosedEnd}\n{UiText.English.ChangeNow(UiText.English.DateWithOnah(endText, false))}\n" +
				$"{UiText.English.ChangeNew(UiText.English.DateWithOnah(Loc.Instance.LongDate(moved), false))}\n\n" +
				$"{UiText.English.ChangeClosedRefresh}\n\n{UiText.English.ChangeClosedTitle}", asked);
			Assert.Equal(end, closed.Period.End);
			Assert.Contains(nameof(PeriodViewModel.EndDay), notified);		// the field goes back to the date kept

			_dialogs.Answers.Enqueue(0);					// Change
			closed.EndDate = closed.EndDate!.Value.AddDays(2);
			Assert.Equal(2, _dialogs.Shown.Count);
			Assert.Equal(moved, closed.Period.End);
		}

		[Fact]
		public async Task EveryWayOfChangingAClosedPeriodAsks()
		{
			WriteHistory();
			var window = await OpenAsync();
			var closed = window.Periods[1];
			var (begin, end) = (closed.Period.Begin, closed.Period.End);

			closed.ToggleBeginNightCommand.Execute(null);
			closed.ToggleEndNightCommand.Execute(null);
			closed.BeginDate = closed.BeginDate!.Value.AddDays(-1);
			closed.BeginHebrewDay = closed.BeginHebrewDay!.Value.AddDays(-1);
			closed.EndHebrewDay = closed.EndHebrewDay!.Value.AddDays(1);

			Assert.Equal(5, _dialogs.Shown.Count);				// none answered, so none changes anything
			Assert.Contains(UiText.English.ChangeClosedStart, _dialogs.Shown[0]);
			Assert.Contains(UiText.English.DateWithOnah(closed.BeginText, true), _dialogs.Shown[0]);	// from night to day, on the same date
			Assert.Contains(UiText.English.DateWithOnah(closed.BeginText, false), _dialogs.Shown[0]);
			Assert.Contains(UiText.English.ChangeClosedEnd, _dialogs.Shown[1]);
			Assert.Equal((begin, end), (closed.Period.Begin, closed.Period.End));
		}

		[Fact]
		public async Task APeriodStillOpenChangesWithoutAsking()
		{
			WriteHistory();
			var window = await OpenAsync();
			window.AddPeriodCommand.Execute(null);
			var open = window.Periods[0];

			open.BeginDate = Today.AddDays(-2);
			open.ToggleBeginNightCommand.Execute(null);
			open.EndDate = Today.AddDays(3);

			Assert.Empty(_dialogs.Shown);
			Assert.Equal(Stamps.FromDate(Today.AddDays(-2), true), open.Period.Begin);
			Assert.Equal(Stamps.FromDate(Today.AddDays(3), false), open.Period.End);
		}
	}
}
