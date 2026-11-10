using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;


namespace Purity.GoogleStandIn
{
	/// <summary>A failure to give the next matching call</summary>
	/// <param name="Status">The HTTP status</param>
	/// <param name="Reason">Google's reason for it</param>
	/// <param name="Domain">Google's domain for it</param>
	/// <param name="PathContains">Only calls whose path holds this, or any call</param>
	public sealed record Fault(int Status, string Reason, string Domain = "global", string? PathContains = null);


	/// <summary>One of the calendars the app made, as it stands</summary>
	public sealed record StandInCalendar(string Id, string Summary, string? Description, IReadOnlyList<JsonObject> Events);


	/// <summary>
	/// Google's sign-in and Calendar API, on this computer, for one made-up person. Only the calls
	/// Purity makes are here, and they're as strict as Google's published rules: a wrong PKCE
	/// verifier, a bad event ID or an alert out of range is refused, as Google would.
	/// </summary>
	public sealed partial class StandInServer : IAsyncDisposable
	{
		public const string ClientId = "stand-in-client";
		private const string AppCalendarsScope = "https://www.googleapis.com/auth/calendar.app.created";
		private const string CalendarListScope = "https://www.googleapis.com/auth/calendar.calendarlist.readonly";


		public static async Task<StandInServer> StartAsync(int port = 0)
		{
			var builder = WebApplication.CreateSlimBuilder();
			builder.Logging.ClearProviders();
			builder.WebHost.ConfigureKestrel(options => options.Listen(System.Net.IPAddress.Loopback, port));
			var app = builder.Build();
			var server = new StandInServer(app);
			server.MapRoutes();
			await app.StartAsync();
			var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
			server.BaseAddress = new Uri(address.TrimEnd('/') + "/");
			return server;
		}

		private StandInServer(WebApplication app) => _app = app;


		public Uri BaseAddress { get; private set; } = null!;
		public Uri Authorize => new(BaseAddress, "o/oauth2/v2/auth");
		public Uri Token => new(BaseAddress, "token");
		public Uri Revoke => new(BaseAddress, "revoke");
		public Uri Calendar => new(BaseAddress, "calendar/v3/");

		/// <summary>Whether the made-up person turns the sign-in down</summary>
		public bool DeclinesSignIn { get; set; }

		/// <summary>Sends the browser back with a state that doesn't match, as a forged redirect would</summary>
		public bool SendsWrongState { get; set; }

		/// <summary>Whether a new calendar's description is kept when it's made. Google only documents the name there.</summary>
		public bool KeepsDescriptionWhenMaking { get; set; }

		/// <summary>The most entries one page of a list holds, so paging can be exercised with few entries</summary>
		public int PageLimit { get; set; } = 2500;

		/// <summary>How many calls have arrived at the Calendar API</summary>
		public int CalendarCalls { get { lock (_lock) return _calendarCalls; } }

		public IReadOnlyList<StandInCalendar> Calendars
		{
			get
			{
				lock (_lock)
					return _calendars.Values.Where(el => el.MadeByTheApp).Select(el => new StandInCalendar(el.Id, el.Summary, el.Description, el.Events.Values.Select(item => item.DeepClone().AsObject()).ToArray())).ToArray();
			}
		}

		/// <summary>The calls that have arrived, newest last, as "METHOD /path"</summary>
		public IReadOnlyList<string> Log { get { lock (_lock) return _log.ToArray(); } }

		/// <summary>The permissions the last sign-in asked for</summary>
		public IReadOnlyList<string> GrantedScopes { get { lock (_lock) return _grantedScopes.ToArray(); } }

		/// <summary>Whether any sign-in is still good</summary>
		public bool HasLiveSignIn { get { lock (_lock) return _refreshTokens.Count > 0; } }


		public void FailNext(Fault fault, int times = 1)
		{
			lock (_lock)
				for (var i = 0; i < times; i++)
					_faults.Add(fault);
		}

		/// <summary>Makes every access token run out, as they do after an hour</summary>
		public void ExpireAccessTokens()
		{
			lock (_lock)
				_accessTokens.Clear();
		}

		/// <summary>Ends every sign-in, as when the person removes the app's access at Google</summary>
		public void RevokeEverything()
		{
			lock (_lock)
			{
				_accessTokens.Clear();
				_refreshTokens.Clear();
			}
		}

