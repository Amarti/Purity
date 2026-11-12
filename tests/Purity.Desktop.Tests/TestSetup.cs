using System.Runtime.CompilerServices;
using Purity.Desktop.Localization;


namespace Purity.Desktop.Tests
{
	internal static class TestSetup
	{
		/// <summary>
		/// Every first start in these tests sees an English computer with Gregorian dates, whatever the
		/// one they run on is set to. Tests of the first start's language pass their own instead.
		/// </summary>
		[ModuleInitializer]
		internal static void UseAnEnglishComputer() => ComputerLanguage.Instead = ComputerLanguage.Default;
	}
}
