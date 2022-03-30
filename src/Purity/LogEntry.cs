using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using NLog;
using NLog.Config;
using NLog.Targets;


namespace Purity
{
	public static class LogEntry
	{
		public static void SetCurrentDirectoryToEntryAssemblyLocation()
		{
			Environment.CurrentDirectory = GetCurrentPath();
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))     // checking for running from '<>.app/Contents/MacOS' and acting accordingly
				if (Environment.CurrentDirectory.Contains("Contents/MacOS"))
					Environment.CurrentDirectory = Directory.GetParent(Environment.CurrentDirectory)?.Parent?.Parent?.FullName ?? Environment.CurrentDirectory;
		}
		public static string GetCurrentPath()
		{
			var args = Environment.GetCommandLineArgs();
			//Logger.Info($"Current directory1 is: {Path.GetDirectoryName(args[0])}");
			//Logger.Info($"Current directory2 is: {AppContext.BaseDirectory}");
			return args.Length > 0 ? Path.GetDirectoryName(args[0]) ?? AppContext.BaseDirectory : AppContext.BaseDirectory;
		}

		public static void ConfigureLogging()
		{
			var config = new LoggingConfiguration();
			var traceTarget = new TraceTarget("tracerTarget") { Layout = "[${threadid}] ${level:uppercase=true} ${logger}:> ${message}", RawWrite = true };
			var fileTarget = new FileTarget("fileTarget")
			{
				Layout = "${longdate} [${threadid}] ${level:uppercase=true} ${logger}:> ${message}",
				FileName = (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "../" : string.Empty) + $"log/{ProductName}.log",
				ArchiveFileName = (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "../" : string.Empty) + "log/" + ProductName + ".log.{#}",
				ArchiveAboveSize = 2 * 1024 * 1024,
				ArchiveNumbering = ArchiveNumberingMode.Rolling,
				MaxArchiveFiles = 5,
				ConcurrentWrites = true,
				KeepFileOpen = false,
				Encoding = Encoding.UTF8,
				WriteBom = true,
			};
			config.AddTarget(fileTarget);
			config.AddRule(LogLevel.Info, LogLevel.Fatal, fileTarget, "*");
			config.AddTarget(traceTarget);
			config.AddRule(LogLevel.Trace, LogLevel.Fatal, traceTarget, "*");
			config.Variables.Add(HOSTNAME_ID, "${hostname}");
			LogManager.Configuration = config;
		}
		private static string GetProductName()
		{
			var assembly = Assembly.GetEntryAssembly();
			var productAttribute = assembly?.GetCustomAttributes(typeof(AssemblyProductAttribute)).SingleOrDefault() as AssemblyProductAttribute;
			return productAttribute?.Product ?? assembly?.GetName().Name ?? string.Empty;
		}

		public static string? ProductName
		{
			get
			{
				if (string.IsNullOrEmpty(_productName))
					_productName = GetProductName();

				return _productName;
			}
		}
		public static string ProductVersion
		{
			get
			{
				if (string.IsNullOrWhiteSpace(_productVersion))
				{
					var assembly = Assembly.GetExecutingAssembly();
					_productVersion = "dev";
					if (assembly?.Location != null)
					{
						var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
						_productVersion = fvi.ProductVersion ?? _productVersion;
					}
				}
				return _productVersion;
			}
		}


		private const string HOSTNAME_ID = "hostname";

		private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
		private static string _productName = string.Empty;
		private static string _productVersion = string.Empty;
	}
}
