using Purity.Desktop.Localization;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>Reading the language a computer is set to, which a first start takes on with its dates</summary>
	public sealed class ComputerLanguageTests
	{
		[Theory]
		[InlineData("he")]
		[InlineData("he-IL")]
		[InlineData("he_IL.UTF-8")]
		[InlineData("HE-il")]
		[InlineData("he@euro")]
		[InlineData("iw")]
		[InlineData("iw_IL")]
		public void HebrewTagsAreHebrew(string tag) =>
			Assert.Equal(Language.Hebrew, ComputerLanguage.LanguageOf(tag));

		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData("en-US")]
		[InlineData("en_IL.UTF-8")]
		[InlineData("heb")]
		[InlineData("C")]
		[InlineData("ar-IL")]
		public void AnythingElseIsEnglish(string? tag) =>
			Assert.Equal(Language.English, ComputerLanguage.LanguageOf(tag));

		[Theory]
		[InlineData(Language.Hebrew, CalendarKind.Hebrew)]
		[InlineData(Language.English, CalendarKind.Gregorian)]
		public void TheDatesGoWithTheLanguage(Language language, CalendarKind calendar) =>
			Assert.Equal(calendar, new ComputerLanguage(language).Calendar);

		[Theory]
		[InlineData(0x040D, Language.Hebrew)]
		[InlineData(0x0409, Language.English)]
		[InlineData(0x0809, Language.English)]			// British English
		[InlineData(0x0401, Language.English)]			// Arabic
		public void WindowsGoesByTheDisplayLanguage(int uiLanguage, Language language) =>
			Assert.Equal(new ComputerLanguage(language), ComputerLanguage.FromWindows((ushort)uiLanguage));

		[Theory]
		[InlineData("LANG=he_IL.UTF-8", Language.Hebrew)]
		[InlineData("LANG=en_US.UTF-8", Language.English)]
		[InlineData("LANG=en_US.UTF-8 LANGUAGE=he:en", Language.Hebrew)]			// LANGUAGE's first choice comes first
		[InlineData("LANG=he_IL.UTF-8 LANGUAGE=en_US:he", Language.English)]
		[InlineData("LANG=en_US.UTF-8 LANGUAGE=:he", Language.Hebrew)]
		[InlineData("LANG=C.UTF-8 LANGUAGE=he", Language.English)]				// unless the locale is the plain C one
		[InlineData("LANGUAGE=he", Language.English)]								// as it is when nothing sets one
		[InlineData("LC_ALL=en_US.UTF-8 LANG=he_IL.UTF-8", Language.English)]
		[InlineData("LC_MESSAGES=he_IL.UTF-8 LANG=en_US.UTF-8", Language.Hebrew)]
		[InlineData("LC_ALL=he_IL.UTF-8 LC_MESSAGES=en_US.UTF-8", Language.Hebrew)]
		[InlineData("", Language.English)]
		public void LinuxGoesByTheLanguageOfMessages(string variables, Language language)
		{
			var set = variables.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(el => el.Split('=')).ToDictionary(el => el[0], el => el[1]);
			Assert.Equal(new ComputerLanguage(language), ComputerLanguage.FromLinux(set.GetValueOrDefault));
		}

		/// <summary>CI runs this on Windows, macOS and Linux, which is how the calls into each system are tried</summary>
		[Fact]
		public void ThisComputerCanBeAsked()
		{
			if (OperatingSystem.IsWindows())
				Assert.NotEqual(0, ComputerLanguage.Windows.GetUserDefaultUILanguage());
			else if (OperatingSystem.IsMacOS())
				Assert.Matches("^[a-z]{2,3}([-_]|$)", ComputerLanguage.Mac.FirstPreferredLanguage());
			else
				ComputerLanguage.FromLinux(Environment.GetEnvironmentVariable);
		}
	}
}
