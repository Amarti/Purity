using CommunityToolkit.Mvvm.ComponentModel;


namespace Purity.Desktop.ViewModels
{
	public sealed partial class SettingsWindowViewModel : ObservableObject
	{
		/// <param name="dataFilePath">The data file in use</param>
		/// <param name="report">Builds the periods lengths report, or returns null when there is nothing to report</param>
		public SettingsWindowViewModel(string dataFilePath, Func<string?> report)
		{
			DataFilePath = dataFilePath;
			_report = report;
		}


		[ObservableProperty]
		public partial string DataFilePath { get; set; }

		/// <inheritdoc cref="SettingsWindowViewModel(string, Func{string?})"/>
		public string? BuildReport() => _report();


		private readonly Func<string?> _report;
	}
}
