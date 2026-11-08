namespace Purity.Parity
{
	/// <summary>Builds reproducible synthetic histories. A failing seed always rebuilds the same history.</summary>
	public static class HistoryGenerator
	{
		public static List<PeriodInput> Generate(int seed, int count, bool pathological)
		{
			var random = new Random(seed);
			var day = new DateTimeOffset(1905 + random.Next(190), 1 + random.Next(12), 1 + random.Next(28), 0, 0, 0, TimeSpan.Zero);
			var result = new List<PeriodInput>(count);

			for (var i = 0; i < count; i++)
			{
				var length = 3 + random.Next(12);
				var begin = Half(day, random.Next(3) == 0);
				var end = Half(day.AddDays(length), random.Next(5) == 0);
				var closed = i < count - 1 || random.Next(3) != 0;
				var skip = random.Next(12) == 0;

				if (pathological)
					switch (random.Next(16))
					{
						case 0:
							end = begin.AddDays(-1 - random.Next(5));	// End before Begin
							break;
						case 1:
							end = end.AddYears(1);						// year typo in the End date
							break;
						case 2:
							end = begin;								// zero-length period
							break;
					}

				result.Add(new PeriodInput(begin, end, closed, skip));

				var gap = 12 + random.Next(40);
				if (pathological && random.Next(16) == 0)
					gap = random.Next(3) - length;						// next period overlaps this one
				day = day.AddDays(length + gap);
			}

			return result;
		}


		private static DateTimeOffset Half(DateTimeOffset day, bool night) => night ? day.AddHours(12) : day;
	}
}
