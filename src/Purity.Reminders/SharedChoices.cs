using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Purity.Core;


namespace Purity.Reminders
{
	/// <summary>
	/// The choices every device shares, kept with the calendar itself so that they travel with it.
	/// Written as a short code with no words in it, since a calendar's description can be seen.
	/// </summary>
	public sealed record SharedChoices(IReadOnlySet<PurityEventType> Kinds, ReminderWording Wording, TimeOnly MikvehTime, TimeOnly OtherTime, bool MoveOffShabbat, ReminderLanguage Language)
	{
		/// <summary>The code, as it follows the tag in the calendar's description</summary>
		public string Format() => string.Join(' ',
			Kinds.Count == 0 ? NoKinds : string.Concat(Kinds.Select(el => (int)el).Order()),
			Wording == ReminderWording.Discreet ? Discreet : Detailed,
			MikvehTime.ToString(TimeFormat, CultureInfo.InvariantCulture),
			OtherTime.ToString(TimeFormat, CultureInfo.InvariantCulture),
			MoveOffShabbat ? "1" : "0",
			Language == ReminderLanguage.Hebrew ? "he" : "en");

		/// <summary>Reads the choices from a calendar's description, or gives null when there are none it can understand</summary>
		public static SharedChoices? Parse(string? description)
		{
			var parts = description?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (parts is not { Length: >= PartCount } || ReminderTag.FromNotes(parts[0]) != ReminderTag.Calendar)
				return null;

			var kinds = new HashSet<PurityEventType>();
			if (parts[1] != NoKinds)
				foreach (var digit in parts[1])
				{
					if (digit is < '0' or > '9' || !Enum.IsDefined((PurityEventType)(digit - '0')))
						return null;
					kinds.Add((PurityEventType)(digit - '0'));
				}

			if (parts[2] is not (Discreet or Detailed) || parts[5] is not ("1" or "0") || parts[6] is not ("en" or "he"))
				return null;
			if (!TimeOnly.TryParseExact(parts[3], TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var mikveh))
				return null;
			if (!TimeOnly.TryParseExact(parts[4], TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var other))
				return null;

			return new SharedChoices(kinds, parts[2] == Discreet ? ReminderWording.Discreet : ReminderWording.Detailed, mikveh, other, parts[5] == "1", parts[6] == "he" ? ReminderLanguage.Hebrew : ReminderLanguage.English);
		}


		private const string TimeFormat = "HHmm";
		private const string NoKinds = "-";
		private const string Discreet = "d";
		private const string Detailed = "f";
		private const int PartCount = 7;					// the tag and six choices; anything a later version adds comes after
	}


	/// <summary>Where a calendar service keeps the choices every device shares</summary>
	public interface IChoiceStore
	{
		/// <returns>The choices, or null when the calendar doesn't hold any it can understand</returns>
		Task<SharedChoices?> ReadChoicesAsync(CancellationToken cancellation);

		Task WriteChoicesAsync(SharedChoices choices, CancellationToken cancellation);
	}
}
