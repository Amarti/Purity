using System.Text;
using Xunit;


namespace Purity.Parity
{
	/// <summary>
	/// Layers 1 to 4, 8 and 9 on the real data. These assert only through the redacted comparison:
	/// a failure names a position, never a date.
	/// </summary>
	public class RealDataTests
	{
		public static TheoryData<string> BothEngines => ["reference", "candidate"];


		private static IEngine Engine(string name) => name == "reference" ? Engines.Reference : Engines.Candidate;

		private static string StoredEventsText(IEnumerable<StoredPeriod> periods)
		{
			var sb = new StringBuilder();
			foreach (var period in periods)
			{
				sb.Append("P\n");
				foreach (var e in period.SubEvents)
					sb.Append($"{e.Type}|{e.Stamp:o}|{e.Note ?? "<null>"}\n");
			}
			return sb.ToString();
		}

		private static string CalculatedEventsText(EngineRun run)
		{
			var sb = new StringBuilder();
			foreach (var period in run.Periods)
			{
				sb.Append("P\n");
				foreach (var e in period.Events)
					sb.Append($"{e.Type}|{e.Stamp}|{e.Note ?? "<null>"}\n");
			}
			return sb.ToString();
		}


		/// <summary>Layer 1: recalculating the whole history reproduces every stored event exactly.</summary>
		[Theory]
		[MemberData(nameof(BothEngines))]
		public void RecalculationReproducesStoredHistory(string engine)
		{
			RealData.Require(RealData.FilePath);
			var stored = RealData.Load(RealData.FilePath);
			Assert.NotEmpty(stored);

			var run = Engine(engine).Recalculate(RealData.Inputs(stored), includeText: false);

			Comparison.SameTextRedacted(StoredEventsText(stored), CalculatedEventsText(run), $"{engine} vs stored history");
		}

		/// <summary>Layer 2: load, recalculate and save produces the same file, byte for byte.</summary>
		[Theory]
		[MemberData(nameof(BothEngines))]
		public void SavedFileIsByteIdentical(string engine)
		{
			RealData.Require(RealData.FilePath);
			var original = File.ReadAllBytes(RealData.FilePath);

			var saved = Engine(engine).RoundTripFile(RealData.FilePath);

			if (engine == "reference" && !OperatingSystem.IsWindows())
				Assert.True(Normalized(original) == Normalized(saved), "reference round trip changed more than line endings");
			else
				Assert.True(original.AsSpan().SequenceEqual(saved), $"{engine} round trip changed the file");
		}

		/// <summary>Layer 3: entering the history one period at a time gives the same result at every step.</summary>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ReplayMatchesAtEveryStep(bool removeAndReAdd)
		{
			RealData.Require(RealData.FilePath);
			var inputs = RealData.Inputs(RealData.Load(RealData.FilePath));

			var (reference, candidate) = Culture.Run(Culture.Production, () =>
				(Engines.Reference.Replay(inputs, removeAndReAdd), Engines.Candidate.Replay(inputs, removeAndReAdd)));

			Assert.Equal(reference.Steps.Count, candidate.Steps.Count);
			for (var i = 0; i < reference.Steps.Count; i++)
				Comparison.SameTextRedacted(Comparison.Dump(reference.Steps[i]), Comparison.Dump(candidate.Steps[i]), $"replay step {i}");

			var recalculated = Culture.Run(Culture.Production, () => Engines.Candidate.Recalculate(inputs));
			Comparison.SameTextRedacted(Comparison.Dump(recalculated), Comparison.Dump(candidate.Final), "replay vs recalculation");
		}

		/// <summary>Layer 4: every visible string for every real event matches what was approved.</summary>
		[Fact]
		public void DisplayMatchesApprovedOutput()
		{
			RealData.Require(RealData.FilePath);
			var inputs = RealData.Inputs(RealData.Load(RealData.FilePath));
			var dump = (IEngine engine) => Culture.Run(Culture.Production, () =>
				engine.Recalculate(inputs).ToText() + engine.PeriodLengthsReport(inputs).Replace("\r\n", "\n"));

			if (Golden.Updating && Platform.ReferenceTextIsTruth)
				File.WriteAllText(RealData.DisplayGoldenPath, dump(Engines.Reference), new UTF8Encoding(false));

			RealData.Require(RealData.DisplayGoldenPath);
			var approved = File.ReadAllText(RealData.DisplayGoldenPath, new UTF8Encoding(false));

			if (Platform.ReferenceTextIsTruth)
				Comparison.SameTextRedacted(approved, dump(Engines.Reference), "reference vs approved display");
			Comparison.SameTextRedacted(approved, dump(Engines.Candidate), "candidate vs approved display");
		}

		/// <summary>Layer 8: both engines treat every historic snapshot alike, including old offset formats.</summary>
		[Fact]
		public void HistoricSnapshotsBehaveTheSame()
		{
			RealData.Require(RealData.HistoryFolder);
			var files = Directory.GetFiles(RealData.HistoryFolder, "*.json").Order(StringComparer.Ordinal).ToList();
			Assert.NotEmpty(files);

			for (var i = 0; i < files.Count; i++)
			{
				var reference = Engines.Reference.RoundTripFile(files[i]);
				var candidate = Engines.Candidate.RoundTripFile(files[i]);
				Assert.True(Normalized(reference) == Normalized(candidate), $"snapshot {i} differs");
			}
		}

		/// <summary>Layer 9: the old app reads what the new one writes and calculates the same.</summary>
		[Fact]
		public void OldAppCanReadNewFiles()
		{
			RealData.Require(RealData.FilePath);
			var written = Path.Combine(Path.GetTempPath(), $"purity-parity-{Guid.NewGuid():N}.json");
			try
			{
				var fromCandidate = Engines.Candidate.RoundTripFile(RealData.FilePath);
				File.WriteAllBytes(written, fromCandidate);

				var fromReference = Engines.Reference.RoundTripFile(written);

				Assert.True(Normalized(fromCandidate) == Normalized(fromReference), "reference recalculated a new file differently");
			}
			finally
			{
				File.Delete(written);
			}
		}


		private static string Normalized(byte[] content) => Encoding.UTF8.GetString(content).Replace("\r\n", "\n");
	}
}
