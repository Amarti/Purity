using Purity.Desktop.ViewModels;
using Purity.Reminders;


namespace Purity.Desktop.Tests
{
	/// <summary>A connection to a calendar held in memory</summary>
	internal sealed class MemoryConnection : IReminderConnection, ICalendarService, IChoiceStore
	{
		public bool IsAvailable { get; set; } = true;
		public bool IsConnected { get; set; } = true;
		public bool RemembersSignIn { get; set; } = true;
		public ICalendarService Calendar => this;
		public IChoiceStore Choices => this;
		public SharedChoices? Stored { get; set; }
		public int ChoiceWrites { get; private set; }

		public List<CalendarEntry> Entries { get; } = new();
		public CalendarServiceException? FailWith { get; set; }
		public int Reads { get; private set; }
		public int Writes { get; private set; }

		/// <summary>Whether signing in waits until it's given up on</summary>
		public bool SignInHangs { get; set; }


		public async Task ConnectAsync(CancellationToken cancellation)
		{
			if (SignInHangs)
				await Task.Delay(Timeout.Infinite, cancellation);
			Fail();
			IsConnected = true;
		}

		public Task DisconnectAsync(CancellationToken cancellation)
		{
			Fail();
			IsConnected = false;
			Entries.Clear();
			return Task.CompletedTask;
		}

		public Task ForgetAsync(CancellationToken cancellation)
		{
			Forgotten++;
			IsConnected = false;
			return Task.CompletedTask;
		}

		/// <summary>How many times the sign-in was forgotten without the calendar being removed</summary>
		public int Forgotten { get; private set; }

		public Task<SharedChoices?> ReadChoicesAsync(CancellationToken cancellation)
		{
			Fail();
			return Task.FromResult(Stored);
		}

		public Task WriteChoicesAsync(SharedChoices choices, CancellationToken cancellation)
		{
			Fail();
			ChoiceWrites++;
			Stored = choices;
			return Task.CompletedTask;
		}

		public Task<IReadOnlyList<CalendarEntry>> ListAsync(CancellationToken cancellation)
		{
			Reads++;
			Fail();
			return Task.FromResult<IReadOnlyList<CalendarEntry>>(Entries.ToArray());
		}

		public Task AddAsync(Reminder reminder, CancellationToken cancellation)
		{
			Wrote();
			Entries.Add(new CalendarEntry(reminder.Tag, reminder.Tag, reminder.Moment, reminder.Title));
			return Task.CompletedTask;
		}

		public Task ChangeAsync(CalendarEntry entry, Reminder reminder, CancellationToken cancellation)
		{
			Wrote();
			Entries[Entries.IndexOf(entry)] = entry with { Moment = reminder.Moment, Title = reminder.Title };
			return Task.CompletedTask;
		}

		public Task DeleteAsync(CalendarEntry entry, CancellationToken cancellation)
		{
			Wrote();
			Entries.Remove(entry);
			return Task.CompletedTask;
		}


		private void Wrote()
		{
			Writes++;
			Fail();
		}

		private void Fail()
		{
			if (FailWith != null)
				throw FailWith;
		}
	}
}
