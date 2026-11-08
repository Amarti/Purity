// Writes a made-up history for screenshots and side-by-side runs of the apps, so nobody needs the
// real file to see the UI. Every date is invented: periods in 2030 to 2031, a few cycle lengths so
// Veset Haflaga changes, one onset after dusk, one skipped interval, and an open period at the end.
//
//   dotnet run tools/make-sample-data.cs -- <output file>

#:project ../src/Purity.Core/Purity.Core.csproj

using System.Text;
using Purity.Core;

if (args.Length != 1)
{
	Console.Error.WriteLine("Usage: dotnet run tools/make-sample-data.cs -- <output file>");
	return 2;
}

var ledger = new PeriodLedger();
var begin = new DateTime(2030, 1, 6);
var cycles = new (int Days, bool AfterDusk, bool Skip)[]
{
	(0, false, false), (29, false, false), (31, true, false), (27, false, false), (33, false, true),
	(28, false, false), (30, false, false), (26, true, false), (32, false, false),
};
for (var i = 0; i < cycles.Length; i++)
{
	begin = begin.AddDays(cycles[i].Days);
	var period = ledger.AddPeriod(Stamps.FromDate(begin, cycles[i].AfterDusk), Stamps.FromDate(begin.AddDays(5), false))!;
	period.SkipStreak = cycles[i].Skip;
	if (i < cycles.Length - 1)
		ledger.AcceptPeriod(period);		// the last one stays open, as a period in progress does
}

File.WriteAllText(args[0], PurityFile.Format(ledger.Data), new UTF8Encoding(false));
Console.WriteLine($"Wrote {ledger.Data.Count} made-up periods to {args[0]}");
return 0;
