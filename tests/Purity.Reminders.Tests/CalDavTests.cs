using Purity.CalDavStandIn;
using Purity.Core;
using Purity.Reminders.CalDav;
using Xunit;


namespace Purity.Reminders.Tests
{
	/// <summary>The real CalDAV code, over HTTP, against the stand-in. Every date, name and password is made up.</summary>
	public sealed class CalDavTests : IAsyncLifetime
	{
		public async ValueTask InitializeAsync() => _server = await CalDavServer.StartAsync();

		public async ValueTask DisposeAsync()
		{
			if (!_stopped)
				await _server.DisposeAsync();
			_http.Dispose();
		}


		[Fact]
		public async Task SigningInFindsTheCalendarsAndKeepsTheSignIn()
		{
			var account = Account();
			Assert.False(account.IsSignedIn);

			await account.SignInAsync(SignIn(), default);

			Assert.True(account.IsSignedIn);
			Assert.True(account.RemembersSignIn);
			Assert.Equal(CalDavServer.User, account.User);
			Assert.NotNull(_secrets.Value);
			Assert.Equal(CalDavServer.User, Account().User);			// read back as the next start would
		}

		[Fact]
		public async Task AWrongPasswordIsRefusedAndNothingIsKept()
		{
			var problem = await Assert.ThrowsAsync<CalendarServiceException>(() => Account().SignInAsync(SignIn() with { Password = "wxyz-wxyz-wxyz-wxyz" }, default));

			Assert.Equal(CalendarProblem.PasswordRefused, problem.Problem);
			Assert.Null(_secrets.Value);
		}

		[Fact]
		public async Task TheWellKnownAddressIsTriedWhenTheOneGivenDoesntAnswer()
		{
			_server.OnlyWellKnown = true;

			await Account().SignInAsync(SignIn(), default);

			Assert.Contains(_server.Calls, el => el.Path == "/.well-known/caldav");
		}

		[Fact]
		public async Task AnAddressWithNoCalendarsSaysSo()
		{
			_server.OnlyWellKnown = true;
			_server.Faults.Add(new Fault(404, PathContains: ".well-known"));

			var problem = await Assert.ThrowsAsync<CalendarServiceException>(() => Account().SignInAsync(SignIn(), default));

			Assert.Equal(CalendarProblem.NoCalendarService, problem.Problem);
			Assert.Null(_secrets.Value);
		}

		[Fact]
		public async Task APasswordIsNeverSentOverPlainHttpToAnotherComputer()
		{
			var problem = await Assert.ThrowsAsync<CalendarServiceException>(() => Account().SignInAsync(new CalDavSignIn(new Uri("http://example.com/"), CalDavServer.User, CalDavServer.Password), default));

			Assert.Equal(CalendarProblem.NotSecure, problem.Problem);
			Assert.Empty(_server.Calls);
		}

		[Fact]
		public async Task ThePasswordDoesntFollowARedirectToAnotherName()
		{
			// The same server under another name, which for all Purity knows is another computer
			_server.RedirectsTo = new Uri($"http://localhost:{_server.BaseAddress.Port}/dav/");

			var problem = await Assert.ThrowsAsync<CalendarServiceException>(() => Account().SignInAsync(SignIn(), default));

			Assert.Equal(CalendarProblem.PasswordRefused, problem.Problem);
			Assert.All(_server.Calls.Where(el => el.Host == "localhost"), el => Assert.False(el.Signed));
			Assert.Contains(_server.Calls, el => el.Host == "localhost");
		}

		[Theory]
		[InlineData("https://caldav.icloud.com/", "https://p42-caldav.icloud.com/123/calendars/", true)]
		[InlineData("https://caldav.icloud.com/", "https://caldav.icloud.com/123/", true)]
		[InlineData("https://dav.example.org/", "https://dav.example.org/x/", true)]
		[InlineData("https://dav.example.org/", "https://other.example.org/x/", true)]
		[InlineData("https://caldav.icloud.com/", "https://icloud.com.example.net/", false)]
		[InlineData("https://caldav.icloud.com/", "http://p42-caldav.icloud.com/", false)]
		[InlineData("https://example.org/", "https://another.org/", false)]
		[InlineData("https://example.org/", "https://dav.another.org/", false)]
		[InlineData("http://127.0.0.1:5232/", "http://localhost:5232/", false)]
		public void ThePasswordGoesOnlyToTheServerItWasGivenFor(string server, string url, bool sent) =>
			Assert.Equal(sent, CalDavAccount.IsSameService(new Uri(server), new Uri(url)));

		[Fact]
		public async Task SigningOutForgetsTheSignIn()
		{
			var account = Account();
			await account.SignInAsync(SignIn(), default);

			account.SignOut();

			Assert.False(account.IsSignedIn);
			Assert.Null(_secrets.Value);
			Assert.False(Account().IsSignedIn);
		}

