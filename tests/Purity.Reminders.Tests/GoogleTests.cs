using Purity.Core;
using Purity.GoogleStandIn;
using Purity.Reminders.Google;
using Xunit;


namespace Purity.Reminders.Tests
{
	/// <summary>The real Google code, over HTTP, against the stand-in. Every date, name and token is made up.</summary>
	public sealed class GoogleTests : IAsyncLifetime
	{
		public async ValueTask InitializeAsync() => _server = await StandInServer.StartAsync();

		public async ValueTask DisposeAsync()
		{
			if (!_stopped)
				await _server.DisposeAsync();
			_http.Dispose();
		}


		[Fact]
		public async Task SigningInKeepsALastingSignIn()
		{
			var account = Account();
			Assert.False(account.IsSignedIn);

			await SignInAsync(account);

			Assert.True(account.IsSignedIn);
			Assert.StartsWith("refresh-", _secrets.Value);
			Assert.True(account.RemembersSignIn);
			Assert.StartsWith("access-", await account.AccessTokenAsync(default));
		}

		[Fact]
		public async Task TurningTheSignInDownSaysSo()
		{
			_server.DeclinesSignIn = true;

			var problem = await Assert.ThrowsAsync<CalendarServiceException>(() => SignInAsync(Account()));

			Assert.Equal(CalendarProblem.SignInNotFinished, problem.Problem);
			Assert.Null(_secrets.Value);
		}

		[Fact]
		public async Task ARedirectThatDoesntMatchIsRefused()
		{
			_server.SendsWrongState = true;

			var problem = await Assert.ThrowsAsync<CalendarServiceException>(() => SignInAsync(Account()));

			Assert.Equal(CalendarProblem.Refused, problem.Problem);
			Assert.Null(_secrets.Value);
		}

		[Fact]
		public async Task GivingUpOnTheSignInStopsWaiting()
		{
			using var cancel = new CancellationTokenSource();
			var waiting = Account().SignInAsync(_ =>
			{
				cancel.Cancel();							// the browser was opened, and the person closes the window
				return Task.CompletedTask;
			}, "Done", cancel.Token);

			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
		}

		[Fact]
		public async Task ASignInCanBeRememberedAcrossRuns()
		{
			await SignInAsync(Account());

			var later = Account();

			Assert.True(later.IsSignedIn);
			Assert.StartsWith("access-", await later.AccessTokenAsync(default));
		}

		[Fact]
		public async Task WithNowhereSafeToKeepItTheSignInLastsThisRun()
		{
			_secrets.CanStore = false;
			var account = Account();

			await SignInAsync(account);

			Assert.True(account.IsSignedIn);
			Assert.False(account.RemembersSignIn);
			Assert.StartsWith("access-", await account.AccessTokenAsync(default));
		}

		[Fact]
		public async Task NotSignedInIsSaid()
		{
			var problem = await Assert.ThrowsAsync<CalendarServiceException>(() => Calendar(Account()).ListAsync(default));

			Assert.Equal(CalendarProblem.SignInNeeded, problem.Problem);
		}

		[Fact]
		public async Task ATokenThatRunsOutIsRenewedWithoutAnyoneNoticing()
		{
			var calendar = await SignedInCalendarAsync();
			await calendar.AddAsync(Reminder("Appointment today"), default);
			_server.ExpireAccessTokens();

			var entries = await calendar.ListAsync(default);

			Assert.Single(entries);
		}

		[Fact]
		public async Task ASignInRemovedAtGoogleAsksToSignInAgain()
		{
			var account = Account();
			await SignInAsync(account);
			var calendar = Calendar(account);
			await calendar.ListAsync(default);
			_server.RevokeEverything();

			var problem = await Assert.ThrowsAsync<CalendarServiceException>(() => calendar.ListAsync(default));

			Assert.Equal(CalendarProblem.SignInNeeded, problem.Problem);
			Assert.False(account.IsSignedIn);
			Assert.Null(_secrets.Value);
		}

		[Fact]
		public async Task SigningOutEndsItAtGoogleToo()
		{
			var account = Account();
			await SignInAsync(account);
			Assert.True(_server.HasLiveSignIn);

			await account.SignOutAsync(default);

			Assert.False(_server.HasLiveSignIn);
			Assert.False(account.IsSignedIn);
			Assert.Null(_secrets.Value);
		}


