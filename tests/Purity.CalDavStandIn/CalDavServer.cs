using System.Net;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;


namespace Purity.CalDavStandIn
{
	/// <summary>A failure to give the next matching call</summary>
	/// <param name="Status">The HTTP status</param>
	/// <param name="Method">Only calls of this method, or any</param>
	/// <param name="PathContains">Only calls whose path holds this, or any</param>
	public sealed record Fault(int Status, string? Method = null, string? PathContains = null);


	/// <summary>One calendar, as it stands</summary>
	/// <param name="Path">Its address on the server</param>
	/// <param name="Name">The name it shows</param>
	/// <param name="Description">Its description, or null</param>
	/// <param name="Events">The text of each event, by its address</param>
	public sealed record StandInCalendar(string Path, string Name, string? Description, IReadOnlyDictionary<string, string> Events);


	/// <summary>One call that arrived</summary>
	/// <param name="Method">Such as PROPFIND</param>
	/// <param name="Path">Its address on the server</param>
	/// <param name="Host">The name it was sent to, such as 127.0.0.1 or localhost</param>
	/// <param name="Signed">Whether it carried a user name and password</param>
	public sealed record StandInCall(string Method, string Path, string Host, bool Signed);


	/// <summary>
	/// A CalDAV server on this computer for one made-up person, as iCloud is laid out: the principal
	/// names a calendar home by its full address, and each calendar is a folder of event files. Only
	/// the calls Purity makes are here, and they're strict: a missing Depth, an event without a UID
	/// or a second event with the same UID is refused, as RFC 4791 lets a server refuse it.
	/// </summary>
	public sealed class CalDavServer : IAsyncDisposable
	{
		public const string User = "someone@example.com";
		public const string Password = "abcd-efgh-ijkl-mnop";
		public const string Principal = "/dav/principals/someone/";
		public const string Home = "/dav/calendars/someone/";