		/// <summary>Deletes a calendar the way the person could at Google</summary>
		public void DeleteCalendarByHand(string id)
		{
			lock (_lock)
				_calendars.Remove(id);
		}

		/// <summary>Adds a calendar of the person's own, which Purity can see in the list but isn't its</summary>
		public void AddCalendarByHand(string id, string summary)
		{
			lock (_lock)
				_calendars[id] = new StoredCalendar(id, summary) { MadeByTheApp = false };
		}

		/// <summary>Renames a calendar the way the person could at Google</summary>
		public void RenameCalendarByHand(string id, string summary)
		{
			lock (_lock)
				_calendars[id].Summary = summary;
		}

		/// <summary>Adds an entry the way the person could at Google, which isn't one of Purity's</summary>
		public void AddEntryByHand(string calendarId, string id, string summary)
		{
			lock (_lock)
				_calendars[calendarId].Events[id] = new JsonObject
				{
					["id"] = id,
					["status"] = "confirmed",
					["summary"] = summary,
					["start"] = new JsonObject { ["dateTime"] = "2031-03-02T10:00:00+02:00" },
					["end"] = new JsonObject { ["dateTime"] = "2031-03-02T11:00:00+02:00" },
				};
		}

		public async ValueTask DisposeAsync()
		{
			await _app.StopAsync();
			await _app.DisposeAsync();
		}


		private void MapRoutes()
		{
			_app.Use(async (context, next) =>
			{
				Fault? fault = null;
				var path = context.Request.Path.Value ?? string.Empty;
				lock (_lock)
				{
					_log.Add($"{context.Request.Method} {path}");
					if (path.StartsWith("/calendar/v3/", StringComparison.Ordinal))
						_calendarCalls++;
					var index = _faults.FindIndex(el => el.PathContains == null || path.Contains(el.PathContains, StringComparison.Ordinal));
					if (index >= 0)
					{
						fault = _faults[index];
						_faults.RemoveAt(index);
					}
				}

				if (fault != null)
					await Error(fault.Status, fault.Reason, fault.Reason, fault.Domain).ExecuteAsync(context);
				else
					await next();
			});

			_app.MapGet("/stand-in/state", () => Results.Json(new JsonObject
			{
				["signedIn"] = HasLiveSignIn,
				["calendars"] = new JsonArray(Calendars.Select(el => (JsonNode?)new JsonObject
				{
					["id"] = el.Id,
					["summary"] = el.Summary,
					["description"] = el.Description,
					["entries"] = new JsonArray(el.Events.Where(item => item["status"]?.ToString() != "cancelled").Select(item => (JsonNode?)item).ToArray()),
				}).ToArray()),
			}));

			_app.MapGet("/o/oauth2/v2/auth", SignIn);
			_app.MapPost("/token", TokenAsync);
			_app.MapPost("/revoke", RevokeToken);

			_app.MapPost("/calendar/v3/calendars", (Func<HttpContext, Task<IResult>>)CreateCalendarAsync);
			_app.MapGet("/calendar/v3/users/me/calendarList", ListCalendars);
			_app.MapPatch("/calendar/v3/calendars/{calendar}", PatchCalendarAsync);
			_app.MapGet("/calendar/v3/calendars/{calendar}", GetCalendar);
			_app.MapDelete("/calendar/v3/calendars/{calendar}", DeleteCalendar);

			_app.MapGet("/calendar/v3/calendars/{calendar}/events", ListEvents);
			_app.MapPost("/calendar/v3/calendars/{calendar}/events", InsertEventAsync);
			_app.MapGet("/calendar/v3/calendars/{calendar}/events/{id}", GetEvent);
			_app.MapPut("/calendar/v3/calendars/{calendar}/events/{id}", UpdateEventAsync);
			_app.MapDelete("/calendar/v3/calendars/{calendar}/events/{id}", DeleteEvent);
		}


