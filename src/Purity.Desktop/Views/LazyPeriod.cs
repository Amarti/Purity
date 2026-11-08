using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Purity.Core;
using Purity.Desktop.ViewModels;


namespace Purity.Desktop.Views
{
	/// <summary>
	/// One period in the main window's list, built only once it comes near the part of the list on
	/// screen. Until then it takes exactly the height it will have, which it knows from a period with
	/// as many events, since everything in a period has a fixed height. So the list knows its full
	/// height from the start, and the scroll bar never has to guess.
	/// </summary>
	/// <remarks>
	/// Avalonia's virtualizing list guesses the height of the items it hasn't built yet. With periods
	/// of different heights the guess kept changing as it scrolled, so the list jumped, and
	/// scrolling down could loop between two places without ever reaching the end. Building every
	/// period at once fixes that too, but takes over a second for 100 periods.
	/// </remarks>
	internal sealed class LazyPeriod : Decorator
	{
		public static readonly StyledProperty<IDataTemplate?> PeriodTemplateProperty =
			AvaloniaProperty.Register<LazyPeriod, IDataTemplate?>(nameof(PeriodTemplate));

		public LazyPeriod()
		{
			EffectiveViewportChanged += (_, e) =>
			{
				if (Child == null && IsNear(e.EffectiveViewport))
					Build();
			};
		}


		public IDataTemplate? PeriodTemplate
		{
			get => GetValue(PeriodTemplateProperty);
			set => SetValue(PeriodTemplateProperty, value);
		}

		/// <summary>Whether the period's controls exist, for tests</summary>
		internal bool IsBuilt => Child != null;


		protected override void OnDataContextChanged(EventArgs e)
		{
			if (_events != null)
				_events.CollectionChanged -= OnEventsChanged;
			_events = (DataContext as PeriodViewModel)?.SubEvents;
			if (_events != null)
				_events.CollectionChanged += OnEventsChanged;

			InvalidateMeasure();
			base.OnDataContextChanged(e);
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			var count = _events?.Count ?? 0;
			if (Child == null && Heights.TryGetValue(count, out var height))
				return new Size(0, height);

			// The first period with this many events shows how tall they all are
			Build();
			var size = base.MeasureOverride(availableSize);
			if (Child != null)
				Heights[count] = size.Height;
			return size;
		}


		/// <summary>Within a screen's height of what's on screen, so it's ready before it scrolls into view</summary>
		private bool IsNear(Rect viewport) =>
			viewport.Height > 0 && viewport.Top - viewport.Height < Bounds.Height && viewport.Bottom + viewport.Height > 0;

		private void Build()
		{
			if (Child == null && PeriodTemplate != null)
				Child = PeriodTemplate.Build(DataContext);
		}

		private void OnEventsChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateMeasure();


		/// <summary>The height of a period, by its number of events</summary>
		private static readonly Dictionary<int, double> Heights = new();

		private ObservableCollection<PurityEvent>? _events;
	}
}
