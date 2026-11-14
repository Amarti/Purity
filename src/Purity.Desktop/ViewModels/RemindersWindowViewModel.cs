using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Purity.Core;
using Purity.Desktop.Localization;
using Purity.Reminders;


namespace Purity.Desktop.ViewModels
{
	/// <param name="Title">What the reminder will say</param>
	/// <param name="When">When it will fire, in the language being shown</param>
	public sealed record ReminderPreview(string Title, string When);


	/// <summary>The choices in the Reminders window. Nothing is kept until OK, except a connection, which is made or ended on the spot.</summary>
	public sealed partial class RemindersWindowViewModel : ObservableObject
	{
		/// <param name="setup">The choices as saved</param>
		/// <param name="periods">The whole history</param>
		/// <param name="clock">What time it is</param>
		/// <param name="save">Keeps the choices, on OK</param>
		/// <param name="connections">The connection to each calendar service there can be one to</param>
		public RemindersWindowViewModel(ReminderSetup setup, IReadOnlyList<PurityPeriod> periods, TimeProvider clock, Action<ReminderSetup>? save = null, ReminderConnections? connections = null)
		{
			_periods = periods;
			_clock = clock;
			_save = save;
			_connections = connections;
			_where = setup.Where == ReminderPlace.File || connections?.For(setup.Where)?.IsAvailable == true ? setup.Where : ReminderPlace.File;
			_scope = setup.Scope;
			_wording = setup.Wording;
			RemindMikveh = setup.Mikveh;
			RemindVesetHachodesh = setup.VesetHachodesh;
			RemindVesetAflaga = setup.VesetAflaga;
			RemindOnaBenonis = setup.OnaBenonis;
			MikvehTime = TimeChoices.Contains(setup.MikvehTime) ? setup.MikvehTime : ReminderSetup.DefaultMikvehTime;
			OtherTime = TimeChoices.Contains(setup.OtherTime) ? setup.OtherTime : ReminderSetup.DefaultOtherTime;
			MoveOffShabbat = setup.MoveOffShabbat;
			_inIsrael = setup.InIsrael ?? YomTov.IsIsraelZone(clock.LocalTimeZone);
			CalendarName = setup.CalendarName;
			IsConnected = Connection?.IsConnected == true;
			_ready = true;
			Refresh();
		}


		/// <summary>The times an alert can be set to, every half hour</summary>
		public IReadOnlyList<string> TimeChoices { get; } = Enumerable.Range(0, 48)
			.Select(el => new TimeOnly(el / 2, el % 2 * 30).ToString("HH:mm", CultureInfo.InvariantCulture))
			.ToArray();

		public bool CanUseGoogle => _connections?.For(ReminderPlace.Google)?.IsAvailable == true;

		public bool CanUseICloud => _connections?.For(ReminderPlace.ICloud)?.IsAvailable == true;

		public bool CanUseCalDav => _connections?.For(ReminderPlace.CalDav)?.IsAvailable == true;

		public string GoogleTip => CanUseGoogle ? Loc.T.RemindersGoogleTip : Loc.T.RemindersGoogleNotInBuild;

		public bool IsFile
		{
			get => _where == ReminderPlace.File;
			set
			{
				if (value)
					Choose(ReminderPlace.File);
			}
		}

		public bool IsGoogle
		{
			get => _where == ReminderPlace.Google;
			set
			{
				if (value)
					Choose(ReminderPlace.Google);
			}
		}

		public bool IsICloud
		{
			get => _where == ReminderPlace.ICloud;
			set
			{
				if (value)
					Choose(ReminderPlace.ICloud);
			}
		}

		public bool IsCalDav
		{
			get => _where == ReminderPlace.CalDav;
			set
			{
				if (value)
					Choose(ReminderPlace.CalDav);
			}
		}

		/// <summary>Whether reminders go to a calendar service, not a file</summary>
		public bool IsService => _where != ReminderPlace.File;