		[Fact]
		public async Task TheFirstCallMakesOneCalendarWithTheChosenName()
		{
			var calendar = await SignedInCalendarAsync();

			await calendar.ListAsync(default);
			await calendar.ListAsync(default);

			Assert.Equal("Personal", _server.Calendars.Single().Summary);
			Assert.Equal(_server.Calendars.Single().Id, _calendarId);
		}

		[Fact]
		public async Task OnlyTheTwoPermissionsAreAskedFor()
		{
			await SignInAsync(Account());

			Assert.Equal(new[] { "https://www.googleapis.com/auth/calendar.app.created", "https://www.googleapis.com/auth/calendar.calendarlist.readonly" }, _server.GrantedScopes);
		}

		[Fact]
		public async Task ANewCalendarCarriesItsTag()
		{
			await (await SignedInCalendarAsync()).ListAsync(default);

			Assert.Equal("ref:" + ReminderTag.Calendar, _server.Calendars.Single().Description);
			Assert.Contains(_server.Log, el => el.StartsWith("PATCH ", StringComparison.Ordinal));
		}

		[Fact]
		public async Task WhenGoogleKeepsTheTagFromTheStartNoSecondCallIsMade()
		{
			_server.KeepsDescriptionWhenMaking = true;

			await (await SignedInCalendarAsync()).ListAsync(default);

			Assert.Equal("ref:" + ReminderTag.Calendar, _server.Calendars.Single().Description);
			Assert.DoesNotContain(_server.Log, el => el.StartsWith("PATCH ", StringComparison.Ordinal));
		}

		[Fact]
		public async Task ASecondComputerFindsTheCalendarAndSeesTheSameEntries()
		{
			var first = await SignedInCalendarAsync();
			await first.AddAsync(Reminder("Appointment today"), default);

			var entries = await SecondComputer(out var secondId).ListAsync(default);

			Assert.Single(_server.Calendars);
			Assert.Equal(_calendarId, secondId());
			Assert.Equal("Appointment today", entries.Single().Title);
		}

		[Fact]
		public async Task TwoComputersWritingTheSameReminderMakeOneEntry()
		{
			var first = await SignedInCalendarAsync();
			var reminder = Reminder("Appointment today");
			await first.AddAsync(reminder, default);

			await SecondComputer(out _).AddAsync(reminder with { Title = "תור הערב" }, default);

			var entry = (await first.ListAsync(default)).Single();
			Assert.Equal("תור הערב", entry.Title);
		}

		[Fact]
		public async Task EitherComputerCanChangeTheReminders()
		{
			var first = await SignedInCalendarAsync();
			var period = ClosedPeriod();
			await CalendarSync.ApplyAsync(ReminderPlanner.PlanAll(new[] { period }, Options()), Now, first);
			var second = SecondComputer(out _);

			var result = await CalendarSync.ApplyAsync(ReminderPlanner.PlanAll(new[] { period }, Options() with { Language = ReminderLanguage.Hebrew }), Now, second);

			Assert.Equal(new SyncResult(0, 3, 0), result);
			Assert.Equal(3, (await first.ListAsync(default)).Count);
			Assert.Single(_server.Calendars);
		}

		[Fact]
		public async Task ChoicesWrittenOnOneComputerAreReadOnAnother()
		{
			var first = await SignedInCalendarAsync();
			await first.ListAsync(default);
			Assert.Null(await first.ReadChoicesAsync(default));
			var choices = new SharedChoices(new HashSet<PurityEventType> { PurityEventType.Mikveh }, ReminderWording.Detailed, new TimeOnly(9, 0), new TimeOnly(16, 30), false, ReminderLanguage.Hebrew);

			await first.WriteChoicesAsync(choices, default);

			Assert.Equal(choices.Format(), (await SecondComputer(out _).ReadChoicesAsync(default))!.Format());
		}

