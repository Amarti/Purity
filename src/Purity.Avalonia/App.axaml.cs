using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MessageBox.Avalonia;
using NLog;
using Purity.Avalonia.ViewModels;
using Purity.Avalonia.Views;


namespace Purity.Avalonia
{
	public class App : Application
	{
		public override void Initialize()
		{
			AvaloniaXamlLoader.Load(this);
		}

		public override void OnFrameworkInitializationCompleted()
		{
			try
			{
				LogEntry.ConfigureLogging();
				Logger.Info("Initialized logging");
				LogEntry.SetCurrentDirectoryToEntryAssemblyLocation();
				Logger.Info($"Working directory is set to be '{Environment.CurrentDirectory}'");
			}
			catch (Exception e)
			{
				Logger.Error($"{e.Message}\n{e.StackTrace}");
			}

			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
				var vm = new MainWindowViewModel();
				var rawData = DataSerializer.Deserialize();
				vm.InitData(rawData);

				desktop.MainWindow = new MainWindow
				{
					DataContext = vm,
				};

				//var curDir = Environment.CurrentDirectory;//LogEntry.GetCurrentPath();
				//var mb = MessageBoxManager.GetMessageBoxStandardWindow(LogEntry.GetProductName(), $"current path is\n'{curDir}'");
				//mb.Show();
				Logger.Info("Started");
			}

			base.OnFrameworkInitializationCompleted();
		}


		private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
	}
}
