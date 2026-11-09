using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;


namespace Purity.Core
{
	/// <summary>One period of a self-test case: only what a person enters</summary>
	public sealed record SelfTestPeriod(DateTimeOffset Begin, DateTimeOffset End, bool Closed, bool SkipStreak);

	/// <summary>A made-up history the self-test calculates</summary>
	public sealed record SelfTestCase(string Name, IReadOnlyList<SelfTestPeriod> Periods);

	/// <param name="Passed">Every line matched the approved output</param>
	/// <param name="Line">The first line that differs, counting from 1, or 0</param>
	/// <param name="Expected">That line in the approved output</param>
	/// <param name="Actual">That line as calculated here</param>
	public sealed record SelfTestResult(bool Passed, int Line = 0, string? Expected = null, string? Actual = null)
	{
		public override string ToString() => Passed
			? "passed"
			: $"failed at line {Line}: expected \"{Expected ?? "nothing"}\", got \"{Actual ?? "nothing"}\"";
	}


	/// <summary>
	/// A quick check that this machine calculates exactly the approved dates. The apps run it at every
	/// start and show no dates if it fails, so a broken runtime, a platform quirk or a bad build can't
	/// put wrong dates in front of anyone or into the data file.
	/// <para/>The cases are made-up histories chosen to reach every rule: leap years with Adar I and
	/// II, onsets after dusk, Veset Haflaga tracking and its reset, skipped intervals, ends at night,
	/// Ona Beinonit across February, the year boundary, an open period, and the known issues kept
	/// for the rav's review (H1 to H4). The approved output is tests/Purity.Parity/Golden/self-test.txt,
	/// embedded here. It was made by the old production engine, and the parity tests check on every
	/// run that this engine still matches it, so it can't drift from production behavior.
	/// </summary>
	public static class SelfTest
	{
		/// <summary>Calculates every case on this machine and compares with the approved output</summary>
		public static SelfTestResult Run()
		{
			try
			{
				return Compare(Expected, Calculate());
			}
			catch (Exception e)
			{
				return new SelfTestResult(false, 0, null, "an error: " + e.GetType().Name + ": " + e.Message);
			}
		}

		/// <summary>The output of every case with this engine, in the parity harness's format</summary>
		public static string Calculate()
		{
			var text = new StringBuilder();
			text.Append(Header).Append('\n');
			foreach (var item in Cases)
			{
				text.Append("### ").Append(item.Name).Append('\n');
				var ledger = new PeriodLedger();
				ledger.Load(item.Periods.Select(el => new PurityPeriod(el.Begin, el.End) { Closed = el.Closed, SkipStreak = el.SkipStreak }));
				ledger.Recalculate();
				Describe(text, ledger);
			}
			return text.ToString();
		}

		/// <summary>Finds the first line where two outputs differ</summary>
		public static SelfTestResult Compare(string expected, string actual)
		{
			var wanted = Lines(expected);
			var got = Lines(actual);
			for (var i = 0; i < Math.Max(wanted.Length, got.Length); i++)
			{
				var a = i < wanted.Length ? wanted[i] : null;
				var b = i < got.Length ? got[i] : null;
				if (a != b)
					return new SelfTestResult(false, i + 1, a, b);
			}
			return new SelfTestResult(true);
		}

		/// <summary>The approved output, made by the old production engine</summary>
		public static string Expected => _expected ??= ReadExpected();

		/// <summary>The rules version the approved output was made under. A bump needs a new approved output.</summary>
		public static string Header => DateText.Invariant($"R|{PeriodLedger.RulesVersion}");

