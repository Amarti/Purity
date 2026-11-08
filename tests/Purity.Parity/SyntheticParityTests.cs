using System.Collections.Concurrent;
using Xunit;


namespace Purity.Parity
{
	/// <summary>Layers 3, 5, 6 and 9 on synthetic data: the code under test against the live reference.</summary>
	public class SyntheticParityTests
	{
		private static readonly int[] EndOffsets = [3, 4, 5, 6, 7, 9, 12, 20];
		private static readonly int[][] TrackedSets = [[], [33], [46, 40, 30]];


		/// <summary>Layer 5: every date from 1900 to 2100, both onot, a range of period lengths.</summary>
		[Fact]
		public void EveryDateBehavesTheSame()
		{
			var reference = Engines.Reference;
			var failures = new ConcurrentQueue<string>();
			var includeText = Platform.ReferenceTextIsTruth;

			Parallel.For(1900, 2101, year =>
			{
				Culture.Run(Culture.Production, () =>
				{
					for (var day = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero); day.Year == year; day = day.AddDays(1))
						foreach (var beginHours in new[] { 0, 12 })
							foreach (var offset in EndOffsets)
								foreach (var endHours in new[] { 0, 12 })
								{
									var input = new PeriodInput(day.AddHours(beginHours), day.AddDays(offset).AddHours(endHours), true, false);
									var tracked = TrackedSets[(day.Day + offset) % TrackedSets.Length];
									var text = includeText && offset == 6;

									var expected = reference.CommitSingle(input, tracked, text);
									var actual = Engines.Candidate.CommitSingle(input, tracked, text);

									if (!expected.SequenceEqual(actual))
										failures.Enqueue($"{input}: expected {string.Join("; ", expected.Select(el => el.ToLine()))} actual {string.Join("; ", actual.Select(el => el.ToLine()))}");
								}
					return 0;
				});
			});

			Assert.True(failures.IsEmpty, $"{failures.Count} differences, first: {failures.FirstOrDefault()}");
		}

		public static TheoryData<int, bool> Seeds()
		{
			var data = new TheoryData<int, bool>();
			foreach (var seed in FixedSeeds().Concat(NightlySeeds()))
				data.Add(seed, seed % 2 == 0);
			return data;
		}

		/// <summary>Layer 6: whole histories, including bad input, give the same result.</summary>
		[Theory]
		[MemberData(nameof(Seeds))]
		public void RandomHistoriesBehaveTheSame(int seed, bool pathological)
		{
			var history = HistoryGenerator.Generate(seed, 40, pathological);

			var (expected, actual) = Culture.Run(Culture.Production, () => (Engines.Reference.Recalculate(history), Engines.Candidate.Recalculate(history)));

			Comparison.SameText(Comparison.Dump(expected), Comparison.Dump(actual), $"seed {seed}");
		}

		/// <summary>Layer 3: the same histories entered one period at a time, with removal and re-entry.</summary>
		[Theory]
		[MemberData(nameof(Seeds))]
		public void RandomReplaysBehaveTheSame(int seed, bool pathological)
		{
			var history = HistoryGenerator.Generate(seed, 25, pathological);

			foreach (var removeAndReAdd in new[] { false, true })
			{
				var (expected, actual) = Culture.Run(Culture.Production, () => (Engines.Reference.Replay(history, removeAndReAdd), Engines.Candidate.Replay(history, removeAndReAdd)));

				Assert.Equal(expected.Steps.Count, actual.Steps.Count);
				for (var i = 0; i < expected.Steps.Count; i++)
					Comparison.SameText(Comparison.Dump(expected.Steps[i]), Comparison.Dump(actual.Steps[i]), $"seed {seed} step {i} removeAndReAdd={removeAndReAdd}");
			}
		}

		/// <summary>Layer 9: the old app reads and recalculates synthetic files written by the new one.</summary>
		[Theory]
		[InlineData(2)]
		[InlineData(3)]
		public void OldAppReadsNewFiles(int seed)
		{
			var history = HistoryGenerator.Generate(seed, 30, pathological: false);
			var source = Path.Combine(Path.GetTempPath(), $"purity-parity-{Guid.NewGuid():N}.json");
			var written = source + ".new";
			try
			{
				var json = System.Text.Json.JsonSerializer.Serialize(history.Select(el => new StoredPeriod(el.Begin, el.End, el.Closed, el.SkipStreak, [])));
				File.WriteAllText(source, json);

				var fromCandidate = Engines.Candidate.RoundTripFile(source);
				File.WriteAllBytes(written, fromCandidate);
				var fromReference = Engines.Reference.RoundTripFile(written);

				Comparison.SameText(Text(fromReference), Text(fromCandidate), $"seed {seed}");
				Assert.True(fromCandidate.Length > 100, "nothing was written");
			}
			finally
			{
				File.Delete(source);
				File.Delete(written);
			}
		}


		private static string Text(byte[] content) => System.Text.Encoding.UTF8.GetString(content).Replace("\r\n", "\n");

		private static IEnumerable<int> FixedSeeds() => Enumerable.Range(1, 200);

		/// <summary>The nightly run sets PURITY_RANDOM_SEED so every night explores new histories.</summary>
		private static IEnumerable<int> NightlySeeds() =>
			int.TryParse(Environment.GetEnvironmentVariable("PURITY_RANDOM_SEED"), out var start) ? Enumerable.Range(start, 500) : [];
	}
}
