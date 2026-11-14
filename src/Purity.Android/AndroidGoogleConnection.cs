using Android.App;
using Android.Content;
using Android.Gms.Auth.Api.Identity;
using Android.Gms.Common;
using Android.Gms.Common.Apis;
using Android.Gms.Extensions;
using Purity.Reminders;
using Purity.Reminders.Google;
using Purity.UI;
using Purity.UI.ViewModels;


namespace Purity.Android
{
	/// <summary>
	/// The phone's connection to a Google account, through Android's own authorization rather than the
	/// desktop's browser sign-in. The person agrees once, on Android's own sheet; after that Play
	/// services hands out tokens by itself, so no lasting token is kept on the phone. Purity keeps only
	/// which account agreed, encrypted, so it knows it's connected and can take the agreement back.
	/// Google must know the app: an Android client in the Google Cloud project, with this package
	/// name and its signing certificate's SHA-1.
	/// </summary>
	internal sealed class AndroidGoogleConnection : IReminderConnection, IGoogleSignIn
	{
		public static AndroidGoogleConnection For(DesktopSettings settings) => new(settings, SecretStores.ForGoogle());


		private AndroidGoogleConnection(DesktopSettings settings, ISecretStore secrets)
		{
			_secrets = secrets;
			_calendar = new GoogleCalendar(this, () => settings.GoogleCalendarId, id => Keep(settings, id), () => CalendarName(settings), TimeZoneInfo.Local);
		}


		/// <summary>Only where Play services is, as on most Android phones, but not on every one</summary>
		public bool IsAvailable =>
			global::Android.App.Application.Context is { } context && GoogleApiAvailability.Instance.IsGooglePlayServicesAvailable(context) == ConnectionResult.Success;

		public bool IsConnected => Account != null;

		public bool RemembersSignIn { get; private set; } = true;

		public bool TakesPassword => false;

		public ICalendarService Calendar => _calendar;

		public IChoiceStore Choices => _calendar;

		public GoogleEndpoints Endpoints => GoogleEndpoints.Real;


		public async Task ConnectAsync(ReminderSignIn? signIn, CancellationToken cancellation)
		{
			await AuthorizeAsync(askThePerson: true, cancellation);
			RemembersSignIn = _secrets.Write(_account ?? AnAccount);
		}

		public async Task DisconnectAsync(CancellationToken cancellation)
		{
			try
			{
				await _calendar.DeleteCalendarAsync(cancellation);
			}
			catch (CalendarServiceException e) when (e.Problem == CalendarProblem.SignInNeeded)
			{
				// The agreement is already gone, so the calendar can only be removed at Google. Forgetting still goes ahead.
			}
			await ForgetAsync(cancellation);
		}

		/// <summary>Takes the agreement back at Google, where that can be done, and forgets it here in any case</summary>
		public async Task ForgetAsync(CancellationToken cancellation)
		{
			var account = Account;
			_secrets.Delete();
			_account = null;
			_read = true;
			var token = _token;
			_token = null;
			if (MainActivity.Current is not { } activity)
				return;

			try
			{
				var client = Identity.GetAuthorizationClient(activity);
				if (token != null)
					await client.ClearToken(ClearTokenRequest.InvokeBuilder().SetToken(token).Build()).AsAsync();
				if (account != null && account != AnAccount)
					await client.RevokeAccess(RevokeAccessRequest.InvokeBuilder().SetAccount(new global::Android.Accounts.Account(account, "com.google")).SetScopes(Scopes()).Build()).AsAsync();
			}
			catch (Exception e) when (e is Java.Lang.Exception or InvalidOperationException)
			{
				// Forgotten here; the agreement can still be taken back in the Google account's own settings
			}
		}

		public async Task<string> AccessTokenAsync(CancellationToken cancellation)
		{
			if (_token != null && DateTimeOffset.UtcNow < _tokenEnds)
				return _token;
			if (!IsConnected)
				throw new CalendarServiceException(CalendarProblem.SignInNeeded, "Nobody is signed in");

			return await AuthorizeAsync(askThePerson: false, cancellation);
		}

		/// <summary>Google turned the token down, so Play services is told to stop handing it out</summary>
		public void ForgetAccessToken()
		{
			if (_token is { } token && MainActivity.Current is { } activity)
				_ = Identity.GetAuthorizationClient(activity).ClearToken(ClearTokenRequest.InvokeBuilder().SetToken(token).Build());
			_token = null;
		}

