using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;


namespace Purity
{
	// TODO: remove cycle beg/end, move them to simple dates, since nobody uses them anyway
	public enum PurityEventType
	{
		CycleBegin	= 1,
		CycleEnd	= 2,
		Mikveh		= 3,
		VesetHodesh	= 4,
		VesetAflaga	= 5,
		OnaBeinonit	= 6,
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


		/// <summary>
		/// Stamp contains date and two states for hours:
		/// <para/>00 stands for beginning of light half of calendar day (more aligned with beginning of gregorian calendar day)
		/// <para/>12 stands for beginning of dark half of calendar day (more aligned with beginning of hebrew calendar day)
		/// </summary>
		public DateTime Stamp { get; set; }
		[JsonIgnore]
		public bool IsAfterDark => Stamp.Hour == 12;
		[JsonIgnore]
		public string StampRepr //=> Stamp.ToString("dd MMMM yyyy");
		{
			get
			{
				switch (Type)
				{
					case PurityEventType.OnaBeinonit:
						return $"{Stamp.Day - 1}-{Stamp:d MMMM yyyy}";
					case PurityEventType.VesetHodesh:
					case PurityEventType.VesetAflaga:
						return $"{Stamp:dd MMMM yyyy} {(IsAfterDark ? "(Night)" : "(Day)")}";
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
			Begin = new PurityEvent(begin, PurityEventType.CycleBegin);
			End = new PurityEvent(end, PurityEventType.CycleEnd);
		}


		/// <summary>
		/// Calculates length between two periods end and begin in half-calendar days (pure half-days + 1)
		/// </summary>
		/// <param name="a">First period</param>
		/// <param name="b">Second period</param>
		public static int GetFullPeriodLength(PurityPeriod a, PurityPeriod b)
		{
			if (a.Begin.Stamp == DateTime.MinValue || b.Begin.Stamp == DateTime.MinValue)
				return 0;
			var l = (b.Begin.Stamp - a.End.Stamp).Days * 2;
			if (!a.Begin.IsAfterDark)
				l += 1;
			return l;
		}
		///// <summary>
		///// Calculates length between two periods beginnings in half-24-hours
		///// </summary>
		///// <param name="a">First period</param>
		///// <param name="b">Second period</param>
		//public static int GetFullPeriodLength(PurityPeriod a, PurityPeriod b)
		//{
		//	if (a.Begin.Stamp == DateTime.MinValue || b.Begin.Stamp == DateTime.MinValue)
		//		return 0;
		//	return Math.Abs((a.Begin.Stamp - b.Begin.Stamp).Days) * 2;
		//}

		public void ClosePeriod(PurityPeriod lastPeriod, HebrewCalendar hec, List<int> recentPeriodsStreak)
		{
			var mt = AddMikveh();
			AddOnaBeinonit();
			//AddVesetHodesh(lastPeriod, hec);
			AddVesetHodesh(hec);
			AddVesetAflaga(recentPeriodsStreak, mt);
			SubEvents = SubEvents.OrderBy(el => el.Stamp).ToList();
		}
		public DateTime AddMikveh()
		{
			var ed = EffectiveEnd;// End.IsAfterDark ? End.Stamp.AddHours(12) : End.Stamp;		// if bdikah is made after dark, it is considered next hebrew date, since all bdikah is counted in the light of day
			ed = ed.AddDays(7);		// adding one full week
			ed = ed.AddHours(12);	// mikveh is always after dark
			AddEvent(ed, PurityEventType.Mikveh);

			return ed;
			//var tm = End.Stamp.AddDays(7);		// adding one full week
			//if (End.IsAfterDark)				// if bdikah is made after dark, it is considered next hebrew date, since all bdikah is counted in the light of day
			//	tm = tm.AddDays(1);
			//AddEvent(tm, PurityEventType.Mikveh);
		}
		public void AddOnaBeinonit()
		{
			var tm = Begin.Stamp.AddDays(7 * 4  + 1);			// adding four full weeks + 1 day
			AddEvent(tm, PurityEventType.OnaBeinonit);
		}
		public void AddVesetHodesh(HebrewCalendar hec)
		{
			var b = Begin.Stamp;
			//if (b.Hour != 0)    // hebrew date starts aligned with greg date from 00:00, so no relation to evening -> if i want to make next hebrew date, i need to add +1 day if time is 12:00
			//	b = b.AddDays(1).AddHours(-12);
			var tm = hec.AddMonths(b, 1);	// adding full hebrew month
			AddEvent(tm, PurityEventType.VesetHodesh);
		}
		//public void AddVesetHodesh(PurityPeriod prevPeriod, HebrewCalendar hec)
		//{
		//	if (prevPeriod == null)
		//		return;

		//	var b = prevPeriod.Begin.Stamp;
		//	//if (b.Hour != 0)    // hebrew date starts aligned with greg date from 00:00, so no relation to evening -> if i want to make next hebrew date, i need to add +1 day if time is 12:00
		//	//	b = b.AddDays(1).AddHours(-12);
		//	var tm = hec.AddMonths(b, 1);	// adding full hebrew month
		//	prevPeriod.AddEvent(tm, PurityEventType.VesetHodesh);
		//}
		public void AddVesetAflaga(List<int> recentPeriodsStreak, DateTime mikvehTime)
		{
			foreach (var p in recentPeriodsStreak)
			{
				var tm = Begin.Stamp.AddHours(12 * p);			// adding p half-calendar days
				if (tm > mikvehTime)
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
				if (Begin.Stamp == DateTime.MinValue || End.Stamp == DateTime.MinValue)
					return 0;
				return (End.Stamp - Begin.Stamp).Days * 2;
			}
		}

		public PurityEvent Begin { get; set; }
		public PurityEvent End { get; set; }
		/// <summary>
		/// End date corrected to beginning of next hebrew calendar day.
		/// <para/>If verification (bdikah) is made after dark, it is considered next hebrew date, since all bdikah is counted in the light of day
		/// </summary>
		[JsonIgnore]
		public DateTime EffectiveEnd => End.IsAfterDark ? End.Stamp.AddHours(12) : End.Stamp;
		public List<PurityEvent> SubEvents { get; set; }
	}
}