		public static IReadOnlyList<SelfTestCase> Cases { get; } = new[]
		{
			Case("Day onset, a full week",
				Period("2033-05-01", "2033-05-07")),
			Case("Night onset, then an end at night (H4)",
				Period("2033-05-01T12", "2033-05-07"),
				Period("2033-05-29", "2033-06-04T12"),
				Period("2033-06-27", "2033-07-03")),
			Case("Veset Haflaga tracking, reset by a longer interval (H3)",
				Period("2033-01-01", "2033-01-07"),
				Period("2033-01-30T12", "2033-02-05"),
				Period("2033-02-25T12", "2033-03-03"),
				Period("2033-03-18T12", "2033-03-24"),
				Period("2033-04-15", "2033-04-21")),
			Case("A skipped interval",
				Period("2033-01-01", "2033-01-07"),
				Period("2033-01-30T12", "2033-02-05"),
				Period("2033-02-15", "2033-02-20", skip: true),
				Period("2033-03-15", "2033-03-20")),
			Case("Adar I and Adar II in a leap year",
				Period("2035-02-19", "2035-02-25"),
				Period("2035-03-19T12", "2035-03-25"),
				Period("2035-04-17", "2035-04-23")),
			Case("Night onset on the first of a month after a 29-day month (H1)",
				Period("2031-01-24T12", "2031-01-30")),
			Case("Night onset on Rosh Hashana (H1)",
				Period("2032-09-05T12", "2032-09-11")),
			Case("The thirtieth before a 29-day month, at night (H2)",
				Period("2031-02-22T12", "2031-02-28")),
			Case("The thirtieth before a 29-day month, by day (H2)",
				Period("2031-02-23", "2031-03-01")),
			Case("Ona Beinonit across February in a leap year",
				Period("2032-01-31", "2032-02-06")),
			Case("Ona Beinonit across February in an ordinary year, and September",
				Period("2033-01-31", "2033-02-06"),
				Period("2033-08-07", "2033-08-13")),
			Case("A year typo in an end, kept until input warnings arrive",
				Period("2033-06-01", "2034-06-07"),
				Period("2033-07-01", "2033-07-07")),
			Case("An open period at the end",
				Period("2034-11-03", "2034-11-09"),
				new SelfTestPeriod(At("2034-12-01T12"), At("2034-12-08"), false, false)),
		};


		private static void Describe(StringBuilder text, PeriodLedger ledger)
		{
			PurityPeriod? previous = null;
			foreach (var period in ledger.Data)
			{
				var length = previous == null ? 0 : PurityPeriod.GetPeriodLength(previous, period);
				var fullLength = previous == null ? 0 : PurityPeriod.GetFullPeriodLength(previous, period);
				text.Append(DateText.Invariant($"P|{period.Begin:o}|{period.End:o}|{(period.Closed ? 1 : 0)}|{(period.SkipStreak ? 1 : 0)}|{length}|{fullLength.ToString("R", CultureInfo.InvariantCulture)}|{ledger.SkipLabel(period)}")).Append('\n');
				foreach (var e in period.SubEvents)
					text.Append(DateText.Invariant($"E|{(int)e.Type}|{e.Stamp:o}|{e.Note ?? "<null>"}|{e.TypeRepr}|{e.StampHebRepr}|{e.StampHebReprAvalonia}|{e.StampGrgRepr}|{(e.IsAfterDusk ? 1 : 0)}{(e.IsAfterDawn ? 1 : 0)}{(e.IsFullDay ? 1 : 0)}")).Append('\n');
				previous = period;
			}
			text.Append("T|").Append(string.Join(",", ledger.TrackedIntervals.Select(el => el.ToString(CultureInfo.InvariantCulture)))).Append('\n');
		}

		private static string ReadExpected()
		{
			using var stream = typeof(SelfTest).Assembly.GetManifestResourceStream(ResourceName)
				?? throw new InvalidOperationException("The approved self-test output is missing from this build");
			using var reader = new StreamReader(stream, new UTF8Encoding(false));
			return reader.ReadToEnd();
		}

		/// <summary>Line endings don't count: git may have changed them</summary>
		private static string[] Lines(string text) => text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');

		private static SelfTestCase Case(string name, params SelfTestPeriod[] periods) => new(name, periods);

		private static SelfTestPeriod Period(string begin, string end, bool skip = false) => new(At(begin), At(end), true, skip);

		/// <summary>A day, or a day's night onah written as "T12"</summary>
		private static DateTimeOffset At(string stamp)
		{
			var night = stamp.EndsWith("T12", StringComparison.Ordinal);
			var day = DateTime.ParseExact(night ? stamp[..^3] : stamp, "yyyy-MM-dd", CultureInfo.InvariantCulture);
			return Stamps.FromDate(day, night);
		}


		private const string ResourceName = "Purity.Core.SelfTest.expected.txt";
		private static string? _expected;
	}
}