		[Fact]
		public async Task ASignInThatCantBeKeptLastsForThisRun()
		{
			_secrets.CanStore = false;
			var account = Account();

			await account.SignInAsync(SignIn(), default);

			Assert.True(account.IsSignedIn);
			Assert.False(account.RemembersSignIn);
		}

		[Fact]
		public async Task AddingMakesPuritysOwnCalendarWithTheEventInIt()
		{
			var calendar = await SignedInCalendarAsync();

			await calendar.AddAsync(Reminder("Appointment today"), default);

			var made = Assert.Single(_server.Calendars);
			Assert.Equal("Personal", made.Name);
			Assert.Equal(ReminderTag.Calendar, ReminderTag.FromNotes(made.Description));
			Assert.StartsWith(CalDavServer.Home + ReminderTag.Calendar + "-", made.Path);
			var (path, text) = Assert.Single(made.Events);
			Assert.Equal(made.Path + Reminder("x").Tag + ".ics", path);
			Assert.Contains("\r\nDTSTART:20310302T060000Z\r\n", text);		// 08:00 two hours east of UTC
			Assert.Contains("\r\nSUMMARY:Appointment today\r\n", text);
			Assert.Contains("\r\nTRIGGER:PT0S\r\n", text);
			Assert.Equal(new Uri(_server.BaseAddress, made.Path).AbsoluteUri, _calendarUrl);
		}

		[Fact]
		public async Task WhatWasAddedIsReadBackAsItWasPlanned()
		{
			var calendar = await SignedInCalendarAsync();
			var reminder = Reminder("תזכורת ע׳, מהערב, יממה שלמה");
			await calendar.AddAsync(reminder, default);

			var entry = Assert.Single(await calendar.ListAsync(default));

			Assert.Equal(reminder.Tag, entry.Tag);
			Assert.Equal(reminder.Moment, entry.Moment);
			Assert.Equal(reminder.Title, entry.Title);
		}

		[Fact]
		public async Task ChangingAndDeletingWorkInPlace()
		{
			var calendar = await SignedInCalendarAsync();
			await calendar.AddAsync(Reminder("Appointment today"), default);
			var entry = Assert.Single(await calendar.ListAsync(default));

			await calendar.ChangeAsync(entry, Reminder("Mikveh today") with { Moment = new DateTime(2031, 3, 3, 17, 0, 0) }, default);
			var changed = Assert.Single(await calendar.ListAsync(default));
			Assert.Equal(("Mikveh today", new DateTime(2031, 3, 3, 17, 0, 0), entry.ServiceId), (changed.Title, changed.Moment, changed.ServiceId));

			await calendar.DeleteAsync(changed, default);
			Assert.Empty(await calendar.ListAsync(default));
			await calendar.DeleteAsync(changed, default);					// already gone is what was wanted
		}

		[Fact]
		public async Task AddingAgainReplacesTheOneThere()
		{
			var calendar = await SignedInCalendarAsync();
			await calendar.AddAsync(Reminder("Appointment today"), default);

			await calendar.AddAsync(Reminder("Mikveh today"), default);

			Assert.Equal("Mikveh today", Assert.Single(await calendar.ListAsync(default)).Title);
		}

		[Fact]
		public async Task AnotherComputerFindsTheSameCalendar()
		{
			var calendar = await SignedInCalendarAsync();
			await calendar.AddAsync(Reminder("Appointment today"), default);

			var second = await SecondComputerAsync();

			Assert.Equal("Appointment today", Assert.Single(await second.ListAsync(default)).Title);
			Assert.Single(_server.Calendars);
		}

		[Fact]
		public async Task OnAServerThatDropsDescriptionsTheAddressStillFindsIt()
		{
			_server.KeepsDescriptions = false;
			var calendar = await SignedInCalendarAsync();
			await calendar.AddAsync(Reminder("Appointment today"), default);
			await calendar.WriteChoicesAsync(Choices(), default);		// not kept, and not a failure

			var second = await SecondComputerAsync();

			Assert.Single(await second.ListAsync(default));
			Assert.Null(await second.ReadChoicesAsync(default));
			Assert.Single(_server.Calendars);
		}

		[Fact]
		public async Task TheChoicesTravelWithTheCalendar()
		{
			var calendar = await SignedInCalendarAsync();
			await calendar.WriteChoicesAsync(Choices(), default);

			var second = await SecondComputerAsync();

			Assert.Equal(Choices().Format(), (await second.ReadChoicesAsync(default))?.Format());
			Assert.Equal(ReminderTag.Calendar, ReminderTag.FromNotes(Assert.Single(_server.Calendars).Description));
		}