		public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
		{
			try
			{
				return await Http.SendAsync(request, cancellation);
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


		/// <summary>Which account agreed, as kept on this phone, or null</summary>
		private string? Account
		{
			get
			{
				if (_account == null && _read)
				{
					_read = false;
					_account = _secrets.Read();
				}
				return _account;
			}
		}

		/// <summary>A token from Play services. Unless the person is there to agree, one that needs them counts as signed out.</summary>
		private async Task<string> AuthorizeAsync(bool askThePerson, CancellationToken cancellation)
		{
			var activity = MainActivity.Current ?? throw new CalendarServiceException(CalendarProblem.SignInNeeded, "Purity isn't on the screen");
			try
			{
				var client = Identity.GetAuthorizationClient(activity);
				var request = AuthorizationRequest.InvokeBuilder().SetRequestedScopes(Scopes()).Build();
				var result = (AuthorizationResult)await client.Authorize(request).AsAsync<AuthorizationResult>();
				if (result.HasResolution)
				{
					if (!askThePerson)
						throw new CalendarServiceException(CalendarProblem.SignInNeeded, "Google wants the person to agree again");

					var answer = await AskAsync(activity, result.PendingIntent!, cancellation);
					result = client.GetAuthorizationResultFromIntent(answer);
				}

#pragma warning disable CS0618			// deprecated, but still the only way to learn which account agreed, which taking it back needs
				_account = result.ToGoogleSignInAccount()?.Account?.Name ?? _account;
#pragma warning restore CS0618
				_token = result.AccessToken ?? throw new CalendarServiceException(CalendarProblem.SignInNotFinished, "Google gave no token");
				_tokenEnds = DateTimeOffset.UtcNow.Add(TokenLife);
				return _token;
			}
			catch (ApiException e)
			{
				throw new CalendarServiceException(e.StatusCode switch
				{
					CommonStatusCodes.NetworkError => CalendarProblem.NoConnection,
					CommonStatusCodes.Canceled => CalendarProblem.SignInNotFinished,
					_ => CalendarProblem.Refused,			// 10 is an app Google doesn't know: no Android client in the project yet
				}, $"Google's authorization failed with status {e.StatusCode}");
			}
		}

		/// <summary>Shows Android's sheet where the person agrees, and waits for their answer</summary>
		private static async Task<Intent?> AskAsync(MainActivity activity, PendingIntent ask, CancellationToken cancellation)
		{
			var answered = new TaskCompletionSource<Intent?>(TaskCreationOptions.RunContinuationsAsynchronously);
			void OnResult(int code, Result result, Intent? data)
			{
				if (code != RequestCode)
					return;
				if (result == Result.Ok)
					answered.TrySetResult(data);
				else
					answered.TrySetException(new CalendarServiceException(CalendarProblem.SignInNotFinished, "The person didn't agree"));
			}

			activity.ActivityResult += OnResult;
			try
			{
				using var giveUp = cancellation.Register(() => answered.TrySetCanceled(cancellation));
				activity.StartIntentSenderForResult(ask.IntentSender, RequestCode, null, 0, 0, 0);
				return await answered.Task;
			}
			finally
			{
				activity.ActivityResult -= OnResult;
			}
		}

		/// <summary>The same two permissions as on the desktop: calendars Purity made, and the list of calendars to find one by</summary>
		private static List<Scope> Scopes() => GoogleAccount.Scope.Split(' ').Select(el => new Scope(el)).ToList();

		private static string CalendarName(DesktopSettings settings) =>
			string.IsNullOrWhiteSpace(settings.Reminders.CalendarName) ? ReminderSetup.DefaultCalendarName : settings.Reminders.CalendarName;

		private static void Keep(DesktopSettings settings, string? calendarId)
		{
			settings.GoogleCalendarId = calendarId;
			try
			{
				settings.Save();
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				// Kept for this run. The settings report their own trouble when they're next saved.
			}
		}


		/// <summary>Kept when Google didn't say which account agreed, so the phone still knows it's connected</summary>
		private const string AnAccount = "connected";

		private const int RequestCode = 0x5075;			// "Pu"

		/// <summary>Google's tokens last an hour; this one is used for less, so it's never out of date mid-run</summary>
		private static readonly TimeSpan TokenLife = TimeSpan.FromMinutes(45);

		private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

		private readonly ISecretStore _secrets;
		private readonly GoogleCalendar _calendar;
		private string? _account;
		private bool _read = true;
		private string? _token;
		private DateTimeOffset _tokenEnds;
	}
}
