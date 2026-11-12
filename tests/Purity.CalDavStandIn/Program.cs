using Purity.CalDavStandIn;


// For trying the real window against the stand-in: prints where it's listening and the made-up
// sign-in, and runs until stopped. In the Reminders window, choose Other and give these.
var port = args.Length == 2 && args[0] == "--port" ? int.Parse(args[1]) : 0;
await using var server = await CalDavServer.StartAsync(port);
Console.WriteLine(server.BaseAddress);
Console.WriteLine($"User name: {CalDavServer.User}");
Console.WriteLine($"Password: {CalDavServer.Password}");

var stop = new TaskCompletionSource();
Console.CancelKeyPress += (_, e) =>
{
	e.Cancel = true;
	stop.TrySetResult();
};
await stop.Task;