		[Fact]
		public async Task ACalendarThePersonDeletedIsMadeAgainAtANewAddress()
		{
			_server.ReusesAddresses = false;								// as iCloud
			var calendar = await SignedInCalendarAsync();
			await calendar.AddAsync(Reminder("Appointment today"), default);
			var first = Assert.Single(_server.Calendars).Path;
			_server.DeleteCalendar(first);

			Assert.Empty(await calendar.ListAsync(default));
			await calendar.AddAsync(Reminder("Appointment today"), default);

			Assert.NotEqual(first, Assert.Single(_server.Calendars).Path);
			Assert.Single(Assert.Single(_server.Calendars).Events);
		}

		[Fact]
		public async Task ChoicesWrittenToADeletedCalendarMakeItAgain()
		{
			var calendar = await SignedInCalendarAsync();
			await calendar.WriteChoicesAsync(Choices(), default);
			_server.DeleteCalendar(Assert.Single(_server.Calendars).Path);

			Assert.Null(await calendar.ReadChoicesAsync(default));
			await calendar.WriteChoicesAsync(Choices(), default);

			Assert.Equal(Choices().Format(), SharedChoices.Parse(Assert.Single(_server.Calendars).Description)?.Format());
		}

		[Fact]
		public async Task ThePersonsOwnCalendarsAreNeverTouched()
		{
			var own = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nBEGIN:VEVENT\r\nUID:dentist\r\nDTSTART:20310302T090000Z\r\nSUMMARY:Dentist\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n";
			_server.AddOwnCalendar("home", "Family", ("dentist.ics", own));
			var calendar = await SignedInCalendarAsync();

			await CalendarSync.ApplyAsync(ReminderPlanner.PlanAll(new[] { ClosedPeriod() }, Options()), new DateTime(2031, 2, 10), calendar);

			var home = _server.Calendars.Single(el => el.Name == "home");
			Assert.Equal(("Family", own), (home.Description, Assert.Single(home.Events).Value));
			Assert.Equal(2, _server.Calendars.Count);
			Assert.DoesNotContain(_server.Calls, el => el.Path.StartsWith(CalDavServer.Home + "home/", StringComparison.Ordinal) && el.Method != "PROPFIND");
		}

		[Fact]
		public async Task ASecondRunChangesNothing()
		{
			var calendar = await SignedInCalendarAsync();
			var wanted = ReminderPlanner.PlanAll(new[] { ClosedPeriod() }, Options() with { Language = ReminderLanguage.Hebrew });

			var first = await CalendarSync.ApplyAsync(wanted, new DateTime(2031, 2, 10), calendar);
			var second = await CalendarSync.ApplyAsync(wanted, new DateTime(2031, 2, 10), await SecondComputerAsync());

			Assert.True(first.Added > 2);
			Assert.Equal(new SyncResult(0, 0, 0), second);
		}

		[Theory]
		[InlineData("DTSTART:20310302T080000", 8)]							// on the local clock already
		[InlineData("DTSTART;TZID=Somewhere/Unknown:20310302T080000", 0)]	// a zone this computer doesn't know: written afresh
		[InlineData("DTSTART;VALUE=DATE:20310302", 0)]
		public void EventsWrittenElsewhereAreReadAsWellAsTheyCanBe(string start, int hour)
		{
			var text = $"BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:x\r\n{start}\r\nSUMMARY:One\\, two\r\nDESCRIPTION:ref:{Reminder("x").Tag}\r\n  and more\r\nBEGIN:VALARM\r\nDESCRIPTION:Not this\r\nEND:VALARM\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n";

			var found = Calendar(Account()).Read(text);

			Assert.NotNull(found);
			Assert.Equal("One, two", found.Value.Title);
			Assert.Equal(Reminder("x").Tag, ReminderTag.FromNotes(found.Value.Notes));
			Assert.Equal(hour == 0 ? DateTime.MinValue : new DateTime(2031, 3, 2, hour, 0, 0), found.Value.Start);
		}

		[Fact]
		public async Task ABusyServerIsWaitedOut()
		{
			var calendar = await SignedInCalendarAsync();
			_server.Faults.Add(new Fault(503, "PUT"));

			await calendar.AddAsync(Reminder("Appointment today"), default);

			Assert.Equal(new[] { TimeSpan.FromSeconds(1) }, _waits);
			Assert.Single(Assert.Single(_server.Calendars).Events);
		}

		[Fact]
		public async Task APasswordThatStopsWorkingAsksForASignIn()
		{
			var calendar = await SignedInCalendarAsync();
			_server.Faults.Add(new Fault(401));

			var problem = await Assert.ThrowsAsync<CalendarServiceException>(() => calendar.ListAsync(default));

			Assert.Equal(CalendarProblem.SignInNeeded, problem.Problem);
		}

