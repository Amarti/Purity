using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;


namespace Purity
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
		public PurityEvent(DateTime stamp, PurityEventType type)
		{
			Stamp = stamp;
			Type = type;
		}


		/// <summary>
		/// Returns string that contains spaces placed between capital letters / digits and uncapital letters
		/// </summary>
		/// <param name="input">Input string to transform</param>
		public static string Spacify(string input)
		{
			var res = string.Empty;
			var prevDigit = true;
			var prevLower = false;
			for (var i = 0; i < input.Length; i++)
			{
				var ch = input[i];
				if (char.IsLetterOrDigit(ch))
				{
					if (char.IsLetter(ch))
					{
						if (char.IsLower(ch))
						{
							res += (prevDigit) ? char.ToUpper(ch) : ch;
							prevDigit = false;
							prevLower = true;
						}
						else
						{
							var nextIsLower = (i != input.Length - 1) && (char.IsLower(input[i + 1]));
							if (i != 0 && !prevLower && !prevDigit && nextIsLower)
								res = res.Insert(res.Length, " ") + ch;
							else
								res += (prevLower ? " " : string.Empty) + ch;
							prevDigit = false;
							prevLower = false;
						}
					}
					else
					{
						res += (!prevDigit && prevLower ? " " : string.Empty) + ch;
						prevDigit = true;
						prevLower = false;
					}
				}
				else
					res += ch;
			}

			return res;
		}

		public static bool IsAfterDark(DateTime d) => d.Hour == 12;

		/// <summary>
		/// Stamp contains date and two states for hours:
		/// <para/>00 stands for beginning of light half of calendar day (more aligned with beginning of gregorian calendar day)
		/// <para/>12 stands for beginning of dark half of calendar day (more aligned with beginning of hebrew calendar day)
		/// </summary>
		public DateTime Stamp { get; set; }
		[JsonIgnore]
		public string StampRepr
		{
			get
			{
				switch (Type)
				{
					case PurityEventType.OnaBeinonit:
						return $"{Stamp.Day - 1}-{Stamp:d MMMM yyyy}";
					case PurityEventType.VesetHodesh:
					case PurityEventType.VesetAflaga:
						return $"{Stamp:dd MMMM yyyy} {(IsAfterDark(Stamp) ? "(Night)" : "(Day)")}";
					default:
					case PurityEventType.Mikveh:
						return Stamp.ToString("dd MMMM yyyy");
				}
			}
		}

		public PurityEventType Type { get; set; }
		[JsonIgnore]
		public string TypeRepr => Spacify(Type.ToString());
		//{
		//	get
		//	{
		//		switch (Type)
		//		{
		//			case PurityEventType.Mikveh:
		//				return Type.ToString();
		//			case PurityEventType.OnaBeinonit:

		//		}
		//	}
		//}
	}


	public class PurityPeriod
	{
		public PurityPeriod()
		{
			SubEvents = new List<PurityEvent>();
		}
		public PurityPeriod(DateTime begin, DateTime end)
			: this()
		{
			BeginDate = begin;
			EndDate = end;
		}


		/// <summary>
		/// Calculates length between two periods end and begin in half-calendar days (pure half-days + 1)
		/// </summary>
		/// <param name="a">First period</param>
		/// <param name="b">Second period</param>
		public static int GetFullPeriodLength(PurityPeriod a, PurityPeriod b)
		{
			if (a.BeginDate == DateTime.MinValue || b.BeginDate == DateTime.MinValue)
				return 0;
			var l = (b.BeginDate - a.EndDate).Days * 2;
			if (!PurityEvent.IsAfterDark(a.BeginDate))
				l += 1;
			return l;
		}


		public void ClosePeriod(PurityPeriod lastPeriod, HebrewCalendar hec, List<int> recentPeriodsStreak)
		{
			var mt = AddMikveh();
			AddOnaBeinonit();
			AddVesetHodesh(hec);
			AddVesetAflaga(recentPeriodsStreak, mt);
			SubEvents = SubEvents.OrderBy(el => el.Stamp).ToList();
		}
		public DateTime AddMikveh()
		{
			var ed = EffectiveEnd;
			ed = ed.AddDays(7);		// adding one full week
			ed = ed.AddHours(12);	// mikveh is always after dark
			AddEvent(ed, PurityEventType.Mikveh);

			return ed;
		}
		public void AddOnaBeinonit()
		{
			var tm = BeginDate.AddDays(7 * 4  + 1);				// adding four full weeks + 1 day
			AddEvent(tm, PurityEventType.OnaBeinonit);
		}
		public void AddVesetHodesh(HebrewCalendar hec)
		{
			var b = BeginDate;
			//if (b.Hour != 0)    // hebrew date starts aligned with greg date from 00:00, so no relation to evening -> if i want to make next hebrew date, i need to add +1 day if time is 12:00
			//	b = b.AddDays(1).AddHours(-12);
			var tm = hec.AddMonths(b, 1);	// adding full hebrew month
			AddEvent(tm, PurityEventType.VesetHodesh);
		}
		public void AddVesetAflaga(List<int> recentPeriodsStreak, DateTime mikvehTime)
		{
			foreach (var p in recentPeriodsStreak)
			{
				var tm = EndDate.AddHours(12 * p);			// adding p half-calendar days
				//if (tm > mikvehTime)
					AddEvent(tm, PurityEventType.VesetAflaga);
			}
		}
		private void AddEvent(DateTime tm, PurityEventType typ)
		{
			if (!SubEvents.Any(el => el.Stamp == tm && el.Type == typ))
				SubEvents.Add(new PurityEvent(tm, typ));
		}


		[JsonIgnore]
		public int Length
		{
			get
			{
				if (BeginDate == DateTime.MinValue || EndDate == DateTime.MinValue)
					return 0;
				return (EndDate - BeginDate).Days * 2;
			}
		}

		//public PurityEvent Begin { get; set; }
		public DateTime BeginDate { get; set; }// => Begin.Stamp;
		//public PurityEvent End { get; set; }
		public DateTime EndDate { get; set; }// => End.Stamp;
		/// <summary>
		/// End date corrected to beginning of next hebrew calendar day.
		/// <para/>If verification (bdikah) is made after dark, it is considered next hebrew date, since all bdikah is counted in the light of day
		/// </summary>
		[JsonIgnore]
		public DateTime EffectiveEnd => PurityEvent.IsAfterDark(EndDate) ? EndDate.AddHours(12) : EndDate;
		public List<PurityEvent> SubEvents { get; set; }
	}
}
