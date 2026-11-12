using Purity.Desktop.ViewModels;
using Purity.Reminders;
using Purity.Reminders.CalDav;


namespace Purity.Desktop
{
	/// <summary>This device's connection to iCloud or another CalDAV server, signed in with a user name and password</summary>
	public sealed class CalDavConnection : IReminderConnection
	{
		public static CalDavConnection ForICloud(DesktopSettings settings) =>
			new(CalDavSignIn.ICloud, SecretStores.ForICloud(), () => settings.ICloudCalendar, value => Keep(settings, () => settings.ICloudCalendar = value), settings, Http);

		/// <summary>Another server, at the address the person gives</summary>
		public static CalDavConnection ForOther(DesktopSettings settings) =>
			new(null, SecretStores.ForCalDav(), () => settings.CalDavCalendar, value => Keep(settings, () => settings.CalDavCalendar = value), settings, Http);

		/// <param name="server">The service's own address, or null for one the person gives</param>
		/// <param name="secrets">Where the sign-in is kept</param>
		/// <param name="getCalendar">The calendar's address, as kept on this device</param>
		/// <param name="setCalendar">Keeps the calendar's address on this device</param>
		/// <param name="settings">Holds the name for a new calendar</param>
		/// <param name="http">Must not follow redirects</param>
		public CalDavConnection(Uri? server, ISecretStore secrets, Func<string?> getCalendar, Action<string?> setCalendar, DesktopSettings settings, HttpClient http)
		{
			_server = server;
			_setCalendar = setCalendar;
			_account = new CalDavAccount(http, secrets);
			_calendar = new CalDavCalendar(_account, getCalendar, setCalendar, () => CalendarName(settings), TimeZoneInfo.Local);
		}


		public bool IsAvailable => true;

		public bool IsConnected => _account.IsSignedIn;

		public bool RemembersSignIn => _account.RemembersSignIn;

		public bool TakesPassword => true;

		public ICalendarService Calendar => _calendar;

		public IChoiceStore Choices => _calendar;


		public async Task ConnectAsync(ReminderSignIn? signIn, CancellationToken cancellation)
		{
			if (signIn == null)
				throw new ArgumentNullException(nameof(signIn), "A CalDAV server takes a user name and password");

			var server = _server ?? Address(signIn.Server);
			await _account.SignInAsync(new CalDavSignIn(server, signIn.User.Trim(), _server == null ? signIn.Password : signIn.Password.Trim()), cancellation);
			_setCalendar(null);							// perhaps another account, so its calendar is looked for afresh
		}

		public async Task DisconnectAsync(CancellationToken cancellation)
		{
			try
			{
				await _calendar.DeleteCalendarAsync(cancellation);
			}
			catch (CalendarServiceException e) when (e.Problem == CalendarProblem.SignInNeeded)
			{
				// The password no longer works, so the calendar can only be removed in a calendar app. Forgetting still goes ahead.
			}
			_account.SignOut();
			_setCalendar(null);
		}

		public Task ForgetAsync(CancellationToken cancellation)
		{
			_account.SignOut();
			_setCalendar(null);
			return Task.CompletedTask;
		}


		/// <summary>The address as typed, which needn't start with https://, or a problem when it isn't one</summary>
		internal static Uri Address(string typed)
		{
			var text = typed.Trim();
			if (!text.Contains("://", StringComparison.Ordinal))
				text = "https://" + text;
			if (!Uri.TryCreate(text, UriKind.Absolute, out var address) || address.Scheme is not ("https" or "http") || address.Host.Length == 0)
				throw new CalendarServiceException(CalendarProblem.NoCalendarService, "That isn't an address");
			return address;
		}

		private static string CalendarName(DesktopSettings settings) =>
			string.IsNullOrWhiteSpace(settings.Reminders.CalendarName) ? ReminderSetup.DefaultCalendarName : settings.Reminders.CalendarName;

		private static void Keep(DesktopSettings settings, Action set)
		{
			set();
			try
			{
				settings.Save();
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				// Kept for this run. The settings report their own trouble when they're next saved.
			}
		}


		private static readonly HttpClient Http = CalDavAccount.NewHttpClient();

		private readonly Uri? _server;
		private readonly Action<string?> _setCalendar;
		private readonly CalDavAccount _account;
		private readonly CalDavCalendar _calendar;
	}
}
