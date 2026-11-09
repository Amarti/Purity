using CommunityToolkit.Mvvm.ComponentModel;


namespace Purity.Desktop.ViewModels
{
	public sealed partial class SettingsWindowViewModel : ObservableObject
	{
		/// <param name="dataFilePath">The data file in use</param>
		/// <param name="report">Builds the periods lengths report, or null when there is nothing to report</param>
		/// <param name="canRestore">Whether this computer has earlier versions of the history to bring back</param>
		/// <param name="canMove">Whether the history can move to another folder now</param>
		/// <param name="appearance">Light, dark, or the system's choice, as set now</param>
		public SettingsWindowViewModel(string dataFilePath, Func<string?>? report, bool canRestore = false, bool canMove = false, Appearance appearance = Appearance.System)
		{
			DataFilePath = dataFilePath;
			_report = report;
			CanRestore = canRestore;
			CanMove = canMove;
			AppearanceIndex = (int)appearance;
		}


		/// <summary>The choices for <see cref="Appearance"/>, in the order of its values</summary>
		public IReadOnlyList<string> AppearanceChoices { get; } = new[] { "Same as the system", "Light", "Dark" };

		/// <summary>Which of <see cref="AppearanceChoices"/> is picked</summary>
		[ObservableProperty]
		public partial int AppearanceIndex { get; set; }

		public Appearance Appearance => Enum.IsDefined((Appearance)AppearanceIndex) ? (Appearance)AppearanceIndex : Appearance.System;


		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(CanAccept))]
		public partial string DataFilePath { get; set; }

		/// <summary>OK needs a data file to use</summary>
		public bool CanAccept => !string.IsNullOrWhiteSpace(DataFilePath);

		public bool CanReport => _report != null;

		public bool CanRestore { get; }

		public bool CanMove { get; }

		/// <summary>The person asked to bring back an earlier version, which closes the settings without changing them</summary>
		public bool RestoreRequested { get; private set; }

		public void RequestRestore() => RestoreRequested = true;

		/// <summary>Where the person asked to move the history, which closes the settings without changing them</summary>
		public string? MoveTo { get; private set; }

		public void RequestMove(string path) => MoveTo = path;

		/// <inheritdoc cref="SettingsWindowViewModel(string, Func{string?}?, bool, bool, Appearance)"/>
		public string? BuildReport() => _report?.Invoke();


		private readonly Func<string?>? _report;
	}
}
