using System.Text;
using Purity.Core;
using Purity.Desktop.ViewModels;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>The Next strip (T1), and how each date stands against today (T5). The single period's mikveh is on the night of 17 January 2031.</summary>
	public sealed partial class MainWindowTests
	{
		[Fact]
		public async Task TheNextDateIsTheEarliestStillToCome()
		{
			var (window, clock) = await OpenOnePeriodOnAsync(new DateTime(2031, 1, 10));
			void On(DateTime day)
			{
				clock.Elapsed = day - Today;
				window.NoticeNewDay();
			}

			Assert.Equal(PurityEventType.Mikveh, window.NextEvent!.Type);
			Assert.Equal("in 7 days", window.NextIn);

			On(new DateTime(2031, 1, 16));
			Assert.Equal("Tomorrow", window.NextIn);

			On(new DateTime(2031, 1, 17));
			Assert.Equal("Today", window.NextIn);

			On(new DateTime(2031, 1, 18));						// that night runs on until this morning
			Assert.Equal(PurityEventType.Mikveh, window.NextEvent!.Type);
			Assert.Equal("Today", window.NextIn);

			On(new DateTime(2031, 1, 19));						// Ona Beinonit is on 3 February, from the evening before
			Assert.Equal(PurityEventType.OnaBeinonit, window.NextEvent!.Type);
			Assert.Equal("in 14 days", window.NextIn);
		}

		[Fact]
		public async Task WithNothingToComeThereIsNoNextDate()
		{
			var (window, _) = await OpenOnePeriodOnAsync(new DateTime(2031, 12, 1));

			Assert.Null(window.NextEvent);
			Assert.Equal(string.Empty, window.NextIn);
		}

		[Fact]
		public async Task TheNextStripGivesWayWhileAPeriodIsOpen()
		{
			var (window, clock) = await OpenOnePeriodOnAsync(new DateTime(2031, 1, 10));
			Assert.True(window.ShowsNextStrip);

			await window.AddOrClosePeriodCommand.ExecuteAsync(null);
			Assert.False(window.ShowsNextStrip);

			await window.Periods[0].RemoveCommand.ExecuteAsync(null);			// back when it's removed
			Assert.True(window.ShowsNextStrip);

			clock.Elapsed += TimeSpan.FromSeconds(5);
			await window.AddOrClosePeriodCommand.ExecuteAsync(null);
			Assert.False(window.ShowsNextStrip);

			clock.Elapsed += TimeSpan.FromSeconds(5);
			_dialogs.Answers.Enqueue(0);							// its end date is still a week ahead
			await window.AddOrClosePeriodCommand.ExecuteAsync(null);
			Assert.True(window.ShowsNextStrip);						// and when it's closed
		}

		[Fact]
		public async Task WithNothingToComeTheNextStripStaysAway()
		{
			var (window, _) = await OpenOnePeriodOnAsync(new DateTime(2031, 12, 1));

			Assert.False(window.ShowsNextStrip);
		}

		[Fact]
		public async Task ADateLeftOverFromAnEarlierPeriodCanBeNext()
		{
			var ledger = new PeriodLedger();
			ledger.AcceptPeriod(ledger.AddPeriod(Stamps.FromDate(new DateTime(2031, 1, 5), false), Stamps.FromDate(new DateTime(2031, 1, 10), false)));
			ledger.AcceptPeriod(ledger.AddPeriod(Stamps.FromDate(new DateTime(2031, 1, 30), false), Stamps.FromDate(new DateTime(2031, 2, 1), false)));
			File.WriteAllBytes(_dataFile, new UTF8Encoding(false).GetBytes(PurityFile.Format(ledger.Data)));

			var window = await OpenAsync(new FixedClock { Elapsed = new DateTime(2031, 2, 2) - Today });

			Assert.Equal(PurityEventType.OnaBeinonit, window.NextEvent!.Type);
			Assert.Contains(window.NextEvent, window.Periods[1].SubEvents);
			Assert.True(window.Periods[1].IsFolded);
		}

		[Fact]
		public async Task PastDatesAreMarkedAndALineSeparatesThemFromTheOnesToCome()
		{
			var (window, clock) = await OpenOnePeriodOnAsync(new DateTime(2031, 1, 20));
			var rows = window.Periods[0].EventRows;

			Assert.True(rows[0].IsPast);
			Assert.Null(rows[0].TodayText);
			Assert.False(rows[1].IsPast);
			Assert.Equal("Today, Monday, 20 January 2031", rows[1].TodayText);
			Assert.All(rows.Skip(2), el => Assert.Null(el.TodayText));
			Assert.True(window.Periods[0].HasTodayLine);

			clock.Elapsed = new DateTime(2031, 1, 10) - Today;			// all to come
			window.NoticeNewDay();
			Assert.All(window.Periods[0].EventRows, el => Assert.False(el.IsPast));
			Assert.False(window.Periods[0].HasTodayLine);

			clock.Elapsed = new DateTime(2031, 12, 1) - Today;			// all past
			window.NoticeNewDay();
			Assert.All(window.Periods[0].EventRows, el => Assert.True(el.IsPast));
			Assert.False(window.Periods[0].HasTodayLine);
		}


		private async Task<(MainWindowViewModel Window, FixedClock Clock)> OpenOnePeriodOnAsync(DateTime day)
		{
			var ledger = new PeriodLedger();
			ledger.AcceptPeriod(ledger.AddPeriod(Stamps.FromDate(new DateTime(2031, 1, 5), false), Stamps.FromDate(new DateTime(2031, 1, 10), false)));
			File.WriteAllBytes(_dataFile, new UTF8Encoding(false).GetBytes(PurityFile.Format(ledger.Data)));
			var clock = new FixedClock { Elapsed = day - Today };
			return (await OpenAsync(clock), clock);
		}
	}
}
