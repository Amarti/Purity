extern alias Candidate;
using System.Text.Json;
using Xunit;
using Core = Candidate::Purity.Core;


namespace Purity.Parity
{
	/// <summary>
	/// Paths the bulk comparisons don't reach: opening a file without recalculating, the guards on
	/// add, accept and remove, and empty or unset values. Mutation testing showed these needed
	/// tests of their own.
	/// </summary>
	public class LedgerTests
	{
		private static readonly DateTimeOffset Day1 = new(2033, 1, 1, 0, 0, 0, TimeSpan.Zero);
		private static readonly DateTimeOffset Day7 = new(2033, 1, 7, 0, 0, 0, TimeSpan.Zero);
		private static readonly DateTimeOffset Day30Night = new(2033, 1, 30, 12, 0, 0, TimeSpan.Zero);
		private static readonly DateTimeOffset Feb5 = new(2033, 2, 5, 0, 0, 0, TimeSpan.Zero);
		private static readonly DateTimeOffset StaleStamp = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

		/// <summary>Two periods whose stored events are deliberately wrong, as a file from an older version can be.</summary>
		private static List<StoredPeriod> StaleHistory() =>
		[
			new(Day1, Day7, true, false, [new StoredEvent(1, StaleStamp, "stale")]),
			new(Day30Night, Feb5, true, false, [new StoredEvent(1, StaleStamp, "stale")]),
		];

		private static List<Core.PurityPeriod> StalePeriods() => Core.PurityFile.Parse(JsonSerializer.Serialize(StaleHistory()));

		private static T WithFile<T>(string json, Func<string, T> action)
		{
			var path = Path.Combine(Path.GetTempPath(), $"purity-parity-{Guid.NewGuid():N}.json");
			try
			{
				File.WriteAllText(path, json);
				return action(path);
			}
			finally
			{
				File.Delete(path);
			}
		}


		[Fact]
		public void OpeningAFileKeepsStoredEventsAndRebuildsTracking()
		{
			var run = Culture.Run(Culture.Production, () => WithFile(JsonSerializer.Serialize(StaleHistory()), Engines.Candidate.LoadOnly));

			Assert.All(run.Periods, period => Assert.Equal("stale", Assert.Single(period.Events).Note));
			Assert.Equal("46", run.Tracked);
		}

		[Fact]
		public void AddPeriodAddsAnOpenPeriod()
		{
			var ledger = new Core.PeriodLedger();

			var period = ledger.AddPeriod(Day1, Day7);

			Assert.NotNull(period);
			Assert.Same(period, Assert.Single(ledger.Data));
			Assert.Equal((Day1, Day7), (period.Begin, period.End));
			Assert.False(period.Closed);
			Assert.Empty(period.SubEvents);
		}

		[Fact]
		public void AddPeriodAddsSeveralDifferentPeriods()
		{
			var ledger = new Core.PeriodLedger();
			ledger.AddPeriod(Day1, Day7);

			var second = ledger.AddPeriod(Day30Night, Feb5);

			Assert.NotNull(second);
			Assert.Equal(2, ledger.Data.Count);
		}

		[Fact]
		public void AddPeriodRefusesASecondPeriodWithTheSameBegin()
		{
			var ledger = new Core.PeriodLedger();
			ledger.AddPeriod(Day1, Day7);
			ledger.AddPeriod(Day30Night, Feb5);

			var duplicate = ledger.AddPeriod(Day1, Feb5);

			Assert.Null(duplicate);
			Assert.Equal(2, ledger.Data.Count);
		}

		[Fact]
		public void AcceptIgnoresNull()
		{
			var ledger = new Core.PeriodLedger();
			ledger.Load(StalePeriods());

			ledger.AcceptPeriod(null);

			Assert.All(ledger.Data, period => Assert.Equal("stale", Assert.Single(period.SubEvents).Note));
		}

		[Fact]
		public void AcceptIgnoresAPeriodWithoutAnEnd()
		{
			var ledger = new Core.PeriodLedger();
			ledger.Load(StalePeriods());
			var open = new Core.PurityPeriod(new DateTimeOffset(2033, 3, 1, 0, 0, 0, TimeSpan.Zero), DateTimeOffset.MinValue);
			ledger.Data.Add(open);

			ledger.AcceptPeriod(open);

			Assert.False(open.Closed);
			Assert.Equal("stale", Assert.Single(ledger.Data[0].SubEvents).Note);
		}

