using System;
using System.Collections.Generic;
using System.Linq;
using Purity.Core;


namespace Purity.Storage
{
	/// <summary>
	/// What a person entered about one period. Events are worked out from these fields, so these
	/// are all a merge ever touches.
	/// </summary>
	public sealed record PeriodRecord(DateTimeOffset Begin, DateTimeOffset End, bool Closed, bool SkipStreak)
	{
		public static PeriodRecord From(PurityPeriod period) => new(period.Begin, period.End, period.Closed, period.SkipStreak);

		public PurityPeriod ToPeriod() => new(Begin, End) { Closed = Closed, SkipStreak = SkipStreak };

		/// <summary>Stamps are symbolic, so the same moment written with another offset is a different stamp</summary>
		public bool SameAs(PeriodRecord other) =>
			Begin.EqualsExact(other.Begin) && End.EqualsExact(other.End) && Closed == other.Closed && SkipStreak == other.SkipStreak;
	}


	public enum MergeSide
	{
		Local,
		Remote,
	}


	/// <summary>One cycle both sides changed, or one a merge won't drop on its own</summary>
	/// <param name="Baseline">The periods before either side changed them</param>
	/// <param name="Local">The periods on this device</param>
	/// <param name="Remote">The periods in the stored file</param>
	public sealed record MergeConflict(IReadOnlyList<PeriodRecord> Baseline, IReadOnlyList<PeriodRecord> Local, IReadOnlyList<PeriodRecord> Remote);


	public sealed class MergeResult
	{
		internal MergeResult(IReadOnlyList<PeriodRecord> merged, IReadOnlyList<MergeConflict> conflicts)
		{
			Merged = merged;
			Conflicts = conflicts;
		}


		/// <summary>The full history, using the given choice for each conflict</summary>
		public IReadOnlyList<PeriodRecord> Resolve(Func<MergeConflict, MergeSide> choose) =>
			Merged.Concat(Conflicts.SelectMany(el => choose(el) == MergeSide.Local ? el.Local : el.Remote))
				.OrderBy(el => el.Begin)
				.ToList();


		/// <summary>Everything the two sides agree on, in order</summary>
		public IReadOnlyList<PeriodRecord> Merged { get; }
		public IReadOnlyList<MergeConflict> Conflicts { get; }
		public bool HasConflicts => Conflicts.Count > 0;
	}


	/// <summary>
	/// Three-way merge of two histories that grew from a common baseline.
	/// <para/>Works per cycle rather than per exact Begin. Two devices can record the same period
	/// one onah apart, and matching on Begin would keep both without asking, which would shift
	/// every date after them. Nothing is ever picked silently: when both sides changed a cycle
	/// differently, the person chooses.
	/// <para/>The sides aren't equal. A change on this device is something the person just did. A
	/// closed period that changed or went missing in the stored file may be a deliberate correction
	/// elsewhere, but it looks exactly like a stale copy written over a newer one, or a wiped file,
	/// so the person decides that too. From the stored file, only what normally happens between
	/// devices goes through without asking: new periods, and open periods moving on.
	/// </summary>
	public static class HistoryMerge
	{
		/// <summary>Periods beginning closer together than this belong to the same cycle</summary>
		public const int SameCycleDays = 14;


		public static MergeResult Merge(IReadOnlyList<PeriodRecord> baseline, IReadOnlyList<PeriodRecord> local, IReadOnlyList<PeriodRecord> remote)
		{
			var merged = new List<PeriodRecord>();
			var conflicts = new List<MergeConflict>();
			foreach (var cycle in SplitIntoCycles(baseline, local, remote))
			{
				if (Same(cycle.Local, cycle.Remote))
					merged.AddRange(cycle.Local);
				else if (Same(cycle.Local, cycle.Baseline) && KeepsClosedPeriods(cycle))
					merged.AddRange(cycle.Remote);
				else if (Same(cycle.Remote, cycle.Baseline))
					merged.AddRange(cycle.Local);
				else
					conflicts.Add(new MergeConflict(cycle.Baseline, cycle.Local, cycle.Remote));
			}

			return new MergeResult(merged, conflicts);
		}


		private static bool KeepsClosedPeriods(Cycle cycle) =>
			cycle.Remote.Count >= cycle.Baseline.Count
			&& cycle.Baseline.Where(el => el.Closed).All(el => cycle.Remote.Any(el.SameAs));

		internal static bool Same(IReadOnlyList<PeriodRecord> a, IReadOnlyList<PeriodRecord> b) =>
			a.Count == b.Count && a.Zip(b).All(el => el.First.SameAs(el.Second));

		private static List<Cycle> SplitIntoCycles(IReadOnlyList<PeriodRecord> baseline, IReadOnlyList<PeriodRecord> local, IReadOnlyList<PeriodRecord> remote)
		{
			var entries = baseline.Select(el => (Record: el, Side: 0))
				.Concat(local.Select(el => (Record: el, Side: 1)))
				.Concat(remote.Select(el => (Record: el, Side: 2)))
				.OrderBy(el => el.Record.Begin);

			var cycles = new List<Cycle>();
			DateTimeOffset? previous = null;
			foreach (var (record, side) in entries)
			{
				if (previous == null || (record.Begin - previous.Value).TotalDays >= SameCycleDays)
					cycles.Add(new Cycle());

				(side == 0 ? cycles[^1].Baseline : side == 1 ? cycles[^1].Local : cycles[^1].Remote).Add(record);
				previous = record.Begin;
			}

			return cycles;
		}


		private sealed class Cycle
		{
			public List<PeriodRecord> Baseline { get; } = new();
			public List<PeriodRecord> Local { get; } = new();
			public List<PeriodRecord> Remote { get; } = new();
		}
	}
}
