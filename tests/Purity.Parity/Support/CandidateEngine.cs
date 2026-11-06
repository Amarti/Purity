extern alias Candidate;
using System.Text;
using System.Text.Json;
using Core = Candidate::Purity.Core;


namespace Purity.Parity
{
	/// <summary>Adapter for the code under test: the new Purity.Core engine.</summary>
	public sealed class CandidateEngine : IEngine
	{
		public string Name => "candidate";


		public EngineRun Recalculate(IReadOnlyList<PeriodInput> inputs, bool includeText = true)
		{
			var ledger = new Core.PeriodLedger();
			ledger.Load(inputs.Select(ToPeriod));
			ledger.Recalculate();
			return Snapshot(ledger, includeText);
		}

		public ReplayRun Replay(IReadOnlyList<PeriodInput> inputs, bool removeAndReAdd = false)
		{
			var ledger = new Core.PeriodLedger();
			var steps = new List<EngineRun>();
			foreach (var input in inputs.OrderBy(el => el.Begin))
			{
				var period = Enter(ledger, input);
				if (removeAndReAdd && ledger.Data.Count > 0 && ledger.Data[^1] == period)
				{
					ledger.RemovePeriod(period);
					Enter(ledger, input);
				}
				steps.Add(Snapshot(ledger, true));
			}
			return new ReplayRun(steps);
		}

		public IReadOnlyList<EventDump> CommitSingle(PeriodInput input, IReadOnlyList<int> tracked, bool includeText)
		{
			var period = ToPeriod(input);
			period.Commit(tracked);
			return period.SubEvents.Select(el => ToEvent(el, includeText)).ToList();
		}

		public byte[] RoundTripFile(string sourcePath)
		{
			var ledger = new Core.PeriodLedger();
			ledger.Load(LoadOrEmpty(sourcePath));
			ledger.Recalculate();
			return new UTF8Encoding(false).GetBytes(Core.PurityFile.Format(ledger.Data));
		}

		public EngineRun LoadOnly(string path)
		{
			var ledger = new Core.PeriodLedger();
			ledger.Load(LoadOrEmpty(path));
			return Snapshot(ledger, true);
		}

		public (int Length, float FullLength) Lengths(PeriodInput first, PeriodInput second) =>
			(Core.PurityPeriod.GetPeriodLength(ToPeriod(first), ToPeriod(second)), Core.PurityPeriod.GetFullPeriodLength(ToPeriod(first), ToPeriod(second)));

		public (string Text, string TextAvalonia) HebrewDate(DateTimeOffset dayStamp)
		{
			var e = new Core.PurityEvent(Core.PurityEventType.Mikveh, dayStamp);
			return (e.StampHebRepr, e.StampHebReprAvalonia);
		}

		public string PeriodLengthsReport(IReadOnlyList<PeriodInput> inputs) =>
			Core.PurityFile.PeriodLengthsReport(inputs.OrderBy(el => el.Begin).Select(ToPeriod).ToList());


		/// <summary>Mirrors how the old app treated an unreadable file, so old and new can be compared on every snapshot.</summary>
		private static List<Core.PurityPeriod> LoadOrEmpty(string path)
		{
			try
			{
				return Core.PurityFile.Parse(File.ReadAllText(path));
			}
			catch (JsonException)
			{
				return [];
			}
		}

		private static Core.PurityPeriod ToPeriod(PeriodInput input) =>
			new(input.Begin, input.End) { Closed = input.Closed, SkipStreak = input.SkipStreak };

		private static Core.PurityPeriod Enter(Core.PeriodLedger ledger, PeriodInput input)
		{
			var period = new Core.PurityPeriod(input.Begin, input.End) { SkipStreak = input.SkipStreak };
			ledger.Data.Add(period);
			if (input.Closed)
				ledger.AcceptPeriod(period);
			return period;
		}

		private static EngineRun Snapshot(Core.PeriodLedger ledger, bool includeText)
		{
			var periods = new List<PeriodDump>(ledger.Data.Count);
			Core.PurityPeriod? previous = null;
			foreach (var period in ledger.Data)
			{
				periods.Add(new PeriodDump(
					period.Begin.ToString("o"),
					period.End.ToString("o"),
					period.Closed,
					period.SkipStreak,
					previous == null ? 0 : Core.PurityPeriod.GetPeriodLength(previous, period),
					previous == null ? 0 : Core.PurityPeriod.GetFullPeriodLength(previous, period),
					ledger.SkipLabel(period),
					period.SubEvents.Select(el => ToEvent(el, includeText)).ToList()));
				previous = period;
			}
			return new EngineRun(periods, string.Join(',', ledger.TrackedIntervals));
		}

		private static EventDump ToEvent(Core.PurityEvent e, bool includeText) =>
			new(
				(int)e.Type,
				e.Stamp.ToString("o"),
				e.Note,
				e.TypeRepr,
				includeText ? e.StampHebRepr : string.Empty,
				includeText ? e.StampHebReprAvalonia : string.Empty,
				includeText ? e.StampGrgRepr : string.Empty,
				e.IsAfterDusk,
				e.IsAfterDawn,
				e.IsFullDay);
	}
}
