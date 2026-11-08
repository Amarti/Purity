using System.Text.RegularExpressions;
using Purity.Desktop.Views;
using Xunit;


namespace Purity.Desktop.Tests
{
	[Collection(AppDataCollection.Name)]
	public sealed class WindowTitleTests : IDisposable
	{
		public WindowTitleTests()
		{
			Environment.SetEnvironmentVariable("PURITY_APP_DATA", _folder);
		}

		public void Dispose()
		{
			Environment.SetEnvironmentVariable("PURITY_APP_DATA", null);
			try
			{
				Directory.Delete(_folder, true);
			}
			catch (IOException)
			{
				// temp gets cleaned anyway
			}
		}


		[Fact]
		public void TheVersionIsThreeNumbers() => Assert.Matches(@"^\d+\.\d+\.\d+$", AppVersion.Number);

		[Fact]
		public Task TheMainWindowsTitleShowsTheVersion() => Ui.Run(() =>
		{
			var window = new MainWindow(new StartOptions(Path.Combine(_folder, "pureData.json")));
			Assert.Equal($"Purity {AppVersion.Number}", window.Title);
		});

		/// <summary>
		/// The version is set in the project file alone. The packaging templates carry a placeholder
		/// the build fills in, so no second copy can fall behind.
		/// </summary>
		[Fact]
		public void TheVersionIsSetOnlyInTheProjectFile()
		{
			var desktop = FindSource("src/Purity.Desktop");
			if (desktop == null)
				Assert.Skip("The source isn't here, as in the platform check kit");

			var project = File.ReadAllText(Path.Combine(desktop, "Purity.Desktop.csproj"));
			Assert.Equal(AppVersion.Number, Regex.Match(project, "<Version>(.*?)</Version>").Groups[1].Value);
			foreach (var template in new[] { "Packaging/macOS/Info.plist", "Packaging/linux/control" })
			{
				var text = File.ReadAllText(Path.Combine(desktop, template));
				Assert.Contains("@VERSION@", text);
				Assert.DoesNotContain(AppVersion.Number, text);
			}
		}


		/// <summary>A folder of the repo, found by walking up from the tests, or null outside the repo</summary>
		private static string? FindSource(string relative)
		{
			for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
			{
				var candidate = Path.Combine(dir.FullName, relative);
				if (Directory.Exists(candidate))
					return candidate;
			}
			return null;
		}


		private readonly string _folder = Path.Combine(Path.GetTempPath(), $"purity-title-{Guid.NewGuid():N}");
	}
}
