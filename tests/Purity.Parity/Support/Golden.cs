using System.Globalization;
using System.Text;
using Xunit;


namespace Purity.Parity
{
	/// <summary>
	/// Approved output files. They change only with a rules-version bump and a reviewed list of
	/// every changed date. Set PURITY_UPDATE_GOLDEN=1 to regenerate them from the engine.
	/// </summary>
	public static class Golden
	{
		public static bool Updating => Environment.GetEnvironmentVariable("PURITY_UPDATE_GOLDEN") == "1";


		public static string Read(string name) => File.ReadAllText(RepoPaths.Golden(name), new UTF8Encoding(false));

		/// <summary>Compares output with an approved file, or rewrites the file when updating.</summary>
		public static void Check(string name, string actual, string context)
		{
			if (Updating)
			{
				Directory.CreateDirectory(Path.GetDirectoryName(RepoPaths.Golden(name))!);
				File.WriteAllText(RepoPaths.Golden(name), actual, new UTF8Encoding(false));
				return;
			}

			Comparison.SameText(Read(name), actual, context);
		}
	}

	/// <summary>Synthetic histories whose full output is approved and stored in the repo.</summary>
	public static class SyntheticCorpus
	{
		public const string FileName = "synthetic-histories.txt";


		public static string Build(IEngine engine) => Culture.Run(Culture.Production, () =>
		{
			var sb = new StringBuilder();
			for (var seed = 1; seed <= 24; seed++)
			{
				var history = HistoryGenerator.Generate(seed, 30, pathological: seed % 2 == 0);
				sb.Append("### seed ").Append(seed).Append('\n');
				sb.Append(engine.Recalculate(history).ToText());
				sb.Append("### report\n");
				sb.Append(engine.PeriodLengthsReport(history).Replace("\r\n", "\n"));
			}
			return sb.ToString();
		});
	}

	/// <summary>Every Hebrew date in English letters, one per day of each kind of year</summary>
	public static class HebrewEnglishTable
	{
		public const string FileName = "hebrew-dates-english.txt";


		public static string Build(IEngine engine)
		{
			var calendar = new HebrewCalendar();
			var table = new SortedDictionary<string, string>(StringComparer.Ordinal);
			for (var day = calendar.MinSupportedDateTime.Date; day < calendar.MaxSupportedDateTime.Date; day = day.AddDays(1))
			{
				var year = calendar.GetYear(day);
				var key = $"{(calendar.IsLeapYear(year) ? 'L' : 'C')}|{calendar.GetMonth(day):00}|{calendar.GetDayOfMonth(day):00}";
				var value = engine.HebrewDateInEnglish(new DateTimeOffset(day, TimeSpan.Zero));

				if (table.TryGetValue(key, out var existing))
					Assert.True(existing == value, $"{engine.Name}: {key} has two spellings on {day:yyyy-MM-dd}");
				else
					table.Add(key, value);
			}

			var sb = new StringBuilder();
			foreach (var (key, value) in table)
				sb.Append(key).Append('|').Append(value).Append('\n');
			return sb.ToString();
		}
	}

	/// <summary>
	/// Every Hebrew date text that can appear. It depends only on the day, the month and whether the
	/// year is a leap year, so a small reviewable table covers the whole calendar.
	/// </summary>
	public static class HebrewTable
	{
		public const string FileName = "hebrew-dates.txt";


		public static string Build(IEngine engine)
		{
			var calendar = new HebrewCalendar();
			var table = new SortedDictionary<string, string>(StringComparer.Ordinal);
			for (var day = calendar.MinSupportedDateTime.Date; day < calendar.MaxSupportedDateTime.Date; day = day.AddDays(1))
			{
				var year = calendar.GetYear(day);
				var key = $"{(calendar.IsLeapYear(year) ? 'L' : 'C')}|{calendar.GetMonth(day):00}|{calendar.GetDayOfMonth(day):00}";
				var (text, textAvalonia) = engine.HebrewDate(new DateTimeOffset(day, TimeSpan.Zero));
				var value = $"{text}|{textAvalonia}";

				if (table.TryGetValue(key, out var existing))
					Assert.True(existing == value, $"{engine.Name}: {key} has two spellings on {day:yyyy-MM-dd}");
				else
					table.Add(key, value);
			}

			var sb = new StringBuilder();
			foreach (var (key, value) in table)
				sb.Append(key).Append('|').Append(value).Append('\n');
			return sb.ToString();
		}
	}
}
