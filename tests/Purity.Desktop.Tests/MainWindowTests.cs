using System.Text;
using Purity.Core;
using Purity.Desktop.ViewModels;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>
	/// The main window's behavior with real files in a temp folder. Every date is made up. The
	/// tests share the process's PURITY_APP_DATA setting, so they run one at a time.
	/// </summary>
	[Collection(AppDataCollection.Name)]
	public sealed partial class MainWindowTests : IDisposable
	{
		public MainWindowTests()
		{
			Directory.CreateDirectory(_root);
			Environment.SetEnvironmentVariable("PURITY_APP_DATA", Path.Combine(_root, "app data"));
		}

		public void Dispose()
		{
			_viewModel?.Dispose();
			Environment.SetEnvironmentVariable("PURITY_APP_DATA", null);
			try
			{
				Directory.Delete(_root, true);
			}
			catch (IOException)
			{
				// a watcher may hold the folder for a moment; temp gets cleaned anyway
			}
		}


		[Fact]
		public async Task OpeningShowsThePeriodsAndLeavesTheFileAlone()
		{
			var content = WriteHistory();

			var window = await OpenAsync();

			Assert.Equal(3, window.Periods.Count);
			Assert.Equal(window.Periods.Select(el => el.Period.Begin).Order(), window.Periods.Select(el => el.Period.Begin));
			Assert.All(window.Periods, el => Assert.True(el.IsClosed));
			Assert.True(window.Periods[^1].IsLast);
			var engine = new PeriodLedger();
			engine.Load(PurityFile.Parse(History()));
			Assert.Equal(new[] { "Skip", engine.SkipLabel(engine.Data[1]), engine.SkipLabel(engine.Data[2]) }, window.Periods.Select(el => el.SkipLabel));
			Assert.StartsWith("Skip (", window.Periods[1].SkipLabel);
			Assert.Null(window.Warning);
			Assert.True(window.SelfTestResult?.Passed, window.SelfTestResult?.ToString());
			Assert.Empty(_dialogs.Shown);
			Assert.Equal(content, File.ReadAllBytes(_dataFile));
		}

		[Fact]
		public async Task AcceptingSavesExactlyWhatTheEngineCalculates()
		{
			WriteHistory();
			var window = await OpenAsync();

			window.AddPeriodCommand.Execute(null);
			var added = window.Periods[^1];
			Assert.False(added.IsClosed);
			Assert.Equal(Today, added.BeginDate);
			Assert.Equal(Today.AddDays(7), added.EndDate);
			added.EndDate = Today.AddDays(5);
			await added.AcceptCommand.ExecuteAsync(null);

			var expected = new PeriodLedger();
			expected.Load(PurityFile.Parse(History()));
			var period = expected.AddPeriod(Stamps.FromDate(Today, false), Stamps.FromDate(Today.AddDays(7), false))!;
			period.End = Stamps.FromDate(Today.AddDays(5), false);
			expected.AcceptPeriod(period);

			Assert.Equal(PurityFile.Format(expected.Data), ReadData());
			Assert.True(window.Periods[^1].IsClosed);
			Assert.Equal(expected.Data[^1].SubEvents.Count, window.Periods[^1].SubEvents.Count);
			Assert.Null(window.Warning);
		}

		[Fact]
		public async Task AFailedSelfTestShowsNoDatesAndLeavesTheFileAlone()
		{
			var content = WriteHistory();
			_viewModel = new MainWindowViewModel(_dialogs, _dataFile, new FixedClock(), _pending.Enqueue,
				() => new SelfTestResult(false, 7, "E|1|2033-05-14T12:00:00.0000000+00:00", "E|1|2033-05-15T12:00:00.0000000+00:00"));

			await _viewModel.StartAsync();

			Assert.Contains("date calculations", Assert.Single(_dialogs.Shown));
			Assert.Contains("line 7", _dialogs.Shown[0]);
			Assert.Contains("date calculations failed", _viewModel.Warning);
			Assert.Empty(_viewModel.Periods);
			Assert.False(_viewModel.AddPeriodCommand.CanExecute(null));
			Assert.False(_viewModel.RecalculateCommand.CanExecute(null));
			Assert.False(Directory.Exists(Path.Combine(_root, "app data", "vaults")));		// the data file was never opened
			Assert.Equal(content, File.ReadAllBytes(_dataFile));
		}

		[Fact]
		public async Task OnlyOnePeriodCanBeOpenAtATime()
		{
			WriteHistory();
			var window = await OpenAsync();
			Assert.True(window.AddPeriodCommand.CanExecute(null));
			Assert.Equal("Add period", window.AddPeriodTip);

			window.AddPeriodCommand.Execute(null);
			Assert.False(window.AddPeriodCommand.CanExecute(null));
			Assert.Equal("Accept or remove the open period first", window.AddPeriodTip);

			window.Periods[^1].BeginDate = Today.AddDays(1);	// a different day, which WPF would have let through
			window.AddPeriodCommand.Execute(null);
			Assert.Equal(4, window.Periods.Count);

			await window.Periods[^1].RemoveCommand.ExecuteAsync(null);	// an open period goes without asking
			Assert.Equal(3, window.Periods.Count);
			Assert.True(window.AddPeriodCommand.CanExecute(null));

			window.AddPeriodCommand.Execute(null);
			await window.Periods[^1].AcceptCommand.ExecuteAsync(null);
			Assert.True(window.AddPeriodCommand.CanExecute(null));
			Assert.Equal("Add period", window.AddPeriodTip);
			Assert.Empty(_dialogs.Shown);
		}

		[Fact]
		public async Task AnOpenPeriodInTheFileBlocksAddingUntilAccepted()
		{
			var ledger = new PeriodLedger();
			ledger.Load(PurityFile.Parse(History()));
			ledger.AddPeriod(Stamps.FromDate(new DateTime(2031, 4, 29), false), Stamps.FromDate(new DateTime(2031, 5, 4), false));
			File.WriteAllText(_dataFile, PurityFile.Format(ledger.Data), new UTF8Encoding(false));

			var window = await OpenAsync();
			Assert.False(window.Periods[^1].IsClosed);
			Assert.False(window.AddPeriodCommand.CanExecute(null));

			await window.Periods[^1].AcceptCommand.ExecuteAsync(null);
			Assert.True(window.AddPeriodCommand.CanExecute(null));
		}

		[Fact]
		public async Task PickingADateKeepsTheNightChoice()
		{
			WriteHistory();
			var window = await OpenAsync();
			window.AddPeriodCommand.Execute(null);
			var added = window.Periods[^1];

			added.ToggleBeginNightCommand.Execute(null);
			added.BeginDate = new DateTime(2031, 6, 3);

			Assert.True(added.BeginIsAfterDusk);
			Assert.Equal(new DateTimeOffset(2031, 6, 3, 12, 0, 0, TimeSpan.Zero), added.Period.Begin);
			Assert.Equal(new DateTime(2031, 6, 3), added.BeginDate);

			added.ToggleBeginNightCommand.Execute(null);
			Assert.Equal(new DateTimeOffset(2031, 6, 3, 0, 0, 0, TimeSpan.Zero), added.Period.Begin);
		}

		[Fact]
		public async Task RemovingAnAcceptedPeriodAsksFirst()
		{
			var content = WriteHistory();
			var window = await OpenAsync();
			var last = window.Periods[^1];

			_dialogs.Answers.Enqueue(1);					// No
			await last.RemoveCommand.ExecuteAsync(null);
			Assert.Equal(3, window.Periods.Count);
			Assert.Equal(content, File.ReadAllBytes(_dataFile));

			_dialogs.Answers.Enqueue(0);					// Yes
			await last.RemoveCommand.ExecuteAsync(null);
			Assert.Equal(2, window.Periods.Count);
			Assert.True(window.Periods[^1].IsLast);

			var expected = new PeriodLedger();
			expected.Load(PurityFile.Parse(History()));
			expected.RemovePeriod(expected.Data[^1]);
			Assert.Equal(PurityFile.Format(expected.Data), ReadData());
			Assert.All(_dialogs.Shown, el => Assert.Contains("remove this period", el));
		}

		[Fact]
		public async Task AnUnreadableFileIsNeverWritten()
		{
			var damaged = Encoding.UTF8.GetBytes("[ { \"Begin\": \"2031-01-0");
			File.WriteAllBytes(_dataFile, damaged);

			var window = await OpenAsync();
			Assert.Contains("couldn't be read", Assert.Single(_dialogs.Shown));
			Assert.Contains("can't be read", window.Warning);

			window.AddPeriodCommand.Execute(null);
			await window.Periods[^1].AcceptCommand.ExecuteAsync(null);
			Assert.Single(_dialogs.Shown);					// told once, not on every save
			Assert.Equal(damaged, File.ReadAllBytes(_dataFile));

			_dialogs.Answers.Enqueue(1);					// Keep open
			Assert.False(await window.PrepareToCloseAsync());
			Assert.Equal(damaged, File.ReadAllBytes(_dataFile));
			Assert.Single(Directory.GetFiles(Path.Combine(_root, "app data"), "held.json", SearchOption.AllDirectories));
		}

		[Fact]
		public async Task AClashWithTheFileAsksWhichVersionToKeep()
		{
			WriteHistory();
			var window = await OpenAsync();

			// Another device corrects the end of the last accepted period, while this one adds a period
			var elsewhere = new PeriodLedger();
			elsewhere.Load(PurityFile.Parse(History()));
			elsewhere.Data[^1].End = elsewhere.Data[^1].End.AddDays(1);
			elsewhere.Recalculate();
			File.WriteAllText(_dataFile, PurityFile.Format(elsewhere.Data), new UTF8Encoding(false));

			_dialogs.Answers.Enqueue(1);					// Keep the file's
			window.AddPeriodCommand.Execute(null);
			await window.Periods[^1].AcceptCommand.ExecuteAsync(null);

			Assert.Contains("disagree", Assert.Single(_dialogs.Shown));
			var saved = PurityFile.Parse(ReadData());
			Assert.Equal(4, saved.Count);
			Assert.Equal(elsewhere.Data[^1].End, saved[2].End);
			Assert.Equal(Stamps.FromDate(Today, false), saved[3].Begin);
			Assert.Equal(4, window.Periods.Count);
			Assert.Null(window.Warning);
		}

		[Fact]
		public async Task WithoutADataFileItAsksForOne()
		{
			var window = new MainWindowViewModel(_dialogs, null, new FixedClock(), _pending.Enqueue);
			_viewModel = window;

			await window.StartAsync();

			Assert.Equal(1, _dialogs.SettingsShown);
			Assert.Equal("Choose your data file in Settings.", window.Warning);
			Assert.False(window.AddPeriodCommand.CanExecute(null));
			Assert.Empty(window.Periods);
		}


		/// <summary>Three accepted periods, one of them beginning after dusk</summary>
		private static string History()
		{
			var ledger = new PeriodLedger();
			foreach (var (begin, night) in new[] { (new DateTime(2031, 1, 5), false), (new DateTime(2031, 3, 2), true), (new DateTime(2031, 3, 31), false) })
				ledger.AcceptPeriod(ledger.AddPeriod(Stamps.FromDate(begin, night), Stamps.FromDate(begin.AddDays(5), false)));
			return PurityFile.Format(ledger.Data);
		}

		private byte[] WriteHistory()
		{
			var content = new UTF8Encoding(false).GetBytes(History());
			File.WriteAllBytes(_dataFile, content);
			return content;
		}

		private string ReadData() => File.ReadAllText(_dataFile);

		private async Task<MainWindowViewModel> OpenAsync()
		{
			_viewModel = new MainWindowViewModel(_dialogs, _dataFile, new FixedClock(), _pending.Enqueue);
			await _viewModel.StartAsync();
			return _viewModel;
		}


		private static readonly DateTime Today = new(2031, 6, 1);

		private readonly string _root = Path.Combine(Path.GetTempPath(), $"purity-desktop-{Guid.NewGuid():N}");
		private string _dataFile => Path.Combine(_root, "pureData.json");
		private readonly FakeDialogs _dialogs = new();
		/// <summary>File watcher notices, which arrive on the watcher's thread and only run when a test runs them</summary>
		private readonly System.Collections.Concurrent.ConcurrentQueue<Func<Task>> _pending = new();
		private MainWindowViewModel? _viewModel;


		private sealed class FixedClock : TimeProvider
		{
			public override DateTimeOffset GetUtcNow() => new(Today.AddHours(9), TimeSpan.Zero);
		}

		private sealed class FakeDialogs : IAppDialogs
		{
			public async Task<int?> AskAsync(string text, params string[] buttons)
			{
				Shown.Add(text);
				if (WhileAsking != null)
					await WhileAsking(text);
				return Answers.TryDequeue(out var answer) ? answer : null;
			}

			/// <summary>Runs while a question is on screen, before it's answered</summary>
			public Func<string, Task>? WhileAsking { get; set; }

			public Task<bool> ShowSettingsAsync(SettingsWindowViewModel settings)
			{
				SettingsShown++;
				return Task.FromResult(false);
			}

			public List<string> Shown { get; } = new();
			public Queue<int> Answers { get; } = new();
			public int SettingsShown { get; private set; }
		}
	}
}