		[Fact]
		public async Task WithChoicesInItTheCalendarIsStillFound()
		{
			var first = await SignedInCalendarAsync();
			await first.WriteChoicesAsync(new SharedChoices(new HashSet<PurityEventType> { PurityEventType.Mikveh }, ReminderWording.Discreet, new TimeOnly(8, 0), new TimeOnly(17, 0), true, ReminderLanguage.English), default);

			await SecondComputer(out var secondId).ListAsync(default);

			Assert.Equal(_calendarId, secondId());
			Assert.Single(_server.Calendars);
			Assert.StartsWith("ref:" + ReminderTag.Calendar + " 1 d 0800 1700 1 en", _server.Calendars.Single().Description);
		}

		[Fact]
		public async Task ChoicesWrittenAfterTheCalendarWasDeletedMakeItAgain()
		{
			var first = await SignedInCalendarAsync();
			await first.ListAsync(default);
			var gone = _calendarId!;
			_server.DeleteCalendarByHand(gone);

			await first.WriteChoicesAsync(new SharedChoices(new HashSet<PurityEventType> { PurityEventType.Mikveh }, ReminderWording.Discreet, new TimeOnly(8, 0), new TimeOnly(17, 0), true, ReminderLanguage.English), default);

			Assert.NotEqual(gone, _calendarId);
			Assert.Equal(_calendarId, _server.Calendars.Single().Id);
			Assert.NotNull(await first.ReadChoicesAsync(default));
		}

		[Fact]
		public async Task ACalendarThatJustHasTheSameNameIsLeftAlone()
		{
			var calendar = await SignedInCalendarAsync();
			_server.AddCalendarByHand("mine@example", "Personal");

			await calendar.ListAsync(default);

			Assert.NotEqual("mine@example", _calendarId);
			Assert.Single(_server.Calendars);
		}

		[Fact]
		public async Task ARenamedCalendarIsStillFound()
		{
			var first = await SignedInCalendarAsync();
			await first.ListAsync(default);
			_server.RenameCalendarByHand(_calendarId!, "Something else");

			await SecondComputer(out var secondId).ListAsync(default);

			Assert.Equal(_calendarId, secondId());
			Assert.Single(_server.Calendars);
		}

		[Fact]
		public async Task TheCalendarIsFoundAcrossPagesOfTheList()
		{
			_server.PageLimit = 1;
			_server.AddCalendarByHand("mine@example", "Mine");
			var first = await SignedInCalendarAsync();
			await first.ListAsync(default);

			await SecondComputer(out var secondId).ListAsync(default);

			Assert.Equal(_calendarId, secondId());
		}

		[Fact]
		public async Task StoppingOnOneComputerEndsTheSignInOnThemAll()
		{
			var account = Account();
			await SignInAsync(account);
			var secondSecrets = new MemorySecrets();
			var second = new GoogleAccount(_http, new GoogleClient(StandInServer.ClientId), new GoogleEndpoints(_server.Authorize, _server.Token, _server.Revoke, _server.Calendar), secondSecrets, _clock);
			await SignInAsync(second);

			await account.SignOutAsync(default);
			second.ForgetAccessToken();

			var problem = await Assert.ThrowsAsync<CalendarServiceException>(() => second.AccessTokenAsync(default));
			Assert.Equal(CalendarProblem.SignInNeeded, problem.Problem);
		}

		[Fact]
		public async Task AnEntryIsWrittenAsApproved()
		{
			var calendar = await SignedInCalendarAsync();
			var reminder = Reminder("Appointment today");

			await calendar.AddAsync(reminder, default);

			var item = _server.Calendars.Single().Events.Single();
			Assert.Equal(reminder.Tag, item["id"]!.ToString());
			Assert.Equal("Appointment today", item["summary"]!.ToString());
			Assert.Equal("ref:" + reminder.Tag, item["description"]!.ToString());
			Assert.Equal("private", item["visibility"]!.ToString());
			Assert.Equal("transparent", item["transparency"]!.ToString());
			Assert.Equal("2031-03-02T08:00:00+02:00", item["start"]!["dateTime"]!.ToString());
			Assert.Equal("2031-03-02T08:15:00+02:00", item["end"]!["dateTime"]!.ToString());
			Assert.False(item["reminders"]!["useDefault"]!.GetValue<bool>());
			Assert.Equal("popup", item["reminders"]!["overrides"]![0]!["method"]!.ToString());
			Assert.Equal(0, item["reminders"]!["overrides"]![0]!["minutes"]!.GetValue<int>());
		}

