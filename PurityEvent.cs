using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;

namespace Purity
{
	public enum PurityEventType
	{
		CycleBegin		= 1,
		CycleEnd		= 2,
		Mikveh			= 3,
		VesetHodesh		= 4,
		VesetAflaga		= 5,
		TkufaBeinonit	= 6,
	}


	public class PurityEvent
	{
		public PurityEvent(DateTime stamp, PurityEventType type)
		{
			Stamp = stamp;
			Type = type;
		}

		/// <summary>
		/// Returns string that contains spaces placed between capital letters / digits and uncapital letters.
		/// </summary>
		/// <param name="input">Input string to transform.</param>
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


		public DateTime Stamp { get; set; }
		[JsonIgnore]
		public string StampRepr => Stamp.ToString("dd/MM/yyyy");
		public PurityEventType Type { get; set; }
		[JsonIgnore]
		public string TypeRepr => Spacify(Type.ToString());
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
		/// Calculates length between two periods beginnings in half-24-hours
		/// </summary>
		/// <param name="a">First period</param>
		/// <param name="b">Second period</param>
		public static int GetFullPeriodLength(PurityPeriod a, PurityPeriod b)
		{
			if (a.Begin.Stamp == DateTime.MinValue || b.Begin.Stamp == DateTime.MinValue)
				return 0;
			return (a.Begin.Stamp - b.Begin.Stamp).Days * 2;
		}

		public void ClosePeriod(PurityPeriod lastPeriod, HebrewCalendar hec, List<int> recentPeriodsStreak)
		{
			AddMikveh();
			AddVesetHodesh(lastPeriod, hec);
			AddVesetAflaga(recentPeriodsStreak);
			AddTkufaBeinonit();
			SubEvents = SubEvents.OrderBy(el => el.Stamp).ToList();
		}
		public void AddMikveh()
		{
			var tm = End.Stamp.AddDays(7);						// adding one full week
			AddEvent(tm, PurityEventType.Mikveh);
		}
		public void AddVesetHodesh(PurityPeriod prevPeriod, HebrewCalendar hec)
		{
			if (prevPeriod == null)
				return;

			var b = prevPeriod.Begin.Stamp;
			//if (b.Hour != 0)    // hebrew date starts aligned with greg date from 00:00, so no relation to evening -> if i want to make next hebrew date, i need to add +1 day if time is 12:00
			//	b = b.AddDays(1).AddHours(-12);
			var tm = hec.AddMonths(b, 1);	// adding full hebrew month
			AddEvent(tm, PurityEventType.VesetHodesh);
		}
		public void AddVesetAflaga(List<int> recentPeriodsStreak)
		{
			foreach (var p in recentPeriodsStreak)
			{
				var tm = Begin.Stamp.AddHours(12 * p);			// adding p half-24-hours
				AddEvent(tm, PurityEventType.VesetAflaga);
			}
		}
		public void AddTkufaBeinonit()
		{
			var tm = Begin.Stamp.AddDays(7 * 4);				// adding four full weeks
			AddEvent(tm, PurityEventType.TkufaBeinonit);
		}
		private void AddEvent(DateTime tm, PurityEventType typ)
		{
			if (!SubEvents.Any(el => el.Stamp == tm && el.Type == typ))
				SubEvents.Add(new PurityEvent(tm, typ));
		}


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
		public List<PurityEvent> SubEvents { get; set; }
	}
}