		[Fact]
		public void AcceptClosesThePeriodAndRecalculatesEverything()
		{
			var ledger = new Core.PeriodLedger();
			ledger.Load(StalePeriods());
			var period = ledger.Data[^1];
			period.Closed = false;

			ledger.AcceptPeriod(period);

			Assert.True(period.Closed);
			Assert.DoesNotContain(ledger.Data.SelectMany(el => el.SubEvents), el => el.Note == "stale");
		}

		[Fact]
		public void RemoveFromAnEmptyLedgerDoesNothing()
		{
			Assert.False(new Core.PeriodLedger().RemovePeriod(new Core.PurityPeriod(Day1, Day7)));
		}

		[Fact]
		public void RemoveRefusesAnyPeriodButTheLast()
		{
			var ledger = new Core.PeriodLedger();
			ledger.Load(StalePeriods());
			var first = ledger.Data[0];

			var removed = ledger.RemovePeriod(first);

			Assert.False(removed);
			Assert.Equal(2, ledger.Data.Count);
			Assert.Equal("46", string.Join(',', ledger.TrackedIntervals));
		}

		[Fact]
		public void RemoveTakesOutTheLastPeriod()
		{
			var ledger = new Core.PeriodLedger();
			ledger.Load(StalePeriods());

			var removed = ledger.RemovePeriod(ledger.Data[^1]);

			Assert.True(removed);
			Assert.Single(ledger.Data);
			Assert.Empty(ledger.TrackedIntervals);
		}

		/// <summary>Rebuilding tracking after a removal must not recalculate the events that remain.</summary>
		[Fact]
		public void RemoveKeepsStoredEventsOfRemainingPeriods()
		{
			var ledger = new Core.PeriodLedger();
			ledger.Load(StalePeriods());

			ledger.RemovePeriod(ledger.Data[^1]);

			Assert.Equal("stale", Assert.Single(ledger.Data[0].SubEvents).Note);
		}

		[Fact]
		public void CommitReplacesEarlierEvents()
		{
			var period = new Core.PurityPeriod(Day1, Day7);
			period.Commit([]);

			period.End = Day7.AddDays(2);
			period.Commit([]);

			var mikveh = Assert.Single(period.SubEvents, el => el.Type == Core.PurityEventType.Mikveh);
			Assert.Equal(Day7.AddDays(9).AddHours(12), mikveh.Stamp);
		}

		[Fact]
		public void FileHoldingOnlyNullIsAnEmptyHistory()
		{
			var saved = WithFile("null", Engines.Candidate.RoundTripFile);

			Assert.Equal("[]", System.Text.Encoding.UTF8.GetString(saved));
		}

		[Fact]
		public void UnknownEventTypeHasNoText()
		{
			Assert.Equal(string.Empty, new Core.PurityEvent((Core.PurityEventType)99, Day1).TypeRepr);
		}

		[Theory]
		[InlineData(0, "")]
		[InlineData(1, "Periods lengths\n")]
		public void ReportWithTooLittleData(int periods, string expected)
		{
			var inputs = Enumerable.Repeat(new PeriodInput(Day1, Day7, true, false), periods).ToList();

			var report = Culture.Run(Culture.Production, () => Engines.Candidate.PeriodLengthsReport(inputs));

			Assert.Equal(expected, report.Replace("\r\n", "\n"));
		}

		/// <summary>An unset End or Begin means no interval, on either side.</summary>
		[Fact]
		public void UnsetDatesGiveNoInterval()
		{
			var complete = new PeriodInput(Day1, Day7, true, false);
			var later = new PeriodInput(Day30Night, Feb5, true, false);
			var noEnd = complete with { End = DateTimeOffset.MinValue };
			var noBegin = later with { Begin = DateTimeOffset.MinValue };

			Assert.Equal((0, 0f), Engines.Candidate.Lengths(noEnd, later));
			Assert.Equal((0, 0f), Engines.Candidate.Lengths(complete, noBegin));
		}

		[Fact]
		public void SkipRemovesTheIntervalButNotTheFullLength()
		{
			var first = new PeriodInput(Day1, Day7, true, false);
			var second = new PeriodInput(Day30Night, Feb5, true, true);

			var (length, fullLength) = Engines.Candidate.Lengths(first, second);

			Assert.Equal(0, length);
			Assert.Equal(28.5f, fullLength);
		}
	}
}
