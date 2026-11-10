using Purity.Core;
using Purity.Desktop.Localization;
using Purity.Desktop.ViewModels;
using Purity.Reminders;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>The choices in the Reminders window. Every date is made up.</summary>
	public sealed partial class MainWindowTests
	{
		// 2 March 2031 is a Sunday
		private static readonly DateTime SundayMarch2 = new(2031, 3, 2);


		[Fact]
		public void RemindersStartAsMikvehOnlyAndDiscreet()
		{
			var reminders = Reminders();

			Assert.True(reminders.IsMikvehOnly);
			Assert.True(reminders.IsDiscreet);
			Assert.Equal("Appointment today", reminders.Preview.Single().Title);
			Assert.Null(reminders.PreviewNote);
			Assert.True(reminders.CanSaveFile);
		}

		[Fact]
		public void ThePreviewFollowsTheChoices()
		{
			var reminders = Reminders();

			reminders.IsAllDates = true;
			Assert.Equal(new[] { "Reminder O, from Saturday night, full day", "Appointment today", "Reminder H, tonight" }, reminders.Preview.Select(el => el.Title));

			reminders.IsDetailed = true;
			Assert.Equal("Mikveh today", reminders.Preview[1].Title);

			reminders.IsChosen = true;
			reminders.RemindMikveh = false;
			Assert.Empty(reminders.Preview);
			Assert.False(reminders.CanSaveFile);

			reminders.RemindVesetAflaga = true;
			Assert.Equal("Veset Aflaga tonight", reminders.Preview.Single().Title);
		}

		[Fact]
		public void ThePreviewSaysWhenInTheLanguageShown()
		{
			var reminders = Reminders();

			Assert.Equal("Sunday, 2 March 2031, 08:00", reminders.Preview.Single().When);

			reminders.MikvehTime = "09:30";
			Assert.Equal("Sunday, 2 March 2031, 09:30", reminders.Preview.Single().When);
		}

		[Fact]
		public Task HebrewWordingFollowsTheLanguageShown() => Ui.Run(() =>
		{
			Loc.Instance.Use(Language.Hebrew);
			try
			{
				var reminders = Reminders();

				Assert.Equal("תור הערב", reminders.Preview.Single().Title);
				Assert.Contains("BEGIN:VEVENT", reminders.BuildFile());
				Assert.Contains("X-WR-CALNAME:אישי", reminders.BuildFile());
			}
			finally
			{
				Loc.Instance.Use(Language.English);
			}
		});

		[Fact]
		public void TheMoveOffShabbatCanBeTurnedOff()
		{
			var reminders = Reminders(Saturday());
			Assert.Equal("Friday, 7 March 2031, 08:00", reminders.Preview.Single().When);

			reminders.MoveOffShabbat = false;

			Assert.Equal("Saturday, 8 March 2031, 08:00", reminders.Preview.Single().When);
		}

		[Fact]
		public void AnOpenPeriodExplainsWhyThereAreNoReminders()
		{
			var open = new PurityPeriod(new DateTimeOffset(2031, 2, 25, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2031, 3, 4, 0, 0, 0, TimeSpan.Zero));

			var reminders = Reminders(DatedPeriod(), open);

			Assert.Empty(reminders.Preview);
			Assert.Equal(UiText.English.RemindersOpenPeriod, reminders.PreviewNote);
			Assert.False(reminders.CanSaveFile);
		}

		[Fact]
		public void NothingToComeSaysSo()
		{
			var reminders = new RemindersWindowViewModel(new ReminderSetup(), new[] { DatedPeriod() }, new ClockAt(new DateTime(2032, 1, 1)));

			Assert.Equal(UiText.English.RemindersNone, reminders.PreviewNote);
		}

		[Fact]
		public void TheFileHoldsWhatThePreviewShowsAndTheNameChosen()
		{
			var reminders = Reminders();

			var usual = reminders.BuildFile();
			reminders.CalendarName = "  Family  ";
			var named = reminders.BuildFile();

			Assert.Contains("SUMMARY:Appointment today", usual);
			Assert.Contains("X-WR-CALNAME:Personal", usual);
			Assert.Contains("X-WR-CALNAME:Family", named);
		}

		[Fact]
		public void NothingIsKeptUntilOk()
		{
			ReminderSetup? kept = null;
			var reminders = new RemindersWindowViewModel(new ReminderSetup(), new[] { DatedPeriod() }, new ClockAt(new DateTime(2031, 1, 1)), el => kept = el);

			reminders.IsChosen = true;
			reminders.RemindOnaBenonis = true;
			reminders.IsDetailed = true;
			reminders.OtherTime = "16:30";
			reminders.MoveOffShabbat = false;
			reminders.CalendarName = " Family ";
			Assert.Null(kept);

			reminders.Accept();

			Assert.Equal(ReminderScope.Chosen, kept!.Scope);
			Assert.True(kept.Mikveh);
			Assert.True(kept.OnaBenonis);
			Assert.False(kept.VesetHachodesh);
			Assert.Equal(ReminderWording.Detailed, kept.Wording);
			Assert.Equal("16:30", kept.OtherTime);
			Assert.False(kept.MoveOffShabbat);
			Assert.Equal("Family", kept.CalendarName);
		}

		[Fact]
		public void TheChoicesComeBackAsSaved()
		{
			var setup = new ReminderSetup { Scope = ReminderScope.AllDates, Wording = ReminderWording.Detailed, MikvehTime = "07:00", OtherTime = "18:30", MoveOffShabbat = false, CalendarName = "Family" };

			var reminders = new RemindersWindowViewModel(setup, new[] { DatedPeriod() }, new ClockAt(new DateTime(2031, 1, 1)));

			Assert.True(reminders.IsAllDates);
			Assert.True(reminders.IsDetailed);
			Assert.Equal("07:00", reminders.MikvehTime);
			Assert.Equal("18:30", reminders.OtherTime);
			Assert.False(reminders.MoveOffShabbat);
			Assert.Equal("Family", reminders.CalendarName);
		}

		[Fact]
		public void ChoicesThisVersionDoesntKnowFallBackToTheUsualOnes()
		{
			var setup = new ReminderSetup { ScopeText = "Everything", WordingText = "Loud", MikvehTime = "soon", OtherTime = "08:17" };

			var reminders = new RemindersWindowViewModel(setup, new[] { DatedPeriod() }, new ClockAt(new DateTime(2031, 1, 1)));

			Assert.True(reminders.IsMikvehOnly);
			Assert.True(reminders.IsDiscreet);
			Assert.Equal(ReminderSetup.DefaultMikvehTime, reminders.MikvehTime);
			Assert.Equal(ReminderSetup.DefaultOtherTime, reminders.OtherTime);
		}

		[Fact]
		public void EveryHalfHourCanBeChosen()
		{
			var times = Reminders().TimeChoices;

			Assert.Equal(48, times.Count);
			Assert.Equal("00:00", times[0]);
			Assert.Equal("08:00", times[16]);
			Assert.Equal("23:30", times[^1]);
		}

		[Fact]
		public async Task TheSettingsOpenRemindersOnTheHistoryAndKeepTheChoices()
		{
			var window = await OpenFromSettingsWithOneChangeAsync();
			_dialogs.InSettings = settings =>
			{
				Assert.True(settings.CanOpenReminders);
				var reminders = settings.NewReminders()!;
				reminders.IsDetailed = true;
				reminders.OtherTime = "16:00";
				reminders.Accept();
				return false;												// Cancel in Settings leaves what Reminders kept
			};

			await window.OpenSettingsCommand.ExecuteAsync(null);

			var saved = DesktopSettings.Load().Reminders;
			Assert.Equal(ReminderWording.Detailed, saved.Wording);
			Assert.Equal("16:00", saved.OtherTime);
			Assert.Empty(_dialogs.Shown);
		}

		[Fact]
		public void WithoutReminderSupportTheSettingsDontOfferThem()
		{
			var settings = new SettingsWindowViewModel("pureData.json", null);

			Assert.False(settings.CanOpenReminders);
			Assert.Null(settings.NewReminders());
		}


		private static RemindersWindowViewModel Reminders(params PurityPeriod[] periods) =>
			new(new ReminderSetup(), periods.Length > 0 ? periods : new[] { DatedPeriod() }, new ClockAt(new DateTime(2031, 1, 1)));

		/// <summary>A closed period whose mikveh is on Sunday night, with the other kinds around it</summary>
		private static PurityPeriod DatedPeriod()
		{
			var period = new PurityPeriod(new DateTimeOffset(2031, 2, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2031, 2, 6, 0, 0, 0, TimeSpan.Zero)) { Closed = true };
			period.SubEvents.Add(new PurityEvent(PurityEventType.OnaBeinonit, Stamp(SundayMarch2, false)));
			period.SubEvents.Add(new PurityEvent(PurityEventType.Mikveh, Stamp(SundayMarch2, true)));
			period.SubEvents.Add(new PurityEvent(PurityEventType.VesetHodesh, Stamp(SundayMarch2.AddDays(1), true)));
			period.SubEvents.Add(new PurityEvent(PurityEventType.VesetAflaga, Stamp(SundayMarch2.AddDays(2), true), "(42)"));
			return period;
		}

		private static PurityPeriod Saturday()
		{
			var period = new PurityPeriod(new DateTimeOffset(2031, 2, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2031, 2, 6, 0, 0, 0, TimeSpan.Zero)) { Closed = true };
			period.SubEvents.Add(new PurityEvent(PurityEventType.Mikveh, Stamp(SundayMarch2.AddDays(6), true)));
			return period;
		}

		private static DateTimeOffset Stamp(DateTime date, bool night) => new(date.AddHours(night ? 12 : 0), TimeSpan.Zero);


		private sealed class ClockAt(DateTime now) : TimeProvider
		{
			public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);

			public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
		}
	}
}
