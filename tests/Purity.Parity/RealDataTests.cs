using System.Text;
using Xunit;


namespace Purity.Parity
{
	/// <summary>
	/// Layers 1 to 4 and 8 on the real data. These assert only through the redacted comparison:
	/// a failure names a position, never a date.
	/// </summary>
	public class RealDataTests
	{
		/// <summary>
		/// Snapshots that hold periods. The rest predate the current file format and read as empty.
		/// An unreadable file also reads as empty, so this count is what would notice one.
		/// </summary>
		private const int ReadableSnapshots = 22;


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
		[Fact]
		public void RecalculationReproducesStoredHistory()
		{
			RealData.Require(RealData.FilePath);
			var stored = RealData.Load(RealData.FilePath);
			Assert.NotEmpty(stored);

			var run = Engines.Candidate.Recalculate(RealData.Inputs(stored), includeText: false);

			Comparison.SameTextRedacted(StoredEventsText(stored), CalculatedEventsText(run), "engine vs stored history");
		}

		/// <summary>Layer 2: load, recalculate and save produces the same file, byte for byte.</summary>
		[Fact]
		public void SavedFileIsByteIdentical()
		{
			RealData.Require(RealData.FilePath);
			var original = File.ReadAllBytes(RealData.FilePath);

			var saved = Engines.Candidate.RoundTripFile(RealData.FilePath);

			Assert.True(original.AsSpan().SequenceEqual(saved), "round trip changed the file");
		}

		/// <summary>Layer 3: after each period entered, the result is what recalculating those periods gives.</summary>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ReplayMatchesAtEveryStep(bool removeAndReAdd)
		{
			RealData.Require(RealData.FilePath);
			var inputs = RealData.Inputs(RealData.Load(RealData.FilePath)).OrderBy(el => el.Begin).ToList();

			var replay = Culture.Run(Culture.Production, () => Engines.Candidate.Replay(inputs, removeAndReAdd));

			Assert.Equal(inputs.Count, replay.Steps.Count);
			for (var i = 0; i < inputs.Count; i++)
			{
				var soFar = inputs.Take(i + 1).ToList();
				var recalculated = Culture.Run(Culture.Production, () => Engines.Candidate.Recalculate(soFar));
				Comparison.SameTextRedacted(recalculated.ToText(), replay.Steps[i].ToText(), $"replay step {i}");
			}
		}

		/// <summary>Layer 4: every visible string for every real event matches what was approved.</summary>
		[Fact]
		public void DisplayMatchesApprovedOutput()
		{
			RealData.Require(RealData.FilePath);
			var inputs = RealData.Inputs(RealData.Load(RealData.FilePath));
			var actual = Culture.Run(Culture.Production, () =>
				Engines.Candidate.Recalculate(inputs).ToText() + Engines.Candidate.PeriodLengthsReport(inputs).Replace("\r\n", "\n"));

			if (Golden.Updating)
				File.WriteAllText(RealData.DisplayGoldenPath, actual, new UTF8Encoding(false));

			RealData.Require(RealData.DisplayGoldenPath);
			var approved = File.ReadAllText(RealData.DisplayGoldenPath, new UTF8Encoding(false));

			Comparison.SameTextRedacted(approved, actual, "engine vs approved display");
		}

		/// <summary>Layer 8: the historic snapshots, with their old offset formats, can still be read.</summary>
		[Fact]
		public void HistoricSnapshotsCanBeRead()
		{
			RealData.Require(RealData.HistoryFolder);
			var files = Directory.GetFiles(RealData.HistoryFolder, "*.json");
			Assert.NotEmpty(files);

			var readable = files.Count(el => Encoding.UTF8.GetString(Engines.Candidate.RoundTripFile(el)).Trim() != "[]");

			Assert.True(readable >= ReadableSnapshots, $"only {readable} snapshots could be read, expected at least {ReadableSnapshots}");
		}
	}
}