		private IResult SignIn(HttpRequest request)
		{
			var query = request.Query;
			string redirect = query["redirect_uri"].ToString(), state = query["state"].ToString(), scope = query["scope"].ToString(), challenge = query["code_challenge"].ToString();
			if (query["client_id"] != ClientId || query["response_type"] != "code" || query["code_challenge_method"] != "S256" || challenge.Length == 0)
				return Results.BadRequest("The sign-in request is missing something Google requires");
			if (!LoopbackAddress().IsMatch(redirect))
				return Results.BadRequest("A desktop app's redirect must be a loopback address");
			if (!scope.Split(' ').Contains(AppCalendarsScope))
				return Results.BadRequest("The app didn't ask for the expected permission");

			if (SendsWrongState)
				state = "forged";
			if (DeclinesSignIn)
				return Results.Redirect($"{redirect}?error=access_denied&state={Uri.EscapeDataString(state)}");

			lock (_lock)
				_grantedScopes = scope.Split(' ').ToList();
			var code = Random("code");
			lock (_lock)
				_codes[code] = new PendingCode(challenge, redirect);
			return Results.Redirect($"{redirect}?code={Uri.EscapeDataString(code)}&state={Uri.EscapeDataString(state)}");
		}

		private async Task<IResult> TokenAsync(HttpRequest request)
		{
			var form = await request.ReadFormAsync();
			if (form["client_id"] != ClientId)
				return TokenError("invalid_client");

			lock (_lock)
			{
				if (form["grant_type"] == "authorization_code")
				{
					if (!_codes.Remove(form["code"].ToString(), out var pending) || pending.Redirect != form["redirect_uri"])
						return TokenError("invalid_grant");

					var expected = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(form["code_verifier"].ToString()))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
					if (expected != pending.Challenge)
						return TokenError("invalid_grant");

					var refresh = Random("refresh");
					_refreshTokens.Add(refresh);
					return Tokens(refresh);
				}

				if (form["grant_type"] == "refresh_token")
					return _refreshTokens.Contains(form["refresh_token"].ToString()) ? Tokens(null) : TokenError("invalid_grant");
			}
			return TokenError("unsupported_grant_type");
		}

		private IResult RevokeToken(HttpRequest request)
		{
			lock (_lock)
			{
				if (!_refreshTokens.Contains(request.Query["token"].ToString()))
					return TokenError("invalid_token");

				_refreshTokens.Clear();						// Google ends the app's access for the whole account, on every computer
				_accessTokens.Clear();
				return Results.Ok();
			}
		}

		private IResult Tokens(string? refresh)
		{
			var access = Random("access");
			_accessTokens.Add(access);
			var body = new JsonObject { ["access_token"] = access, ["expires_in"] = 3599, ["scope"] = AppCalendarsScope, ["token_type"] = "Bearer" };
			if (refresh != null)
				body["refresh_token"] = refresh;
			return Results.Json(body);
		}

		private static IResult TokenError(string error) => Results.Json(new JsonObject { ["error"] = error }, statusCode: 400);


		private async Task<IResult> CreateCalendarAsync(HttpContext context)
		{
			if (Unauthorized(context) is { } refused)
				return refused;

			var body = await BodyAsync(context);
			if (body?["summary"]?.ToString() is not { Length: > 0 } summary)
				return Error(400, "required", "Missing title.");

			lock (_lock)
			{
				var id = $"{++_nextCalendar}stand.in@group.calendar.google.com";
				_calendars[id] = new StoredCalendar(id, summary) { Description = KeepsDescriptionWhenMaking ? body["description"]?.ToString() : null };
				return Results.Json(new JsonObject { ["id"] = id, ["summary"] = summary, ["description"] = _calendars[id].Description });
			}
		}

		private IResult ListCalendars(HttpContext context)
		{
			if (Unauthorized(context) is { } refused)
				return refused;

			var size = int.TryParse(context.Request.Query["maxResults"], out var asked) ? asked : 100;
			if (size is < 1 or > 250)
				return Error(400, "invalid", "Invalid value for maxResults.");

			lock (_lock)
			{
				if (!_grantedScopes.Contains(CalendarListScope))
					return Error(403, "insufficientPermissions", "Insufficient Permission");

				var all = _calendars.Values.ToArray();
				var start = int.TryParse(context.Request.Query["pageToken"], out var token) ? token : 0;
				var page = all.Skip(start).Take(Math.Min(size, PageLimit)).Select(el => (JsonNode?)new JsonObject { ["id"] = el.Id, ["summary"] = el.Summary, ["description"] = el.Description, ["accessRole"] = "owner" }).ToArray();
				var result = new JsonObject { ["items"] = new JsonArray(page) };
				if (start + page.Length < all.Length)
					result["nextPageToken"] = (start + page.Length).ToString(System.Globalization.CultureInfo.InvariantCulture);
				return Results.Json(result);
			}
		}

