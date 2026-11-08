using System.Text.Json;
using Xunit;


namespace Purity.Parity
{
	/// <summary>
	/// Access to the real data file. It lives in a gitignored folder, decrypted from the encrypted
	/// fixture, and never enters the repo in plaintext.
	/// </summary>
	public static class RealData
	{
		public static string Folder =>
			Environment.GetEnvironmentVariable("PURITY_PRIVATE_DATA") is { Length: > 0 } dir ? dir : Path.Combine(RepoPaths.Root, "tests", "private");

		public static string FilePath => Path.Combine(Folder, "pureData.json");
		public static string HistoryFolder => Path.Combine(Folder, "history");
		public static string DisplayGoldenPath => Path.Combine(Folder, "real-display.golden.txt");


		/// <summary>Skips when the data isn't available, unless this run is a release gate.</summary>
		public static void Require(string path)
		{
			if (File.Exists(path) || Directory.Exists(path))
				return;

			if (Environment.GetEnvironmentVariable("PURITY_REQUIRE_REAL_DATA") == "1")
				Assert.Fail("Real data is required for this run but wasn't found");

			Assert.Skip("Real data isn't available here");
		}

		public static List<StoredPeriod> Load(string path) =>
			JsonSerializer.Deserialize<List<StoredPeriod>>(File.ReadAllText(path)) ?? [];

		public static List<PeriodInput> Inputs(IEnumerable<StoredPeriod> periods) =>
			periods.Select(el => new PeriodInput(el.Begin, el.End, el.Closed, el.SkipStreak)).ToList();
	}

	public sealed record StoredEvent(int Type, DateTimeOffset Stamp, string? Note);

	public sealed record StoredPeriod(DateTimeOffset Begin, DateTimeOffset End, bool Closed, bool SkipStreak, List<StoredEvent> SubEvents);
}
