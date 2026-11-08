using Xunit;


namespace Purity.Parity
{
	/// <summary>
	/// Layer 10: the machine must not change the output. The reference always runs under the
	/// production culture; the code under test runs under each hostile culture. Time zones and the
	/// ICU or NLS choice are set from outside the process by the CI matrix, which repeats this suite.
	/// </summary>
	public class EnvironmentTests
	{
		public static TheoryData<string> Cultures => ["en-US", "en-IL", "he-IL", "de-DE", "ar-SA", "th-TH", "fa-IR", "ru-RU"];


		[Theory]
		[MemberData(nameof(Cultures))]
		public void CultureDoesNotChangeResults(string culture)
		{
			foreach (var seed in new[] { 11, 12, 13 })
			{
				var history = HistoryGenerator.Generate(seed, 30, pathological: seed % 2 == 0);

				var expected = Culture.Run(Culture.Production, () => Engines.Reference.Recalculate(history));
				var actual = Culture.Run(culture, () => Engines.Candidate.Recalculate(history));

				Comparison.SameText(Comparison.Dump(expected), Comparison.Dump(actual), $"culture {culture}, seed {seed}");
			}
		}

		[Theory]
		[MemberData(nameof(Cultures))]
		public void CultureDoesNotChangeTheReport(string culture)
		{
			var history = HistoryGenerator.Generate(5, 30, pathological: false);

			var expected = Culture.Run(Culture.Production, () => Engines.Reference.PeriodLengthsReport(history));
			var actual = Culture.Run(culture, () => Engines.Candidate.PeriodLengthsReport(history));

			Comparison.SameText(expected.Replace("\r\n", "\n"), actual.Replace("\r\n", "\n"), $"report under {culture}");
		}

		[Theory]
		[MemberData(nameof(Cultures))]
		public void CultureDoesNotChangeTheFile(string culture)
		{
			var history = HistoryGenerator.Generate(6, 30, pathological: true);
			var source = Path.Combine(Path.GetTempPath(), $"purity-parity-{Guid.NewGuid():N}.json");
			try
			{
				File.WriteAllText(source, System.Text.Json.JsonSerializer.Serialize(history.Select(el => new StoredPeriod(el.Begin, el.End, el.Closed, el.SkipStreak, []))));

				var expected = Culture.Run(Culture.Production, () => Engines.Reference.RoundTripFile(source));
				var actual = Culture.Run(culture, () => Engines.Candidate.RoundTripFile(source));

				Comparison.SameText(Text(expected), Text(actual), $"file under {culture}");
			}
			finally
			{
				File.Delete(source);
			}
		}


		private static string Text(byte[] content) => System.Text.Encoding.UTF8.GetString(content).Replace("\r\n", "\n");
	}
}
