using System.Globalization;
using System.Text.Json.Serialization;
using Purity.Core;
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


	/// <summary>Where reminders go</summary>
	public enum ReminderPlace
	{
		File,
		Google,
		ICloud,

		/// <summary>Another CalDAV server, given by its address</summary>
		CalDav,
	}


	/// <summary>
	/// How reminders are set up on this device. Kept in the settings, not in the data file, so
	/// each device has its own. Text is read leniently, as the rest of the settings are.
	/// </summary>
	public sealed class ReminderSetup
	{
		public const string DefaultMikvehTime = "08:00";
		public const string DefaultOtherTime = "17:00";
		public const string DefaultCalendarName = "Purity";


		[JsonPropertyName("Where")]
		public string WhereText { get; set; } = nameof(ReminderPlace.File);

		[JsonIgnore]
		public ReminderPlace Where
		{
			get => Enum.TryParse<ReminderPlace>(WhereText, out var value) && Enum.IsDefined(value) ? value : ReminderPlace.File;
			set => WhereText = value.ToString();
		}

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

		/// <summary>The calendar's name, or empty for the usual one</summary>
		public string CalendarName { get; set; } = string.Empty;


		[JsonPropertyName("Language")]
		public string? LanguageText { get; set; }

		/// <summary>The language of the reminders, once a computer has set one. Before that, the one on show.</summary>
		[JsonIgnore]
		public ReminderLanguage? Language
		{
			get => Enum.TryParse<ReminderLanguage>(LanguageText, out var value) && Enum.IsDefined(value) ? value : null;
			set => LanguageText = value?.ToString();
		}

		/// <summary>Whether the person changed the choices here and the calendar hasn't got them yet</summary>
		public bool ChoicesUnshared { get; set; }


		/// <summary>These choices as the planner takes them</summary>
		public ReminderOptions ToOptions(ReminderLanguage language) => new()
		{
			Kinds = ChosenKinds(),
			Language = Language ?? language,
			Wording = Wording,
			MikvehTime = TimeOf(MikvehTime, DefaultMikvehTime),
			OtherTime = TimeOf(OtherTime, DefaultOtherTime),
			MoveOffShabbat = MoveOffShabbat,
		};


		/// <summary>These choices as every computer shares them</summary>
		/// <param name="language">The language on show, when no computer has set one yet</param>
		public SharedChoices ToShared(ReminderLanguage language)
		{
			var options = ToOptions(language);
			return new SharedChoices(options.Kinds, options.Wording, options.MikvehTime, options.OtherTime, options.MoveOffShabbat, options.Language);
		}

		/// <summary>Takes up the choices the calendar holds. Where reminders go and the calendar's name stay this computer's own.</summary>
		public void Adopt(SharedChoices shared)
		{
			var every = Enum.GetValues<PurityEventType>().Length;
			Scope = shared.Kinds.Count == every ? ReminderScope.AllDates
				: shared.Kinds.Count == 1 && shared.Kinds.Contains(PurityEventType.Mikveh) ? ReminderScope.MikvehOnly
				: ReminderScope.Chosen;
			Mikveh = shared.Kinds.Contains(PurityEventType.Mikveh);
			VesetHachodesh = shared.Kinds.Contains(PurityEventType.VesetHodesh);
			VesetAflaga = shared.Kinds.Contains(PurityEventType.VesetAflaga);
			OnaBenonis = shared.Kinds.Contains(PurityEventType.OnaBeinonit);
			Wording = shared.Wording;
			MikvehTime = shared.MikvehTime.ToString("HH:mm", CultureInfo.InvariantCulture);
			OtherTime = shared.OtherTime.ToString("HH:mm", CultureInfo.InvariantCulture);
			MoveOffShabbat = shared.MoveOffShabbat;
			Language = shared.Language;
		}


		private HashSet<PurityEventType> ChosenKinds()
		{
			if (Scope == ReminderScope.MikvehOnly)
				return new HashSet<PurityEventType> { PurityEventType.Mikveh };
			if (Scope == ReminderScope.AllDates)
				return Enum.GetValues<PurityEventType>().ToHashSet();

			var kinds = new HashSet<PurityEventType>();
			if (Mikveh)
				kinds.Add(PurityEventType.Mikveh);
			if (VesetHachodesh)
				kinds.Add(PurityEventType.VesetHodesh);
			if (VesetAflaga)
				kinds.Add(PurityEventType.VesetAflaga);
			if (OnaBenonis)
				kinds.Add(PurityEventType.OnaBeinonit);
			return kinds;
		}

		private static TimeOnly TimeOf(string text, string usual) =>
			TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : TimeOnly.ParseExact(usual, "HH:mm", CultureInfo.InvariantCulture);
	}
}
