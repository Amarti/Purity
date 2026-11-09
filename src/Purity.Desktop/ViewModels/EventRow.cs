using Purity.Core;
using Purity.Desktop.Localization;


namespace Purity.Desktop.ViewModels
{
	/// <summary>One date in a period's list, and how it stands against today (T5)</summary>
	/// <param name="TodayText">The line marking today, on the first date still to come after some that have passed; otherwise null</param>
	public sealed record EventRow(PurityEvent Event, bool IsPast, string? TodayText)
	{
		/// <summary>The kind of date, in the language being shown</summary>
		public string TypeText { get; } = Loc.T.EventType(Event.Type);

		/// <summary>The Gregorian date, in the language being shown</summary>
		public string GregorianText { get; } = Loc.Instance.GregorianText(Event);
	}


	/// <summary>
	/// When a calculated date falls, for what the app shows about today. Only reads the dates the
	/// engine worked out, and decides nothing about them.
	/// </summary>
	internal static class EventTiming
	{
		/// <summary>Whether it's behind us. A night date runs until that next morning, so it counts through that next day.</summary>
		public static bool IsOver(PurityEvent item, DateTime today) => item.Stamp.AddHours(12).UtcDateTime.Date < today;

		/// <summary>The moment it begins. Ona Beinonit is a full day that begins the evening before its date, as its "21-22 Nov" shows.</summary>
		public static DateTimeOffset Begins(PurityEvent item) => item.IsFullDay ? item.Stamp.AddHours(-12) : item.Stamp;

		/// <summary>How far off it is, in words</summary>
		public static string DaysAway(PurityEvent item, DateTime today)
		{
			var days = (Begins(item).UtcDateTime.Date - today).Days;
			return days switch
			{
				<= 0 => Loc.T.Today,
				1 => Loc.T.Tomorrow,
				_ => Loc.T.InDays(days),
			};
		}
	}
}
