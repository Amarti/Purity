using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using NLog;
using NLog.Config;
using NLog.Targets;


namespace Purity.Avalonia
{
	public static class LogEntry
	{
		public static void SetCurrentDirectoryToEntryAssemblyLocation()
		{
			Environment.CurrentDirectory = GetCurrentPath();
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))     // running from *.app/Contents/MacOS
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
			var productName = GetProductName();
			var traceTarget = new TraceTarget("tracerTarget") { Layout = "[${threadid}] ${level:uppercase=true} ${logger}:> ${message}", RawWrite = true };
			var fileTarget = new FileTarget("fileTarget")
			{
				Layout = "${longdate} [${threadid}] ${level:uppercase=true} ${logger}:> ${message}",
				FileName = $"../log/{productName}.log",
				ArchiveFileName = "../log/" + productName + ".log.{#}",
				ArchiveAboveSize = 2 * 1024 * 1024,
				ArchiveNumbering = ArchiveNumberingMode.Rolling,
				MaxArchiveFiles = 5,
				ConcurrentWrites = true,
				KeepFileOpen = false,
				Encoding = Encoding.UTF8,
				WriteBom = true,
			};
			config.AddTarget(fileTarget);
			config.AddRule(LogLevel.Trace, LogLevel.Fatal, fileTarget, "*");
			config.AddTarget(traceTarget);
			config.AddRule(LogLevel.Trace, LogLevel.Fatal, traceTarget, "*");
			config.Variables.Add(HOSTNAME_ID, "${hostname}");
			LogManager.Configuration = config;
		}
		public static string GetProductName()
		{
			var assembly = Assembly.GetEntryAssembly();
			var productAttribute = assembly?.GetCustomAttributes(typeof(AssemblyProductAttribute)).SingleOrDefault() as AssemblyProductAttribute;
			return productAttribute?.Product ?? assembly?.GetName().Name ?? string.Empty;
		}

		//public static string CurrentSystemVersion
		//{
		//	get
		//	{
		//		if (string.IsNullOrWhiteSpace(_systemVersion))
		//		{
		//			var assembly = Assembly.GetEntryAssembly();
		//			_systemVersion = "dev";
		//			if (assembly.Location != null)
		//			{
		//				var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
		//				_systemVersion = fvi.ProductVersion;
		//			}
		//		}
		//		return _systemVersion;
		//	}
		//}
		//public static string CurrentSystemSoftware
		//{
		//	get
		//	{
		//		if (string.IsNullOrWhiteSpace(_systemSoftware))
		//		{
		//			var assembly = Assembly.GetEntryAssembly();
		//			_systemSoftware = assembly.GetName().Name + " " + CurrentSystemVersion;
		//		}
		//		return _systemSoftware;
		//	}
		//}
		//public static string CurrentSystemIPAddress
		//{
		//	get
		//	{
		//		if (string.IsNullOrWhiteSpace(_systemIPAddress))
		//			_systemIPAddress = GlobalDiagnosticsContext.Get(SYSTEM_IP_ID) ?? string.Empty;
		//		return _systemIPAddress;
		//	}
		//}


		private const string HOSTNAME_ID = "hostname";

		//private static string _systemName;
		//private static string _systemIPAddress;
		//private static string _systemVersion;
		//private static string _systemSoftware;

		private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
	}
}
