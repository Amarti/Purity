using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;


namespace Purity.Reminders.CalDav
{
	/// <param name="Server">Where the calendars are: iCloud's address, or the one the person gave</param>
	/// <param name="User">The Apple Account, or the user name at the server</param>
	/// <param name="Password">An app-specific password for iCloud, or the server's own password</param>
	public sealed record CalDavSignIn(Uri Server, string User, string Password)
	{
		/// <summary>iCloud's calendars, for every Apple Account</summary>
		public static Uri ICloud { get; } = new("https://caldav.icloud.com/");

		/// <summary>Not the password, which never goes into a log or a message</summary>
		public override string ToString() => $"{User} at {Server}";
	}


	/// <summary>
	/// The sign-in to a CalDAV server, such as iCloud. The server takes the user name and password
	/// with every call, so they're kept in the system's secure store, and only sent over https to the
	/// server they were given for.
	/// </summary>
	public sealed class CalDavAccount
	{
		/// <param name="http">Must not follow redirects itself: here they're checked before the password goes along</param>
		/// <param name="secrets">Where the sign-in is kept</param>
		/// <param name="wait">Waits before trying again</param>
		public CalDavAccount(HttpClient http, ISecretStore secrets, Func<TimeSpan, CancellationToken, Task>? wait = null)
		{
			_http = http;
			_secrets = secrets;
			_wait = wait ?? Task.Delay;
		}

		/// <summary>A client that leaves redirects to the account, as it needs</summary>
		public static HttpClient NewHttpClient() =>
			new(new SocketsHttpHandler { AllowAutoRedirect = false, PooledConnectionLifetime = TimeSpan.FromMinutes(5) }) { Timeout = TimeSpan.FromSeconds(30) };


		public bool IsSignedIn => SignIn != null;

		/// <summary>False when this computer has nowhere safe to keep the sign-in, so it lasts until the app closes</summary>
		public bool RemembersSignIn { get; private set; } = true;

		/// <summary>Who's signed in, or null</summary>
		public string? User => SignIn?.User;


