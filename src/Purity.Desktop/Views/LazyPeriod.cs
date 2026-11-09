using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Threading;
using Purity.Core;
using Purity.Desktop.ViewModels;


namespace Purity.Desktop.Views
{
	/// <summary>
	/// One period in the main window's list, built only once it comes near the part of the list on
	/// screen. Until then it takes exactly the height it will have, which it knows from a period with
	/// as many events, or from any other folded one, since everything in a period has a fixed height.
	/// So the list knows its full height from the start, and the scroll bar never has to guess.
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

		public static readonly StyledProperty<IDataTemplate?> FoldedTemplateProperty =
			AvaloniaProperty.Register<LazyPeriod, IDataTemplate?>(nameof(FoldedTemplate));

		public static readonly StyledProperty<bool> IsFoldedProperty =
			AvaloniaProperty.Register<LazyPeriod, bool>(nameof(IsFolded));

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

		/// <summary>The single line a folded period shows instead (T2)</summary>
		public IDataTemplate? FoldedTemplate
		{
			get => GetValue(FoldedTemplateProperty);
			set => SetValue(FoldedTemplateProperty, value);
		}

		public bool IsFolded
		{
			get => GetValue(IsFoldedProperty);
			set => SetValue(IsFoldedProperty, value);
		}

		/// <summary>Whether the period's controls exist, for tests</summary>
		internal bool IsBuilt => Child != null;


		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property != IsFoldedProperty)
				return;

			InvalidateMeasure();				// even one not built yet changes height
			if (Child == null)
				return;

			Child = null;
			Build();
			if (!IsFolded)
				Dispatcher.UIThread.Post(() => Child?.BringIntoView(), DispatcherPriority.Background);		// opened near the bottom, it would be mostly out of sight
		}

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
			var kind = IsFolded ? Folded : (_events?.Count ?? 0) + ((DataContext as PeriodViewModel)?.HasTodayLine == true ? WithTodayLine : 0);
			if (Child == null && Heights.TryGetValue(kind, out var height))
				return new Size(0, height);

			// The first period of this kind shows how tall they all are
			Build();
			var size = base.MeasureOverride(availableSize);
			if (Child != null)
				Heights[kind] = size.Height;
			return size;
		}


		/// <summary>Within a screen's height of what's on screen, so it's ready before it scrolls into view</summary>
		private bool IsNear(Rect viewport) =>
			viewport.Height > 0 && viewport.Top - viewport.Height < Bounds.Height && viewport.Bottom + viewport.Height > 0;

		private void Build()
		{
			if (Child == null && (IsFolded ? FoldedTemplate : PeriodTemplate) is { } template)
				Child = template.Build(DataContext);
		}

		private void OnEventsChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateMeasure();


		/// <summary>The kind of period in <see cref="Heights"/> that a folded one is; otherwise the kind is its number of events</summary>
		private const int Folded = -1;

		/// <summary>Added to the kind of a period with the line marking today, which makes it taller</summary>
		private const int WithTodayLine = 1000;

		/// <summary>The height of a period, by its kind</summary>
		private static readonly Dictionary<int, double> Heights = new();

		private ObservableCollection<PurityEvent>? _events;
	}
}
