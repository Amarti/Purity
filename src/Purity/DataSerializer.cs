using System;
using System.Collections.Generic;
using System.Globalization;
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

				WriteAtomically(filePath, ToJson(data));
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
		/// <param name="fileText">The file as it was read, to notice later whether someone else changed it</param>
		public static List<PurityPeriod> DeserializeData(string? filePath, out bool loadFailed, out string? fileText)
		{
			loadFailed = false;
			fileText = null;
			var data = new List<PurityPeriod>();
			try
			{
				if (string.IsNullOrWhiteSpace(filePath))
					filePath = GetDefaultDataFilePath();

				var json = ReadWhenFree(filePath);
				fileText = json;
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
		/// The data file's text right now, or null if it's missing or can't be read at the moment
		/// </summary>
		public static string? ReadDataText(string? filePath)
		{
			try
			{
				return ReadWhenFree(DataFilePathOrDefault(filePath));
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				return null;
			}
		}

		/// <summary>
		/// The data as it would be written
		/// </summary>
		public static string ToJson(List<PurityPeriod> data) => JsonSerializer.Serialize(data, DEFAULT_SERIALIZATION_OPTIONS);

		public static bool IsSameFile(string? a, string? b) =>
			string.Equals(Path.GetFullPath(DataFilePathOrDefault(a)), Path.GetFullPath(DataFilePathOrDefault(b)), StringComparison.OrdinalIgnoreCase);

		/// <summary>
		/// Writes the data beside the data file, named the way sync clients name a conflicted copy, so the new apps find it
		/// </summary>
		/// <returns>Path of the copy, or null if it couldn't be written</returns>
		public static string? SerializeConflictedCopy(List<PurityPeriod> data, string? filePath)
		{
			try
			{
				var path = Path.GetFullPath(DataFilePathOrDefault(filePath));
				var stamp = DateTime.Now.ToString("yyyy-MM-dd HHmmss", CultureInfo.InvariantCulture);
				var copy = Path.Combine(Path.GetDirectoryName(path)!, $"{Path.GetFileNameWithoutExtension(path)} (conflicted copy {stamp}){Path.GetExtension(path)}");

				WriteAtomically(copy, ToJson(data));
				return copy;
			}
			catch (Exception e)
			{
				Logger.Error($"{nameof(SerializeConflictedCopy)}: {e.Message}\n{e.StackTrace}");
				return null;
			}
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
				catch (Exception e) when (attempt < LOCK_ATTEMPTS && e is IOException or UnauthorizedAccessException)
				{
					Thread.Sleep(LOCK_RETRY_DELAY_MS);	// Windows refuses while a sync client or a scanner has the file open
				}
			}
		}
		/// <summary>
		/// Reads a file, waiting out a sync client or a scanner that has it open for a moment. A missing file is reported at once
		/// </summary>
		private static string ReadWhenFree(string path)
		{
			for (var attempt = 1; ; attempt++)
			{
				try
				{
					return File.ReadAllText(path);
				}
				catch (Exception e) when (attempt < LOCK_ATTEMPTS && e is not (FileNotFoundException or DirectoryNotFoundException) && e is IOException or UnauthorizedAccessException)
				{
					Thread.Sleep(LOCK_RETRY_DELAY_MS);
				}
			}
		}
		private static string DataFilePathOrDefault(string? filePath) =>
			string.IsNullOrWhiteSpace(filePath) ? GetDefaultDataFilePath() : filePath;
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
		private const int LOCK_ATTEMPTS = 10;
		private const int LOCK_RETRY_DELAY_MS = 300;

		public const string CONFLICT_MESSAGE = "The data file was changed elsewhere while the app was open, so it wasn't overwritten.\n\nYour entries from this session are in a copy beside it:\n{0}";
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
