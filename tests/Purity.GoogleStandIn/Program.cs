using Purity.GoogleStandIn;


// For trying the real window against the stand-in: prints where it's listening and runs until stopped
var port = args.Length == 2 && args[0] == "--port" ? int.Parse(args[1]) : 0;
await using var server = await StandInServer.StartAsync(port);
Console.WriteLine(server.BaseAddress);

var stop = new TaskCompletionSource();
Console.CancelKeyPress += (_, e) =>
{
	e.Cancel = true;
	stop.TrySetResult();
};
await stop.Task;
