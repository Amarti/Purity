using System.Reflection;


namespace Purity.UI
{
	/// <summary>
	/// The app's version. It's set in one place, &lt;Version&gt; in Purity.UI.csproj, and the Mac
	/// bundle and the Linux package take it from there too.
	/// </summary>
	public static class AppVersion
	{
		/// <summary>Three numbers, as in the project file. The build also records the commit it came from after a "+", which is left out here.</summary>
		public static string Number { get; } = Read();


		private static string Read()
		{
			var assembly = typeof(AppVersion).Assembly;
			var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
				?? assembly.GetName().Version?.ToString(3)
				?? "unknown";
			return version.Split('+')[0];
		}
	}
}
