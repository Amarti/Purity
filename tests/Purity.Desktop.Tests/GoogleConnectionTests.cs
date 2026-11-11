using Purity.GoogleStandIn;
using Purity.Reminders;
using Purity.Reminders.Google;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>The app's own connection to Google, against the stand-in, and where it keeps the sign-in. Everything here is made up.</summary>
	[Collection(AppDataCollection.Name)]
	public sealed class GoogleConnectionTests : IAsyncLifetime
	{
		public async ValueTask InitializeAsync()
		{
			Directory.CreateDirectory(_root);
			Environment.SetEnvironmentVariable("PURITY_APP_DATA", Path.Combine(_root, "app data"));
			_server = await StandInServer.StartAsync();
		}

		public async ValueTask DisposeAsync()
		{
			await _server.DisposeAsync();
			Environment.SetEnvironmentVariable("PURITY_APP_DATA", null);
			try
			{
				Directory.Delete(_root, true);
			}
			catch (IOException)
			{
				// temp gets cleaned anyway
			}
		}


		[Fact]
		public void WithoutAnAppIdGoogleIsNotOffered()
		{
			var connection = new GoogleConnection(null, GoogleEndpoints.Real, new MemorySecrets(), new DesktopSettings(), _ => Task.CompletedTask);

			Assert.False(connection.IsAvailable);
			Assert.False(connection.IsConnected);
			Assert.Throws<InvalidOperationException>(() => connection.Calendar);
		}

		[Fact]
		public async Task ConnectingUpdatingAndStoppingAllTheWayThrough()
		{
			var settings = new DesktopSettings { Reminders = new ReminderSetup { CalendarName = "Family" } };
			var connection = Connection(settings);
			Assert.True(connection.IsAvailable);
			Assert.False(connection.IsConnected);

			await connection.ConnectAsync(default);
			Assert.True(connection.IsConnected);
			Assert.True(_server.HasLiveSignIn);

			await connection.Calendar.AddAsync(ReminderPlanner.TestReminder(ReminderLanguage.English, new DateTime(2031, 3, 2, 8, 0, 0)), default);
			Assert.Equal("Family", _server.Calendars.Single().Summary);
			Assert.Equal(_server.Calendars.Single().Id, settings.GoogleCalendarId);
			Assert.Equal(settings.GoogleCalendarId, DesktopSettings.Load().GoogleCalendarId);

			await connection.DisconnectAsync(default);

			Assert.False(connection.IsConnected);
			Assert.False(_server.HasLiveSignIn);
			Assert.Empty(_server.Calendars);
			Assert.Null(settings.GoogleCalendarId);
		}

		[Fact]
		public async Task ANewCalendarGetsTheUsualNameWhenNoneIsChosen()
		{
			var connection = Connection(new DesktopSettings());
			await connection.ConnectAsync(default);

			await connection.Calendar.ListAsync(default);

			Assert.Equal("Purity", _server.Calendars.Single().Summary);
		}

		[Fact]
		public async Task ASecondComputerUsesTheSameCalendar()
		{
			var first = Connection(new DesktopSettings());
			await first.ConnectAsync(default);
			await first.Calendar.ListAsync(default);
			var second = new DesktopSettings();
			var other = Connection(second);
			await other.ConnectAsync(default);

			await other.Calendar.ListAsync(default);

			Assert.Equal(_server.Calendars.Single().Id, second.GoogleCalendarId);
		}

		[Fact]
		public async Task AfterASignInEndedAtGoogleStoppingStillForgetsIt()
		{
			var settings = new DesktopSettings();
			var connection = Connection(settings);
			await connection.ConnectAsync(default);
			await connection.Calendar.ListAsync(default);
			_server.RevokeEverything();

			await connection.DisconnectAsync(default);

			Assert.False(connection.IsConnected);
		}

		[Fact]
		public void TheStandInSwitchOnlyExistsInDebugBuilds()
		{
			var options = StartOptions.Parse(new[] { "--google-stand-in", "http://127.0.0.1:1234/" });
#if DEBUG
			Assert.Equal(new Uri("http://127.0.0.1:1234/"), options.GoogleStandIn);
#else
			Assert.Null(options.GoogleStandIn);
#endif
		}

		[Fact]
		public void OnWindowsTheSignInIsEncryptedForThisUser()
		{
			if (!OperatingSystem.IsWindows())
				return;

			var file = Path.Combine(_root, "secret");
			var store = new WindowsSecretStore(file);
			Assert.Null(store.Read());

			Assert.True(store.Write("a made-up token"));

			Assert.Equal("a made-up token", store.Read());
			Assert.DoesNotContain("made-up", System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(file)));

			File.WriteAllBytes(file, new byte[] { 1, 2, 3 });
			Assert.Null(store.Read());								// damaged means signed out, not a crash

			store.Delete();
			Assert.False(File.Exists(file));
			store.Delete();											// deleting what's gone is fine
		}

		[Fact]
		public void AMissingSystemToolIsAnAnswerNotACrash() =>
			Assert.False(SecretStores.Run("no-such-tool-anywhere", Array.Empty<string>()).Worked);


		private GoogleConnection Connection(DesktopSettings settings) => new(
			new GoogleClient(StandInServer.ClientId),
			new GoogleEndpoints(_server.Authorize, _server.Token, _server.Revoke, _server.Calendar),
			new MemorySecrets(),
			settings,
			async url =>
			{
				using var browser = new HttpClient();
				using var response = await browser.GetAsync(url);
			});


		private readonly string _root = Path.Combine(Path.GetTempPath(), $"purity-google-{Guid.NewGuid():N}");
		private StandInServer _server = null!;


		private sealed class MemorySecrets : ISecretStore
		{
			public string? Read() => _value;

			public bool Write(string secret)
			{
				_value = secret;
				return true;
			}

			public void Delete() => _value = null;

			private string? _value;
		}
	}
}
