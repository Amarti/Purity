using System.Text;
using Purity.Core;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>A history kept in a document a phone's picker gave, as in Google Drive. Every date is made up.</summary>
	public sealed partial class MainWindowTests
	{
		[Fact]
		public async Task AHistoryInADocumentOpensAndSavesThere()
		{
			var documents = new MemoryDocuments();
			var file = documents.Add("pureData.json", Encoding.UTF8.GetBytes(History()));
			var window = await OpenDocumentAsync(documents, "pureData.json");
			Assert.Equal(3, window.Periods.Count);
			Assert.Null(window.Warning);

			window.AddPeriodCommand.Execute(null);
			window.Periods[0].EndDate = Today.AddDays(5);
			await window.Periods[0].AcceptCommand.ExecuteAsync(null);

			Assert.Equal(4, PurityFile.Parse(new UTF8Encoding(false).GetString(file.Content)).Count);
			Assert.Null(window.Warning);
			Assert.False(File.Exists(_dataFile));							// nothing went anywhere else
		}

		[Fact]
		public async Task ADocumentOutOfReachSaysToChooseItAgain()
		{
			var window = await OpenDocumentAsync(new MemoryDocuments(), "gone.json");

			Assert.Equal(UiText.English.WarnDocumentGone, window.Warning);
			Assert.Empty(window.Periods);
			Assert.False(window.AddPeriodCommand.CanExecute(null));
		}

		[Fact]
		public async Task ADocumentAProviderCantReadIsUnreadableNotACrash()
		{
			var documents = new MemoryDocuments();
			documents.Add("pureData.json", Encoding.UTF8.GetBytes(History())).FailWith = new InvalidOperationException("The provider went away");

			var window = await OpenDocumentAsync(documents, "pureData.json");

			Assert.Equal(UiText.English.WarnUnreadable, window.Warning);
			Assert.Empty(window.Periods);
		}

		[Fact]
		public async Task ComingBackToPurityReadsWhatAnotherDeviceSaved()
		{
			var documents = new MemoryDocuments();
			var file = documents.Add("pureData.json", Encoding.UTF8.GetBytes(History()));
			var window = await OpenDocumentAsync(documents, "pureData.json");

			var other = new PeriodLedger();
			other.Load(PurityFile.Parse(History()));
			other.AcceptPeriod(other.AddPeriod(Stamps.FromDate(new DateTime(2031, 4, 29), false), Stamps.FromDate(new DateTime(2031, 5, 4), false))!);
			file.Content = new UTF8Encoding(false).GetBytes(PurityFile.Format(other.Data));
			await window.ComeBackAsync();
			while (_pending.TryDequeue(out var work))
				await work();

			Assert.Equal(4, window.Periods.Count);
		}

		[Fact]
		public async Task AHistoryMovesIntoAnEmptyDocumentWithItsBackups()
		{
			var content = WriteHistory();
			var window = await OpenAsync(new FixedClock(), new MemoryDocuments());
			var documents = (MemoryDocuments)_documents!;
			var file = documents.Add("Drive copy.json", Array.Empty<byte>());
			_dialogs.InSettings = settings =>
			{
				settings.RequestMove(MemoryDocuments.LocationOf("Drive copy.json"));
				return false;
			};

			await window.OpenSettingsCommand.ExecuteAsync(null);

			Assert.Equal(content, file.Content);
			Assert.Equal(MemoryDocuments.LocationOf("Drive copy.json"), window.DataFilePath);
			Assert.Contains("Drive copy.json", Assert.Single(_dialogs.Shown));
			Assert.Equal(3, window.Periods.Count);
			Assert.Equal(content, File.ReadAllBytes(_dataFile));			// the old file stays, as another device may use it
		}

		[Fact]
		public async Task AMoveNeverWritesOverADocumentThatHoldsSomething()
		{
			WriteHistory();
			var window = await OpenAsync(new FixedClock(), new MemoryDocuments());
			var file = ((MemoryDocuments)_documents!).Add("someone's.json", Encoding.UTF8.GetBytes("[]"));
			_dialogs.InSettings = settings =>
			{
				settings.RequestMove(MemoryDocuments.LocationOf("someone's.json"));
				return false;
			};

			await window.OpenSettingsCommand.ExecuteAsync(null);

			Assert.Equal("[]", Encoding.UTF8.GetString(file.Content));
			Assert.Equal(UiText.English.MoveOntoFile, Assert.Single(_dialogs.Shown));
			Assert.Equal(_dataFile, window.DataFilePath);
		}

		[Fact]
		public async Task RemovingEverythingDeletesTheDocumentToo()
		{
			var documents = new MemoryDocuments();
			var file = documents.Add("pureData.json", Encoding.UTF8.GetBytes(History()));
			var window = await OpenDocumentAsync(documents, "pureData.json");
			_dialogs.Answers.Enqueue(0);										// remove for good
			_dialogs.InSettings = settings =>
			{
				settings.RequestWipe();
				return false;
			};

			await window.OpenSettingsCommand.ExecuteAsync(null);

			Assert.True(file.Deleted);
			Assert.Equal(1, _dialogs.Closed);
			Assert.Contains(UiText.English.Wiped, _dialogs.Shown[^1]);
		}

		[Fact]
		public async Task ADocumentThatWontGoIsNamedAsLeftBehind()
		{
			var documents = new MemoryDocuments();
			var file = documents.Add("pureData.json", Encoding.UTF8.GetBytes(History()));
			var window = await OpenDocumentAsync(documents, "pureData.json");
			file.FailWith = new InvalidOperationException("The provider refuses");
			_dialogs.Answers.Enqueue(0);
			_dialogs.InSettings = settings =>
			{
				settings.RequestWipe();
				return false;
			};

			await window.OpenSettingsCommand.ExecuteAsync(null);

			Assert.False(file.Deleted);
			Assert.Contains(UiText.English.WipeDocumentLeft, _dialogs.Shown[^1]);
		}


		private async Task<MainWindowViewModel> OpenDocumentAsync(MemoryDocuments documents, string name)
		{
			_documents = documents;
			_viewModel = new MainWindowViewModel(_dialogs, MemoryDocuments.LocationOf(name), new FixedClock(), _pending.Enqueue, documents: documents);
			await _viewModel.StartAsync();
			return _viewModel;
		}

		/// <summary>Opens the data file the settings name, as the app does, and not one given for this run only, which can't move</summary>
		private async Task<MainWindowViewModel> OpenAsync(FixedClock clock, MemoryDocuments documents)
		{
			_documents = documents;
			new DesktopSettings { DataFilePath = _dataFile }.Save();
			_viewModel = new MainWindowViewModel(_dialogs, null, clock, _pending.Enqueue, documents: documents);
			await _viewModel.StartAsync();
			return _viewModel;
		}


		private IDocuments? _documents;
	}
}
