using Purity.Desktop.Localization;
using Purity.Desktop.ViewModels;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>A first start takes on the computer's language with its dates, and a saved choice always wins</summary>
	public sealed partial class MainWindowTests
	{
		private static readonly ComputerLanguage HebrewComputer = new(Language.Hebrew);


		[Fact]
		public Task TheFirstStartTakesOnTheComputersLanguageWithItsDates() => BackToEnglishAfter(async () =>
		{
			_dialogs.Answers.Enqueue(0);						// Start a new history
			_viewModel = new MainWindowViewModel(_dialogs, null, new FixedClock(), _pending.Enqueue, computerLanguage: () => HebrewComputer);
			Assert.True(await _viewModel.StartAsync());

			Assert.Contains(UiText.Hebrew.FirstStartTitle, Assert.Single(_dialogs.Shown));
			Assert.Equal(Language.Hebrew, Loc.Instance.Language);
			Assert.Equal(CalendarKind.Hebrew, Loc.Instance.Calendar);
			var saved = DesktopSettings.Load();					// saved along with where the history is
			Assert.Equal(Language.Hebrew, saved.Language);
			Assert.Equal(CalendarKind.Hebrew, saved.Calendar);
			Assert.Equal(DefaultDataFile, saved.DataFilePath);
		});

		[Fact]
		public Task ClosingTheFirstQuestionLeavesTheLanguageToTheNextStart() => BackToEnglishAfter(async () =>
		{
			_viewModel = new MainWindowViewModel(_dialogs, null, new FixedClock(), _pending.Enqueue, computerLanguage: () => HebrewComputer);
			Assert.False(await _viewModel.StartAsync());
			Assert.False(File.Exists(SettingsFile));

			using var next = new MainWindowViewModel(_dialogs, null, new FixedClock(), _pending.Enqueue, computerLanguage: () => ComputerLanguage.Default);
			Assert.Equal(Language.English, Loc.Instance.Language);
			Assert.Equal(CalendarKind.Gregorian, Loc.Instance.Calendar);
		});

		[Fact]
		public async Task SavedSettingsKeepTheirLanguageWhateverTheComputerIsSetTo()
		{
			WriteHistory();
			new DesktopSettings { DataFilePath = _dataFile }.Save();
			_viewModel = new MainWindowViewModel(_dialogs, null, new FixedClock(), _pending.Enqueue, computerLanguage: () => HebrewComputer);
			Assert.True(await _viewModel.StartAsync());

			Assert.Equal(Language.English, Loc.Instance.Language);
			Assert.Equal(CalendarKind.Gregorian, Loc.Instance.Calendar);
			Assert.Equal(3, _viewModel.Periods.Count);
		}

		[Fact]
		public Task SettingsThatCantBeReadTakeOnTheComputersLanguageToo() => BackToEnglishAfter(() =>
		{
			Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
			File.WriteAllText(SettingsFile, "{ not settings");
			_viewModel = new MainWindowViewModel(_dialogs, null, new FixedClock(), _pending.Enqueue, computerLanguage: () => HebrewComputer);

			Assert.Equal(Language.Hebrew, Loc.Instance.Language);
			Assert.Equal(CalendarKind.Hebrew, Loc.Instance.Calendar);
			return Task.CompletedTask;
		});

		[Fact]
		public void OnlyDefaultsAreNew()
		{
			Assert.True(DesktopSettings.Load().IsNew);
			new DesktopSettings().Save();
			Assert.False(DesktopSettings.Load().IsNew);
			Assert.False(new DesktopSettings().IsNew);
		}


		private string SettingsFile => Path.Combine(_root, "app data", "settings.json");
	}
}
