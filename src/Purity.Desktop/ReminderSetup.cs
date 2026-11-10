using System.Text.Json.Serialization;
using Purity.Reminders;


namespace Purity.Desktop
{
	/// <summary>Which dates get reminders</summary>
	public enum ReminderScope
	{
		MikvehOnly,
		AllDates,
		Chosen,
	}


	/// <summary>
	/// How reminders are set up on this device. Kept in the settings, not in the data file, so
	/// each device has its own. Text is read leniently, as the rest of the settings are.
	/// </summary>
	public sealed class ReminderSetup
	{
		public const string DefaultMikvehTime = "08:00";
		public const string DefaultOtherTime = "17:00";


		[JsonPropertyName("Scope")]
		public string ScopeText { get; set; } = nameof(ReminderScope.MikvehOnly);

		[JsonIgnore]
		public ReminderScope Scope
		{
			get => Enum.TryParse<ReminderScope>(ScopeText, out var value) && Enum.IsDefined(value) ? value : ReminderScope.MikvehOnly;
			set => ScopeText = value.ToString();
		}

		/// <summary>Which dates are picked when the scope is <see cref="ReminderScope.Chosen"/></summary>
		public bool Mikveh { get; set; } = true;
		public bool VesetHachodesh { get; set; }
		public bool VesetAflaga { get; set; }
		public bool OnaBenonis { get; set; }

		[JsonPropertyName("Wording")]
		public string WordingText { get; set; } = nameof(ReminderWording.Discreet);

		[JsonIgnore]
		public ReminderWording Wording
		{
			get => Enum.TryParse<ReminderWording>(WordingText, out var value) && Enum.IsDefined(value) ? value : ReminderWording.Discreet;
			set => WordingText = value.ToString();
		}

		public string MikvehTime { get; set; } = DefaultMikvehTime;
		public string OtherTime { get; set; } = DefaultOtherTime;
		public bool MoveOffShabbat { get; set; } = true;

		/// <summary>The calendar's name, or empty for the usual one in the language being shown</summary>
		public string CalendarName { get; set; } = string.Empty;
	}
}
