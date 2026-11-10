using Purity.Desktop.ViewModels;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>Light or dark (T7) and the window's place (T8), as the settings keep them</summary>
	public sealed partial class MainWindowTests
	{
		[Fact]
		public async Task SettingsChangeTheAppearanceAndKeepItForNextTime()
		{
			var window = await OpenFromSettingsWithOneChangeAsync();
			var changes = new List<string?>();
			window.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
			_dialogs.InSettings = settings =>
			{
				Assert.Equal(Appearance.System, settings.Appearance);
				settings.IsDark = true;
				return true;
			};

			await window.OpenSettingsCommand.ExecuteAsync(null);

			Assert.Equal(Appearance.Dark, window.Appearance);
			Assert.Contains(nameof(MainWindowViewModel.Appearance), changes);
			Assert.Equal(Appearance.Dark, DesktopSettings.Load().Appearance);
			Assert.Equal(_dataFile, DesktopSettings.Load().DataFilePath);
			Assert.Empty(_dialogs.Shown);

			// Cancel leaves it as it was
			_dialogs.InSettings = settings =>
			{
				Assert.Equal(Appearance.Dark, settings.Appearance);
				settings.IsLight = true;
				return false;
			};
			await window.OpenSettingsCommand.ExecuteAsync(null);
			Assert.Equal(Appearance.Dark, window.Appearance);
			Assert.Equal(Appearance.Dark, DesktopSettings.Load().Appearance);
		}

		[Fact]
		public async Task TheWindowsPlaceIsKeptForNextTime()
		{
			var window = await OpenFromSettingsWithOneChangeAsync();
			var placement = new WindowPlacement(120, 80, 820, 640, true);

			window.RememberWindow(placement);

			Assert.Equal(placement, DesktopSettings.Load().Window);
			Assert.Equal(_dataFile, DesktopSettings.Load().DataFilePath);
			using var next = new MainWindowViewModel(_dialogs, null, new FixedClock(), _pending.Enqueue);
			Assert.Equal(placement, next.WindowPlacement);
		}

		[Fact]
		public async Task AWindowOpenedOnADataFileForThisRunOnlyIsKeptToo()
		{
			WriteHistory();
			var window = await OpenAsync();					// given for this run only, as --data does
			var placement = new WindowPlacement(120, 80, 820, 640, false);

			window.RememberWindow(placement);

			var settings = DesktopSettings.Load();
			Assert.Equal(placement, settings.Window);
			Assert.True(string.IsNullOrWhiteSpace(settings.DataFilePath), "A file given for one run mustn't reach the settings");
		}

		[Fact]
		public async Task ClosingTheFirstQuestionKeepsNoWindowPlaceEither()
		{
			_viewModel = new MainWindowViewModel(_dialogs, null, new FixedClock(), _pending.Enqueue);
			Assert.False(await _viewModel.StartAsync());

			_viewModel.RememberWindow(new WindowPlacement(120, 80, 820, 640, false));

			Assert.False(File.Exists(AppPaths.SettingsFile));
		}

		[Fact]
		public void AnAppearanceThisVersionDoesntKnowKeepsTheOtherSettings()
		{
			Directory.CreateDirectory(AppPaths.Folder);
			File.WriteAllText(AppPaths.SettingsFile, $$"""
				{
				  "DataFilePath": {{System.Text.Json.JsonSerializer.Serialize(_dataFile)}},
				  "Appearance": "Sepia",
				  "Window": { "X": 5, "Y": 6, "Width": 700, "Height": 500, "Maximized": false }
				}
				""");

			var settings = DesktopSettings.Load();

			Assert.Equal(Appearance.System, settings.Appearance);
			Assert.Equal(_dataFile, settings.DataFilePath);
			Assert.Equal(new WindowPlacement(5, 6, 700, 500, false), settings.Window);
		}

		[Fact]
		public void SettingsFromBefore220StillOpen()
		{
			Directory.CreateDirectory(AppPaths.Folder);
			File.WriteAllText(AppPaths.SettingsFile, $$"""{ "DataFilePath": {{System.Text.Json.JsonSerializer.Serialize(_dataFile)}} }""");

			var settings = DesktopSettings.Load();

			Assert.Equal(Appearance.System, settings.Appearance);
			Assert.Null(settings.Window);
			Assert.Equal(_dataFile, settings.DataFilePath);
		}
	}
}
