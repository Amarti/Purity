using System.Text;
using Purity.Core;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>Copies of the data file that a sync app leaves beside it, as when Dropbox can't merge two devices' saves</summary>
	public sealed partial class MainWindowTests
	{
		[Fact]
		public async Task ACopyWithMissingEntriesIsBroughtInAndSetAside()
		{
			WriteHistory();
			var copy = WriteCopy("pureData (Eli's conflicted copy 2031-06-02).json", WithPeriod(History(), new DateTime(2031, 4, 28)));
			_dialogs.Answers.Enqueue(0);					// Bring it in
			_dialogs.Answers.Enqueue(0);					// OK

			var window = await OpenAsync();

			Assert.Contains("pureData (Eli's conflicted copy 2031-06-02).json", _dialogs.Shown[0]);
			Assert.Contains("1 new period", _dialogs.Shown[1]);
			Assert.Equal(2, _dialogs.Shown.Count);
			Assert.Equal(4, window.Periods.Count);
			Assert.Equal(Encoding.UTF8.GetString(copy.Content), ReadData());	// the copy's periods, recalculated by the same engine
			Assert.False(File.Exists(copy.Path));
			Assert.Contains(BackupContents(), el => el.SequenceEqual(copy.Content));
			Assert.Null(window.Warning);

			await window.CheckForCopiesAsync();
			Assert.Equal(2, _dialogs.Shown.Count);
		}

		[Fact]
		public async Task CopiesPutOffWaitForRefresh()
		{
			var content = WriteHistory();
			var copy = WriteCopy("pureData (conflicted copy 2031-06-02 101500).json", WithPeriod(History(), new DateTime(2031, 4, 28)));
			_dialogs.Answers.Enqueue(1);					// Not now

			var window = await OpenAsync();
			Assert.Single(_dialogs.Shown);
			Assert.Contains("Press Refresh to bring it in", window.Warning);
			Assert.True(File.Exists(copy.Path));
			Assert.Equal(content, File.ReadAllBytes(_dataFile));

			await window.CheckForCopiesAsync();				// coming back to the window doesn't ask again
			Assert.Single(_dialogs.Shown);

			_dialogs.Answers.Enqueue(0);					// Bring it in
			_dialogs.Answers.Enqueue(0);					// OK
			await window.RecalculateCommand.ExecuteAsync(null);
			Assert.Equal(3, _dialogs.Shown.Count);
			Assert.Equal(4, window.Periods.Count);
			Assert.False(File.Exists(copy.Path));
			Assert.Null(window.Warning);
		}

		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public async Task ACopyThatDisagreesAsksWhichVersionToKeep(bool keepCopy)
		{
			WriteHistory();
			var elsewhere = Changed(History());
			var copy = WriteCopy("pureData (1).json", PurityFile.Format(elsewhere.Data));
			_dialogs.Answers.Enqueue(0);					// Bring it in
			_dialogs.Answers.Enqueue(keepCopy ? 1 : 0);		// Keep the copy's, or this computer's
			_dialogs.Answers.Enqueue(0);					// OK

			var window = await OpenAsync();

			Assert.Contains("In the copy \"pureData (1).json\"", _dialogs.Shown[1]);
			Assert.Contains("Brought in 1 copy", _dialogs.Shown[2]);
			var saved = PurityFile.Parse(ReadData());
			Assert.Equal(keepCopy ? elsewhere.Data[^1].End : PurityFile.Parse(History())[^1].End, saved[^1].End);
			Assert.Equal(3, saved.Count);
			Assert.False(File.Exists(copy.Path));
			Assert.Contains(BackupContents(), el => el.SequenceEqual(copy.Content));
			Assert.Null(window.Warning);
		}

		[Fact]
		public async Task AnAnswerPutOffIsAskedOnRefreshAndTheCopyIsThenSetAside()
		{
			WriteHistory();
			var elsewhere = Changed(History());
			var copy = WriteCopy("pureData (1).json", PurityFile.Format(elsewhere.Data));
			_dialogs.Answers.Enqueue(0);					// Bring it in, then the question about the period is closed unanswered

			var window = await OpenAsync();
			Assert.Equal(3, _dialogs.Shown.Count);
			Assert.Contains("Press Refresh when you're ready", _dialogs.Shown[2]);
			Assert.Contains("disagree", window.Warning);
			Assert.True(File.Exists(copy.Path));

			_dialogs.Answers.Enqueue(1);					// Keep the copy's
			_dialogs.Answers.Enqueue(0);					// OK
			await window.RecalculateCommand.ExecuteAsync(null);

			Assert.Contains("In the copy \"pureData (1).json\"", _dialogs.Shown[3]);
			Assert.Contains("now in this computer's backups", _dialogs.Shown[4]);
			Assert.Equal(elsewhere.Data[^1].End, PurityFile.Parse(ReadData())[^1].End);
			Assert.False(File.Exists(copy.Path));
			Assert.Null(window.Warning);
		}

		[Fact]
		public async Task AChangeArrivingDuringTheQuestionLeavesTheCopyForLater()
		{
			WriteHistory();
			var elsewhere = Changed(History());
			var copy = WriteCopy("pureData (1).json", PurityFile.Format(elsewhere.Data));
			var newer = WithPeriod(History(), new DateTime(2031, 4, 28));
			_dialogs.WhileAsking = async text =>
			{
				if (!text.Contains("In the copy"))
					return;

				// While the person is choosing, another device saves and the sync app delivers it
				_dialogs.WhileAsking = null;
				File.WriteAllText(_dataFile, newer, new UTF8Encoding(false));
				await RunNextNoticeAsync();
			};
			_dialogs.Answers.Enqueue(0);					// Bring it in
			_dialogs.Answers.Enqueue(1);					// Keep the copy's, answering a question that's gone by now
			_dialogs.Answers.Enqueue(0);					// OK

			var window = await OpenAsync();

			Assert.Contains("left where it is", _dialogs.Shown[^1]);
			Assert.True(File.Exists(copy.Path));
			var saved = PurityFile.Parse(ReadData());
			Assert.Equal(4, saved.Count);										// the other device's period is kept
			Assert.Equal(PurityFile.Parse(History())[2].End, saved[2].End);	// and the stale answer wasn't applied
			var shown = _dialogs.Shown.Count;

			await window.CheckForCopiesAsync();				// it waits for Refresh rather than asking at once
			Assert.Equal(shown, _dialogs.Shown.Count);
			Assert.Contains("Press Refresh", window.Warning);

			_dialogs.Answers.Enqueue(0);					// Bring it in
			_dialogs.Answers.Enqueue(1);					// Keep the copy's
			_dialogs.Answers.Enqueue(0);					// OK
			await window.RecalculateCommand.ExecuteAsync(null);
			Assert.False(File.Exists(copy.Path));
			saved = PurityFile.Parse(ReadData());
			Assert.Equal(4, saved.Count);
			Assert.Equal(elsewhere.Data[^1].End, saved[2].End);
			Assert.Null(window.Warning);
		}

		[Fact]
		public async Task AQuestionReplacedDuringTheAnswerIsAskedAfresh()
		{
			WriteHistory();
			var copy = WriteCopy("pureData (1).json", PurityFile.Format(Changed(History()).Data));
			var corrected = new PeriodLedger();
			corrected.Load(PurityFile.Parse(History()));
			corrected.Data[0].End = corrected.Data[0].End.AddDays(1);
			corrected.Recalculate();
			_dialogs.WhileAsking = async text =>
			{
				if (!text.Contains("In the copy"))
					return;

				// Meanwhile another device corrects an accepted period, which raises a question of its own
				_dialogs.WhileAsking = null;
				File.WriteAllText(_dataFile, PurityFile.Format(corrected.Data), new UTF8Encoding(false));
				await RunNextNoticeAsync();
			};
			_dialogs.Answers.Enqueue(0);					// Bring it in
			_dialogs.Answers.Enqueue(1);					// Keep the copy's, for the question that was replaced
			_dialogs.Answers.Enqueue(1);					// Keep the file's, for the new question
			_dialogs.Answers.Enqueue(0);					// OK

			var window = await OpenAsync();

			var questions = _dialogs.Shown.Where(el => el.Contains("Which version")).ToList();
			Assert.Equal(2, questions.Count);
			Assert.Contains("In the data file", questions[1]);
			var saved = PurityFile.Parse(ReadData());
			Assert.Equal(corrected.Data[0].End, saved[0].End);					// the answer to the new question
			Assert.Equal(PurityFile.Parse(History())[2].End, saved[2].End);	// not the stale one
			Assert.True(File.Exists(copy.Path));								// the copy waits to be brought in again
			Assert.Contains("Press Refresh", window.Warning);
		}

		[Fact]
		public async Task ADamagedCopyIsLeftWhereItIs()
		{
			var content = WriteHistory();
			var copy = WriteCopy("pureData (conflicted copy).json", "[ { \"Begin\": \"2031-0");
			_dialogs.Answers.Enqueue(0);					// Bring it in
			_dialogs.Answers.Enqueue(0);					// OK

			var window = await OpenAsync();

			Assert.Contains("can't be read", _dialogs.Shown[1]);
			Assert.True(File.Exists(copy.Path));
			Assert.Equal(content, File.ReadAllBytes(_dataFile));
			Assert.Equal(3, window.Periods.Count);

			await window.CheckForCopiesAsync();
			Assert.Equal(2, _dialogs.Shown.Count);
		}

		[Fact]
		public async Task CopiesWaitWhileTheDataFileIsUnreadable()
		{
			File.WriteAllBytes(_dataFile, Encoding.UTF8.GetBytes("[ { \"Begin\": \"2031-01-0"));
			var copy = WriteCopy("pureData (conflicted copy).json", History());

			var window = await OpenAsync();

			Assert.Contains("couldn't be read", Assert.Single(_dialogs.Shown));
			Assert.True(File.Exists(copy.Path));
			await window.CheckForCopiesAsync();
			Assert.Single(_dialogs.Shown);
		}


		/// <summary>The history with one more accepted period, as another device would have it</summary>
		private static string WithPeriod(string history, DateTime begin)
		{
			var ledger = new PeriodLedger();
			ledger.Load(PurityFile.Parse(history));
			ledger.AcceptPeriod(ledger.AddPeriod(Stamps.FromDate(begin, false), Stamps.FromDate(begin.AddDays(5), false)));
			return PurityFile.Format(ledger.Data);
		}

		/// <summary>The history with the last period's end moved, as a correction on another device</summary>
		private static PeriodLedger Changed(string history)
		{
			var ledger = new PeriodLedger();
			ledger.Load(PurityFile.Parse(history));
			ledger.Data[^1].End = ledger.Data[^1].End.AddDays(2);
			ledger.Recalculate();
			return ledger;
		}

		/// <summary>Waits for the file watcher's notice of a change, then lets the window handle it</summary>
		private async Task RunNextNoticeAsync()
		{
			var deadline = DateTime.UtcNow.AddSeconds(15);
			Func<Task>? notice;
			while (!_pending.TryDequeue(out notice))
			{
				if (DateTime.UtcNow > deadline)
					throw new TimeoutException("The file watcher never reported the change");
				await Task.Delay(50);
			}
			await notice();
		}

		private (string Path, byte[] Content) WriteCopy(string name, string text)
		{
			var path = Path.Combine(_root, name);
			var content = new UTF8Encoding(false).GetBytes(text);
			File.WriteAllBytes(path, content);
			return (path, content);
		}

		private IEnumerable<byte[]> BackupContents() =>
			Directory.GetFiles(Path.Combine(_root, "app data"), "*.json", SearchOption.AllDirectories)
				.Where(el => Path.GetFileName(Path.GetDirectoryName(el)) == "backups")
				.Select(File.ReadAllBytes);
	}
}
