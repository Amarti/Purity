using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
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
				WriteAtomically(filePath, json);
			}
			catch (Exception e)
			{
				Logger.Error($"{nameof(SerializeData)}: {e.Message}\n{e.StackTrace}");
			}
		}

		/// <summary>
		/// Reads the data file. A file that doesn't exist yet is an empty history
		/// </summary>
		/// <param name="filePath">Path to the data file</param>
		/// <param name="loadFailed">The file couldn't be read. The result is then empty, and must never be saved over the file</param>
		public static List<PurityPeriod> DeserializeData(string? filePath, out bool loadFailed)
		{
			loadFailed = false;
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
				loadFailed = e is not FileNotFoundException;
				Logger.Error($"{nameof(DeserializeData)}: {e.Message}\n{e.StackTrace}");
			}

			return data;
		}
		/// <summary>
		/// Replaces the file in one step, so a crash or a full disk leaves the old content whole.<br/>
		/// If the rename keeps failing, the new content stays in the temporary file beside it
		/// </summary>
		private static void WriteAtomically(string filePath, string text)
		{
			var tempPath = filePath + TEMP_EXTENSION;
			using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
			{
				stream.Write(new UTF8Encoding(false).GetBytes(text));
				stream.Flush(true);
			}

			for (var attempt = 1; ; attempt++)
			{
				try
				{
					File.Move(tempPath, filePath, true);
					return;
				}
				catch (Exception e) when (attempt < MOVE_ATTEMPTS && e is IOException or UnauthorizedAccessException)
				{
					Thread.Sleep(MOVE_RETRY_DELAY_MS);	// Windows refuses while a sync client or a scanner has the file open
				}
			}
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
			var p = json.IndexOf(VERSION_TOKEN);
			if (p == -1)
				return 0;

			p += VERSION_TOKEN.Length;
			var pc = json.IndexOf(',', p);
			if (p > 0 && pc > 0)
			{
				var v = json.Substring(p, pc - p);
				if (int.TryParse(v, out var i))
					return i;
			}

			return 0;
		}


		public static void ExportPeriodsLengthsReport(List<PurityPeriod> data, string? filePath = null)
		{
			if (data.Count < 1)
				return;

			try
			{
				var sb = new StringBuilder("Periods lengths" + Environment.NewLine);
				for (int i = 0; i < data.Count - 1; i++)
					sb.AppendLine(PurityPeriod.GetFullPeriodLength(data[i], data[i + 1]).ToString("0.0"));

				File.WriteAllText(filePath ?? REPORT_FILE_NAME, sb.ToString());
			}
			catch (Exception e)
			{
				Logger.Error($"{nameof(ExportPeriodsLengthsReport)}: {e.Message}\n{e.StackTrace}");
			}
		}


		private const string SETTINGS_FILE_NAME = "pureSettings.json";
		private const string DATA_FILE_NAME = "pureData.json";
		private const string REPORT_FILE_NAME = "periodsLengths.csv";
		private const string BACKUP_EXTENSION = ".bck";
		private const string TEMP_EXTENSION = ".tmp";
		private const string VERSION_TOKEN = "\"Version\":";
		private const int MOVE_ATTEMPTS = 10;
		private const int MOVE_RETRY_DELAY_MS = 300;

		public const string LOAD_FAILED_MESSAGE = "The data file couldn't be read, so your history isn't shown.\n\nNothing will be saved until it can be read, and anything you enter now will be lost. Close the app, let your sync finish, and open it again.";

		public static readonly JsonSerializerOptions DEFAULT_SERIALIZATION_OPTIONS = new JsonSerializerOptions
		{
			WriteIndented = true,
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,  // default encoder escapes '<', '>' and other non-HTML-safe symbols
			Converters = {}
		};

		private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
	}
}
