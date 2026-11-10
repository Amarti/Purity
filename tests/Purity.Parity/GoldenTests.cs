using Xunit;


namespace Purity.Parity
{
	/// <summary>Layer 4 on synthetic data: output compared with approved files stored in the repo.</summary>
	public class GoldenTests
	{
		[Fact]
		public void CandidateMatchesApprovedSyntheticOutput() =>
			Golden.Check(SyntheticCorpus.FileName, SyntheticCorpus.Build(Engines.Candidate), "candidate vs approved output");

		[Fact]
		public void CandidateMatchesApprovedHebrewDatesInEnglish() =>
			Golden.Check(HebrewEnglishTable.FileName, HebrewEnglishTable.Build(Engines.Candidate), "candidate vs approved Hebrew dates in English");

		[Fact]
		public void CandidateMatchesApprovedHebrewTable() =>
			Golden.Check(HebrewTable.FileName, HebrewTable.Build(Engines.Candidate), "candidate vs approved Hebrew table");
	}
}
