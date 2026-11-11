using Purity.Core;
using Xunit;


namespace Purity.Reminders.Tests
{
	/// <summary>Bringing a calendar in line with the reminders wanted, on a calendar held in memory. Every date is made up.</summary>
	public sealed class CalendarSyncTests
	{
		[Fact]
		public async Task WhatsMissingIsAdded()
		{
			var calendar = new MemoryCalendar();

			var result = await Apply(calendar, Reminder("a", 3), Reminder("b", 4));

			Assert.Equal(new SyncResult(2, 0, 0), result);
			Assert.Equal(new[] { "a", "b" }, calendar.Tags);
		}

		[Fact]
		public async Task RunningAgainChangesNothing()
		{
			var calendar = new MemoryCalendar();
			var wanted = new[] { Reminder("a", 3), Reminder("b", 4) };
			await Apply(calendar, wanted);
			calendar.Writes = 0;

			var result = await Apply(calendar, wanted);

			Assert.Equal(new SyncResult(0, 0, 0), result);
			Assert.Equal(0, calendar.Writes);
		}

		[Fact]
		public async Task ANewTitleOrTimeChangesTheEntryInPlace()
		{
			var calendar = new MemoryCalendar();
			await Apply(calendar, Reminder("a", 3), Reminder("b", 4));

			var result = await Apply(calendar, Reminder("a", 3, "תור הערב"), Reminder("b", 5));

			Assert.Equal(new SyncResult(0, 2, 0), result);
			Assert.Equal(new[] { "a", "b" }, calendar.Tags);
			Assert.Equal("תור הערב", calendar.Entries.Single(el => el.Tag == "a").Title);
			Assert.Equal(Now.AddDays(5), calendar.Entries.Single(el => el.Tag == "b").Moment);
		}

		[Fact]
		public async Task WhatsNoLongerWantedIsDeleted()
		{
			var calendar = new MemoryCalendar();
			await Apply(calendar, Reminder("a", 3), Reminder("b", 4));

			var result = await Apply(calendar, Reminder("b", 4));

			Assert.Equal(new SyncResult(0, 0, 1), result);
			Assert.Equal(new[] { "b" }, calendar.Tags);
		}

		[Fact]
		public async Task NothingWantedEmptiesTheCalendar()
		{
			var calendar = new MemoryCalendar();
			await Apply(calendar, Reminder("a", 3), Reminder("b", -4));

			await Apply(calendar);

			Assert.Empty(calendar.Tags);
		}

		[Fact]
		public async Task AReminderThatHasFiredStaysButIsNotRewritten()
		{
			var calendar = new MemoryCalendar();
			calendar.Entries.Add(new CalendarEntry("a", "a", Now.AddDays(-1), "Appointment today"));

			var result = await Apply(calendar, Reminder("a", -1, "תור הערב"));

			Assert.Equal(new SyncResult(0, 0, 0), result);
			Assert.Equal("Appointment today", calendar.Entries.Single().Title);
		}

		[Fact]
		public async Task AReminderThatHasFiredIsNotAddedAfterTheFact()
		{
			var calendar = new MemoryCalendar();

			await Apply(calendar, Reminder("a", -1));

			Assert.Empty(calendar.Tags);
		}

		[Fact]
		public async Task ASecondCopyIsRemoved()
		{
			var calendar = new MemoryCalendar();
			calendar.Entries.Add(new CalendarEntry("one", "a", Now.AddDays(3), "Title a"));
			calendar.Entries.Add(new CalendarEntry("two", "a", Now.AddDays(3), "Title a"));

			var result = await Apply(calendar, Reminder("a", 3));

			Assert.Equal(new SyncResult(0, 0, 1), result);
			Assert.Equal("one", calendar.Entries.Single().ServiceId);
		}

		[Fact]
		public async Task ATestReminderStaysUntilItFires()
		{
			var calendar = new MemoryCalendar();
			calendar.Entries.Add(new CalendarEntry("t", ReminderTag.Test, Now.AddMinutes(2), "Test reminder"));

			await Apply(calendar);
			Assert.Single(calendar.Tags);

			await CalendarSync.ApplyAsync(Array.Empty<Reminder>(), Now.AddMinutes(3), calendar);
			Assert.Empty(calendar.Tags);
		}

		[Fact]
		public async Task AFailureInTheMiddleIsPutRightByTheNextRun()
		{
			var calendar = new MemoryCalendar { FailAfterWrites = 1 };
			var wanted = new[] { Reminder("a", 3), Reminder("b", 4) };

			await Assert.ThrowsAsync<CalendarServiceException>(() => Apply(calendar, wanted));
			Assert.Single(calendar.Tags);

			calendar.FailAfterWrites = null;
			await Apply(calendar, wanted);

			Assert.Equal(new[] { "a", "b" }, calendar.Tags);
		}

		[Fact]
		public void ATestReminderFiresOnAWholeMinuteSoon()
		{
			var moment = ReminderPlanner.TestReminder(ReminderLanguage.English, new DateTime(2031, 3, 2, 9, 30, 45)).Moment;

			Assert.Equal(new DateTime(2031, 3, 2, 9, 32, 0), moment);
			Assert.Equal("תזכורת בדיקה", ReminderPlanner.TestReminder(ReminderLanguage.Hebrew, Now).Title);
			Assert.Equal(ReminderTag.Test, ReminderPlanner.TestReminder(ReminderLanguage.English, Now).Tag);
		}

		[Theory]
		[InlineData("ref:pr1spauvisupfo274ehddao", "pr1spauvisupfo274ehddao")]
		[InlineData("some words\nref:pr1spauvisupfo274ehddao\nmore", "pr1spauvisupfo274ehddao")]
		[InlineData("<p>ref:pr1spauvisupfo274ehddao</p>", "pr1spauvisupfo274ehddao")]
		[InlineData("ref:pr2spauvisupfo274ehddao", null)]
		[InlineData("ref:pr1short", null)]
		[InlineData("a plain note", null)]
		[InlineData(null, null)]
		public void OnlyOurTagsAreRecognised(string? notes, string? tag) => Assert.Equal(tag, ReminderTag.FromNotes(notes));


		private static readonly DateTime Now = new(2031, 1, 1, 9, 0, 0);

		private static Reminder Reminder(string tag, int days, string? title = null) =>
			new(tag, Now.AddDays(days), title ?? $"Title {tag}", PurityEventType.Mikveh);

		private static Task<SyncResult> Apply(MemoryCalendar calendar, params Reminder[] wanted) => CalendarSync.ApplyAsync(wanted, Now, calendar);


		private sealed class MemoryCalendar : ICalendarService
		{
			public List<CalendarEntry> Entries { get; } = new();
			public int Writes { get; set; }
			public int? FailAfterWrites { get; set; }
			public string[] Tags => Entries.Select(el => el.Tag).ToArray();

			public Task<IReadOnlyList<CalendarEntry>> ListAsync(CancellationToken cancellation) =>
				Task.FromResult<IReadOnlyList<CalendarEntry>>(Entries.ToArray());

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
				if (Writes++ >= FailAfterWrites)
					throw new CalendarServiceException(CalendarProblem.NoConnection, "gone");
			}
		}
	}
}
