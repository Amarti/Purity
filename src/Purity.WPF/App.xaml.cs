using System;
using System.Configuration;
using System.Windows;
using NLog;


namespace Purity.WPF
{
	public partial class App : Application
	{
		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

			try
			{
				LogEntry.ConfigureLogging();
				Logger.Info("Initialized logging");
				LogEntry.SetCurrentDirectoryToEntryAssemblyLocation();
				Logger.Info($"Working directory is set to be '{Environment.CurrentDirectory}'");
			}
			catch (Exception ex)
			{
				Logger.Error($"{ex.Message}\n{ex.StackTrace}");
			}

			//SetLocalization();
		}

		//private static void SetLocalization()
		//{
		//	try
		//	{
		//		var filePath = PathFinder.GetLocalizationFilePath();
		//		var l = Configuration.DEFAULT_LANGUAGE;

		//		if (File.Exists(filePath))
		//		{
		//			var t = File.ReadAllText(filePath);
		//			if (!string.IsNullOrWhiteSpace(t))
		//				l = t;
		//		}

		//		var resourceDictionary = new ResourceDictionary();
		//		var languageFile = l + PathFinder.XAML_EXTENSION;
		//		resourceDictionary.Source = new Uri("pack://application:,,,/Languages/" + languageFile);
		//		Current.Resources.MergedDictionaries.Add(resourceDictionary);
		//	}
		//	catch (Exception e)
		//	{
		//		//Logger.Error($"{nameof(SetLocalization)}: {e.Message}\n{e.StackTrace}");
		//	}
		//}

		private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
	}
}
