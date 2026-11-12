using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace Purity.Reminders
{
	/// <param name="ServiceId">What the service calls this entry</param>
	/// <param name="Tag">What marks it as ours, and which reminder it is</param>
	/// <param name="Moment">When it fires, on the local clock</param>
	/// <param name="Title">What it says</param>
	public sealed record CalendarEntry(string ServiceId, string Tag, DateTime Moment, string Title);


	/// <summary>A calendar that holds reminders. Whatever isn't tagged as ours is invisible here, and so can't be touched.</summary>
	public interface ICalendarService
	{
		Task<IReadOnlyList<CalendarEntry>> ListAsync(CancellationToken cancellation);
		Task AddAsync(Reminder reminder, CancellationToken cancellation);
		Task ChangeAsync(CalendarEntry entry, Reminder reminder, CancellationToken cancellation);
		Task DeleteAsync(CalendarEntry entry, CancellationToken cancellation);
	}


	public enum CalendarProblem
	{
		/// <summary>The service couldn't be reached. The next run catches up.</summary>
		NoConnection,

		/// <summary>The sign-in is gone or no longer works, and the person has to sign in again</summary>
		SignInNeeded,

		/// <summary>The person turned the sign-in down, or didn't finish it in time</summary>
		SignInNotFinished,

		/// <summary>The service answered, and refused</summary>
		Refused,

		/// <summary>Signing in, the user name or the password wasn't accepted</summary>
		PasswordRefused,

		/// <summary>Signing in, nothing at the address offers calendars</summary>
		NoCalendarService,

		/// <summary>The address doesn't use https, so the password would be sent in the open</summary>
		NotSecure,
	}


	public sealed class CalendarServiceException(CalendarProblem problem, string detail) : Exception(detail)
	{
		public CalendarProblem Problem { get; } = problem;
	}


	/// <param name="Added">Entries added</param>
	/// <param name="Changed">Entries changed in place</param>
	/// <param name="Removed">Entries deleted</param>
	public sealed record SyncResult(int Added, int Changed, int Removed);


	/// <summary>
	/// Brings a calendar in line with the reminders wanted, by reading what's there and sending only
	/// the differences. There's no record of what was sent before, so nothing can go out of step:
	/// after an interrupted run, a reinstall or a run on another device, the next one puts it right.
	/// </summary>
	public static class CalendarSync
	{
		/// <param name="wanted">Every reminder of the newest period, fired or not</param>
		/// <param name="now">The time now, on the local clock</param>
		/// <param name="calendar">The calendar to bring in line</param>
		/// <param name="cancellation">Stops between entries. What's done stays done.</param>
		public static async Task<SyncResult> ApplyAsync(IReadOnlyList<Reminder> wanted, DateTime now, ICalendarService calendar, CancellationToken cancellation = default)
		{
			var present = await calendar.ListAsync(cancellation);
			var wantedTags = wanted.Select(el => el.Tag).ToHashSet(StringComparer.Ordinal);
			var kept = new Dictionary<string, CalendarEntry>(StringComparer.Ordinal);
			var unwanted = new List<CalendarEntry>();
			foreach (var entry in present)
			{
				var pendingTest = entry.Tag == ReminderTag.Test && entry.Moment >= now;
				if ((wantedTags.Contains(entry.Tag) || pendingTest) && kept.TryAdd(entry.Tag, entry))
					continue;

				unwanted.Add(entry);						// no longer wanted, or a second copy of one that is
			}

			foreach (var entry in unwanted)
				await calendar.DeleteAsync(entry, cancellation);

			int added = 0, changed = 0;
			foreach (var reminder in wanted.Where(el => el.Moment >= now))
			{
				if (!kept.TryGetValue(reminder.Tag, out var entry))
				{
					await calendar.AddAsync(reminder, cancellation);
					added++;
				}
				else if (entry.Moment != reminder.Moment || entry.Title != reminder.Title)
				{
					await calendar.ChangeAsync(entry, reminder, cancellation);
					changed++;
				}
			}
			return new SyncResult(added, changed, unwanted.Count);
		}
	}


	/// <summary>Keeps a secret, such as a sign-in, in the system's secure store</summary>
	public interface ISecretStore
	{
		string? Read();

		/// <returns>False when this computer has nowhere safe to keep it</returns>
		bool Write(string secret);

		void Delete();
	}
}
