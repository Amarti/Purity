using System.Text;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>Bringing back an earlier version of the history from this computer's backups</summary>
	public sealed partial class MainWindowTests
	{
		[Fact]
		public async Task AnEarlierVersionCanBeBroughtBack()
		{
			var window = await OpenWithOneChangeAsync();
			var current = ReadData();
			_dialogs.InSettings = settings =>
			{
				Assert.True(settings.CanRestore);
				settings.RequestRestore();
				return false;
			};
			_dialogs.ChooseBackup = choices => choices.Single(el => el.Label.Contains("3 periods")).Backup;
			_dialogs.Answers.Enqueue(0);					// Restore
			_dialogs.Answers.Enqueue(0);					// OK

			await window.OpenSettingsCommand.ExecuteAsync(null);

			var label = Assert.Single(_dialogs.Choices!).Label;
			Assert.StartsWith("Until ", label);
			Assert.EndsWith("3 periods, the last beginning 31 March 2031", label);
			Assert.Contains("Bring back your history as it was until", _dialogs.Shown[0]);
			Assert.Contains("back as it was until", _dialogs.Shown[1]);
			Assert.Equal(History(), ReadData());								// exactly as it was, events included
			Assert.Equal(3, window.Periods.Count);
			Assert.Contains(BackupContents(), el => Encoding.UTF8.GetString(el) == current);	// and the version it replaced is kept
			Assert.Null(window.Warning);

			// Asked again, the list offers the version just replaced, and marks the one matching today's history
			_dialogs.ChooseBackup = _ => null;
			await window.OpenSettingsCommand.ExecuteAsync(null);
			Assert.Equal(2, _dialogs.Choices!.Count);
			Assert.Contains(_dialogs.Choices, el => el.Label.Contains("4 periods") && !el.Label.EndsWith("the same as now"));
			Assert.Contains(_dialogs.Choices, el => el.Label.EndsWith("3 periods, the last beginning 31 March 2031, the same as now"));
		}

		[Fact]
		public async Task CancellingARestoreChangesNothing()
		{
			var window = await OpenWithOneChangeAsync();
			var current = ReadData();
			_dialogs.InSettings = settings => { settings.RequestRestore(); return false; };
			_dialogs.ChooseBackup = choices => choices[0].Backup;
			_dialogs.Answers.Enqueue(1);					// Cancel

			await window.OpenSettingsCommand.ExecuteAsync(null);
			Assert.Equal(current, ReadData());
			Assert.Equal(4, window.Periods.Count);

			_dialogs.ChooseBackup = _ => null;				// or closes the list without choosing
			await window.OpenSettingsCommand.ExecuteAsync(null);
			Assert.Single(_dialogs.Shown);
			Assert.Equal(current, ReadData());
		}

		[Fact]
		public async Task ADamagedBackupChangesNothing()
		{
			var window = await OpenWithOneChangeAsync();
			var current = ReadData();
			var backup = Directory.GetFiles(Path.Combine(_root, "app data"), "*.json", SearchOption.AllDirectories)
				.Single(el => Path.GetFileName(Path.GetDirectoryName(el)) == "backups");
			File.WriteAllText(backup, "[ { \"Begin\": \"2031-0");
			_dialogs.InSettings = settings => { settings.RequestRestore(); return false; };
			_dialogs.ChooseBackup = choices => choices[0].Backup;
			_dialogs.Answers.Enqueue(0);					// Restore
			_dialogs.Answers.Enqueue(0);					// OK

			await window.OpenSettingsCommand.ExecuteAsync(null);

			Assert.EndsWith("can't be read", _dialogs.Choices![0].Label);
			Assert.Contains("can't be read, so nothing was changed", _dialogs.Shown[^1]);
			Assert.Equal(current, ReadData());
			Assert.Equal(4, window.Periods.Count);
		}

		[Fact]
		public async Task RestoringIsOffUntilThereIsAnEarlierVersion()
		{
			WriteHistory();
			var window = await OpenAsync();
			var offered = true;
			_dialogs.InSettings = settings => { offered = settings.CanRestore; return false; };

			await window.OpenSettingsCommand.ExecuteAsync(null);

			Assert.False(offered);
		}


		/// <summary>Opens the history and accepts one more period, which keeps the version before it as a backup</summary>
		private async Task<ViewModels.MainWindowViewModel> OpenWithOneChangeAsync()
		{
			WriteHistory();
			var window = await OpenAsync();
			window.AddPeriodCommand.Execute(null);
			await window.Periods[^1].AcceptCommand.ExecuteAsync(null);
			Assert.Equal(4, window.Periods.Count);
			return window;
		}
	}
}
