namespace Purity
{
	/// <summary>
	/// Application settings holder
	/// </summary>
	public class Settings
	{
		public Settings()
		{
			DataFilePath = string.Empty;
		}
		public Settings(Settings origin)
		{
			DataFilePath = origin.DataFilePath;
		}
		

		/// <summary>
		/// Absolute path to the data file.<br/>
		/// Optional.
		/// </summary>
		public string DataFilePath { get; set; }
	}
}
