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

		/// <summary>Where the main window was when it last closed, or null before it ever closed with a data file chosen</summary>
		public WindowPlacement? Window { get; set; }


		/// <summary>The saved settings, or defaults when there are none or they can't be read</summary>
		public static DesktopSettings Load()
		{
			try
			{
				var path = AppPaths.SettingsFile;
				if (!File.Exists(path))
					return new DesktopSettings();

				return JsonSerializer.Deserialize(File.ReadAllText(path), SettingsJson.Default.DesktopSettings) ?? new DesktopSettings();
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
			{
				return new DesktopSettings();
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