		[Fact]
		public async Task ReadingBackGivesTheSameEntryInHebrewToo()
		{
			var calendar = await SignedInCalendarAsync();
			var reminder = Reminder("תזכורת ח׳, במוצ\"ש");

			await calendar.AddAsync(reminder, default);
			var entry = (await calendar.ListAsync(default)).Single();

			Assert.Equal(reminder.Tag, entry.Tag);
			Assert.Equal(reminder.Moment, entry.Moment);
			Assert.Equal(reminder.Title, entry.Title);
		}

		[Fact]
		public async Task EntriesThatArentOursAreNotSeen()
		{
			var calendar = await SignedInCalendarAsync();
			await calendar.AddAsync(Reminder("Appointment today"), default);
			_server.AddEntryByHand(_calendarId!, "byhandentry", "My own note");

			var entries = await calendar.ListAsync(default);

			Assert.Equal("Appointment today", entries.Single().Title);
		}

		[Fact]
		public async Task ALongListComesInPages()
		{
			var calendar = await SignedInCalendarAsync();
			_server.PageLimit = 2;
			for (var i = 0; i < 5; i++)
				await calendar.AddAsync(Reminder($"Title {i}", key: i), default);

			Assert.Equal(5, (await calendar.ListAsync(default)).Count);
		}

		[Fact]
		public async Task ChangingAnEntryKeepsItsIdentity()
		{
			var calendar = await SignedInCalendarAsync();
			var first = Reminder("Appointment today");
			await calendar.AddAsync(first, default);
			var entry = (await calendar.ListAsync(default)).Single();

			await calendar.ChangeAsync(entry, first with { Title = "תור הערב", Moment = first.Moment.AddHours(1) }, default);

			var changed = (await calendar.ListAsync(default)).Single();
			Assert.Equal(entry.Tag, changed.Tag);
			Assert.Equal("תור הערב", changed.Title);
			Assert.Equal(first.Moment.AddHours(1), changed.Moment);
		}

		[Fact]
		public async Task ADeletedEntryIsGoneAndDeletingAgainIsFine()
		{
			var calendar = await SignedInCalendarAsync();
			await calendar.AddAsync(Reminder("Appointment today"), default);
			var entry = (await calendar.ListAsync(default)).Single();

			await calendar.DeleteAsync(entry, default);
			await calendar.DeleteAsync(entry, default);

			Assert.Empty(await calendar.ListAsync(default));
		}

		[Fact]
		public async Task AReminderThatComesBackIsRestored()
		{
			var calendar = await SignedInCalendarAsync();
			var reminder = Reminder("Appointment today");
			await calendar.AddAsync(reminder, default);
			await calendar.DeleteAsync((await calendar.ListAsync(default)).Single(), default);

			await calendar.AddAsync(reminder, default);					// Google keeps a deleted entry's ID for a while

			Assert.Equal(reminder.Title, (await calendar.ListAsync(default)).Single().Title);
			Assert.Contains(_server.Log, el => el.StartsWith("PUT ", StringComparison.Ordinal));
		}

		[Fact]
		public async Task ACalendarDeletedAtGoogleIsMadeAgain()
		{
			var calendar = await SignedInCalendarAsync();
			await calendar.AddAsync(Reminder("Appointment today"), default);
			var gone = _calendarId!;
			_server.DeleteCalendarByHand(gone);

			Assert.Empty(await calendar.ListAsync(default));

			Assert.NotEqual(gone, _calendarId);
			Assert.Equal(_calendarId, _server.Calendars.Single().Id);
		}

		[Fact]
		public async Task DeletingTheCalendarTakesEverythingWithIt()
		{
			var calendar = await SignedInCalendarAsync();
			await calendar.AddAsync(Reminder("Appointment today"), default);

			await calendar.DeleteCalendarAsync(default);

			Assert.Empty(_server.Calendars);
			Assert.Null(_calendarId);
		}

