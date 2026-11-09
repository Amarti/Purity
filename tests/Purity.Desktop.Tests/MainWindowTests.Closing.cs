using System.Text;
using Purity.Core;
using Purity.Desktop.ViewModels;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>The wide button adds a period and later closes it, in place of a tick on each period</summary>
	public sealed partial class MainWindowTests
	{
		[Fact]
		public async Task TheWideButtonAddsAPeriodThenClosesIt()
		{
			var content = WriteHistory();
			var clock = new FixedClock();
			var window = await OpenAsync(clock);
			Assert.False(window.HasOpenPeriod);
			Assert.Equal("New period", window.AddOrCloseLabel);

			await window.AddOrClosePeriodCommand.ExecuteAsync(null);
			Assert.Equal(4, window.Periods.Count);
			Assert.False(window.Periods[0].IsClosed);
			Assert.True(window.HasOpenPeriod);
			Assert.Equal("Close period", window.AddOrCloseLabel);
			Assert.Equal(content, File.ReadAllBytes(_dataFile));			// nothing is saved until it's closed

			clock.Elapsed += TimeSpan.FromSeconds(5);
			_dialogs.Answers.Enqueue(0);					// its To date is still a week ahead
			await window.AddOrClosePeriodCommand.ExecuteAsync(null);
			Assert.Equal(4, window.Periods.Count);
			Assert.True(window.Periods[0].IsClosed);
			Assert.NotEmpty(window.Periods[0].SubEvents);
			Assert.False(window.HasOpenPeriod);
			Assert.Equal("New period", window.AddOrCloseLabel);
			Assert.NotEqual(content, File.ReadAllBytes(_dataFile));
		}

		[Fact]
		public async Task ADoubleClickOnTheWideButtonOnlyAddsThePeriod()
		{
			var content = WriteHistory();
			var clock = new FixedClock();
			var window = await OpenAsync(clock);

			await window.AddOrClosePeriodCommand.ExecuteAsync(null);
			clock.Elapsed += TimeSpan.FromMilliseconds(200);
			await window.AddOrClosePeriodCommand.ExecuteAsync(null);

			Assert.True(window.HasOpenPeriod);
			Assert.Equal(4, window.Periods.Count);
			Assert.Equal(content, File.ReadAllBytes(_dataFile));
		}

		[Fact]
		public async Task ADoubleClickWhileClosingDoesntStartAnotherPeriod()
		{
			WriteHistory();
			var clock = new FixedClock();
			var window = await OpenAsync(clock);
			await window.AddOrClosePeriodCommand.ExecuteAsync(null);
			clock.Elapsed += TimeSpan.FromSeconds(5);
			_dialogs.Answers.Enqueue(0);					// its To date is still a week ahead

			await window.AddOrClosePeriodCommand.ExecuteAsync(null);
			clock.Elapsed += TimeSpan.FromMilliseconds(200);
			await window.AddOrClosePeriodCommand.ExecuteAsync(null);

			Assert.False(window.HasOpenPeriod);
			Assert.Equal(4, window.Periods.Count);
		}

		[Fact]
		public async Task ClosingWithAToDateStillToComeAsksFirst()
		{
			var content = WriteHistory();
			var clock = new FixedClock();
			var window = await OpenAsync(clock);
			await window.AddOrClosePeriodCommand.ExecuteAsync(null);
			var open = window.Periods[0];
			clock.Elapsed += TimeSpan.FromSeconds(5);

			_dialogs.Answers.Enqueue(1);					// Keep open
			await window.AddOrClosePeriodCommand.ExecuteAsync(null);

			Assert.Contains(open.EndText, Assert.Single(_dialogs.Shown));
			Assert.False(open.IsClosed);
			Assert.Equal(content, File.ReadAllBytes(_dataFile));

			clock.Elapsed += TimeSpan.FromSeconds(5);
			await window.AddOrClosePeriodCommand.ExecuteAsync(null);		// closing the question keeps it open too
			Assert.False(open.IsClosed);

			clock.Elapsed += TimeSpan.FromSeconds(5);
			_dialogs.Answers.Enqueue(0);					// Close period
			await window.AddOrClosePeriodCommand.ExecuteAsync(null);
			Assert.True(open.IsClosed);
		}

		[Fact]
		public async Task ClosingOnTheToDateOrLaterDoesntAsk()
		{
			WriteHistory();
			var clock = new FixedClock();
			var window = await OpenAsync(clock);
			await window.AddOrClosePeriodCommand.ExecuteAsync(null);
			var open = window.Periods[0];
			open.EndDate = Today;
			clock.Elapsed += TimeSpan.FromSeconds(5);

			await window.AddOrClosePeriodCommand.ExecuteAsync(null);

			Assert.Empty(_dialogs.Shown);
			Assert.True(open.IsClosed);
		}

		[Fact]
		public async Task TheWideButtonClosesTheOpenPeriodEvenWhenItIsntFirst()
		{
			var ledger = new PeriodLedger();
			ledger.AcceptPeriod(ledger.AddPeriod(Stamps.FromDate(new DateTime(2031, 2, 3), false), Stamps.FromDate(new DateTime(2031, 2, 8), false)));
			ledger.AddPeriod(Stamps.FromDate(new DateTime(2031, 1, 5), false), Stamps.FromDate(new DateTime(2031, 1, 10), false));
			File.WriteAllBytes(_dataFile, new UTF8Encoding(false).GetBytes(PurityFile.Format(ledger.Data)));
			var window = await OpenAsync();
			var open = window.Periods[1];
			Assert.False(open.IsClosed);
			Assert.False(open.IsFolded);					// one waiting to be closed always shows

			await window.AddOrClosePeriodCommand.ExecuteAsync(null);

			Assert.True(open.IsClosed);
			Assert.False(window.HasOpenPeriod);
		}

		[Fact]
		public async Task AnOpenPeriodIsShownApartFromTheList()
		{
			WriteHistory();
			var clock = new FixedClock();
			var window = await OpenAsync(clock);
			Assert.Empty(window.OpenPeriods);
			Assert.Equal(window.Periods, window.ClosedPeriods);

			await window.AddOrClosePeriodCommand.ExecuteAsync(null);
			var open = window.Periods[0];
			Assert.Equal(new[] { open }, window.OpenPeriods);
			Assert.Equal(3, window.ClosedPeriods.Count);
			Assert.DoesNotContain(open, window.ClosedPeriods);

			clock.Elapsed += TimeSpan.FromSeconds(5);
			_dialogs.Answers.Enqueue(0);
			await window.AddOrClosePeriodCommand.ExecuteAsync(null);
			Assert.Empty(window.OpenPeriods);
			Assert.Same(open, window.ClosedPeriods[0]);
		}

		[Fact]
		public async Task TheWideButtonSaysWhyWhenAPeriodAlreadyBeginsToday()
		{
			WriteHistory();
			var clock = new FixedClock();
			var window = await OpenAsync(clock);
			await window.AddOrClosePeriodCommand.ExecuteAsync(null);
			clock.Elapsed += TimeSpan.FromSeconds(5);
			_dialogs.Answers.Enqueue(0);
			await window.AddOrClosePeriodCommand.ExecuteAsync(null);
			_dialogs.Shown.Clear();
			clock.Elapsed += TimeSpan.FromSeconds(5);

			await window.AddOrClosePeriodCommand.ExecuteAsync(null);

			Assert.Contains("already in the history", Assert.Single(_dialogs.Shown));
			Assert.Equal(4, window.Periods.Count);
			Assert.False(window.HasOpenPeriod);
		}

		[Fact]
		public async Task AnEmptyHistorySaysWhatToDoFirst()
		{
			File.WriteAllText(_dataFile, "[]");
			var window = await OpenAsync();
			Assert.True(window.ShowsEmptyHint);

			window.AddPeriodCommand.Execute(null);
			Assert.False(window.ShowsEmptyHint);

			await window.Periods[0].RemoveCommand.ExecuteAsync(null);
			Assert.True(window.ShowsEmptyHint);
		}

		[Fact]
		public async Task AHistoryShowsNoFirstStepMessage()
		{
			WriteHistory();

			var window = await OpenAsync();

			Assert.False(window.ShowsEmptyHint);
		}

		[Fact]
		public async Task AFileThatCantBeReadShowsNoFirstStepMessage()
		{
			File.WriteAllText(_dataFile, "this is not a history");

			var window = await OpenAsync();

			Assert.Empty(window.Periods);
			Assert.False(window.ShowsEmptyHint);
		}

		[Fact]
		public async Task TheWideButtonIsOffWithoutADataFile()
		{
			_viewModel = new MainWindowViewModel(_dialogs, null, new FixedClock(), _pending.Enqueue);
			await _viewModel.StartAsync();

			Assert.False(_viewModel.AddOrClosePeriodCommand.CanExecute(null));
		}
	}
}
