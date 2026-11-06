extern alias RefV1;
using System.Reflection;
using System.Text;
using Core = RefV1::Purity;
using Vm = RefV1::Purity.ViewModels;


namespace Purity.Parity
{
	/// <summary>Adapter for the frozen engine at tag engine-v1-reference.</summary>
	public sealed class ReferenceEngine : IEngine
	{
		public string Name => "reference";


		public EngineRun Recalculate(IReadOnlyList<PeriodInput> inputs, bool includeText = true)
		{
			var vm = NewViewModel();
			foreach (var input in inputs.OrderBy(el => el.Begin))
				vm.Data.Add(ToPeriod(input));

			vm.Recalculate();
			return Snapshot(vm, includeText);
		}

		public ReplayRun Replay(IReadOnlyList<PeriodInput> inputs, bool removeAndReAdd = false)
		{
			var vm = NewViewModel();
			var steps = new List<EngineRun>();
			foreach (var input in inputs.OrderBy(el => el.Begin))
			{
				var period = Enter(vm, input);
				if (removeAndReAdd && vm.Data.Count > 0 && vm.Data[^1] == period)
				{
					vm.RemovePeriod(period);
					Enter(vm, input);
				}
				steps.Add(Snapshot(vm, true));
			}
			return new ReplayRun(steps);
		}

		public IReadOnlyList<EventDump> CommitSingle(PeriodInput input, IReadOnlyList<int> tracked, bool includeText)
		{
			var period = ToPeriod(input);
			period.Commit(Core.CultureHolder.Instance.HebrewCalendar, tracked.ToList());
			return period.SubEvents.Select(el => ToEvent(el, includeText)).ToList();
		}

		public byte[] RoundTripFile(string sourcePath)
		{
			var target = Path.Combine(Path.GetTempPath(), $"purity-parity-{Guid.NewGuid():N}.json");
			try
			{
				var vm = NewViewModel();
				vm.SetSettings(new Core.Settings { DataFilePath = sourcePath });
				vm.Recalculate();
				Core.DataSerializer.SerializeData(vm.Data, target);
				return File.ReadAllBytes(target);
			}
			finally
			{
				File.Delete(target);
			}
		}

		public EngineRun LoadOnly(string path)
		{
			var vm = NewViewModel();
			vm.SetSettings(new Core.Settings { DataFilePath = path });
			return Snapshot(vm, true);
		}

		public (int Length, float FullLength) Lengths(PeriodInput first, PeriodInput second) =>
			(Core.PurityPeriod.GetPeriodLength(ToPeriod(first), ToPeriod(second)), Core.PurityPeriod.GetFullPeriodLength(ToPeriod(first), ToPeriod(second)));

		public (string Text, string TextAvalonia) HebrewDate(DateTimeOffset dayStamp)
		{
			var e = new Core.PurityEvent(Core.PurityEventType.Mikveh, dayStamp);
			return (e.StampHebRepr, e.StampHebReprAvalonia);
		}

		public string PeriodLengthsReport(IReadOnlyList<PeriodInput> inputs)
		{
			var target = Path.Combine(Path.GetTempPath(), $"purity-parity-{Guid.NewGuid():N}.csv");
			try
			{
				var data = inputs.OrderBy(el => el.Begin).Select(ToPeriod).ToList();
				Core.DataSerializer.ExportPeriodsLengthsReport(data, target);
				return File.Exists(target) ? File.ReadAllText(target, Encoding.UTF8) : string.Empty;
			}
			finally
			{
				File.Delete(target);
			}
		}


		private static Vm.MainWindowViewModelImpl<Vm.PurityPeriodViewModelImpl> NewViewModel() =>
			new(new Core.Settings(), (period, owner) => new Vm.PurityPeriodViewModelImpl(period, owner));

		private static Core.PurityPeriod ToPeriod(PeriodInput input) =>
			new(input.Begin, input.End) { Closed = input.Closed, SkipStreak = input.SkipStreak };

		private static Core.PurityPeriod Enter(Vm.MainWindowViewModelImpl<Vm.PurityPeriodViewModelImpl> vm, PeriodInput input)
		{
			var period = new Core.PurityPeriod(input.Begin, input.End) { SkipStreak = input.SkipStreak };
			vm.Data.Add(period);
			vm.PurityPeriods.Add(new Vm.PurityPeriodViewModelImpl(period, vm));
			if (input.Closed)
				vm.AcceptPeriod(period);
			return period;
		}

		private static EngineRun Snapshot(Vm.MainWindowViewModelImpl<Vm.PurityPeriodViewModelImpl> vm, bool includeText)
		{
			var periods = new List<PeriodDump>(vm.Data.Count);
			Core.PurityPeriod? previous = null;
			foreach (var period in vm.Data)
			{
				var periodVm = new Vm.PurityPeriodViewModelImpl(period, vm);
				periodVm.UpdatePeriodLength();
				periods.Add(new PeriodDump(
					period.Begin.ToString("o"),
					period.End.ToString("o"),
					period.Closed,
					period.SkipStreak,
					previous == null ? 0 : Core.PurityPeriod.GetPeriodLength(previous, period),
					previous == null ? 0 : Core.PurityPeriod.GetFullPeriodLength(previous, period),
					periodVm.SkipPeriodLength,
					period.SubEvents.Select(el => ToEvent(el, includeText)).ToList()));
				previous = period;
			}
			return new EngineRun(periods, string.Join(',', TrackedIntervals(vm)));
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

		private static List<int> TrackedIntervals(object vm) =>
			(List<int>)vm.GetType().GetField("_recentPeriodsStreak", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(vm)!;
	}
}
