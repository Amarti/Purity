using Xunit;


namespace Purity.Parity
{
	/// <summary>
	/// The redacted comparison is all that stands between real data and a public CI log, so its own
	/// behavior is pinned here with synthetic text.
	/// </summary>
	public class ComparisonTests
	{
		private static Exception Failure(string expected, string actual) =>
			Assert.ThrowsAny<Exception>(() => Comparison.SameTextRedacted(expected, actual, "context"));


		[Fact]
		public void RedactedAcceptsIdenticalText()
		{
			Comparison.SameTextRedacted("1|2033-05-14|x\n", "1|2033-05-14|x\n", "context");
		}

		[Theory]
		[InlineData("a\n", "a\n\n")]
		[InlineData("a\n\n", "a\n")]
		[InlineData("a", "a\n")]
		public void RedactedRejectsADifferenceInTrailingBlankLines(string expected, string actual)
		{
			Assert.Contains("context", Failure(expected, actual).Message);
		}

		[Theory]
		[InlineData("1|2033-05-14|x\n", "1|2033-05-15|x\n", "line 1, field 1")]
		[InlineData("1|2033-05-14|x\n", "1|2033-05-14|y\n", "line 1, field 2")]
		[InlineData("a\n1|2033-05-14|x\n", "a\n", "line 2, field 0")]
		[InlineData("a\n", "a\n1|2033-05-14|x\n", "line 2, field 0")]
		public void RedactedFailureNamesAPositionAndNoValue(string expected, string actual, string position)
		{
			var message = Failure(expected, actual).Message;

			Assert.Contains(position, message);
			Assert.DoesNotContain("2033", message);
			Assert.DoesNotContain("|", message);
		}
	}
}
