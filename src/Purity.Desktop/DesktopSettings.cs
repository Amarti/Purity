using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace Purity.Desktop
{
	/// <summary>This device's settings. Kept per user, not beside the app as the WPF app does, so the app folder can be read-only.</summary>
	public sealed class DesktopSettings
	{
		/// <summary>Absolute path to the data file, or empty when none was chosen yet</summary>
		public string DataFilePath { get; set; } = string.Empty;


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


	[JsonSerializable(typeof(DesktopSettings))]
	[JsonSourceGenerationOptions(WriteIndented = true)]
	internal sealed partial class SettingsJson : JsonSerializerContext
	{}
}
