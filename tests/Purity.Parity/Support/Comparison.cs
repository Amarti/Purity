using Xunit;


namespace Purity.Parity
{
	public static class Comparison
	{
		/// <summary>Compares two canonical dumps and names the first differing line, showing both versions.</summary>
		public static void SameText(string expected, string actual, string context)
		{
			if (expected == actual)
				return;

			var (e, a) = (expected.Split('\n'), actual.Split('\n'));
			for (var i = 0; i < Math.Max(e.Length, a.Length); i++)
			{
				var (le, la) = (i < e.Length ? e[i] : "<missing>", i < a.Length ? a[i] : "<missing>");
				if (le != la)
					Assert.Fail($"{context}: line {i + 1} differs\n  expected: {le}\n  actual:   {la}");
			}
		}

		/// <summary>
		/// Same comparison for real data. Actions logs on a public repo are public, so this reports
		/// only where the difference is and never prints a value.
		/// </summary>
		public static void SameTextRedacted(string expected, string actual, string context)
		{
			if (expected == actual)
				return;

			var (e, a) = (expected.Split('\n'), actual.Split('\n'));
			for (var i = 0; i < Math.Max(e.Length, a.Length); i++)
			{
				var (le, la) = (i < e.Length ? e[i] : string.Empty, i < a.Length ? a[i] : string.Empty);
				if (le == la)
					continue;

				var (fe, fa) = (le.Split('|'), la.Split('|'));
				var field = 0;
				while (field < Math.Min(fe.Length, fa.Length) && fe[field] == fa[field])
					field++;

				Assert.Fail($"{context}: differs at line {i + 1}, field {field} (values withheld: real data)");
			}

			Assert.Fail($"{context}: differs in trailing blank lines");
		}

		/// <summary>Canonical text for comparing with a live reference run on this platform.</summary>
		public static string Dump(EngineRun run) => run.ToText(Platform.ReferenceTextIsTruth);
	}
}
