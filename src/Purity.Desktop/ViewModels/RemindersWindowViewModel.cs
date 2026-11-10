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


	/// <summary>The choices in the Reminders window. Nothing is kept until OK.</summary>
	public sealed partial class RemindersWindowViewModel : ObservableObject
	{
		/// <param name="setup">The choices as saved</param>
		/// <param name="periods">The whole history</param>
		/// <param name="clock">What time it is</param>
		/// <param name="save">Keeps the choices, on OK</param>
		public RemindersWindowViewModel(ReminderSetup setup, IReadOnlyList<PurityPeriod> periods, TimeProvider clock, Action<ReminderSetup>? save = null)
		{
			_periods = periods;
			_clock = clock;
			_save = save;
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
			_ready = true;
			Refresh();
		}


		/// <summary>The times an alert can be set to, every half hour</summary>
		public IReadOnlyList<string> TimeChoices { get; } = Enumerable.Range(0, 48)
			.Select(el => new TimeOnly(el / 2, el % 2 * 30).ToString(TimeFormat, CultureInfo.InvariantCulture))
			.ToArray();

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
			var name = string.IsNullOrWhiteSpace(CalendarName) ? Loc.T.RemindersDefaultCalendar : CalendarName.Trim();
			return IcsFile.Write(Planned(), name, _clock.GetUtcNow().UtcDateTime);
		}

		/// <summary>Keeps the choices, on OK</summary>
		public void Accept() => _save?.Invoke(new ReminderSetup
		{
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
		});


		partial void OnRemindMikvehChanged(bool value) => Refresh();
		partial void OnRemindVesetHachodeshChanged(bool value) => Refresh();
		partial void OnRemindVesetAflagaChanged(bool value) => Refresh();
		partial void OnRemindOnaBenonisChanged(bool value) => Refresh();
		partial void OnMikvehTimeChanged(string value) => Refresh();
		partial void OnOtherTimeChanged(string value) => Refresh();
		partial void OnMoveOffShabbatChanged(bool value) => Refresh();

		[RelayCommand]
		private void ToggleMore() => ShowsMore = !ShowsMore;

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

		private IReadOnlyList<Reminder> Planned() => ReminderPlanner.Plan(_periods, new ReminderOptions
		{
			Kinds = Kinds(),
			Language = Loc.Instance.Language == Language.Hebrew ? ReminderLanguage.Hebrew : ReminderLanguage.English,
			Wording = _wording,
			MikvehTime = TimeOnly.ParseExact(MikvehTime, TimeFormat, CultureInfo.InvariantCulture),
			OtherTime = TimeOnly.ParseExact(OtherTime, TimeFormat, CultureInfo.InvariantCulture),
			MoveOffShabbat = MoveOffShabbat,
		}, _clock.GetLocalNow().DateTime);

		private HashSet<PurityEventType> Kinds()
		{
			if (_scope == ReminderScope.MikvehOnly)
				return new HashSet<PurityEventType> { PurityEventType.Mikveh };
			if (_scope == ReminderScope.AllDates)
				return Enum.GetValues<PurityEventType>().ToHashSet();

			var kinds = new HashSet<PurityEventType>();
			if (RemindMikveh)
				kinds.Add(PurityEventType.Mikveh);
			if (RemindVesetHachodesh)
				kinds.Add(PurityEventType.VesetHodesh);
			if (RemindVesetAflaga)
				kinds.Add(PurityEventType.VesetAflaga);
			if (RemindOnaBenonis)
				kinds.Add(PurityEventType.OnaBeinonit);
			return kinds;
		}


		private const string TimeFormat = "HH:mm";
		private const int PreviewCount = 3;

		private readonly IReadOnlyList<PurityPeriod> _periods;
		private readonly TimeProvider _clock;
		private readonly Action<ReminderSetup>? _save;
		private readonly bool _ready;
		private ReminderScope _scope;
		private ReminderWording _wording;
	}
}
