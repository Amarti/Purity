using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace Purity.Reminders.Google
{
	/// <summary>Where Google's services are. Only tests point these anywhere else.</summary>
	public sealed record GoogleEndpoints(Uri Authorize, Uri Token, Uri Revoke, Uri Calendar)
	{
		public static GoogleEndpoints Real { get; } = new(
			new Uri("https://accounts.google.com/o/oauth2/v2/auth"),
			new Uri("https://oauth2.googleapis.com/token"),
			new Uri("https://oauth2.googleapis.com/revoke"),
			new Uri("https://www.googleapis.com/calendar/v3/"));
	}


	/// <param name="Id">The app's ID at Google. It comes from the build, never from the public repo.</param>
	/// <param name="Secret">Google gives desktop apps one too, and says it can't really be kept secret</param>
	public sealed record GoogleClient(string Id, string? Secret = null);


	/// <summary>
	/// The sign-in to one Google account. The person signs in on Google's own page in their browser,
	/// so the app only ever holds a token, and it can only write to calendars the app itself made.
	/// </summary>
	public sealed class GoogleAccount(HttpClient http, GoogleClient client, GoogleEndpoints endpoints, ISecretStore secrets, TimeProvider clock)
	{
		/// <summary>
		/// The only permissions asked for: calendars this app made, and the list of calendars, which
		/// is how another computer finds the one already made. The list holds names, never events.
		/// </summary>
		public const string Scope = "https://www.googleapis.com/auth/calendar.app.created https://www.googleapis.com/auth/calendar.calendarlist.readonly";


		public GoogleEndpoints Endpoints => endpoints;

		public bool IsSignedIn => RefreshToken != null;

		/// <summary>False when this computer has nowhere safe to keep the sign-in, so it lasts until the app closes</summary>
		public bool RemembersSignIn { get; private set; } = true;


		/// <summary>Opens Google's sign-in page and waits for the person to finish</summary>
		/// <param name="openBrowser">Opens the page in the person's browser</param>
		/// <param name="finishedPage">The words shown in the browser once it's done</param>
		/// <param name="cancellation">Gives up waiting</param>
		public async Task SignInAsync(Func<Uri, Task> openBrowser, string finishedPage, CancellationToken cancellation)
		{
			var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
			var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
			var state = Base64Url(RandomNumberGenerator.GetBytes(16));

			using var redirect = new LoopbackRedirect();
			using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
			limit.CancelAfter(SignInTime);

			var answer = redirect.WaitAsync(finishedPage, limit.Token);
			await openBrowser(new Uri(endpoints.Authorize + "?" + Query(new Dictionary<string, string>
			{
				["client_id"] = client.Id,
				["redirect_uri"] = redirect.Address,
				["response_type"] = "code",
				["scope"] = Scope,
				["code_challenge"] = challenge,
				["code_challenge_method"] = "S256",
				["state"] = state,
			})));

			NameValueCollection query;
			try
			{
				query = await answer;
			}
			catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
			{
				throw new CalendarServiceException(CalendarProblem.SignInNotFinished, "The sign-in wasn't finished in time");
			}

			if (query["state"] != state)
				throw new CalendarServiceException(CalendarProblem.Refused, "The sign-in answer didn't match the request");
			if (query["error"] != null || query["code"] is not { } code)
				throw new CalendarServiceException(CalendarProblem.SignInNotFinished, "The sign-in was turned down");

			var tokens = await RequestTokensAsync(new Dictionary<string, string>
			{
				["grant_type"] = "authorization_code",
				["code"] = code,
				["code_verifier"] = verifier,
				["redirect_uri"] = redirect.Address,
			}, cancellation);
			if (tokens.RefreshToken == null)
				throw new CalendarServiceException(CalendarProblem.Refused, "Google didn't give a lasting sign-in");

			_refreshToken = tokens.RefreshToken;
			RemembersSignIn = secrets.Write(tokens.RefreshToken);
			Remember(tokens);
		}

		/// <summary>A token for the next call, renewing it when it has run out</summary>
		public async Task<string> AccessTokenAsync(CancellationToken cancellation)
		{
			if (_accessToken != null && clock.GetUtcNow() < _accessTokenEnds)
				return _accessToken;

			if (RefreshToken is not { } refresh)
				throw new CalendarServiceException(CalendarProblem.SignInNeeded, "Not signed in");

			Remember(await RequestTokensAsync(new Dictionary<string, string>
			{
				["grant_type"] = "refresh_token",
				["refresh_token"] = refresh,
			}, cancellation));
			return _accessToken!;
		}

		/// <summary>Stops using the current token, which the service just turned down</summary>
		public void ForgetAccessToken() => _accessToken = null;

		/// <summary>Ends the sign-in, here and at Google. Forgetting it here always happens, even offline.</summary>
		public async Task SignOutAsync(CancellationToken cancellation)
		{
			var refresh = RefreshToken;
			Forget();
			if (refresh == null)
				return;

			try
			{
				using var request = new HttpRequestMessage(HttpMethod.Post, endpoints.Revoke + "?" + Query(new Dictionary<string, string> { ["token"] = refresh }));
				using var response = await http.SendAsync(request, cancellation);
			}
			catch (HttpRequestException)
			{
				// Purity has already forgotten it; the person can also remove the access at Google
			}
		}

		/// <summary>Sends a call, and reports not reaching the service the same way everywhere</summary>
		internal async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
		{
			try
			{
				return await http.SendAsync(request, cancellation);
			}
			catch (HttpRequestException e)
			{
				throw new CalendarServiceException(CalendarProblem.NoConnection, e.Message);
			}
			catch (TaskCanceledException e) when (!cancellation.IsCancellationRequested)
			{
				throw new CalendarServiceException(CalendarProblem.NoConnection, e.Message);
			}
		}


		private string? RefreshToken => _refreshToken ??= secrets.Read();

		private async Task<GoogleTokens> RequestTokensAsync(Dictionary<string, string> form, CancellationToken cancellation)
		{
			form["client_id"] = client.Id;
			if (client.Secret != null)
				form["client_secret"] = client.Secret;

			using var request = new HttpRequestMessage(HttpMethod.Post, endpoints.Token) { Content = new FormUrlEncodedContent(form) };
			using var response = await SendAsync(request, cancellation);
			if (response.IsSuccessStatusCode)
				return await response.Content.ReadFromJsonAsync(GoogleJson.Default.GoogleTokens, cancellation)
					?? throw new CalendarServiceException(CalendarProblem.Refused, "Google's answer was empty");

			var error = await response.Content.ReadAsStringAsync(cancellation);
			if (error.Contains("invalid_grant", StringComparison.Ordinal))
			{
				Forget();									// it can't be used again, so the person has to sign in
				throw new CalendarServiceException(CalendarProblem.SignInNeeded, "The sign-in no longer works");
			}
			throw new CalendarServiceException(CalendarProblem.Refused, $"Sign-in refused: {(int)response.StatusCode}");
		}

		private void Remember(GoogleTokens tokens)
		{
			_accessToken = tokens.AccessToken;
			_accessTokenEnds = clock.GetUtcNow().AddSeconds(tokens.ExpiresIn - ExpiryMarginSeconds);
		}

		private void Forget()
		{
			_refreshToken = null;
			_accessToken = null;
			secrets.Delete();
		}

		private static string Query(Dictionary<string, string> values)
		{
			var text = new StringBuilder();
			foreach (var (key, value) in values)
				text.Append(text.Length > 0 ? "&" : string.Empty).Append(key).Append('=').Append(Uri.EscapeDataString(value));
			return text.ToString();
		}

		private static string Base64Url(ReadOnlySpan<byte> bytes) =>
			Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');


		private const int ExpiryMarginSeconds = 60;
		private static readonly TimeSpan SignInTime = TimeSpan.FromMinutes(5);

		private string? _refreshToken;
		private string? _accessToken;
		private DateTimeOffset _accessTokenEnds;


		/// <summary>The address on this computer that Google's sign-in page sends the browser back to</summary>
		private sealed class LoopbackRedirect : IDisposable
		{
			public LoopbackRedirect()
			{
				var probe = new TcpListener(IPAddress.Loopback, 0);
				probe.Start();
				var port = ((IPEndPoint)probe.LocalEndpoint).Port;
				probe.Stop();

				Address = $"http://127.0.0.1:{port}/";
				_listener.Prefixes.Add(Address);
				_listener.Start();
			}


			public string Address { get; }


			public async Task<NameValueCollection> WaitAsync(string finishedPage, CancellationToken cancellation)
			{
				await using (cancellation.Register(_listener.Stop))
				{
					while (true)
					{
						HttpListenerContext context;
						try
						{
							context = await _listener.GetContextAsync();
						}
						catch (Exception e) when (e is HttpListenerException or ObjectDisposedException or InvalidOperationException)
						{
							cancellation.ThrowIfCancellationRequested();
							throw new CalendarServiceException(CalendarProblem.Refused, "The sign-in couldn't be received");
						}

						if (context.Request.Url?.AbsolutePath != "/" || context.Request.QueryString.Count == 0)
						{
							context.Response.StatusCode = (int)HttpStatusCode.NotFound;	// the browser asking for an icon, say
							context.Response.Close();
							continue;
						}

						var page = Encoding.UTF8.GetBytes($"<!doctype html><meta charset=\"utf-8\"><title>OK</title><p style=\"font: 16px sans-serif; margin: 3em; text-align: center\">{WebUtility.HtmlEncode(finishedPage)}</p>");
						context.Response.ContentType = "text/html; charset=utf-8";
						context.Response.ContentLength64 = page.Length;
						await context.Response.OutputStream.WriteAsync(page, CancellationToken.None);
						context.Response.Close();
						return context.Request.QueryString;
					}
				}
			}

			public void Dispose() => ((IDisposable)_listener).Dispose();


			private readonly HttpListener _listener = new();
		}
	}
}
