using Purity.Desktop.Localization;
using Purity.Reminders;


namespace Purity.Desktop.ViewModels
{
	/// <summary>This device's connection to a calendar service, so the view models can be tested without one</summary>
	public interface IReminderConnection
	{
		/// <summary>Whether this build can connect at all</summary>
		bool IsAvailable { get; }

		bool IsConnected { get; }

		/// <summary>False when this computer has nowhere safe to keep the sign-in</summary>
		bool RemembersSignIn { get; }

		/// <summary>Whether signing in takes a user name and password typed in the window, rather than the service's own page</summary>
		bool TakesPassword { get; }

		ICalendarService Calendar { get; }

		/// <summary>Where the calendar keeps the choices every computer shares</summary>
		IChoiceStore Choices { get; }

		/// <param name="signIn">The user name and password, for a service that takes them</param>
		Task ConnectAsync(ReminderSignIn? signIn, CancellationToken cancellation);

		/// <summary>Removes the calendar and ends the sign-in</summary>
		Task DisconnectAsync(CancellationToken cancellation);

		/// <summary>Ends the sign-in and leaves the calendar alone. Forgetting the sign-in here always happens, even offline.</summary>
		Task ForgetAsync(CancellationToken cancellation);
	}


	/// <param name="Server">The server's address, for a service that has no address of its own</param>
	/// <param name="User">The Apple Account or user name</param>
	/// <param name="Password">The password</param>
	public sealed record ReminderSignIn(string Server, string User, string Password)
	{
		/// <summary>Not the password, which never goes into a log or a message</summary>
		public override string ToString() => $"{User} at {Server}";
	}


	/// <summary>This device's connection to each calendar service there can be one to</summary>
	public sealed class ReminderConnections(IReadOnlyDictionary<ReminderPlace, IReminderConnection> connections)
	{
		/// <summary>One service's connection alone, as for a test</summary>
		public static ReminderConnections Of(ReminderPlace place, IReminderConnection connection) =>
			new(new Dictionary<ReminderPlace, IReminderConnection> { [place] = connection });


		/// <summary>The connection to where reminders go, or null for a file, or a service this build has nothing for</summary>
		public IReminderConnection? For(ReminderPlace place) => connections.GetValueOrDefault(place);

		public IEnumerable<KeyValuePair<ReminderPlace, IReminderConnection>> All => connections;
	}


	/// <param name="Text">What to say</param>
	/// <param name="Kind">How it's shown</param>
	public sealed record ReminderNotice(string Text, MessageKind Kind);


	internal static class ReminderProblems
	{
		/// <summary>What to tell the person about a calendar that couldn't be reached or updated</summary>
		public static string Say(CalendarProblem problem, ReminderPlace place) => (problem, place) switch
		{
			(CalendarProblem.NotSecure, _) => Loc.T.RemindersNotSecure,
			(CalendarProblem.NoCalendarService, _) => Loc.T.RemindersNoCalendarService,
			(CalendarProblem.NoConnection, ReminderPlace.ICloud) => Loc.T.RemindersICloudNoConnection,
			(CalendarProblem.NoConnection, ReminderPlace.CalDav) => Loc.T.RemindersCalDavNoConnection,
			(CalendarProblem.NoConnection, _) => Loc.T.RemindersNoConnection,
			(CalendarProblem.SignInNeeded, ReminderPlace.ICloud) => Loc.T.RemindersICloudSignInNeeded,
			(CalendarProblem.SignInNeeded, ReminderPlace.CalDav) => Loc.T.RemindersCalDavSignInNeeded,
			(CalendarProblem.SignInNeeded, _) => Loc.T.RemindersSignInNeeded,
			(CalendarProblem.PasswordRefused, ReminderPlace.ICloud) => Loc.T.RemindersICloudPasswordRefused,
			(CalendarProblem.PasswordRefused, _) => Loc.T.RemindersCalDavPasswordRefused,
			(CalendarProblem.SignInNotFinished, _) => Loc.T.RemindersSignInNotFinished,
			(_, ReminderPlace.ICloud) => Loc.T.RemindersICloudFailed,
			(_, ReminderPlace.CalDav) => Loc.T.RemindersCalDavFailed,
			_ => Loc.T.RemindersCalendarFailed,
		};
	}
}