		/// <summary>Checks the sign-in by finding where the calendars are, and keeps it if it works</summary>
		public async Task SignInAsync(CalDavSignIn signIn, CancellationToken cancellation)
		{
			if (!IsSafe(signIn.Server))
				throw new CalendarServiceException(CalendarProblem.NotSecure, "The address doesn't use https");

			Uri home;
			try
			{
				home = await DiscoverAsync(signIn, cancellation);
			}
			catch (CalendarServiceException e) when (e.Problem == CalendarProblem.SignInNeeded)
			{
				throw new CalendarServiceException(CalendarProblem.PasswordRefused, "The server didn't accept the user name or the password");
			}

			_signIn = signIn;
			_home = home;
			RemembersSignIn = _secrets.Write(Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new StoredSignIn { Server = signIn.Server.AbsoluteUri, User = signIn.User, Password = signIn.Password }, CalDavJson.Default.StoredSignIn)));
		}

		/// <summary>
		/// Forgets the sign-in on this computer. A server can't be told to forget a password, so an
		/// app-specific one goes on working until it's revoked where it was made.
		/// </summary>
		public void SignOut()
		{
			_secrets.Delete();
			_signIn = null;
			_home = null;
			_read = true;
			RemembersSignIn = true;
		}


		/// <summary>Where this account's calendars are</summary>
		internal async Task<Uri> HomeAsync(CancellationToken cancellation)
		{
			if (_home != null)
				return _home;

			var signIn = SignIn ?? throw new CalendarServiceException(CalendarProblem.SignInNeeded, "Nobody is signed in");
			return _home = await DiscoverAsync(signIn, cancellation);
		}

		/// <summary>Forgets where the calendars are, so they're looked for again</summary>
		internal void ForgetHome() => _home = null;

		/// <summary>
		/// Sends a call, following redirects and waiting out the failures a server says to wait out.
		/// The password goes only to the server it was given for, and never over plain http to
		/// another computer. A 401 means the password no longer works.
		/// </summary>
		/// <param name="depth">The WebDAV depth, or null for none</param>
		/// <param name="body">The body, or null for none</param>
		/// <param name="contentType">The body's type</param>
		/// <param name="ifNoneMatch">Sends If-None-Match: *, so a PUT can't overwrite what's there</param>
		internal async Task<HttpResponseMessage> SendAsync(HttpMethod method, Uri url, string? depth, string? body, string contentType, CancellationToken cancellation, bool ifNoneMatch = false)
		{
			var signIn = SignIn ?? throw new CalendarServiceException(CalendarProblem.SignInNeeded, "Nobody is signed in");
			return await SendAsync(signIn, method, url, depth, body, contentType, cancellation, ifNoneMatch);
		}


		private CalDavSignIn? SignIn
		{
			get
			{
				if (_signIn == null && _read)
				{
					_read = false;
					_signIn = Stored(_secrets.Read());
				}
				return _signIn;
			}
		}

		private async Task<HttpResponseMessage> SendAsync(CalDavSignIn signIn, HttpMethod method, Uri url, string? depth, string? body, string contentType, CancellationToken cancellation, bool ifNoneMatch = false)
		{
			var hops = 0;
			for (var attempt = 0; ; attempt++)
			{
				if (!IsSafe(url))
					throw new CalendarServiceException(CalendarProblem.Refused, "The server sent Purity to an address without https");

				using var request = new HttpRequestMessage(method, url);
				if (IsSameService(signIn.Server, url))
					request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(signIn.User + ":" + signIn.Password)));
				if (depth != null)
					request.Headers.Add("Depth", depth);
				if (ifNoneMatch)
					request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Any);
				if (body != null)
					request.Content = new StringContent(body, new UTF8Encoding(false), contentType);

				HttpResponseMessage response;
				try
				{
					response = await _http.SendAsync(request, cancellation);
				}
				catch (HttpRequestException e)
				{
					throw new CalendarServiceException(CalendarProblem.NoConnection, e.Message);
				}
				catch (TaskCanceledException e) when (!cancellation.IsCancellationRequested)
				{
					throw new CalendarServiceException(CalendarProblem.NoConnection, e.Message);
				}

				var status = (int)response.StatusCode;
				if (status is 301 or 302 or 307 or 308 && response.Headers.Location is { } location && hops < MaxRedirects)
				{
					response.Dispose();
					url = new Uri(url, location);
					hops++;
					attempt--;
					continue;
				}

				if (response.StatusCode == HttpStatusCode.Unauthorized)
				{
					response.Dispose();
					throw new CalendarServiceException(CalendarProblem.SignInNeeded, "The server turned the sign-in down");
				}

				if (attempt >= Retries || status is not (429 or 500 or 502 or 503 or 504))
					return response;

				response.Dispose();
				await _wait(TimeSpan.FromSeconds(1 << attempt), cancellation);
			}
		}

		/// <summary>Where the calendars are: the person's principal, found at the address given or at its well-known one, holds it</summary>
		private async Task<Uri> DiscoverAsync(CalDavSignIn signIn, CancellationToken cancellation)
		{
			var principal = await HrefAsync(signIn, signIn.Server, CalDavXml.Dav + "current-user-principal", cancellation)
				?? await HrefAsync(signIn, new Uri(signIn.Server, "/.well-known/caldav"), CalDavXml.Dav + "current-user-principal", cancellation)
				?? throw new CalendarServiceException(CalendarProblem.NoCalendarService, "No CalDAV principal at the address");
			var home = await HrefAsync(signIn, principal, CalDavXml.CalDav + "calendar-home-set", cancellation)
				?? throw new CalendarServiceException(CalendarProblem.NoCalendarService, "The principal names no calendar home");
			return CalDavXml.AsFolder(home);
		}

		/// <summary>The address a property holds, or null when the server has no such property there</summary>
		private async Task<Uri?> HrefAsync(CalDavSignIn signIn, Uri url, XName property, CancellationToken cancellation)
		{
			using var response = await SendAsync(signIn, CalDavXml.PropFind, url, "0", CalDavXml.PropFindBody(property), CalDavXml.XmlType, cancellation);
			if (response.StatusCode != HttpStatusCode.MultiStatus)
				return null;

			var answer = await CalDavXml.ReadAsync(response, cancellation);
			foreach (var item in answer)
				if (item.Found(property)?.Element(CalDavXml.Dav + "href")?.Value is { Length: > 0 } href)
					return new Uri(item.Url, href.Trim());
			return null;
		}


		private static CalDavSignIn? Stored(string? secret)
		{
			if (secret == null)
				return null;

			try
			{
				var stored = JsonSerializer.Deserialize(Convert.FromBase64String(secret), CalDavJson.Default.StoredSignIn);
				return stored is { Server: { } server, User: { } user, Password: { } password } && Uri.TryCreate(server, UriKind.Absolute, out var address)
					? new CalDavSignIn(address, user, password)
					: null;
			}
			catch (Exception e) when (e is FormatException or JsonException)
			{
				return null;							// not one this version can read, so the person connects again
			}
		}

		/// <summary>https, or plain http to this computer itself, as for a calendar server run here</summary>
		private static bool IsSafe(Uri url) => url.Scheme == Uri.UriSchemeHttps || (url.Scheme == Uri.UriSchemeHttp && url.IsLoopback);

		/// <summary>
		/// Whether a call is going to the server the password was given for: the same computer, or
		/// another under the same domain, as iCloud's calendars are on p01-caldav.icloud.com and on.
		/// </summary>
		internal static bool IsSameService(Uri server, Uri url)
		{
			if (string.Equals(server.Host, url.Host, StringComparison.OrdinalIgnoreCase))
				return server.Scheme == url.Scheme || url.Scheme == Uri.UriSchemeHttps;
			if (url.Scheme != Uri.UriSchemeHttps || server.HostNameType != UriHostNameType.Dns)
				return false;

			var dot = server.Host.IndexOf('.');
			var parent = dot < 0 ? null : server.Host[(dot + 1)..];
			return parent != null && parent.Contains('.') && url.Host.EndsWith("." + parent, StringComparison.OrdinalIgnoreCase);
		}


		private const int MaxRedirects = 5;
		private const int Retries = 3;

		private readonly HttpClient _http;
		private readonly ISecretStore _secrets;
		private readonly Func<TimeSpan, CancellationToken, Task> _wait;
		private CalDavSignIn? _signIn;
		private Uri? _home;
		private bool _read = true;
	}


	internal sealed class StoredSignIn
	{
		public string? Server { get; set; }
		public string? User { get; set; }
		public string? Password { get; set; }
	}


	[JsonSerializable(typeof(StoredSignIn))]
	[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
	internal sealed partial class CalDavJson : JsonSerializerContext
	{}
}
