using System.Globalization;
using System.Text;


namespace Purity.Parity
{
	/// <summary>The only facts a user enters for a period; everything else is calculated.</summary>
	public sealed record PeriodInput(DateTimeOffset Begin, DateTimeOffset End, bool Closed, bool SkipStreak);

	public sealed record EventDump(
		int Type,
		string Stamp,
		string? Note,
		string TypeText,
		string HebrewText,
		string HebrewTextAvalonia,
		string GregorianText,
		bool Dusk,
		bool Dawn,
		bool FullDay)
	{
		public string ToLine() =>
			$"E|{Type}|{Stamp}|{Note ?? "<null>"}|{TypeText}|{HebrewText}|{HebrewTextAvalonia}|{GregorianText}|{(Dusk ? 1 : 0)}{(Dawn ? 1 : 0)}{(FullDay ? 1 : 0)}";

		/// <summary>Same event without month-name text, for platforms whose month names can't be trusted.</summary>
		public EventDump WithoutOsText() => this with { HebrewText = string.Empty, HebrewTextAvalonia = string.Empty, GregorianText = string.Empty };
	}

	public sealed record PeriodDump(
		string Begin,
		string End,
		bool Closed,
		bool SkipStreak,
		int Length,
		float FullLength,
		string SkipLabel,
		IReadOnlyList<EventDump> Events)
	{
		public string HeaderLine() =>
			$"P|{Begin}|{End}|{(Closed ? 1 : 0)}|{(SkipStreak ? 1 : 0)}|{Length}|{FullLength.ToString("R", CultureInfo.InvariantCulture)}|{SkipLabel}";
	}

	/// <summary>The canonical result of an engine run: everything a user can see and everything that feeds it.</summary>
	public sealed record EngineRun(IReadOnlyList<PeriodDump> Periods, string Tracked)
	{
		public string ToText(bool includeOsText = true)
		{
			var sb = new StringBuilder();
			foreach (var p in Periods)
			{
				sb.Append(p.HeaderLine()).Append('\n');
				foreach (var e in p.Events)
					sb.Append((includeOsText ? e : e.WithoutOsText()).ToLine()).Append('\n');
			}
			sb.Append("T|").Append(Tracked).Append('\n');
			return sb.ToString();
		}
	}

	/// <summary>State after every step of entering periods one at a time.</summary>
	public sealed record ReplayRun(IReadOnlyList<EngineRun> Steps)
	{
		public EngineRun Final => Steps[^1];
	}
}
