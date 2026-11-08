using CommunityToolkit.Mvvm.ComponentModel;


namespace Purity.Desktop.ViewModels
{
	public sealed partial class SettingsWindowViewModel : ObservableObject
	{
		/// <param name="dataFilePath">The data file in use</param>
		/// <param name="report">Builds the periods lengths report, or returns null when there is nothing to report</param>
		/// <param name="canRestore">Whether this computer has earlier versions of the history to bring back</param>
		public SettingsWindowViewModel(string dataFilePath, Func<string?> report, bool canRestore = false)
		{
			DataFilePath = dataFilePath;
			_report = report;
			CanRestore = canRestore;
		}


		[ObservableProperty]
		public partial string DataFilePath { get; set; }

		public bool CanRestore { get; }

		/// <summary>The person asked to bring back an earlier version, which closes the settings without changing them</summary>
		public bool RestoreRequested { get; private set; }

		public void RequestRestore() => RestoreRequested = true;

		/// <inheritdoc cref="SettingsWindowViewModel(string, Func{string?}, bool)"/>
		public string? BuildReport() => _report();


		private readonly Func<string?> _report;
	}
}
