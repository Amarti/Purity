using System.Globalization;


namespace Purity.Parity
{
	public static class RepoPaths
	{
		public static string Root { get; } = FindRoot();

		public static string Golden(string name) => Path.Combine(Root, "tests", "Purity.Parity", "Golden", name);


		private static string FindRoot()
		{
			for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
				if (File.Exists(Path.Combine(dir.FullName, "Purity.sln")))
					return dir.FullName;

			throw new InvalidOperationException("Repository root not found");
		}
	}

	public static class Culture
	{
		/// <summary>The Windows region of the production machine, which sets the visible Gregorian text ("Sept", not "Sep").</summary>
		public const string Production = "en-IL";


		/// <summary>True when the OS supplies no cultures at all, as on a machine without ICU.</summary>
		public static bool Invariant =>
			Environment.GetEnvironmentVariable("DOTNET_SYSTEM_GLOBALIZATION_INVARIANT") is "1" or "true";


		public static T Run<T>(string name, Func<T> action)
		{
			if (Invariant)
				return action();

			var (culture, uiCulture) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
			try
			{
				CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
				return action();
			}
			finally
			{
				(CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (culture, uiCulture);
			}
		}
	}
}
