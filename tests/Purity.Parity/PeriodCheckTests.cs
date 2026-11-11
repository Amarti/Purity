extern alias Candidate;
using Xunit;
using Core = Candidate::Purity.Core;


namespace Purity.Parity
{
	/// <summary>
	/// Warnings for odd input. They only read the dates, so beside what they find, these tests prove
	/// they leave the history exactly as it was. Every date is made up.
	/// </summary>
	public class PeriodCheckTests
	{
		private static readonly DateTimeOffset Start = new(2033, 1, 2, 0, 0, 0, TimeSpan.Zero);

		private static DateTimeOffset Day(int days, bool night = false) => Start.AddDays(days).AddHours(night ? 12 : 0);

		private static Core.PurityPeriod Closed(DateTimeOffset begin, DateTimeOffset end) => new(begin, end) { Closed = true };

		/// <summary>Three periods of five days, four weeks apart</summary>
		private static List<Core.PurityPeriod> Usual() => [Closed(Day(0), Day(5)), Closed(Day(28, true), Day(33)), Closed(Day(56), Day(61))];


		[Fact]
		public void AUsualHistoryHasNothingOdd()
		{
			var data = Usual();

			Assert.All(data, el => Assert.Empty(Core.PeriodChecks.Check(data, el)));
		}

		[Fact]
		public void AnEndBeforeTheStartIsOdd()
		{
			var data = Usual();
			var period = Closed(Day(90), Day(85));
			data.Add(period);

			var found = Assert.Single(Core.PeriodChecks.Check(data, period));

			Assert.Equal(Core.OddityKind.EndsBeforeItBegins, found.Kind);
			Assert.Equal(period.End, found.Other);
		}

		[Theory]
		[InlineData(false, 30, false)]
		[InlineData(true, 30, false)]
		[InlineData(false, 31, true)]
		[InlineData(true, 31, true)]
		[InlineData(false, 365, true)]
		public void OnlyMoreThanThirtyDaysIsVeryLong(bool beginsAtNight, int days, bool odd)
		{
			var data = Usual();
			var period = Closed(Day(90, beginsAtNight), Day(90 + days));
			data.Add(period);

			var found = Core.PeriodChecks.Check(data, period);

			if (!odd)
				Assert.Empty(found);
			else
				Assert.Equal(new Core.Oddity(Core.OddityKind.VeryLong, period.End, days), Assert.Single(found));
		}

		/// <summary>Exactly where the engine's interval to the previous period drops below zero, whether that period ended by day or at night</summary>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void BeginningBeforeThePreviousEndIsWhereTheIntervalTurnsNegative(bool previousEndsAtNight)
		{
			var previous = Closed(Day(0), Day(5, previousEndsAtNight));
			for (var onot = -6; onot <= 6; onot++)
			{
				var period = Closed(Day(5).AddHours(12 * onot), Day(12));
				var found = Core.PeriodChecks.Check([previous, period], period);

				var negative = Core.PurityPeriod.GetPeriodLength(previous, period) < 0;
				Assert.Equal(negative, found.Any(el => el.Kind == Core.OddityKind.BeginsBeforePreviousEnds));
				if (negative)
					Assert.Equal(previous.End, found.Single(el => el.Kind == Core.OddityKind.BeginsBeforePreviousEnds).Other);
			}
		}

		[Fact]
		public void SkippingAnIntervalDoesntHideAnOverlap()
		{
			var previous = Closed(Day(0), Day(5));
			var period = Closed(Day(3), Day(9));
			period.SkipStreak = true;

			Assert.Equal(0, Core.PurityPeriod.GetPeriodLength(previous, period));
			Assert.Contains(Core.PeriodChecks.Check([previous, period], period), el => el.Kind == Core.OddityKind.BeginsBeforePreviousEnds);
		}

		[Fact]
		public void TwoPeriodsBeginningTogetherOverlap()
		{
			var first = Closed(Day(0), Day(5));
			var second = Closed(Day(0), Day(4));

			Assert.Contains(Core.PeriodChecks.Check([first, second], second), el => el.Kind == Core.OddityKind.BeginsBeforePreviousEnds);
		}

		[Fact]
		public void AnOverlapShowsFromBothSides()
		{
			var earlier = Closed(Day(0), Day(10));
			var later = Closed(Day(8), Day(13));
			List<Core.PurityPeriod> data = [later, earlier];

			Assert.Equal(new Core.Oddity(Core.OddityKind.EndsAfterNextBegins, later.Begin), Assert.Single(Core.PeriodChecks.Check(data, earlier)));
			Assert.Equal(new Core.Oddity(Core.OddityKind.BeginsBeforePreviousEnds, earlier.End), Assert.Single(Core.PeriodChecks.Check(data, later)));
		}

		[Fact]
		public void APeriodStillOpenThatGoesInAmongTheEarlierOnesIsOdd()
		{
			var data = Usual();
			var open = new Core.PurityPeriod(Day(14), Day(19));
			data.Add(open);

			Assert.Equal(new Core.Oddity(Core.OddityKind.BeginsBeforeNewest, Day(56)), Assert.Single(Core.PeriodChecks.Check(data, open)));

			open.Closed = true;
			Assert.Empty(Core.PeriodChecks.Check(data, open));			// once in the history, only the dates themselves count
		}

		[Fact]
		public void OnlyClosedPeriodsCountAsTheNewest()
		{
			var closed = Closed(Day(0), Day(5));
			var open = new Core.PurityPeriod(Day(28), Day(33));
			var another = new Core.PurityPeriod(Day(20), Day(25));

			Assert.Empty(Core.PeriodChecks.Check([closed, open], open));
			Assert.DoesNotContain(Core.PeriodChecks.Check([closed, open, another], another), el => el.Kind == Core.OddityKind.BeginsBeforeNewest);
		}

		[Fact]
		public void CheckingChangesNothing()
		{
			var ledger = new Core.PeriodLedger();
			foreach (var period in Usual())
				ledger.AcceptPeriod(ledger.AddPeriod(period.Begin, period.End));
			ledger.AcceptPeriod(ledger.AddPeriod(Day(70), Day(66)));
			var open = ledger.AddPeriod(Day(40), Day(45))!;
			var before = Core.PurityFile.Format(ledger.Data);
			var order = ledger.Data.ToList();

			Assert.NotEmpty(Core.PeriodChecks.Check(ledger.Data, open));
			Assert.NotEmpty(Core.PeriodChecks.Check(ledger.Data, ledger.Data[^2]));

			Assert.Equal(before, Core.PurityFile.Format(ledger.Data));
			Assert.Equal(order, ledger.Data);
		}
	}
}
