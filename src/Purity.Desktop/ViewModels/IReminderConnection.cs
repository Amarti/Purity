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

		ICalendarService Calendar { get; }

		/// <summary>Where the calendar keeps the choices every computer shares</summary>
		IChoiceStore Choices { get; }

		Task ConnectAsync(CancellationToken cancellation);

		/// <summary>Removes the calendar and ends the sign-in</summary>
		Task DisconnectAsync(CancellationToken cancellation);
	}


	/// <param name="Text">What to say</param>
	/// <param name="Kind">How it's shown</param>
	public sealed record ReminderNotice(string Text, MessageKind Kind);


	internal static class ReminderProblems
	{
		/// <summary>What to tell the person about a calendar that couldn't be updated</summary>
		public static string Say(CalendarProblem problem) => problem switch
		{
			CalendarProblem.NoConnection => Loc.T.RemindersNoConnection,
			CalendarProblem.SignInNeeded => Loc.T.RemindersSignInNeeded,
			CalendarProblem.SignInNotFinished => Loc.T.RemindersSignInNotFinished,
			_ => Loc.T.RemindersCalendarFailed,
		};
	}
}
