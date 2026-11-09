using System.Text;
using Purity.Core;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>Which periods show as one line (T2), and what that line says</summary>
	public sealed partial class MainWindowTests
	{
		[Fact]
		public async Task OnlyTheFirstPeriodStartsOpen()
		{
			WriteHistory();

			var window = await OpenAsync();

			Assert.Equal(new[] { false, true, true }, window.Periods.Select(el => el.IsFolded));
		}

		[Fact]
		public async Task AStillOpenPeriodIsTheOnlyOneThatStartsOpen()
		{
			var ledger = new PeriodLedger();
			ledger.AcceptPeriod(ledger.AddPeriod(Stamps.FromDate(new DateTime(2031, 1, 5), false), Stamps.FromDate(new DateTime(2031, 1, 10), false)));
			ledger.AddPeriod(Stamps.FromDate(new DateTime(2031, 2, 3), false), Stamps.FromDate(new DateTime(2031, 2, 8), false));
			File.WriteAllBytes(_dataFile, new UTF8Encoding(false).GetBytes(PurityFile.Format(ledger.Data)));

			var window = await OpenAsync();

			Assert.False(window.Periods[0].IsClosed);
			Assert.Equal(new[] { false, true }, window.Periods.Select(el => el.IsFolded));
		}

		[Fact]
		public async Task RemovingTheNewestOpensTheOneBeforeIt()
		{
			WriteHistory();
			var window = await OpenAsync();
			Assert.True(window.Periods[1].IsFolded);

			_dialogs.Answers.Enqueue(0);					// Yes
			await window.Periods[0].RemoveCommand.ExecuteAsync(null);

			Assert.False(window.Periods[0].IsFolded);
			Assert.True(window.Periods[1].IsFolded);
		}

		[Fact]
		public async Task FoldingChangesNothingInTheDataFile()
		{
			var content = WriteHistory();
			var window = await OpenAsync();

			window.Periods[1].ToggleFoldCommand.Execute(null);
			window.Periods[2].ToggleFoldCommand.Execute(null);
			window.Periods[2].ToggleFoldCommand.Execute(null);

			Assert.False(window.Periods[1].IsFolded);
			Assert.True(window.Periods[2].IsFolded);
			Assert.True(await window.PrepareToCloseAsync());
			Assert.Equal(content, File.ReadAllBytes(_dataFile));
		}

		[Fact]
		public async Task TheFirstPeriodInTheListAlwaysStaysOpen()
		{
			WriteHistory();
			var window = await OpenAsync();
			var previous = window.Periods[0];

			Assert.False(previous.CanFold);
			previous.ToggleFoldCommand.Execute(null);
			Assert.False(previous.IsFolded);
			Assert.True(window.Periods[1].CanFold);

			// A new period takes over as first, so the one before it folds
			window.AddPeriodCommand.Execute(null);
			Assert.False(window.Periods[0].CanFold);
			Assert.False(window.Periods[0].IsFolded);
			Assert.True(previous.CanFold);
			Assert.True(previous.IsFolded);

			// Accepted with an earlier date, the new one moves down, and whichever is first then shows in full
			window.Periods[0].BeginDate = new DateTime(2031, 3, 10);
			window.Periods[0].EndDate = new DateTime(2031, 3, 15);
			await window.Periods[0].AcceptCommand.ExecuteAsync(null);
			Assert.Same(previous, window.Periods[0]);
			Assert.False(previous.IsFolded);
			Assert.False(previous.CanFold);
		}

		[Fact]
		public async Task AFoldedLineSaysTheDatesAndTheInterval()
		{
			WriteHistory();
			var window = await OpenAsync();
			var engine = new PeriodLedger();
			engine.Load(PurityFile.Parse(History()));
			var middle = window.Periods[1];

			Assert.Equal("Sunday, 2 March 2031", middle.BeginText);
			Assert.Equal("Friday, 7 March 2031", middle.EndText);
			Assert.True(middle.BeginIsAfterDusk);
			Assert.False(middle.EndIsAfterDusk);
			Assert.Equal($"({engine.PeriodLength(engine.Data[1])})", middle.FoldedNote);
			Assert.Equal(string.Empty, window.Periods[2].FoldedNote);		// nothing before the first period

			middle.SkipStreak = true;
			Assert.Equal("Skipped", middle.FoldedNote);
		}
	}
}