		public static async Task<CalDavServer> StartAsync(int port = 0)
		{
			var builder = WebApplication.CreateSlimBuilder();
			builder.Logging.ClearProviders();
			builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, port));
			var app = builder.Build();
			var server = new CalDavServer(app);
			app.Run(server.HandleAsync);
			await app.StartAsync();
			var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
			server.BaseAddress = new Uri(address.TrimEnd('/') + "/");
			return server;
		}

		private CalDavServer(WebApplication app) => _app = app;


		public Uri BaseAddress { get; private set; } = null!;

		/// <summary>Whether the address itself answers, or only its well-known one, as some servers do</summary>
		public bool OnlyWellKnown { get; set; }

		/// <summary>Where the address itself sends every call, or null to answer them</summary>
		public Uri? RedirectsTo { get; set; }

		/// <summary>Whether a calendar's description is kept, as some servers don't</summary>
		public bool KeepsDescriptions { get; set; } = true;

		/// <summary>Whether an address that was used once and deleted can be used again. iCloud's can't.</summary>
		public bool ReusesAddresses { get; set; } = true;

		/// <summary>Failures for the next matching calls, one each</summary>
		public List<Fault> Faults { get { lock (_lock) return _faults; } }

		public IReadOnlyList<StandInCall> Calls { get { lock (_lock) return _calls.ToArray(); } }

		public IReadOnlyList<StandInCalendar> Calendars
		{
			get
			{
				lock (_lock)
					return _calendars.Select(el => new StandInCalendar(el.Key, el.Value.Name, el.Value.Description, new Dictionary<string, string>(el.Value.Events))).ToArray();
			}
		}


		/// <summary>A calendar of the person's own, which Purity must never touch</summary>
		public void AddOwnCalendar(string name, string description, params (string File, string Text)[] events)
		{
			lock (_lock)
			{
				var calendar = new Calendar { Name = name, Description = description };
				foreach (var (file, text) in events)
					calendar.Events[Home + name + "/" + file] = text;
				_calendars[Home + name + "/"] = calendar;
			}
		}

		/// <summary>As when the person deletes a calendar in their calendar app</summary>
		public void DeleteCalendar(string path)
		{
			lock (_lock)
				_calendars.Remove(path);
		}

		/// <summary>As when an event is changed in a calendar app</summary>
		public void SetEvent(string path, string text)
		{
			lock (_lock)
				_calendars[path[..(path.LastIndexOf('/') + 1)]].Events[path] = text;
		}

		public async ValueTask DisposeAsync()
		{
			await _app.StopAsync();
			await _app.DisposeAsync();
		}


		private async Task HandleAsync(HttpContext context)
		{
			var request = context.Request;
			var path = Uri.UnescapeDataString(request.Path.Value ?? "/");
			var signed = IsSigned(request);
			Fault? fault;
			lock (_lock)
			{
				_calls.Add(new StandInCall(request.Method, path, request.Host.Host, signed));
				fault = _faults.FirstOrDefault(el => (el.Method == null || el.Method == request.Method) && (el.PathContains == null || path.Contains(el.PathContains, StringComparison.Ordinal)));
				if (fault != null)
					_faults.Remove(fault);
			}

			if (fault != null)
			{
				context.Response.StatusCode = fault.Status;
				return;
			}

			if (path == "/.well-known/caldav")
			{
				Redirect(context, "/dav/");
				return;
			}

			if (path == "/" && RedirectsTo is { } elsewhere)
			{
				Redirect(context, elsewhere.AbsoluteUri);
				return;
			}

			if (!signed)
			{
				context.Response.StatusCode = 401;
				context.Response.Headers.WWWAuthenticate = "Basic realm=\"Stand-in\"";
				return;
			}

			var body = await new StreamReader(request.Body, Encoding.UTF8).ReadToEndAsync();
			var (status, answer, type) = Answer(request, path, body);
			context.Response.StatusCode = status;
			if (answer != null)
			{
				context.Response.ContentType = type;
				await context.Response.WriteAsync(answer);
			}
		}

		private (int Status, string? Body, string Type) Answer(HttpRequest request, string path, string body)
		{
			lock (_lock)
			{
				var depth = request.Headers["Depth"].ToString();
				switch (request.Method)
				{
					case "PROPFIND":
						if (depth is not ("0" or "1"))
							return (400, null, Xml);
						return PropFind(path, depth, body);

					case "REPORT":
						if (depth != "1" || !_calendars.TryGetValue(path, out var queried))
							return (depth != "1" ? 400 : 404, null, Xml);
						return (207, MultiStatus(queried.Events.Select(el => Resource(el.Key, (Dav + "getetag", Etag(el.Value)), (CalDav + "calendar-data", el.Value)))), Xml);

					case "MKCALENDAR":
						return MakeCalendar(path, body);

					case "PROPPATCH":
						if (!_calendars.TryGetValue(path, out var patched))
							return (404, null, Xml);
						if (!KeepsDescriptions)
							return (403, null, Xml);
						var description = XDocument.Parse(body).Descendants(CalDav + "calendar-description").FirstOrDefault();
						if (description == null)
							return (400, null, Xml);
						patched.Description = description.Value;
						return (207, MultiStatus(new[] { Resource(path, (CalDav + "calendar-description", null)) }), Xml);

					case "PUT":
						return Put(request, path, body);

					case "DELETE":
						if (_calendars.Remove(path))
							return (204, null, Xml);
						var holder = _calendars.Values.FirstOrDefault(el => el.Events.ContainsKey(path));
						return holder != null && holder.Events.Remove(path) ? (204, null, Xml) : (404, null, Xml);

					case "GET":
						var file = _calendars.Values.SelectMany(el => el.Events).FirstOrDefault(el => el.Key == path);
						return file.Key != null ? (200, file.Value, "text/calendar") : (404, null, Xml);

					default:
						return (405, null, Xml);
				}
			}
		}

		private (int, string?, string) PropFind(string path, string depth, string body)
		{
			var asked = XDocument.Parse(body).Descendants(Dav + "prop").Elements().Select(el => el.Name).ToArray();
			if (path is "/" or "/dav/" && !(OnlyWellKnown && path == "/"))
				return (207, MultiStatus(new[] { Resource(path, asked, name => name == Dav + "current-user-principal" ? new XElement(Dav + "href", Principal) : null) }), Xml);
			if (path == Principal)
				return (207, MultiStatus(new[] { Resource(path, asked, name => name == CalDav + "calendar-home-set" ? new XElement(Dav + "href", new Uri(BaseAddress, Home).AbsoluteUri) : null) }), Xml);
			if (path == Home)
			{
				var items = new List<XElement> { Resource(path, asked, name => name == Dav + "resourcetype" ? new XElement(Dav + "collection") : null) };
				if (depth == "1")
					items.AddRange(_calendars.Select(el => CalendarResource(el.Key, el.Value, asked)));
				return (207, MultiStatus(items), Xml);
			}
			if (_calendars.TryGetValue(path, out var calendar))
				return (207, MultiStatus(new[] { CalendarResource(path, calendar, asked) }), Xml);
			return (404, null, Xml);
		}

		private (int, string?, string) MakeCalendar(string path, string body)
		{
			if (!path.StartsWith(Home, StringComparison.Ordinal) || !path.EndsWith('/') || path[Home.Length..^1].Contains('/'))
				return (403, null, Xml);
			if (_calendars.ContainsKey(path) || (!ReusesAddresses && _used.Contains(path)))
				return (405, null, Xml);

			var request = XDocument.Parse(body);
			var components = request.Descendants(CalDav + "comp").Select(el => (string?)el.Attribute("name")).ToArray();
			if (components.Length > 0 && !components.Contains("VEVENT"))
				return (403, null, Xml);

			_calendars[path] = new Calendar
			{
				Name = request.Descendants(Dav + "displayname").FirstOrDefault()?.Value ?? string.Empty,
				Description = KeepsDescriptions ? request.Descendants(CalDav + "calendar-description").FirstOrDefault()?.Value : null,
			};
			_used.Add(path);
			return (201, null, Xml);
		}

		private (int, string?, string) Put(HttpRequest request, string path, string body)
		{
			var folder = path[..(path.LastIndexOf('/') + 1)];
			if (!_calendars.TryGetValue(folder, out var calendar))
				return (409, null, Xml);
			if (request.ContentType?.StartsWith("text/calendar", StringComparison.OrdinalIgnoreCase) != true)
				return (415, null, Xml);

			var exists = calendar.Events.ContainsKey(path);
			if (exists && request.Headers.IfNoneMatch.ToString() == "*")
				return (412, null, Xml);

			// One calendar object per UID, and nothing that isn't an event with one
			var lines = body.Replace("\r\n ", string.Empty).Split("\r\n");
			if (lines.FirstOrDefault() != "BEGIN:VCALENDAR" || !lines.Contains("BEGIN:VEVENT") || !lines.Any(el => el.StartsWith("DTSTART", StringComparison.Ordinal)))
				return (400, null, Xml);
			var uid = lines.FirstOrDefault(el => el.StartsWith("UID:", StringComparison.Ordinal))?[4..];
			if (string.IsNullOrEmpty(uid))
				return (400, null, Xml);
			if (calendar.Events.Any(el => el.Key != path && el.Value.Contains("\r\nUID:" + uid + "\r\n", StringComparison.Ordinal)))
				return (409, null, Xml);

			calendar.Events[path] = body;
			return (exists ? 204 : 201, null, Xml);
		}

		private static XElement CalendarResource(string path, Calendar calendar, IReadOnlyList<XName> asked) =>
			Resource(path, asked, name =>
				name == Dav + "resourcetype" ? new object[] { new XElement(Dav + "collection"), new XElement(CalDav + "calendar") }
				: name == Dav + "displayname" ? calendar.Name
				: name == CalDav + "calendar-description" ? calendar.Description
				: null);

		/// <summary>A response with every property asked for: found ones with their values, the rest as not found</summary>
		private static XElement Resource(string path, IReadOnlyList<XName> asked, Func<XName, object?> value)
		{
			var found = asked.Select(el => (Name: el, Value: value(el))).ToArray();
			var item = new XElement(Dav + "response", new XElement(Dav + "href", Uri.EscapeDataString(path).Replace("%2F", "/")));
			if (found.Any(el => el.Value != null))
				item.Add(PropStat(200, found.Where(el => el.Value != null).Select(el => new XElement(el.Name, el.Value))));
			if (found.Any(el => el.Value == null))
				item.Add(PropStat(404, found.Where(el => el.Value == null).Select(el => new XElement(el.Name))));
			return item;
		}

		private static XElement Resource(string path, params (XName Name, string? Value)[] properties) =>
			Resource(path, properties.Select(el => el.Name).ToArray(), name => properties.First(el => el.Name == name).Value ?? (object)string.Empty);

		private static XElement PropStat(int status, IEnumerable<XElement> properties) =>
			new(Dav + "propstat", new XElement(Dav + "prop", properties), new XElement(Dav + "status", $"HTTP/1.1 {status} {(status == 200 ? "OK" : "Not Found")}"));

		private static string MultiStatus(IEnumerable<XElement> items) =>
			new XElement(Dav + "multistatus", new XAttribute(XNamespace.Xmlns + "d", Dav.NamespaceName), new XAttribute(XNamespace.Xmlns + "cal", CalDav.NamespaceName), items).ToString(SaveOptions.DisableFormatting);

		private static string Etag(string text) => "\"" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16] + "\"";

		private static void Redirect(HttpContext context, string location)
		{
			context.Response.StatusCode = 301;
			context.Response.Headers.Location = location;
		}

		private static bool IsSigned(HttpRequest request)
		{
			var header = request.Headers.Authorization.ToString();
			if (!header.StartsWith("Basic ", StringComparison.Ordinal))
				return false;
			try
			{
				return Encoding.UTF8.GetString(Convert.FromBase64String(header[6..])) == User + ":" + Password;
			}
			catch (FormatException)
			{
				return false;
			}
		}


		private sealed class Calendar
		{
			public string Name { get; set; } = string.Empty;
			public string? Description { get; set; }
			public Dictionary<string, string> Events { get; } = new();
		}


		private const string Xml = "application/xml; charset=utf-8";
		private static readonly XNamespace Dav = "DAV:";
		private static readonly XNamespace CalDav = "urn:ietf:params:xml:ns:caldav";

		private readonly WebApplication _app;
		private readonly object _lock = new();
		private readonly List<Fault> _faults = new();
		private readonly List<StandInCall> _calls = new();
		private readonly Dictionary<string, Calendar> _calendars = new();
		private readonly HashSet<string> _used = new();
	}
}
