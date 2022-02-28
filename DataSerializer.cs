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
		public static void Serialize(List<PurityPeriod> data, string filePath = null, bool backup = false)
		{
			try
			{
				filePath ??= GetDefaultDataFilePath();

				if (backup && File.Exists(filePath))
					File.Copy(filePath, filePath + BACKUP_EXTENSION, true);

				var json = JsonSerializer.Serialize(data, DEFAULT_SERIALIZATION_OPTIONS);
				File.WriteAllText(filePath, json);
			}
			catch (Exception e)
			{
				Logger.Error($"{nameof(Serialize)}: {e.Message}\n{e.StackTrace}");
			}
		}

		public static List<PurityPeriod> Deserialize(string filePath = null)
		{
			List<PurityPeriod> data;
			try
			{
				filePath ??= GetDefaultDataFilePath();

				var json = File.ReadAllText(filePath);
				var v = GetConfigurationVersion(json);
				data = JsonSerializer.Deserialize<List<PurityPeriod>>(json, DEFAULT_SERIALIZATION_OPTIONS);

				UpgradeData(data);

				return data;
			}
			catch (Exception e)
			{
				Logger.Error($"{nameof(Deserialize)}: {e.Message}\n{e.StackTrace}");
			}

			data = new List<PurityPeriod>();

			return data;
		}
		private static string GetDefaultDataFilePath()
		{
			return DATA_FILE_NAME;
		}
		private static void UpgradeData(List<PurityPeriod> data)
		{
			// ¯\_(ツ)_/¯
		}

		private static int GetConfigurationVersion(string json)
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

		private const string DATA_FILE_NAME = "pureData.json";
		private const string BACKUP_EXTENSION = ".bck";
		private const string VERSION_TOKEN = "\"Version\":";

		public static JsonSerializerOptions DEFAULT_SERIALIZATION_OPTIONS = new JsonSerializerOptions
		{
			WriteIndented = true,
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,  // default encoder escapes '<', '>' and other non-HTML-safe symbols
			Converters = {}
		};

		private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
	}
}
