using System.Globalization;


namespace Purity.Avalonia
{
	public class CultureHolder
	{
		private CultureHolder()
		{
			HebrewCalendar = new HebrewCalendar();
			HebrewCulture = CultureInfo.CreateSpecificCulture("he-IL");
			HebrewCulture.DateTimeFormat.Calendar = HebrewCalendar;

			// TDOO: tests -> hebrew day starts aligned with greg day from 00:00, so no relation to evening -> if i want to make next hebrew date, i need to add +1 day if time is 12:00

			////Thread.CurrentThread.CurrentCulture = culture;
			//var tm = DateTime.Now;
			//Trace.WriteLine($"{_hec.GetMonth(tm)}, {_hec.GetDayOfMonth(tm)}");
			//tm = tm.AddHours(12);
			//Trace.WriteLine($"{_hec.GetMonth(tm)}, {_hec.GetDayOfMonth(tm)}");

			////if (tm.Hour != 0)    // TODO: check
			////	tm.AddDays(1);
			//tm = _hec.AddMonths(tm, 1);   // adding full hebrew month
			//Trace.WriteLine(string.Format("{0,12}{1,15:MMM}", _hec.GetMonth(tm), tm));
		}


		public static CultureHolder Instance => _instance ??= new CultureHolder();
		private static CultureHolder? _instance;

		public readonly HebrewCalendar HebrewCalendar;
		public readonly CultureInfo HebrewCulture;
	}
}
