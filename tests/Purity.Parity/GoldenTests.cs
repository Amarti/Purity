using Xunit;


namespace Purity.Parity
{
	/// <summary>Layer 4 on synthetic data: output compared with approved files stored in the repo.</summary>
	public class GoldenTests
	{
		[Fact]
		public void ReferenceMatchesApprovedSyntheticOutput() =>
			Golden.CheckReference(SyntheticCorpus.FileName, SyntheticCorpus.Build(Engines.Reference));

		[Fact]
		public void CandidateMatchesApprovedSyntheticOutput() =>
			Comparison.SameText(Golden.Read(SyntheticCorpus.FileName), SyntheticCorpus.Build(Engines.Candidate), "candidate vs approved output");

		[Fact]
		public void ReferenceMatchesApprovedHebrewTable() =>
			Golden.CheckReference(HebrewTable.FileName, HebrewTable.Build(Engines.Reference));

		[Fact]
		public void CandidateMatchesApprovedHebrewTable() =>
			Comparison.SameText(Golden.Read(HebrewTable.FileName), HebrewTable.Build(Engines.Candidate), "candidate vs approved Hebrew table");
	}
}