		private async Task<IResult> PatchCalendarAsync(HttpContext context, string calendar)
		{
			if (Unauthorized(context) is { } refused)
				return refused;

			var body = await BodyAsync(context);
			if (body == null)
				return Error(400, "invalid", "Not JSON.");

			lock (_lock)
			{
				if (!_calendars.TryGetValue(calendar, out var found) || !found.MadeByTheApp)
					return NotFound();

				found.Summary = body["summary"]?.ToString() ?? found.Summary;
				found.Description = body["description"]?.ToString() ?? found.Description;
				return Results.Json(new JsonObject { ["id"] = found.Id, ["summary"] = found.Summary, ["description"] = found.Description });
			}
		}

		private IResult GetCalendar(HttpContext context, string calendar)
		{
			if (Unauthorized(context) is { } refused)
				return refused;

			lock (_lock)
				return _calendars.TryGetValue(calendar, out var found) ? Results.Json(new JsonObject { ["id"] = found.Id, ["summary"] = found.Summary, ["description"] = found.Description }) : NotFound();
		}

		private IResult DeleteCalendar(HttpContext context, string calendar)
		{
			if (Unauthorized(context) is { } refused)
				return refused;

			lock (_lock)
				return _calendars.Remove(calendar) ? Results.NoContent() : NotFound();
		}

		private IResult ListEvents(HttpContext context, string calendar)
		{
			if (Unauthorized(context) is { } refused)
				return refused;

			var query = context.Request.Query;
			var size = int.TryParse(query["maxResults"], out var asked) ? asked : 250;
			if (size is < 1 or > 2500)
				return Error(400, "invalid", "Invalid value for maxResults.");

			lock (_lock)
			{
				if (!_calendars.TryGetValue(calendar, out var found))
					return NotFound();

				var showDeleted = query["showDeleted"] == "true";
				var all = found.Events.Values.Where(el => showDeleted || el["status"]?.ToString() != "cancelled").ToArray();
				var start = int.TryParse(query["pageToken"], out var token) ? token : 0;
				var page = all.Skip(start).Take(Math.Min(size, PageLimit)).Select(el => (JsonNode?)el.DeepClone()).ToArray();
				var result = new JsonObject { ["items"] = new JsonArray(page) };
				if (start + page.Length < all.Length)
					result["nextPageToken"] = (start + page.Length).ToString(System.Globalization.CultureInfo.InvariantCulture);
				return Results.Json(result);
			}
		}

		private async Task<IResult> InsertEventAsync(HttpContext context, string calendar)
		{
			if (Unauthorized(context) is { } refused)
				return refused;

			var body = await BodyAsync(context);
			if (body == null)
				return Error(400, "invalid", "Not JSON.");
			if (Invalid(body) is { } problem)
				return Error(400, "invalid", problem);

			var id = body["id"]?.ToString() ?? Random("e").ToLowerInvariant().Replace('-', 'a').Replace('_', 'b');
			if (!EventId().IsMatch(id))
				return Error(400, "invalid", "Invalid resource id value.");

			lock (_lock)
			{
				if (!_calendars.TryGetValue(calendar, out var found))
					return NotFound();
				if (found.Events.ContainsKey(id))					// a deleted entry keeps its ID for a while
					return Error(409, "duplicate", "The requested identifier already exists.");

				body["id"] = id;
				body["status"] ??= "confirmed";
				found.Events[id] = body;
				return Results.Json(body.DeepClone());
			}
		}

		private IResult GetEvent(HttpContext context, string calendar, string id)
		{
			if (Unauthorized(context) is { } refused)
				return refused;

			lock (_lock)
				return _calendars.TryGetValue(calendar, out var found) && found.Events.TryGetValue(id, out var item) ? Results.Json(item.DeepClone()) : NotFound();
		}

		private async Task<IResult> UpdateEventAsync(HttpContext context, string calendar, string id)
		{
			if (Unauthorized(context) is { } refused)
				return refused;

			var body = await BodyAsync(context);
			if (body == null)
				return Error(400, "invalid", "Not JSON.");
			if (Invalid(body) is { } problem)
				return Error(400, "invalid", problem);

			lock (_lock)
			{
				if (!_calendars.TryGetValue(calendar, out var found) || !found.Events.TryGetValue(id, out var existing))
					return NotFound();
				if (existing["status"]?.ToString() == "cancelled" && body["status"]?.ToString() != "confirmed")
					return Error(410, "deleted", "Resource has been deleted");

				body["id"] = id;
				body["status"] ??= "confirmed";
				found.Events[id] = body;
				return Results.Json(body.DeepClone());
			}
		}