		[Fact]
		public async Task AServerThatRefusesSaysSo()
		{
			var calendar = await SignedInCalendarAsync();
			_server.Faults.Add(new Fault(403, "MKCALENDAR"));

			var problem = await Assert.ThrowsAsync<CalendarServiceException>(() => calendar.AddAsync(Reminder("Appointment today"), default));

			Assert.Equal(CalendarProblem.Refused, problem.Problem);
		}

		[Fact]
		public async Task NoServerMeansNoConnection()
		{
			var calendar = await SignedInCalendarAsync();
			await _server.DisposeAsync();
			_stopped = true;

			var problem = await Assert.ThrowsAsync<CalendarServiceException>(() => calendar.ListAsync(default));

			Assert.Equal(CalendarProblem.NoConnection, problem.Problem);
		}

		[Fact]
		public async Task DeletingTheCalendarRemovesItAndAllItsReminders()
		{
			var calendar = await SignedInCalendarAsync();
			await calendar.AddAsync(Reminder("Appointment today"), default);

			await calendar.DeleteCalendarAsync(default);

			Assert.Empty(_server.Calendars);
			Assert.Null(_calendarUrl);
		}

		[Fact]
		public async Task AComputerThatNeverUsedTheCalendarCanStillDeleteIt()
		{
			var calendar = await SignedInCalendarAsync();
			await calendar.AddAsync(Reminder("Appointment today"), default);

			await (await SecondComputerAsync()).DeleteCalendarAsync(default);

			Assert.Empty(_server.Calendars);
		}


		private static readonly TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone("Made up", TimeSpan.FromHours(2), "Made up", "Made up");

		private CalDavSignIn SignIn() => new(_server.BaseAddress, CalDavServer.User, CalDavServer.Password);

		private CalDavAccount Account() => new(_http, _secrets, (delay, _) =>
		{
			_waits.Add(delay);
			return Task.CompletedTask;
		});

		private CalDavCalendar Calendar(CalDavAccount account) => new(account, () => _calendarUrl, value => _calendarUrl = value, () => "Personal", Zone);

		private async Task<CalDavCalendar> SignedInCalendarAsync()
		{
			var account = Account();
			await account.SignInAsync(SignIn(), default);
			return Calendar(account);
		}

		/// <summary>Another computer, signed in to the same account, that doesn't yet know the calendar's address</summary>
		private async Task<CalDavCalendar> SecondComputerAsync()
		{
			string? known = null;
			var account = new CalDavAccount(_http, new MemorySecrets(), (_, _) => Task.CompletedTask);
			await account.SignInAsync(SignIn(), default);
			return new CalDavCalendar(account, () => known, value => known = value, () => "Personal", Zone);
		}

		private static Reminder Reminder(string title)
		{
			var period = new PurityPeriod(new DateTimeOffset(2031, 2, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2031, 2, 6, 0, 0, 0, TimeSpan.Zero));
			return new Reminder(ReminderTag.Of(period, new PurityEvent(PurityEventType.VesetAflaga, default, "(0)")), new DateTime(2031, 3, 2, 8, 0, 0), title, PurityEventType.Mikveh);
		}

		private static ReminderOptions Options() => new() { Kinds = Enum.GetValues<PurityEventType>().ToHashSet() };

		private static SharedChoices Choices() => new(new HashSet<PurityEventType> { PurityEventType.Mikveh }, ReminderWording.Detailed, new TimeOnly(9, 0), new TimeOnly(18, 30), true, ReminderLanguage.Hebrew);

		private static PurityPeriod ClosedPeriod()
		{
			var period = new PurityPeriod(new DateTimeOffset(2031, 2, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2031, 2, 6, 0, 0, 0, TimeSpan.Zero)) { Closed = true };
			period.SubEvents.Add(new PurityEvent(PurityEventType.Mikveh, new DateTimeOffset(2031, 3, 2, 12, 0, 0, TimeSpan.Zero)));
			period.SubEvents.Add(new PurityEvent(PurityEventType.VesetHodesh, new DateTimeOffset(2031, 3, 3, 12, 0, 0, TimeSpan.Zero)));
			period.SubEvents.Add(new PurityEvent(PurityEventType.OnaBeinonit, new DateTimeOffset(2031, 3, 4, 0, 0, 0, TimeSpan.Zero)));
			return period;
		}


		private readonly HttpClient _http = CalDavAccount.NewHttpClient();
		private readonly MemorySecrets _secrets = new();
		private readonly List<TimeSpan> _waits = new();
		private CalDavServer _server = null!;
		private string? _calendarUrl;
		private bool _stopped;


		private sealed class MemorySecrets : ISecretStore
		{
			public string? Value { get; private set; }
			public bool CanStore { get; set; } = true;

			public string? Read() => Value;

			public bool Write(string secret)
			{
				if (CanStore)
					Value = secret;
				return CanStore;
			}

			public void Delete() => Value = null;
		}
	}
}