		[Theory]
		[InlineData(403, "rateLimitExceeded", "usageLimits")]
		[InlineData(403, "userRateLimitExceeded", "usageLimits")]
		[InlineData(429, "rateLimitExceeded", "usageLimits")]
		[InlineData(500, "backendError", "global")]
		public async Task AFailureGoogleSaysToWaitOutIsWaitedOut(int status, string reason, string domain)
		{
			var calendar = await SignedInCalendarAsync();
			await calendar.ListAsync(default);
			_server.FailNext(new Fault(status, reason, domain, "/events"), times: 2);

			await calendar.AddAsync(Reminder("Appointment today"), default);

			Assert.Single(_server.Calendars.Single().Events);
			Assert.Equal(2, _waits.Count);
			Assert.True(_waits[1] > _waits[0], "Each wait should be longer than the last");
		}

		[Fact]
		public async Task AFailureThatDoesntClearIsReported()
		{
			var calendar = await SignedInCalendarAsync();
			await calendar.ListAsync(default);
			_server.FailNext(new Fault(500, "backendError", "global", "/events"), times: 10);

			var problem = await Assert.ThrowsAsync<CalendarServiceException>(() => calendar.AddAsync(Reminder("Appointment today"), default));

			Assert.Equal(CalendarProblem.Refused, problem.Problem);
			Assert.Equal(3, _waits.Count);
		}

		[Theory]
		[InlineData(400, "invalid")]
		[InlineData(403, "quotaExceeded")]
		[InlineData(403, "forbidden")]
		public async Task AFlatRefusalIsNotRetried(int status, string reason)
		{
			var calendar = await SignedInCalendarAsync();
			await calendar.ListAsync(default);
			_server.FailNext(new Fault(status, reason, "global", "/events"));

			var problem = await Assert.ThrowsAsync<CalendarServiceException>(() => calendar.AddAsync(Reminder("Appointment today"), default));

			Assert.Equal(CalendarProblem.Refused, problem.Problem);
			Assert.Empty(_waits);
		}

		[Fact]
		public async Task NotReachingGoogleIsSaidAsSuch()
		{
			var calendar = await SignedInCalendarAsync();
			await calendar.ListAsync(default);
			_stopped = true;
			await _server.DisposeAsync();
			_clock.Advance(TimeSpan.FromHours(2));						// the next call has to renew its token, which is what fails

			var problem = await Assert.ThrowsAsync<CalendarServiceException>(() => calendar.ListAsync(default));

			Assert.Equal(CalendarProblem.NoConnection, problem.Problem);
		}


		[Fact]
		public async Task TheWholeRoundTripFromPeriodToCalendar()
		{
			var calendar = await SignedInCalendarAsync();
			var period = ClosedPeriod();

			var result = await CalendarSync.ApplyAsync(ReminderPlanner.PlanAll(new[] { period }, Options()), Now, calendar);
			Assert.Equal(new SyncResult(3, 0, 0), result);

			// Nothing to do the second time, beyond reading
			var writes = _server.Log.Count(el => !el.StartsWith("GET", StringComparison.Ordinal));
			Assert.Equal(new SyncResult(0, 0, 0), await CalendarSync.ApplyAsync(ReminderPlanner.PlanAll(new[] { period }, Options()), Now, calendar));
			Assert.Equal(writes, _server.Log.Count(el => !el.StartsWith("GET", StringComparison.Ordinal)));
		}

		[Fact]
		public async Task AChangeOfLanguageUpdatesEntriesInPlace()
		{
			var calendar = await SignedInCalendarAsync();
			var period = ClosedPeriod();
			await CalendarSync.ApplyAsync(ReminderPlanner.PlanAll(new[] { period }, Options()), Now, calendar);
			var before = _server.Calendars.Single().Events.Select(el => el["id"]!.ToString()).OrderBy(el => el).ToArray();

			var result = await CalendarSync.ApplyAsync(ReminderPlanner.PlanAll(new[] { period }, Options() with { Language = ReminderLanguage.Hebrew }), Now, calendar);

			Assert.Equal(new SyncResult(0, 3, 0), result);
			Assert.Equal(before, _server.Calendars.Single().Events.Select(el => el["id"]!.ToString()).OrderBy(el => el));
			Assert.Contains(_server.Calendars.Single().Events, el => el["summary"]!.ToString() == "תור הערב");
		}

