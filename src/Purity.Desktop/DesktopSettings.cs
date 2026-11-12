using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Purity.Desktop.Localization;


namespace Purity.Desktop
{
	/// <summary>This device's settings. Kept per user, not beside the app as the WPF app does, so the app folder can be read-only.</summary>
	public sealed class DesktopSettings
	{
		/// <summary>Absolute path to the data file, or empty when none was chosen yet</summary>
		public string DataFilePath { get; set; } = string.Empty;

		/// <summary>
		/// <see cref="Appearance"/> as saved. Kept as text and read leniently: a value this version
		/// doesn't know, perhaps from a newer one, means the system's choice, rather than a file that
		/// can't be read and so loses the data file path too.
		/// </summary>
		[JsonPropertyName("Appearance")]
		public string AppearanceText { get; set; } = nameof(Desktop.Appearance.System);

		[JsonIgnore]
		public Appearance Appearance
		{
			get => Enum.TryParse<Appearance>(AppearanceText, out var value) && Enum.IsDefined(value) ? value : Appearance.System;
			set => AppearanceText = value.ToString();
		}

		/// <summary>
		/// <see cref="Language"/> as saved, read leniently for the same reason: a value this version
		/// doesn't know means English.
		/// </summary>
		[JsonPropertyName("Language")]
		public string LanguageText { get; set; } = nameof(Localization.Language.English);

		[JsonIgnore]
		public Language Language
		{
			get => Enum.TryParse<Language>(LanguageText, out var value) && Enum.IsDefined(value) ? value : Language.English;
			set => LanguageText = value.ToString();
		}

		/// <summary>
		/// <see cref="Calendar"/> as saved, read leniently for the same reason: a value this version
		/// doesn't know means Gregorian with the Hebrew date beside it.
		/// </summary>
		[JsonPropertyName("Calendar")]
		public string CalendarText { get; set; } = nameof(CalendarKind.Gregorian);

		[JsonIgnore]
		public CalendarKind Calendar
		{
			get => Enum.TryParse<CalendarKind>(CalendarText, out var value) && Enum.IsDefined(value) ? value : CalendarKind.Gregorian;
			set => CalendarText = value.ToString();
		}

		/// <summary>Whether Hebrew dates are written in English letters everywhere</summary>
		public bool HebrewDatesInEnglish { get; set; }

		/// <summary>How reminders are set up on this device</summary>
		public ReminderSetup Reminders
		{
			get => _reminders;
			set => _reminders = value ?? new ReminderSetup();
		}

		/// <summary>The ID of Purity's calendar in the connected Google account, or null when there's none yet</summary>
		public string? GoogleCalendarId { get; set; }

		/// <summary>Where the main window was when it last closed, or null before it ever closed with a data file chosen</summary>
		public WindowPlacement? Window { get; set; }


		/// <summary>True for the defaults that <see cref="Load"/> gives when nothing was saved, or what was saved can't be read</summary>
		[JsonIgnore]
		public bool IsNew { get; private init; }


		private ReminderSetup _reminders = new();


		/// <summary>The saved settings, or defaults when there are none or they can't be read</summary>
		public static DesktopSettings Load()
		{
			try
			{
				var path = AppPaths.SettingsFile;
				if (!File.Exists(path))
					return new DesktopSettings { IsNew = true };

				return JsonSerializer.Deserialize(File.ReadAllText(path), SettingsJson.Default.DesktopSettings) ?? new DesktopSettings { IsNew = true };
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
			{
				return new DesktopSettings { IsNew = true };
			}
		}

		/// <exception cref="IOException">The settings couldn't be written</exception>
		public void Save()
		{
			Directory.CreateDirectory(AppPaths.Folder);
			var path = AppPaths.SettingsFile;
			var temp = path + ".tmp";
			File.WriteAllText(temp, JsonSerializer.Serialize(this, SettingsJson.Default.DesktopSettings), new UTF8Encoding(false));
			File.Move(temp, path, overwrite: true);
		}
	}


	/// <summary>Light, dark, or whatever the system uses (tweak T7, approved for 2.2.0)</summary>
	public enum Appearance
	{
		System,
		Light,
		Dark,
	}


	/// <summary>The main window's place and size while it isn't maximized, and whether it was</summary>
	/// <param name="X">Left edge, in screen pixels</param>
	/// <param name="Y">Top edge, in screen pixels</param>
	/// <param name="Width">Inside the frame, in the app's units, as the window's own Width</param>
	/// <param name="Height">Inside the frame, in the app's units, as the window's own Height</param>
	/// <param name="Maximized">Whether it was maximized: then the rest is where it goes back to</param>
	public sealed record WindowPlacement(int X, int Y, double Width, double Height, bool Maximized);


	[JsonSerializable(typeof(DesktopSettings))]
	[JsonSourceGenerationOptions(WriteIndented = true)]
	internal sealed partial class SettingsJson : JsonSerializerContext
	{}
}
