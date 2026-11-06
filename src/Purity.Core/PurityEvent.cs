using System;
using System.Text.Json.Serialization;


namespace Purity.Core
{
	public enum PurityEventType
	{
		Mikveh		= 1,
		VesetHodesh	= 2,
		VesetAflaga	= 3,
		OnaBeinonit	= 4,
	}


	public class PurityEvent
	{
		public PurityEvent(PurityEventType type, DateTimeOffset stamp, string? note = null)
		{
			Type = type;
			Stamp = stamp;
			Note = note;
		}


		public static bool IsDateAfterDusk(DateTimeOffset d) => d.Hour == 12;


		public PurityEventType Type { get; set; }
		/// <summary>
		/// Stamp contains date and two states for hours:
		/// <para/>00 stands for beginning of light half of calendar day (more aligned with beginning of gregorian calendar day)
		/// <para/>12 stands for beginning of dark half of calendar day (more aligned with beginning of hebrew calendar day)
		/// </summary>
		public DateTimeOffset Stamp { get; set; }
		/// <summary>
		/// Information for optional presentation
		/// </summary>
		public string? Note { get; set; }

		[JsonIgnore]
		public string TypeRepr => DateText.EventType(Type);
		[JsonIgnore]
		public bool IsAfterDusk => Type != PurityEventType.OnaBeinonit && IsDateAfterDusk(Stamp);
		[JsonIgnore]
		public bool IsAfterDawn => Type != PurityEventType.OnaBeinonit && !IsDateAfterDusk(Stamp);
		[JsonIgnore]
		public bool IsFullDay => Type == PurityEventType.OnaBeinonit;
		[JsonIgnore]
		public string StampHebRepr => DateText.Hebrew(IsDateAfterDusk(Stamp) ? Stamp.AddDays(1) : Stamp);
		[JsonIgnore]
		public string StampHebReprAvalonia => DateText.ReversedForOldAvalonia(StampHebRepr);
		[JsonIgnore]
		public string StampGrgRepr
		{
			get
			{
				var res = $"{DateText.Gregorian(Stamp)} {Note}";
				if (Type == PurityEventType.OnaBeinonit)
					res = (Stamp.Day > 1)
							? DateText.Invariant($"{Stamp.Day - 1}-{res}")
							: $"{DateText.Gregorian(Stamp.AddDays(-1))} - {res}";

				return res;
			}
		}
	}
}
