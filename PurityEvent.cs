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
						return $"{Stamp:d MMMM yyyy} {(IsAfterDark(Stamp) ? "(Night)" : "(Day)")}";
					default:
					case PurityEventType.Mikveh:
						return Stamp.ToString("d MMMM yyyy");
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
			Begin = begin;
			End = end;
		}


		/// <summary>
		/// Calculates length between two periods end and begin in half-calendar days (pure half-days)
		/// </summary>
		/// <param name="a">First period</param>
		/// <param name="b">Second period</param>
		public static int GetFullPeriodLength(PurityPeriod a, PurityPeriod b)
		{
			if (a.End == DateTime.MinValue || b.Begin == DateTime.MinValue || b.SkipStreak)	// skipping streak calculation only to upcoming period
				return 0;
			var l = (int)((b.Begin - a.EffectiveEnd).TotalHours / 12) - 1;
			//if (!PurityEvent.IsAfterDark(a.Begin))
			//	l += 1;
			return l;
		}


		public void Commit(HebrewCalendar hec, List<int> recentPeriodsStreak)
		{
			SubEvents.Clear();
			//if (SubEvents.Count > 0 && recentPeriodsStreak.Count > 0)
			//{
			//	recentPeriodsStreak.RemoveAt(recentPeriodsStreak.Count - 1);
			//	SubEvents.Clear();
			//}

			AddMikveh();
			AddOnaBeinonit();
			AddVesetHodesh(hec);
			AddVesetAflaga(recentPeriodsStreak);
			SubEvents = SubEvents.OrderBy(el => el.Stamp).ToList();
		}
		private void AddMikveh()
		{
			var ed = EffectiveEnd;
			ed = ed.AddDays(7);								// adding one full week
			ed = ed.AddHours(12);							// mikveh is always after dark
			AddEvent(ed, PurityEventType.Mikveh);
		}
		private void AddOnaBeinonit()
		{
			// if period was after dark, this is counted as new hebrew date, so by adding 12 more hours swe increment gregorian day
			var b = PurityEvent.IsAfterDark(Begin) ? Begin.AddHours(12) : Begin;
			var tm = b.AddDays(7 * 4  + 1);					// adding four full weeks + 1 day
			AddEvent(tm, PurityEventType.OnaBeinonit);
		}
		private void AddVesetHodesh(HebrewCalendar hec)
		{
			var tm = hec.AddMonths(Begin, 1);				// adding full hebrew month
			AddEvent(tm, PurityEventType.VesetHodesh);
		}
		private void AddVesetAflaga(List<int> recentPeriodsStreak)
		{
			foreach (var p in recentPeriodsStreak)
			{
				var tm = End.AddHours(12 * p);				// adding p half-calendar days
				AddEvent(tm, PurityEventType.VesetAflaga);
			}
		}
		private void AddEvent(DateTime tm, PurityEventType typ)
		{
			if (!SubEvents.Any(el => el.Stamp == tm && el.Type == typ))
				SubEvents.Add(new PurityEvent(tm, typ));
		}


		public DateTime Begin { get; set; }
		public DateTime End { get; set; }
		/// <summary>
		/// End date corrected to beginning of next hebrew calendar day.
		/// <para/>If verification (bdikah) is made after dark, it is considered next hebrew date, since all bdikah is counted in the light of day
		/// </summary>
		[JsonIgnore]
		public DateTime EffectiveEnd => PurityEvent.IsAfterDark(End) ? End.AddHours(12) : End;
		/// <summary>
		/// Period is closed and needs calculation
		/// </summary>
		public bool Closed { get; set; }
		/// <summary>
		/// Skip adding period into periods streak in some rare cases (surgery, intrauterine device installation, etc)
		/// </summary>
		public bool SkipStreak { get; set; }
		public List<PurityEvent> SubEvents { get; set; }
	}
}
