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
		public PurityEvent(PurityEventType type, DateTime stamp, string? note = null)
		{
			Type = type;
			Stamp = stamp;
			Note = note;
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
		/// This is a temporary solution for bug in Avalonia (v.10) that reverses order of hebrew words in the string<br/>
		/// It also reverses letters of the first word (day, since it always contains " sign, which confuses Avalonia even further)
		/// </summary>
		/// <param name="repr">Input to be reversed</param>
		/// <returns>Input with reversed words order</returns>
		private static string ReverseHebrewWords(string repr)
		{
			var tks = repr.Split('\u0020');	// aka space, just for kicks, really
			tks[0] = new string(tks[0].Reverse().ToArray());
			return string.Join('\u0020', tks.Reverse());
		}

		public static bool IsDateAfterDusk(DateTime d) => d.Hour == 12;


		public PurityEventType Type { get; set; }
		/// <summary>
		/// Stamp contains date and two states for hours:
		/// <para/>00 stands for beginning of light half of calendar day (more aligned with beginning of gregorian calendar day)
		/// <para/>12 stands for beginning of dark half of calendar day (more aligned with beginning of hebrew calendar day)
		/// </summary>
		public DateTime Stamp { get; set; }
		/// <summary>
		/// Information for optional presentation
		/// </summary>
		public string? Note { get; set; }

		[JsonIgnore]
		public string TypeRepr
		{
			get
			{
				switch (Type)
				{
					case PurityEventType.Mikveh:
						return "Mikveh";
					case PurityEventType.VesetHodesh:
						return "Veset Hachodesh";
					case PurityEventType.VesetAflaga:
						return "Veset Aflaga";
					case PurityEventType.OnaBeinonit:
						return "Ona Benonis";
					default:
						return string.Empty;
				}
			}
		}
		[JsonIgnore]
		public bool IsAfterDusk => Type != PurityEventType.OnaBeinonit && IsDateAfterDusk(Stamp);
		[JsonIgnore]
		public bool IsAfterDawn => Type != PurityEventType.OnaBeinonit && !IsDateAfterDusk(Stamp);
		[JsonIgnore]
		public bool IsFullDay => Type == PurityEventType.OnaBeinonit;
		[JsonIgnore]
		public string StampHebRepr
		{
			get
			{
				var es = IsDateAfterDusk(Stamp) ? Stamp.AddDays(1) : Stamp;
				return es.ToString("d MMMM", CultureHolder.Instance.HebrewCulture);
			}
		}
		[JsonIgnore]
		public string StampHebReprAvalonia => ReverseHebrewWords(StampHebRepr);
		[JsonIgnore]
		public string StampGrgRepr
		{
			get
			{
				var res = $"{Stamp:d MMM} {Note}";
				if (Type == PurityEventType.OnaBeinonit)
					res = (Stamp.Day > 1)
							? $"{Stamp.Day - 1}-{res}"
							: $"{Stamp.AddDays(-1):d MMM} - {res}";

				return res;
			}
		}
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
		/// Calculates length between two periods end and begin in half-calendar days (pure half-days)<br/>
		/// Useful for Veset Aflaga calculation
		/// </summary>
		/// <param name="a">First period</param>
		/// <param name="b">Second period</param>
		public static int GetPeriodLength(PurityPeriod a, PurityPeriod b)
		{
			if (a.End == DateTime.MinValue || b.Begin == DateTime.MinValue || b.SkipStreak)	// skipping streak calculation only to upcoming period
				return 0;
			var l = (int)((b.Begin - a.EffectiveEnd).TotalHours / 12) - 1;
			return l;
		}
		/// <summary>
		/// Calculates full period length between two periods begin time in calendar days
		/// </summary>
		/// <param name="a">First period</param>
		/// <param name="b">Second period</param>
		public static float GetFullPeriodLength(PurityPeriod a, PurityPeriod b)
		{
			if (a.End == DateTime.MinValue || b.Begin == DateTime.MinValue)
				return 0;
			var l = (float)((b.Begin - a.Begin).TotalHours / 24) - 1;
			return l;
		}


		public void Commit(HebrewCalendar hec, List<int> recentPeriodsStreak)
		{
			SubEvents.Clear();

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
			ed = ed.AddHours(12);							// mikveh is always after dusk
			AddEvent(ed, PurityEventType.Mikveh);
		}
		private void AddOnaBeinonit()
		{
			// if period was after dusk, this is counted as new hebrew date, so by adding 12 more hours swe increment gregorian day
			var b = PurityEvent.IsDateAfterDusk(Begin) ? Begin.AddHours(12) : Begin;
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
				AddEvent(tm, PurityEventType.VesetAflaga, $"({p})");
			}
		}
		private void AddEvent(DateTime tm, PurityEventType typ, string? note = null)
		{
			if (!SubEvents.Any(el => el.Stamp == tm && el.Type == typ))
				SubEvents.Add(new PurityEvent(typ, tm, note));
		}


		public DateTime Begin { get; set; }
		public DateTime End { get; set; }
		/// <summary>
		/// End date corrected to beginning of next hebrew calendar day.
		/// <para/>If verification (bdikah) is made after dusk, it is considered next hebrew date, since all bdikah is counted in the light of day
		/// </summary>
		[JsonIgnore]
		public DateTime EffectiveEnd => PurityEvent.IsDateAfterDusk(End) ? End.AddHours(12) : End;
		/// <summary>
		/// Period is closed and needs calculation
		/// </summary>
		public bool Closed { get; set; }
		/// <summary>
		/// Omit this period from Veset Aflaga calculation due to some external reasons (surgery, intrauterine device installation, etc)
		/// </summary>
		public bool SkipStreak { get; set; }
		public List<PurityEvent> SubEvents { get; set; }
	}
}
