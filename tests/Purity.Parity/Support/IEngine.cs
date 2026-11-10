namespace Purity.Parity
{
	/// <summary>
	/// What the tests need from the engine. Going through this adapter keeps them the same when
	/// the engine is restructured.
	/// </summary>
	public interface IEngine
	{
		string Name { get; }

		/// <summary>Sorts the periods as loading does, then recalculates everything as the Refresh button does.</summary>
		EngineRun Recalculate(IReadOnlyList<PeriodInput> inputs, bool includeText = true);

		/// <summary>Enters periods one at a time through the accept path, as a user does.</summary>
		ReplayRun Replay(IReadOnlyList<PeriodInput> inputs, bool removeAndReAdd = false);

		/// <summary>Calculates one period given the intervals already being tracked.</summary>
		IReadOnlyList<EventDump> CommitSingle(PeriodInput input, IReadOnlyList<int> tracked, bool includeText);

		/// <summary>Loads a data file, recalculates it, saves it and returns the saved bytes.</summary>
		byte[] RoundTripFile(string sourcePath);

		/// <summary>Opens a data file the way starting the app does: stored events stay as they are.</summary>
		EngineRun LoadOnly(string path);

		/// <summary>The two interval lengths between neighboring periods.</summary>
		(int Length, float FullLength) Lengths(PeriodInput first, PeriodInput second);

		/// <summary>Hebrew date text for a day stamp, as the Windows app shows it, and as the old Avalonia app needed it.</summary>
		(string Text, string TextAvalonia) HebrewDate(DateTimeOffset dayStamp);

		/// <summary>Hebrew date text for a day stamp in English letters.</summary>
		string HebrewDateInEnglish(DateTimeOffset dayStamp);

		/// <summary>The periods lengths CSV report.</summary>
		string PeriodLengthsReport(IReadOnlyList<PeriodInput> inputs);
	}

	public static class Engines
	{
		public static readonly IEngine Candidate = new CandidateEngine();
	}
}
