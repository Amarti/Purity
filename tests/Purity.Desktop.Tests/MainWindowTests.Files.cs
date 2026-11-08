using System.Text;
using Purity.Core;
using Purity.Desktop.ViewModels;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>Where the history is kept: the first start's question, and moving the history to another folder</summary>
	public sealed partial class MainWindowTests
	{
		[Fact]
		public async Task TheFirstStartCanBeginANewHistoryOnThisComputer()
		{
			_dialogs.Answers.Enqueue(0);						// Start a new history
			var window = await StartFreshAsync();

			Assert.Contains("one data file", Assert.Single(_dialogs.Shown));
			Assert.Equal(0, _dialogs.SettingsShown);
			Assert.Equal(DefaultDataFile, window.DataFilePath);
			Assert.Equal(DefaultDataFile, DesktopSettings.Load().DataFilePath);
			Assert.Null(window.Warning);
			Assert.Empty(window.Periods);

			window.AddPeriodCommand.Execute(null);
			await window.Periods[^1].AcceptCommand.ExecuteAsync(null);
			Assert.Single(PurityFile.Parse(File.ReadAllText(DefaultDataFile)));
		}

		[Fact]
		public async Task TheFirstStartCanOpenAFileThePersonHas()
		{
			var content = WriteHistory();
			_dialogs.Answers.Enqueue(1);						// Open my data file...
			_dialogs.PickDataFile = () => _dataFile;
			var window = await StartFreshAsync();

			Assert.Equal(3, window.Periods.Count);
			Assert.Equal(_dataFile, DesktopSettings.Load().DataFilePath);
			Assert.False(File.Exists(DefaultDataFile));
			Assert.Equal(content, File.ReadAllBytes(_dataFile));
		}

		[Fact]
		public async Task CancellingThePickerAsksTheFirstQuestionAgain()
		{
			_dialogs.Answers.Enqueue(1);						// Open my data file..., then cancel the picker
			_dialogs.Answers.Enqueue(0);						// Start a new history
			var window = await StartFreshAsync();

			Assert.Equal(2, _dialogs.Shown.Count);
			Assert.Equal(1, _dialogs.FilesPicked);
			Assert.Equal(DefaultDataFile, window.DataFilePath);
		}

		[Fact]
		public async Task ClosingTheFirstQuestionClosesTheAppAndSetsNothing()
		{
			_viewModel = new MainWindowViewModel(_dialogs, null, new FixedClock(), _pending.Enqueue);

			Assert.False(await _viewModel.StartAsync());

			Assert.Single(_dialogs.Shown);
			Assert.False(File.Exists(Path.Combine(_root, "app data", "settings.json")));
			Assert.Empty(_viewModel.Periods);
		}

		[Fact]
		public async Task MovingTakesTheHistoryAndItsBackupsAlong()
		{
			var window = await OpenFromSettingsWithOneChangeAsync();
			var content = File.ReadAllBytes(_dataFile);
			var target = Path.Combine(_root, "Dropbox", "Purity", "pureData.json");
			_dialogs.InSettings = settings =>
			{
				Assert.True(settings.CanMove);
				settings.RequestMove(target);
				return false;
			};
			_dialogs.Answers.Enqueue(0);						// OK

			await window.OpenSettingsCommand.ExecuteAsync(null);

			Assert.Contains("now kept in " + target, Assert.Single(_dialogs.Shown));
			Assert.Equal(content, File.ReadAllBytes(target));
			Assert.Equal(content, File.ReadAllBytes(_dataFile));			// the old file stays, for any computer still using it
			Assert.Equal(target, window.DataFilePath);
			Assert.Equal(target, DesktopSettings.Load().DataFilePath);
			Assert.Equal(4, window.Periods.Count);
			Assert.Null(window.Warning);

			// The backups came along, and the next change goes to the new place only
			var offered = false;
			_dialogs.InSettings = settings => { offered = settings.CanRestore; return false; };
			await window.OpenSettingsCommand.ExecuteAsync(null);
			Assert.True(offered);
			_dialogs.Answers.Enqueue(0);						// Yes, remove it
			await window.Periods[^1].RemoveCommand.ExecuteAsync(null);
			Assert.Equal(3, PurityFile.Parse(File.ReadAllText(target)).Count);
			Assert.Equal(content, File.ReadAllBytes(_dataFile));
		}

		[Fact]
		public async Task MovingNeverReplacesAFile()
		{
			var window = await OpenFromSettingsWithOneChangeAsync();
			var content = File.ReadAllBytes(_dataFile);
			var target = Path.Combine(_root, "elsewhere.json");
			var someoneElses = Encoding.UTF8.GetBytes("[]");
			File.WriteAllBytes(target, someoneElses);
			_dialogs.InSettings = settings => { settings.RequestMove(target); return false; };
			_dialogs.Answers.Enqueue(0);						// OK

			await window.OpenSettingsCommand.ExecuteAsync(null);

			Assert.Contains("won't replace it", Assert.Single(_dialogs.Shown));
			Assert.Equal(someoneElses, File.ReadAllBytes(target));
			Assert.Equal(content, File.ReadAllBytes(_dataFile));
			Assert.Equal(_dataFile, window.DataFilePath);
		}

		[Fact]
		public async Task ANewHistoryCanMoveBeforeItHasAFile()
		{
			_dialogs.Answers.Enqueue(0);						// Start a new history
			var window = await StartFreshAsync();
			var target = Path.Combine(_root, "Dropbox", "pureData.json");
			_dialogs.InSettings = settings =>
			{
				Assert.False(settings.CanReport);				// nothing to report yet
				settings.RequestMove(target);
				return false;
			};
			_dialogs.Answers.Enqueue(0);						// OK

			await window.OpenSettingsCommand.ExecuteAsync(null);
			Assert.Equal(target, window.DataFilePath);

			window.AddPeriodCommand.Execute(null);
			await window.Periods[^1].AcceptCommand.ExecuteAsync(null);
			Assert.Single(PurityFile.Parse(File.ReadAllText(target)));
			Assert.False(File.Exists(DefaultDataFile));
		}

		[Fact]
		public async Task AFileOpenedForOneRunDoesNotMove()
		{
			var window = await OpenWithOneChangeAsync();				// opened with --data
			var offered = true;
			_dialogs.InSettings = settings => { offered = settings.CanMove; return false; };

			await window.OpenSettingsCommand.ExecuteAsync(null);

			Assert.False(offered);
		}

		[Fact]
		public void SettingsNeedADataFileForOk()
		{
			var settings = new SettingsWindowViewModel(_dataFile, null);
			Assert.True(settings.CanAccept);

			settings.DataFilePath = "  ";
			Assert.False(settings.CanAccept);
			Assert.False(settings.CanReport);
			Assert.False(settings.CanMove);
		}


		private string DefaultDataFile => Path.Combine(_root, "app data", "pureData.json");

		private async Task<MainWindowViewModel> StartFreshAsync()
		{
			_viewModel = new MainWindowViewModel(_dialogs, null, new FixedClock(), _pending.Enqueue);
			Assert.True(await _viewModel.StartAsync());
			return _viewModel;
		}

		/// <summary>The data file chosen in the settings, as in normal use, with one more period than <see cref="History"/></summary>
		private async Task<MainWindowViewModel> OpenFromSettingsWithOneChangeAsync()
		{
			WriteHistory();
			new DesktopSettings { DataFilePath = _dataFile }.Save();
			var window = await StartFreshAsync();
			window.AddPeriodCommand.Execute(null);
			await window.Periods[^1].AcceptCommand.ExecuteAsync(null);
			Assert.Equal(4, window.Periods.Count);
			Assert.Empty(_dialogs.Shown);
			return window;
		}
	}
}
