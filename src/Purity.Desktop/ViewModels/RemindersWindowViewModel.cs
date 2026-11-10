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
		/// <param name="connection">The connection to a calendar service, where there can be one</param>
		public RemindersWindowViewModel(ReminderSetup setup, IReadOnlyList<PurityPeriod> periods, TimeProvider clock, Action<ReminderSetup>? save = null, IReminderConnection? connection = null)
		{
			_periods = periods;
			_clock = clock;
			_save = save;
			_connection = connection;
			_where = setup.Where == ReminderPlace.Google && connection?.IsAvailable == true ? ReminderPlace.Google : ReminderPlace.File;
			_scope = setup.Scope;
			_wording = setup.Wording;
			RemindMikveh = setup.Mikveh;
			RemindVesetHachodesh = setup.VesetHachodesh;
			RemindVesetAflaga = setup.VesetAflaga;
			RemindOnaBenonis = setup.OnaBenonis;
			MikvehTime = TimeChoices.Contains(setup.MikvehTime) ? setup.MikvehTime : ReminderSetup.DefaultMikvehTime;
			OtherTime = TimeChoices.Contains(setup.OtherTime) ? setup.OtherTime : ReminderSetup.DefaultOtherTime;
			MoveOffShabbat = setup.MoveOffShabbat;
			CalendarName = setup.CalendarName;
			IsConnected = connection?.IsConnected == true;
			_ready = true;
			Refresh();
		}


		/// <summary>The times an alert can be set to, every half hour</summary>
		public IReadOnlyList<string> TimeChoices { get; } = Enumerable.Range(0, 48)
			.Select(el => new TimeOnly(el / 2, el % 2 * 30).ToString("HH:mm", CultureInfo.InvariantCulture))
			.ToArray();

		public bool CanUseGoogle => _connection?.IsAvailable == true;

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

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(CanConnect))]
		public partial bool IsConnected { get; private set; }

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(CanConnect))]
		public partial bool IsConnecting { get; private set; }

		public bool CanConnect => !IsConnected && !IsConnecting;

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

		/// <summary>Signs in, in the person's browser</summary>
		/// <returns>What to tell the person, or null when there's nothing to say</returns>
		public async Task<ReminderNotice?> ConnectAsync()
		{
			if (_connection == null || IsConnecting)
				return null;

			using var waiting = _connecting = new CancellationTokenSource();
			IsConnecting = true;
			try
			{
				await _connection.ConnectAsync(waiting.Token);
				IsConnected = _connection.IsConnected;
				if (await _connection.Choices.ReadChoicesAsync(waiting.Token) is { } shared)
					Show(shared);							// another computer has already set them
				return _connection.RemembersSignIn ? null : new ReminderNotice(Loc.T.RemindersNotRemembered, MessageKind.Problem);
			}
			catch (OperationCanceledException)
			{
				return null;								// the person gave up waiting, and knows it
			}
			catch (CalendarServiceException e)
			{
				return new ReminderNotice(ReminderProblems.Say(e.Problem), MessageKind.Problem);
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
				await _connection!.Calendar.AddAsync(ReminderPlanner.TestReminder(Loc.Instance.ReminderLanguage, _clock.GetLocalNow().DateTime), CancellationToken.None);
				return new ReminderNotice(Loc.T.RemindersTestSent, MessageKind.Done);
			}
			catch (CalendarServiceException e)
			{
				IsConnected = _connection!.IsConnected;
				return new ReminderNotice(ReminderProblems.Say(e.Problem), MessageKind.Problem);
			}
		}

		/// <summary>Removes the calendar and ends the sign-in</summary>
		/// <returns>What to tell the person, or null when it all went well</returns>
		public async Task<ReminderNotice?> DisconnectAsync()
		{
			try
			{
				await _connection!.DisconnectAsync(CancellationToken.None);
				IsConnected = _connection.IsConnected;
				return null;
			}
			catch (CalendarServiceException e)
			{
				return new ReminderNotice(ReminderProblems.Say(e.Problem), MessageKind.Problem);
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
			OnPropertyChanged(nameof(IsFile));
			OnPropertyChanged(nameof(IsGoogle));
		}

		private void Choose(ReminderScope scope)
		{
			_scope = scope;
			OnPropertyChanged(nameof(IsMikvehOnly));
			OnPropertyChanged(nameof(IsAllDates));
			OnPropertyChanged(nameof(IsChosen));
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
			ReminderPlanner.Plan(_periods, CurrentSetup().ToOptions(Loc.Instance.ReminderLanguage), _clock.GetLocalNow().DateTime);

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
			CalendarName = CalendarName.Trim(),
		};


		private const int PreviewCount = 3;

		private readonly IReadOnlyList<PurityPeriod> _periods;
		private readonly TimeProvider _clock;
		private readonly Action<ReminderSetup>? _save;
		private readonly IReminderConnection? _connection;
		private readonly bool _ready;
		private ReminderPlace _where;
		private ReminderScope _scope;
		private ReminderWording _wording;
		private CancellationTokenSource? _connecting;
	}
}
