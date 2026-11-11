using Purity.Core;
using Purity.Desktop.Localization;
using Purity.Desktop.ViewModels;
using Purity.Reminders;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>Reminder choices shared between computers through the calendar. Every date is made up.</summary>
	public sealed partial class MainWindowTests
	{
		[Fact]
		public async Task TheFirstComputerPutsItsChoicesInTheCalendar()
		{
			var (_, calendar) = await OpenConnectedAsync();

			Assert.Equal("1234 d 0800 1700 1 en", calendar.Stored!.Format());
		}

		[Fact]
		public async Task AnotherComputersChoicesAreTakenUpAndUsed()
		{
			var (window, calendar) = await OpenConnectedAsync();
			await CloseNewPeriodAsync(window);
			Assert.Contains(calendar.Entries, el => el.Title.StartsWith("Reminder H", StringComparison.Ordinal));
			calendar.Stored = Choices(ReminderWording.Detailed, ReminderLanguage.English, PurityEventType.VesetHodesh);
			var writes = calendar.ChoiceWrites;

			await window.RecalculateCommand.ExecuteAsync(null);
			await window.ReminderSyncFinished;

			Assert.All(calendar.Entries, el => Assert.StartsWith("Veset Hachodesh", el.Title));
			Assert.Equal(writes, calendar.ChoiceWrites);					// taking them up doesn't write them back
			var saved = DesktopSettings.Load().Reminders;
			Assert.Equal(ReminderWording.Detailed, saved.Wording);
			Assert.Equal(ReminderScope.Chosen, saved.Scope);
			Assert.True(saved.VesetHachodesh);
			Assert.False(saved.Mikveh);
		}

		[Fact]
		public async Task AComputerThatJustRunsNeverOverwritesTheCalendarsChoices()
		{
			var (window, calendar) = await OpenConnectedAsync();
			calendar.Stored = Choices(ReminderWording.Detailed, ReminderLanguage.English, PurityEventType.Mikveh);
			var writes = calendar.ChoiceWrites;

			await window.RecalculateCommand.ExecuteAsync(null);
			await window.ReminderSyncFinished;
			await window.RecalculateCommand.ExecuteAsync(null);
			await window.ReminderSyncFinished;

			Assert.Equal(writes, calendar.ChoiceWrites);
			Assert.Equal(ReminderWording.Detailed, calendar.Stored!.Wording);
		}

		[Fact]
		public async Task PressingOkSendsTheChoicesToTheCalendar()
		{
			var (window, calendar) = await OpenConnectedAsync();
			_dialogs.InSettings = settings =>
			{
				var reminders = settings.NewReminders()!;
				reminders.IsDetailed = true;
				reminders.OtherTime = "16:00";
				reminders.Accept();
				return false;
			};

			await window.OpenSettingsCommand.ExecuteAsync(null);
			await window.ReminderSyncFinished;

			Assert.Equal("1234 f 0800 1600 1 en", calendar.Stored!.Format());
			Assert.False(DesktopSettings.Load().Reminders.ChoicesUnshared);
		}

		[Fact]
		public async Task ChoicesMadeOfflineAreSentOnceGoogleCanBeReached()
		{
			var (window, calendar) = await OpenConnectedAsync();
			calendar.FailWith = new CalendarServiceException(CalendarProblem.NoConnection, "gone");
			_dialogs.InSettings = settings =>
			{
				var reminders = settings.NewReminders()!;
				reminders.IsDetailed = true;
				reminders.Accept();
				return false;
			};
			await window.OpenSettingsCommand.ExecuteAsync(null);
			await window.ReminderSyncFinished;
			Assert.True(DesktopSettings.Load().Reminders.ChoicesUnshared);	// still owed, even if the app is closed now
			Assert.Equal(ReminderWording.Discreet, calendar.Stored!.Wording);

			calendar.FailWith = null;
			await window.RecalculateCommand.ExecuteAsync(null);
			await window.ReminderSyncFinished;

			Assert.Equal(ReminderWording.Detailed, calendar.Stored!.Wording);
			Assert.False(DesktopSettings.Load().Reminders.ChoicesUnshared);
		}

		[Fact]
		public Task ChangingTheLanguageSetsTheLanguageOfTheReminders() => Ui.Run(async () =>
		{
			try
			{
				var (window, calendar) = await OpenConnectedAsync();
				_dialogs.InSettings = settings =>
				{
					settings.IsHebrew = true;
					return true;
				};

				await window.OpenSettingsCommand.ExecuteAsync(null);
				await window.ReminderSyncFinished;

				Assert.Equal(ReminderLanguage.Hebrew, calendar.Stored!.Language);
			}
			finally
			{
				Loc.Instance.Use(Language.English);
			}
		});

		[Fact]
		public Task TheCalendarsLanguageIsUsedHoweverThisComputerIsSetUp() => Ui.Run(async () =>
		{
			var (window, calendar) = await OpenConnectedAsync();
			await CloseNewPeriodAsync(window);
			calendar.Stored = Choices(ReminderWording.Discreet, ReminderLanguage.Hebrew, PurityEventType.VesetHodesh);

			await window.RecalculateCommand.ExecuteAsync(null);
			await window.ReminderSyncFinished;

			Assert.All(calendar.Entries, el => Assert.StartsWith("תזכורת", el.Title));
			Assert.Equal(Language.English, Loc.Instance.Language);
		});

		[Fact]
		public async Task ChoicesAFileWasUsingAreNotSentAnywhere()
		{
			var (window, calendar) = await OpenConnectedAsync(ReminderPlace.File);
			_dialogs.InSettings = settings =>
			{
				settings.NewReminders()!.Accept();
				return false;
			};

			await window.OpenSettingsCommand.ExecuteAsync(null);
			await window.ReminderSyncFinished;

			Assert.Equal(0, calendar.ChoiceWrites);
			Assert.False(DesktopSettings.Load().Reminders.ChoicesUnshared);
		}

		[Fact]
		public async Task ConnectingShowsTheChoicesAlreadyInTheCalendar()
		{
			var connection = new MemoryConnection { IsConnected = false, Stored = Choices(ReminderWording.Detailed, ReminderLanguage.English, PurityEventType.Mikveh, PurityEventType.OnaBeinonit) };
			var reminders = RemindersWith(connection);
			Assert.True(reminders.IsMikvehOnly);

			await reminders.ConnectAsync();

			Assert.True(reminders.IsChosen);
			Assert.True(reminders.RemindMikveh);
			Assert.True(reminders.RemindOnaBenonis);
			Assert.False(reminders.RemindVesetAflaga);
			Assert.True(reminders.IsDetailed);
		}

		[Fact]
		public async Task ConnectingToANewCalendarLeavesTheChoicesAlone()
		{
			var reminders = RemindersWith(new MemoryConnection { IsConnected = false });
			reminders.IsAllDates = true;

			await reminders.ConnectAsync();

			Assert.True(reminders.IsAllDates);
		}

		[Theory]
		[InlineData(PurityEventType.Mikveh, ReminderScope.MikvehOnly)]
		public void OnlyTheMikvehMeansMikvehOnly(PurityEventType kind, ReminderScope scope)
		{
			var setup = new ReminderSetup { Scope = ReminderScope.AllDates };

			setup.Adopt(Choices(ReminderWording.Discreet, ReminderLanguage.English, kind));

			Assert.Equal(scope, setup.Scope);
		}

		[Fact]
		public void EveryKindMeansAllDates()
		{
			var setup = new ReminderSetup();

			setup.Adopt(Choices(ReminderWording.Discreet, ReminderLanguage.English, Enum.GetValues<PurityEventType>()));

			Assert.Equal(ReminderScope.AllDates, setup.Scope);
		}

		[Fact]
		public void TakingUpChoicesLeavesWhatBelongsToThisComputerAlone()
		{
			var setup = new ReminderSetup { Where = ReminderPlace.Google, CalendarName = "Family" };

			setup.Adopt(Choices(ReminderWording.Detailed, ReminderLanguage.Hebrew, PurityEventType.Mikveh));

			Assert.Equal(ReminderPlace.Google, setup.Where);
			Assert.Equal("Family", setup.CalendarName);
			Assert.Equal(ReminderLanguage.Hebrew, setup.Language);
		}

		[Fact]
		public void ALanguageThisVersionDoesntKnowMeansTheOneOnShow() =>
			Assert.Null(new ReminderSetup { LanguageText = "Klingon" }.Language);


		private static SharedChoices Choices(ReminderWording wording, ReminderLanguage language, params PurityEventType[] kinds) =>
			new(kinds.ToHashSet(), wording, new TimeOnly(8, 0), new TimeOnly(17, 0), true, language);
	}
}
