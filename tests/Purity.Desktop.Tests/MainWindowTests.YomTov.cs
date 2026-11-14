using Purity.Core;
using Purity.Desktop.ViewModels;
using Purity.Reminders;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>
	/// Which days of Yom Tov move alerts: one, as in Israel, or two. The reminders' dates are made
	/// up; Pesach 2031 is where the calendar puts it, on Tuesday 8 April, and outside Israel the 9th too.
	/// </summary>
	public sealed partial class MainWindowTests
	{
		[Fact]
		public void TheWindowStartsFromTheComputersTimeZone()
		{
			Assert.True(YomTovReminders(new ReminderSetup(), Israel).IsInIsrael);
			Assert.True(YomTovReminders(new ReminderSetup(), null).IsOutsideIsrael);
		}

		[Fact]
		public void OnceChosenTheTimeZoneNoLongerDecides()
		{
			var reminders = YomTovReminders(new ReminderSetup { InIsrael = false }, Israel);

			Assert.True(reminders.IsOutsideIsrael);
			Assert.False(reminders.IsInIsrael);
		}

		[Fact]
		public void OkKeepsWhatWasShownEvenIfOnlyTheTimeZoneChoseIt()
		{
			ReminderSetup? kept = null;
			var reminders = new RemindersWindowViewModel(new ReminderSetup(), new[] { SecondDayOfPesach() }, new ClockAt(new DateTime(2031, 1, 1), Israel), el => kept = el);

			reminders.Accept();
			Assert.True(kept!.InIsrael);

			reminders.IsOutsideIsrael = true;
			reminders.Accept();
			Assert.False(kept.InIsrael);
		}

		[Fact]
		public void ThePreviewFollowsWhichDaysAreKept()
		{
			var reminders = YomTovReminders(new ReminderSetup(), null);
			Assert.Equal("Appointment Wednesday night", reminders.Preview.Single().Title);
			Assert.Equal("Monday, 7 April 2031, 08:00", reminders.Preview.Single().When);

			reminders.IsInIsrael = true;

			Assert.Equal("Appointment today", reminders.Preview.Single().Title);
			Assert.Equal("Wednesday, 9 April 2031, 08:00", reminders.Preview.Single().When);
		}

		[Fact]
		public async Task ConnectingTakesUpTheCalendarsDaysOfYomTov()
		{
			var stored = new SharedChoices(new HashSet<PurityEventType> { PurityEventType.Mikveh }, ReminderWording.Discreet, new TimeOnly(8, 0), new TimeOnly(17, 0), true, ReminderLanguage.English, InIsrael: true);
			var reminders = RemindersWith(new MemoryConnection { IsConnected = false, Stored = stored });
			Assert.True(reminders.IsOutsideIsrael);

			await reminders.ConnectAsync();

			Assert.True(reminders.IsInIsrael);
		}

		[Fact]
		public async Task AnEarlierVersionsCalendarLeavesTheDaysShown()
		{
			var reminders = RemindersWith(new MemoryConnection { IsConnected = false, Stored = Choices(ReminderWording.Detailed, ReminderLanguage.English, PurityEventType.Mikveh) });

			await reminders.ConnectAsync();

			Assert.True(reminders.IsDetailed);
			Assert.True(reminders.IsOutsideIsrael);
		}

		[Fact]
		public void TheSharedCodeSaysWhichDaysAreKept()
		{
			Assert.EndsWith(" y1", new ReminderSetup().ToShared(ReminderLanguage.English, Israel).Format());
			Assert.EndsWith(" y2", new ReminderSetup().ToShared(ReminderLanguage.English, TimeZoneInfo.Utc).Format());
			Assert.EndsWith(" y2", new ReminderSetup { InIsrael = false }.ToShared(ReminderLanguage.English, Israel).Format());
		}

		[Fact]
		public void TakingUpChoicesWithoutYomTovKeepsThisComputersOwn()
		{
			var setup = new ReminderSetup { InIsrael = true };

			setup.Adopt(Choices(ReminderWording.Detailed, ReminderLanguage.English, PurityEventType.Mikveh));
			Assert.True(setup.InIsrael);

			setup.Adopt(Choices(ReminderWording.Detailed, ReminderLanguage.English, PurityEventType.Mikveh) with { InIsrael = false });
			Assert.False(setup.InIsrael);
		}


		private static readonly TimeZoneInfo Israel = TimeZoneInfo.CreateCustomTimeZone("Asia/Jerusalem", TimeSpan.FromHours(2), "Israel", "Israel");

		/// <param name="zone">The computer's time zone, or null for UTC</param>
		private static RemindersWindowViewModel YomTovReminders(ReminderSetup setup, TimeZoneInfo? zone) =>
			new(setup, new[] { SecondDayOfPesach() }, new ClockAt(new DateTime(2031, 1, 1), zone));

		/// <summary>A closed period whose mikveh is on the night after the second day of Pesach, Yom Tov only outside Israel</summary>
		private static PurityPeriod SecondDayOfPesach()
		{
			var period = new PurityPeriod(new DateTimeOffset(2031, 3, 10, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2031, 3, 15, 0, 0, 0, TimeSpan.Zero)) { Closed = true };
			period.SubEvents.Add(new PurityEvent(PurityEventType.Mikveh, Stamp(new DateTime(2031, 4, 9), true)));
			return period;
		}
	}
}
