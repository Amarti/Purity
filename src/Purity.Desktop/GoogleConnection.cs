using System.Reflection;
using Purity.Desktop.Localization;
using Purity.Desktop.ViewModels;
using Purity.Reminders;
using Purity.Reminders.Google;


namespace Purity.Desktop
{
	/// <summary>This device's connection to a Google account</summary>
	public sealed class GoogleConnection : IReminderConnection
	{
		/// <summary>The connection this build can make. Its app ID comes from the build, and without one Google isn't offered.</summary>
		public static GoogleConnection ForThisBuild(StartOptions options, DesktopSettings settings, Func<Uri, Task> openBrowser)
		{
#if DEBUG
			if (options.GoogleStandIn is { } standIn)
				return new GoogleConnection(new GoogleClient("stand-in-client"), new GoogleEndpoints(new Uri(standIn, "o/oauth2/v2/auth"), new Uri(standIn, "token"), new Uri(standIn, "revoke"), new Uri(standIn, "calendar/v3/")), SecretStores.ForGoogle(), settings, VisitWithoutABrowserAsync);
#endif
			return new GoogleConnection(ClientOfThisBuild(), GoogleEndpoints.Real, SecretStores.ForGoogle(), settings, openBrowser);
		}

		/// <param name="client">The app's ID at Google, or null when this build has none</param>
		/// <param name="endpoints">Where Google is</param>
		/// <param name="secrets">Where the sign-in is kept</param>
		/// <param name="settings">Holds the calendar's ID, and the name for a new one</param>
		/// <param name="openBrowser">Opens Google's sign-in page</param>
		public GoogleConnection(GoogleClient? client, GoogleEndpoints endpoints, ISecretStore secrets, DesktopSettings settings, Func<Uri, Task> openBrowser)
		{
			_openBrowser = openBrowser;
			if (client == null)
				return;

			_account = new GoogleAccount(Http, client, endpoints, secrets, TimeProvider.System);
			_calendar = new GoogleCalendar(_account, () => settings.GoogleCalendarId, id => Keep(settings, id), () => CalendarName(settings), TimeZoneInfo.Local);
		}


		public bool IsAvailable => _account != null;

		public bool IsConnected => _account?.IsSignedIn == true;

		public bool RemembersSignIn => _account?.RemembersSignIn != false;

		public bool TakesPassword => false;

		public ICalendarService Calendar => _calendar ?? throw new InvalidOperationException("This build has no Google connection");

		public IChoiceStore Choices => _calendar ?? throw new InvalidOperationException("This build has no Google connection");


		public async Task ConnectAsync(ReminderSignIn? signIn, CancellationToken cancellation)
		{
			if (_account != null)
				await _account.SignInAsync(_openBrowser, Loc.T.RemindersSignInDone, cancellation);
		}

		public async Task DisconnectAsync(CancellationToken cancellation)
		{
			if (_account == null || _calendar == null)
				return;

			try
			{
				await _calendar.DeleteCalendarAsync(cancellation);
			}
			catch (CalendarServiceException e) when (e.Problem == CalendarProblem.SignInNeeded)
			{
				// The sign-in is already gone, so the calendar can only be removed at Google. Forgetting still goes ahead.
			}
			await _account.SignOutAsync(cancellation);
		}

		public async Task ForgetAsync(CancellationToken cancellation)
		{
			if (_account != null)
				await _account.SignOutAsync(cancellation);
		}


		private static GoogleClient? ClientOfThisBuild()
		{
			var values = typeof(GoogleConnection).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().ToDictionary(el => el.Key, el => el.Value);
			return values.GetValueOrDefault("GoogleClientId") is { Length: > 0 } id ? new GoogleClient(id, values.GetValueOrDefault("GoogleClientSecret")) : null;
		}

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

#if DEBUG
		/// <summary>The stand-in approves every sign-in, so nobody's browser needs to open for it</summary>
		private static async Task VisitWithoutABrowserAsync(Uri page)
		{
			using var response = await Http.GetAsync(page);
		}
#endif


		private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

		private readonly Func<Uri, Task> _openBrowser;
		private readonly GoogleAccount? _account;
		private readonly GoogleCalendar? _calendar;
	}
}