		private IResult DeleteEvent(HttpContext context, string calendar, string id)
		{
			if (Unauthorized(context) is { } refused)
				return refused;

			lock (_lock)
			{
				if (!_calendars.TryGetValue(calendar, out var found) || !found.Events.TryGetValue(id, out var item))
					return NotFound();
				if (item["status"]?.ToString() == "cancelled")
					return Error(410, "deleted", "Resource has been deleted");

				item["status"] = "cancelled";
				return Results.NoContent();
			}
		}


		private IResult? Unauthorized(HttpContext context)
		{
			var header = context.Request.Headers.Authorization.ToString();
			var token = header.StartsWith("Bearer ", StringComparison.Ordinal) ? header["Bearer ".Length..] : string.Empty;
			lock (_lock)
				return _accessTokens.Contains(token) ? null : Error(401, "authError", "Invalid Credentials");
		}

		private static async Task<JsonObject?> BodyAsync(HttpContext context)
		{
			try
			{
				return await JsonNode.ParseAsync(context.Request.Body) as JsonObject;
			}
			catch (System.Text.Json.JsonException)
			{
				return null;
			}
		}

		/// <summary>What Google's documentation says an event must look like, for the fields Purity sends</summary>
		private static string? Invalid(JsonObject body)
		{
			foreach (var side in new[] { "start", "end" })
			{
				if (body[side]?["dateTime"]?.ToString() is not { } time)
					return $"Missing {side}.";
				if (!TimeWithOffset().IsMatch(time) && body[side]?["timeZone"] == null)
					return $"The {side} time needs an offset or a time zone.";
			}

			if (body["reminders"] is JsonObject reminders && reminders["overrides"] is JsonArray overrides)
			{
				if (reminders["useDefault"]?.GetValue<bool>() == true)
					return "Overrides can be set only when useDefault is false.";
				if (overrides.Count > 5)
					return "The maximum number of override reminders is 5.";
				foreach (var item in overrides)
				{
					if (item?["method"]?.ToString() is not ("email" or "popup"))
						return "Invalid reminder method.";
					if (item["minutes"]?.GetValue<int>() is not (>= 0 and <= 40320))
						return "Invalid reminder minutes.";
				}
			}
			return null;
		}

		private static IResult NotFound() => Error(404, "notFound", "Not Found");

		private static IResult Error(int status, string reason, string message, string domain = "global") => Results.Json(new JsonObject
		{
			["error"] = new JsonObject
			{
				["errors"] = new JsonArray(new JsonObject { ["domain"] = domain, ["reason"] = reason, ["message"] = message }),
				["code"] = status,
				["message"] = message,
			},
		}, statusCode: status);

		private static string Random(string prefix) => $"{prefix}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant()}";

		[GeneratedRegex(@"^http://(127\.0\.0\.1|localhost)(:\d+)?/?$")]
		private static partial Regex LoopbackAddress();

		[GeneratedRegex("^[a-v0-9]{5,1024}$")]
		private static partial Regex EventId();

		[GeneratedRegex(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(\.\d+)?(Z|[+-]\d\d:\d\d)$")]
		private static partial Regex TimeWithOffset();


		private sealed record PendingCode(string Challenge, string Redirect);

		private sealed class StoredCalendar(string id, string summary)
		{
			public string Id { get; } = id;
			public string Summary { get; set; } = summary;
			public string? Description { get; set; }
			public bool MadeByTheApp { get; init; } = true;
			public Dictionary<string, JsonObject> Events { get; } = new();
		}


		private readonly WebApplication _app;
		private readonly object _lock = new();
		private readonly Dictionary<string, PendingCode> _codes = new();
		private readonly HashSet<string> _accessTokens = new();
		private readonly HashSet<string> _refreshTokens = new();
		private readonly Dictionary<string, StoredCalendar> _calendars = new();
		private readonly List<Fault> _faults = new();
		private readonly List<string> _log = new();
		private List<string> _grantedScopes = new();
		private int _nextCalendar;
		private int _calendarCalls;
	}
}
