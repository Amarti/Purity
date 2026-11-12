using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;


namespace Purity.Desktop.Localization
{
	/// <summary>
	/// The language this computer is set to, which a first start takes on, with Hebrew dates for
	/// Hebrew and Gregorian ones for English, on every system alike. The app is built with invariant
	/// globalization, so .NET's cultures don't know the language, and each system is asked in its
	/// own way:
	/// <list type="bullet">
	/// <item>Windows: the display language.</item>
	/// <item>macOS: the first preferred language under Language &amp; Region.</item>
	/// <item>Linux: the language of messages from the usual environment variables.</item>
	/// </list>
	/// Anything else, or anything that can't be read, means English.
	/// </summary>
	public readonly record struct ComputerLanguage(Language Language)
	{
		public static readonly ComputerLanguage Default = new(Language.English);

		/// <summary>Used instead of asking the computer, so tests don't depend on the one they run on</summary>
		internal static ComputerLanguage? Instead { get; set; }


		/// <summary>The dates go with the language, whatever calendar the computer itself shows</summary>
		public CalendarKind Calendar => Language == Language.Hebrew ? CalendarKind.Hebrew : CalendarKind.Gregorian;


		public static ComputerLanguage OfThisComputer()
		{
			if (Instead is { } given)
				return given;

			// Only a nicety of the first start, so nothing it meets may stop the app from starting
			try
			{
				if (OperatingSystem.IsWindows())
					return FromWindows(Windows.GetUserDefaultUILanguage());
				if (OperatingSystem.IsMacOS())
					return new(LanguageOf(Mac.FirstPreferredLanguage()));
				if (OperatingSystem.IsLinux())
					return FromLinux(Environment.GetEnvironmentVariable);
			}
			catch (Exception)
			{}
			return Default;
		}


		/// <param name="uiLanguage">The display language's LANGID, whose low 10 bits name the language</param>
		internal static ComputerLanguage FromWindows(ushort uiLanguage) =>
			new((uiLanguage & 0x3FF) == Windows.LangHebrew ? Language.Hebrew : Language.English);

		/// <summary>
		/// As gettext finds the language of messages: LC_ALL, then LC_MESSAGES, then LANG, though
		/// LANGUAGE's first choice comes before them all unless they make it the plain C locale.
		/// </summary>
		internal static ComputerLanguage FromLinux(Func<string, string?> variable)
		{
			var locale = new[] { "LC_ALL", "LC_MESSAGES", "LANG" }.Select(variable).FirstOrDefault(el => !string.IsNullOrEmpty(el));
			var plain = locale is null or "C" or "POSIX" || locale.StartsWith("C.", StringComparison.Ordinal);
			var first = variable("LANGUAGE")?.Split(':').FirstOrDefault(el => el.Length > 0);
			return new(LanguageOf(first != null && !plain ? first : locale));
		}

		/// <summary>Hebrew for a tag or locale of Hebrew, such as "he", "he-IL" or "he_IL.UTF-8", or its old code "iw"</summary>
		internal static Language LanguageOf(string? tag) =>
			tag != null && Regex.IsMatch(tag, "^(he|iw)([-_.@]|$)", RegexOptions.IgnoreCase) ? Language.Hebrew : Language.English;


		internal static class Windows
		{
			public const int LangHebrew = 0x0D;

			[DllImport("kernel32.dll")]
			public static extern ushort GetUserDefaultUILanguage();
		}


		internal static class Mac
		{
			private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
			private const uint Utf8 = 0x08000100;

			/// <summary>The first of the languages chosen under Language &amp; Region, such as "he-IL"</summary>
			public static string? FirstPreferredLanguage()
			{
				var languages = CFLocaleCopyPreferredLanguages();
				if (languages == IntPtr.Zero)
					return null;
				try
				{
					return CFArrayGetCount(languages) > 0 ? Text(CFArrayGetValueAtIndex(languages, 0)) : null;
				}
				finally
				{
					CFRelease(languages);
				}
			}

			private static string? Text(IntPtr text)
			{
				if (text == IntPtr.Zero)
					return null;
				var buffer = new byte[256];
				return CFStringGetCString(text, buffer, buffer.Length, Utf8) ? Encoding.UTF8.GetString(buffer, 0, Array.IndexOf(buffer, (byte)0)) : null;
			}

			[DllImport(CoreFoundation)]
			private static extern IntPtr CFLocaleCopyPreferredLanguages();

			[DllImport(CoreFoundation)]
			private static extern nint CFArrayGetCount(IntPtr array);

			[DllImport(CoreFoundation)]
			private static extern IntPtr CFArrayGetValueAtIndex(IntPtr array, nint index);

			[DllImport(CoreFoundation)]
			[return: MarshalAs(UnmanagedType.U1)]
			private static extern bool CFStringGetCString(IntPtr text, byte[] buffer, nint size, uint encoding);

			[DllImport(CoreFoundation)]
			private static extern void CFRelease(IntPtr item);
		}
	}
}
