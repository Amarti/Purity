using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using NLog;


namespace Purity
{
	public static class DataSerializer
	{
		public static void SerializeSettings(Settings settings, string? filePath = null)
		{
			try
			{
				filePath ??= SETTINGS_FILE_NAME;

				var json = JsonSerializer.Serialize(settings, DEFAULT_SERIALIZATION_OPTIONS);
				File.WriteAllText(filePath, json);
			}
			catch (Exception e)
			{
				Logger.Error($"{nameof(SerializeSettings)}: {e.Message}\n{e.StackTrace}");
			}
		}

		public static Settings DeserializeSettings(string? filePath = null)
		{
			var settings = new Settings();
			try
			{
				filePath ??= SETTINGS_FILE_NAME;

				var json = File.ReadAllText(filePath);
				settings = JsonSerializer.Deserialize<Settings>(json, DEFAULT_SERIALIZATION_OPTIONS) ?? settings;

				return settings;
			}
			catch (Exception e)
			{
				Logger.Error($"{nameof(DeserializeSettings)}: {e.Message}\n{e.StackTrace}");
			}

			return settings;
		}


		public static void SerializeData(List<PurityPeriod> data, string? filePath = null, bool backup = false)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(filePath))
					filePath = GetDefaultDataFilePath();

				if (backup && File.Exists(filePath))
					File.Copy(filePath, filePath + BACKUP_EXTENSION, true);

				var json = JsonSerializer.Serialize(data, DEFAULT_SERIALIZATION_OPTIONS);
				File.WriteAllText(filePath, json);
			}
			catch (Exception e)
			{
				Logger.Error($"{nameof(SerializeData)}: {e.Message}\n{e.StackTrace}");
			}
		}

		public static List<PurityPeriod> DeserializeData(string? filePath = null)
		{
			var data = new List<PurityPeriod>();
			try
			{
				if (string.IsNullOrWhiteSpace(filePath))
					filePath = GetDefaultDataFilePath();

				var json = File.ReadAllText(filePath);
				var v = GetDataVersion(json);
				data = JsonSerializer.Deserialize<List<PurityPeriod>>(json, DEFAULT_SERIALIZATION_OPTIONS) ?? data;

				UpgradeData(data);

				return data;
			}
			catch (Exception e)
			{
				Logger.Error($"{nameof(DeserializeData)}: {e.Message}\n{e.StackTrace}");
			}

			return data;
		}
		private static string GetDefaultDataFilePath()
		{
			return DATA_FILE_NAME;
		}
		private static void UpgradeData(List<PurityPeriod>? data)
		{
			// ¯\_(ツ)_/¯
		}
		private static int GetDataVersion(string json)
		{
			var p = json.IndexOf(VERSION_TOKEN) + VERSION_TOKEN.Length;
			var pc = json.IndexOf(',', p);
			if (p > 0 && pc > 0)
			{
				var v = json.Substring(p, pc - p);
				if (int.TryParse(v, out var i))
					return i;
			}

			return 0;
		}


		private const string SETTINGS_FILE_NAME = "pureSettings.json";
		private const string DATA_FILE_NAME = "pureData.json";
		private const string BACKUP_EXTENSION = ".bck";
		private const string VERSION_TOKEN = "\"Version\":";

		public static readonly JsonSerializerOptions DEFAULT_SERIALIZATION_OPTIONS = new JsonSerializerOptions
		{
			WriteIndented = true,
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,  // default encoder escapes '<', '>' and other non-HTML-safe symbols
			Converters = {}
		};

		private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
	}
}