		/// <summary>Connected to the service chosen</summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(CanConnect), nameof(ShowsSignIn))]
		public partial bool IsConnected { get; private set; }

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(CanConnect), nameof(ShowsSignIn))]
		public partial bool IsConnecting { get; private set; }

		/// <summary>Whether there's a sign-in to start</summary>
		public bool CanConnect => !IsConnected && !IsConnecting;

		/// <summary>Whether the service chosen takes a user name and password, typed here</summary>
		public bool TakesPassword => Connection?.TakesPassword == true;

		public bool ShowsSignIn => TakesPassword && CanConnect;

		/// <summary>The address of a CalDAV server, which iCloud and Google don't need</summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(HasSignIn))]
		public partial string SignInServer { get; set; } = string.Empty;

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(HasSignIn))]
		public partial string SignInUser { get; set; } = string.Empty;

		/// <summary>Cleared once it's used, so it's held no longer than it must be</summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(HasSignIn))]
		public partial string SignInPassword { get; set; } = string.Empty;

		/// <summary>Whether everything the sign-in takes has been typed</summary>
		public bool HasSignIn => !TakesPassword || (SignInUser.Trim().Length > 0 && SignInPassword.Trim().Length > 0 && (!IsCalDav || SignInServer.Trim().Length > 0));

		/// <summary>The links in <see cref="ServiceIntro"/>: for iCloud, where to make an app-specific password, and Apple's page about them</summary>
		public IReadOnlyList<Uri> IntroLinks => IsICloud ? new[] { AppPasswordPage, AppPasswordHelp } : Array.Empty<Uri>();

		public Uri AppPasswordPage { get; } = new("https://account.apple.com/");

		/// <summary>Apple's page on app-specific passwords, in the language on show</summary>
		public Uri AppPasswordHelp => new(Loc.Instance.Language == Language.Hebrew ? "https://support.apple.com/he-il/102654" : "https://support.apple.com/en-il/102654");

		public string ICloudTip => Loc.T.RemindersICloudTip;

		public string CalDavTip => Loc.T.RemindersCalDavTip;

		/// <summary>What the service chosen does with the dates. Its parts in square brackets are links to <see cref="IntroLinks"/>.</summary>
		public string ServiceIntro => _where switch
		{
			ReminderPlace.ICloud => Loc.T.RemindersICloudIntro,
			ReminderPlace.CalDav => Loc.T.RemindersCalDavIntro,
			_ => Loc.T.RemindersGoogleIntro,
		};

		public string ConnectText => _where switch
		{
			ReminderPlace.ICloud => Loc.T.RemindersConnectICloud,
			ReminderPlace.CalDav => Loc.T.RemindersConnectCalDav,
			_ => Loc.T.RemindersConnect,
		};

		public string ConnectingText => _where == ReminderPlace.Google ? Loc.T.RemindersConnecting : Loc.T.RemindersConnectingServer;

		public string UserLabel => IsICloud ? Loc.T.RemindersAppleAccount : Loc.T.RemindersUserName;

		public string PasswordLabel => IsICloud ? Loc.T.RemindersAppSpecificPassword : Loc.T.RemindersPassword;

		public string DisconnectQuestion => _where switch
		{
			ReminderPlace.ICloud => Loc.T.RemindersDisconnectICloudQuestion,
			ReminderPlace.CalDav => Loc.T.RemindersDisconnectCalDavQuestion,
			_ => Loc.T.RemindersDisconnectQuestion,
		};

		public string DisconnectBody => _where switch
		{
			ReminderPlace.ICloud => Loc.T.RemindersDisconnectICloudBody,
			ReminderPlace.CalDav => Loc.T.RemindersDisconnectCalDavBody,
			_ => Loc.T.RemindersDisconnectBody,
		};

		public bool IsMikvehOnly
		{
			get => _scope == ReminderScope.MikvehOnly;
			set
			{
				if (value)
					Choose(ReminderScope.MikvehOnly);
			}
		}

		public bool IsAllDates
		{
			get => _scope == ReminderScope.AllDates;
			set
			{
				if (value)
					Choose(ReminderScope.AllDates);
			}
		}

		public bool IsChosen
		{
			get => _scope == ReminderScope.Chosen;
			set
			{
				if (value)
					Choose(ReminderScope.Chosen);
			}
		}

		[ObservableProperty]
		public partial bool RemindMikveh { get; set; }

		[ObservableProperty]
		public partial bool RemindVesetHachodesh { get; set; }

		[ObservableProperty]
		public partial bool RemindVesetAflaga { get; set; }

		[ObservableProperty]
		public partial bool RemindOnaBenonis { get; set; }

		public bool IsDiscreet
		{
			get => _wording == ReminderWording.Discreet;
			set
			{
				if (value)
					Choose(ReminderWording.Discreet);
			}
		}

		public bool IsDetailed
		{
			get => _wording == ReminderWording.Detailed;
			set
			{
				if (value)
					Choose(ReminderWording.Detailed);
			}
		}

		[ObservableProperty]
		public partial string MikvehTime { get; set; } = ReminderSetup.DefaultMikvehTime;

		[ObservableProperty]
		public partial string OtherTime { get; set; } = ReminderSetup.DefaultOtherTime;

		[ObservableProperty]
		public partial bool MoveOffShabbat { get; set; }

		/// <summary>One day of Yom Tov, as in Israel. Shown from the computer's time zone until it's chosen.</summary>
		public bool IsInIsrael
		{
			get => _inIsrael;
			set
			{
				if (value)
					ChooseYomTov(true);
			}
		}

		/// <summary>Two days of Yom Tov, as kept outside Israel</summary>
		public bool IsOutsideIsrael
		{
			get => !_inIsrael;
			set
			{
				if (value)
					ChooseYomTov(false);
			}
		}

		[ObservableProperty]
		public partial string CalendarName { get; set; } = string.Empty;

		[ObservableProperty]
		public partial bool ShowsMore { get; private set; }

		/// <summary>The next few reminders, as these choices would make them</summary>
		[ObservableProperty]
		public partial IReadOnlyList<ReminderPreview> Preview { get; private set; } = Array.Empty<ReminderPreview>();

		/// <summary>Why there's nothing to preview, or null when there is</summary>
		[ObservableProperty]
		public partial string? PreviewNote { get; private set; }

		[ObservableProperty]
		public partial bool CanSaveFile { get; private set; }


		/// <summary>The reminders as a calendar file</summary>
		public string BuildFile()
		{
			var name = string.IsNullOrWhiteSpace(CalendarName) ? ReminderSetup.DefaultCalendarName : CalendarName.Trim();
			return IcsFile.Write(Planned(), name, _clock.GetUtcNow().UtcDateTime);
		}

		/// <summary>Keeps the choices, on OK</summary>
		public void Accept() => _save?.Invoke(CurrentSetup());

		/// <summary>Signs in: in the person's browser for Google, or with the user name and password typed here</summary>
		/// <returns>What to tell the person, or null when there's nothing to say</returns>
		public async Task<ReminderNotice?> ConnectAsync()
		{
			if (Connection is not { } connection || IsConnecting || !HasSignIn)
				return null;

			var place = _where;
			using var waiting = _connecting = new CancellationTokenSource();
			IsConnecting = true;
			try
			{
				var signIn = connection.TakesPassword ? new ReminderSignIn(SignInServer, SignInUser, SignInPassword) : null;
				await connection.ConnectAsync(signIn, waiting.Token);
				SignInPassword = string.Empty;
				IsConnected = Connection?.IsConnected == true;
				if (await connection.Choices.ReadChoicesAsync(waiting.Token) is { } shared)
					Show(shared);							// another computer has already set them
				return connection.RemembersSignIn ? null : new ReminderNotice(Loc.T.RemindersNotRemembered, MessageKind.Problem);
			}
			catch (OperationCanceledException)
			{
				return null;								// the person gave up waiting, and knows it
			}
			catch (CalendarServiceException e)
			{
				return new ReminderNotice(ReminderProblems.Say(e.Problem, place), MessageKind.Problem);
			}
			finally
			{
				_connecting = null;
				IsConnecting = false;
			}
		}

		public void CancelConnecting() => _connecting?.Cancel();

		/// <summary>Adds a reminder that fires a couple of minutes from now, to prove the alerts arrive</summary>
		public async Task<ReminderNotice> SendTestAsync()
		{
			try
			{
				await Connection!.Calendar.AddAsync(ReminderPlanner.TestReminder(Loc.Instance.ReminderLanguage, _clock.GetLocalNow().DateTime), CancellationToken.None);
				return new ReminderNotice(Loc.T.RemindersTestSent, MessageKind.Done);
			}
			catch (CalendarServiceException e)
			{
				IsConnected = Connection!.IsConnected;
				return new ReminderNotice(ReminderProblems.Say(e.Problem, _where), MessageKind.Problem);
			}
		}

		/// <summary>Removes the calendar and ends the sign-in</summary>
		/// <returns>What to tell the person, or null when it all went well</returns>
		public async Task<ReminderNotice?> DisconnectAsync()
		{
			try
			{
				await Connection!.DisconnectAsync(CancellationToken.None);
				IsConnected = Connection.IsConnected;
				return null;
			}
			catch (CalendarServiceException e)
			{
				return new ReminderNotice(ReminderProblems.Say(e.Problem, _where), MessageKind.Problem);
			}
		}


		partial void OnRemindMikvehChanged(bool value) => Refresh();
		partial void OnRemindVesetHachodeshChanged(bool value) => Refresh();
		partial void OnRemindVesetAflagaChanged(bool value) => Refresh();
		partial void OnRemindOnaBenonisChanged(bool value) => Refresh();
		partial void OnMikvehTimeChanged(string value) => Refresh();
		partial void OnOtherTimeChanged(string value) => Refresh();
		partial void OnMoveOffShabbatChanged(bool value) => Refresh();

		[RelayCommand]
		private void ToggleMore() => ShowsMore = !ShowsMore;

		private void Show(SharedChoices shared)
		{
			var setup = new ReminderSetup();
			setup.Adopt(shared);
			_scope = setup.Scope;
			_wording = setup.Wording;
			RemindMikveh = setup.Mikveh;
			RemindVesetHachodesh = setup.VesetHachodesh;
			RemindVesetAflaga = setup.VesetAflaga;
			RemindOnaBenonis = setup.OnaBenonis;
			MikvehTime = TimeChoices.Contains(setup.MikvehTime) ? setup.MikvehTime : ReminderSetup.DefaultMikvehTime;
			OtherTime = TimeChoices.Contains(setup.OtherTime) ? setup.OtherTime : ReminderSetup.DefaultOtherTime;
			MoveOffShabbat = setup.MoveOffShabbat;
			if (shared.InIsrael is { } inIsrael)				// an earlier version didn't share it, so what's shown stays
				_inIsrael = inIsrael;
			OnPropertyChanged(nameof(IsInIsrael));
			OnPropertyChanged(nameof(IsOutsideIsrael));
			OnPropertyChanged(nameof(IsMikvehOnly));
			OnPropertyChanged(nameof(IsAllDates));
			OnPropertyChanged(nameof(IsChosen));
			OnPropertyChanged(nameof(IsDiscreet));
			OnPropertyChanged(nameof(IsDetailed));
			Refresh();
		}

		private void Choose(ReminderPlace where)
		{
			_where = where;
			IsConnected = Connection?.IsConnected == true;
			foreach (var name in new[] { nameof(IsFile), nameof(IsGoogle), nameof(IsICloud), nameof(IsCalDav), nameof(IsService), nameof(IntroLinks), nameof(TakesPassword), nameof(ShowsSignIn), nameof(HasSignIn), nameof(ServiceIntro), nameof(ConnectText), nameof(ConnectingText), nameof(UserLabel), nameof(PasswordLabel), nameof(DisconnectQuestion), nameof(DisconnectBody) })
				OnPropertyChanged(name);
		}

		/// <summary>The connection to the service chosen, or null for a file</summary>
		private IReminderConnection? Connection => _where == ReminderPlace.File ? null : _connections?.For(_where);

		private void Choose(ReminderScope scope)
		{
			_scope = scope;
			OnPropertyChanged(nameof(IsMikvehOnly));
			OnPropertyChanged(nameof(IsAllDates));
			OnPropertyChanged(nameof(IsChosen));
			Refresh();
		}

		private void ChooseYomTov(bool inIsrael)
		{
			_inIsrael = inIsrael;
			OnPropertyChanged(nameof(IsInIsrael));
			OnPropertyChanged(nameof(IsOutsideIsrael));
			Refresh();
		}

		private void Choose(ReminderWording wording)
		{
			_wording = wording;
			OnPropertyChanged(nameof(IsDiscreet));
			OnPropertyChanged(nameof(IsDetailed));
			Refresh();
		}

		private void Refresh()
		{
			if (!_ready)
				return;

			var planned = Planned();
			Preview = planned.Take(PreviewCount)
				.Select(el => new ReminderPreview(el.Title, Loc.Instance.Moment(new DateTimeOffset(el.Moment, TimeSpan.Zero), withWeekday: true)))
				.ToArray();
			PreviewNote = planned.Count > 0 ? null : _periods.Any(el => !el.Closed) ? Loc.T.RemindersOpenPeriod : Loc.T.RemindersNone;
			CanSaveFile = planned.Count > 0;
		}

		private IReadOnlyList<Reminder> Planned() =>
			ReminderPlanner.Plan(_periods, CurrentSetup().ToOptions(Loc.Instance.ReminderLanguage, _clock.LocalTimeZone), _clock.GetLocalNow().DateTime);

		private ReminderSetup CurrentSetup() => new()
		{
			Where = _where,
			Scope = _scope,
			Mikveh = RemindMikveh,
			VesetHachodesh = RemindVesetHachodesh,
			VesetAflaga = RemindVesetAflaga,
			OnaBenonis = RemindOnaBenonis,
			Wording = _wording,
			MikvehTime = MikvehTime,
			OtherTime = OtherTime,
			MoveOffShabbat = MoveOffShabbat,
			InIsrael = _inIsrael,							// chosen now, even if only by the time zone
			CalendarName = CalendarName.Trim(),
		};


		private const int PreviewCount = 3;

		private readonly IReadOnlyList<PurityPeriod> _periods;
		private readonly TimeProvider _clock;
		private readonly Action<ReminderSetup>? _save;
		private readonly ReminderConnections? _connections;
		private readonly bool _ready;
		private ReminderPlace _where;
		private ReminderScope _scope;
		private ReminderWording _wording;
		private bool _inIsrael;
		private CancellationTokenSource? _connecting;
	}
}