		[Fact]
		public async Task ANewPeriodClearsAndRemovingItBringsThemBack()
		{
			var calendar = await SignedInCalendarAsync();
			var period = ClosedPeriod();
			await CalendarSync.ApplyAsync(ReminderPlanner.PlanAll(new[] { period }, Options()), Now, calendar);
			var open = new PurityPeriod(period.Begin.AddDays(20), period.Begin.AddDays(27));

			await CalendarSync.ApplyAsync(ReminderPlanner.PlanAll(new[] { period, open }, Options()), Now, calendar);
			Assert.Empty(await calendar.ListAsync(default));

			await CalendarSync.ApplyAsync(ReminderPlanner.PlanAll(new[] { period }, Options()), Now, calendar);
			Assert.Equal(3, (await calendar.ListAsync(default)).Count);
		}


		private static readonly DateTime Now = new(2031, 1, 1, 9, 0, 0);
		private static readonly TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone("Made up", TimeSpan.FromHours(2), "Made up", "Made up");

		private GoogleAccount Account() => new(_http, new GoogleClient(StandInServer.ClientId), new GoogleEndpoints(_server.Authorize, _server.Token, _server.Revoke, _server.Calendar), _secrets, _clock);

		private Task SignInAsync(GoogleAccount account) => account.SignInAsync(async url =>
		{
			using var browser = new HttpClient();
			using var response = await browser.GetAsync(url);			// follows the redirect back, as a browser would
		}, "Done", default);

		private GoogleCalendar Calendar(GoogleAccount account) => new(account, () => _calendarId, value => _calendarId = value, () => "Personal", Zone, (delay, _) =>
		{
			_waits.Add(delay);
			return Task.CompletedTask;
		});

		/// <summary>Another computer, signed in to the same account, that doesn't yet know the calendar's ID</summary>
		private GoogleCalendar SecondComputer(out Func<string?> id)
		{
			string? known = null;
			id = () => known;
			var account = Account();
			return new GoogleCalendar(account, () => known, value => known = value, () => "Personal", Zone, (_, _) => Task.CompletedTask);
		}

		private async Task<GoogleCalendar> SignedInCalendarAsync()
		{
			var account = Account();
			await SignInAsync(account);
			return Calendar(account);
		}

		private static Reminder Reminder(string title, int key = 0)
		{
			var period = new PurityPeriod(new DateTimeOffset(2031, 2, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2031, 2, 6, 0, 0, 0, TimeSpan.Zero));
			return new Reminder(ReminderTag.Of(period, new PurityEvent(PurityEventType.VesetAflaga, default, $"({key})")), new DateTime(2031, 3, 2, 8, 0, 0), title, PurityEventType.Mikveh);
		}

		private static ReminderOptions Options() => new() { Kinds = Enum.GetValues<PurityEventType>().ToHashSet() };

		private static PurityPeriod ClosedPeriod()
		{
			var period = new PurityPeriod(new DateTimeOffset(2031, 2, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2031, 2, 6, 0, 0, 0, TimeSpan.Zero)) { Closed = true };
			period.SubEvents.Add(new PurityEvent(PurityEventType.Mikveh, new DateTimeOffset(2031, 3, 2, 12, 0, 0, TimeSpan.Zero)));
			period.SubEvents.Add(new PurityEvent(PurityEventType.VesetHodesh, new DateTimeOffset(2031, 3, 3, 12, 0, 0, TimeSpan.Zero)));
			period.SubEvents.Add(new PurityEvent(PurityEventType.OnaBeinonit, new DateTimeOffset(2031, 3, 4, 0, 0, 0, TimeSpan.Zero)));
			return period;
		}


		private readonly HttpClient _http = new();
		private readonly MemorySecrets _secrets = new();
		private readonly MovableClock _clock = new();
		private readonly List<TimeSpan> _waits = new();
		private StandInServer _server = null!;
		private string? _calendarId;
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

		private sealed class MovableClock : TimeProvider
		{
			public void Advance(TimeSpan by) => _now += by;

			public override DateTimeOffset GetUtcNow() => _now;

			private DateTimeOffset _now = new(2031, 1, 1, 7, 0, 0, TimeSpan.Zero);
		}
	}
}
