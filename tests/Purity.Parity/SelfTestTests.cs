extern alias Candidate;
using System.Text;
using Xunit;
using Core = Candidate::Purity.Core;


namespace Purity.Parity
{
	/// <summary>
	/// The self-test the apps run at every start. Its approved output was made by the old
	/// production engine, changes only under the approved-output rule, and must be what the apps carry.
	/// </summary>
	public class SelfTestTests
	{
		public const string FileName = "self-test.txt";


		[Fact]
		public void TheEngineMatchesTheApprovedOutput() =>
			Golden.Check(FileName, Build(Engines.Candidate), "candidate vs approved self-test");

		[Fact]
		public void TheAppsCarryTheApprovedOutput() =>
			Assert.Equal(Golden.Read(FileName).Replace("\r\n", "\n"), Core.SelfTest.Expected.Replace("\r\n", "\n"));

		/// <summary>The apps' own run, calculated by Core itself, on whatever machine runs the tests</summary>
		[Fact]
		public void TheSelfTestPassesHere()
		{
			var result = Core.SelfTest.Run();
			Assert.True(result.Passed, result.ToString());
		}

		[Fact]
		public void AChangedDateIsCaughtWithItsLine()
		{
			var lines = Core.SelfTest.Expected.Replace("\r\n", "\n").Split('\n');
			var index = Array.FindIndex(lines, el => el.StartsWith("E|2|", StringComparison.Ordinal));
			var original = lines[index];
			lines[index] = original.Replace(":00:00.0000000+00:00", ":00:00.0000000+03:00");

			var result = Core.SelfTest.Compare(Core.SelfTest.Expected, string.Join('\n', lines));

			Assert.False(result.Passed);
			Assert.Equal(index + 1, result.Line);
			Assert.Equal(original, result.Expected);
			Assert.Equal(lines[index], result.Actual);
			Assert.False(Core.SelfTest.Compare(Core.SelfTest.Expected, string.Join('\n', lines.Take(index))).Passed);	// cut short
		}

		[Fact]
		public void TheCasesReachEveryRule()
		{
			var text = Golden.Read(FileName);

			Assert.StartsWith(Core.SelfTest.Header + "\n", text.Replace("\r\n", "\n"));
			foreach (var type in new[] { "E|1|", "E|2|", "E|3|", "E|4|" })
				Assert.Contains(type, text);										// Mikveh, Veset Hachodesh, Veset Aflaga, Ona Beinonit
			Assert.Contains("T12:00:00.0000000+00:00|", text);						// a stamp after dusk
			Assert.Contains("|1|1|0|", text);										// a skipped period
			Assert.Contains("|0|0|", text);											// an open period
			Assert.Contains("(-", text);											// the negative interval of a year typo
			Assert.Contains("אדר א׳", text);
			Assert.Contains("אדר ב׳", text);
			Assert.Contains("ל' שבט", text);										// H1
			Assert.Contains("28-29 Feb", text);
			Assert.Contains("28 Feb - 1 Mar", text);
			Assert.Contains(" Sept", text);
		}


		/// <summary>Every case's output in the harness's format, as the self-test writes it</summary>
		private static string Build(IEngine engine) => Culture.Run(Culture.Production, () =>
		{
			var text = new StringBuilder(Core.SelfTest.Header).Append('\n');
			foreach (var item in Core.SelfTest.Cases)
			{
				text.Append("### ").Append(item.Name).Append('\n');
				var inputs = item.Periods.Select(el => new PeriodInput(el.Begin, el.End, el.Closed, el.SkipStreak)).ToList();
				text.Append(engine.Recalculate(inputs).ToText());
			}
			return text.ToString();
		});
	}
}
