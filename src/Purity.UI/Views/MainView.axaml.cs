using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Purity.UI.ViewModels;


namespace Purity.UI.Views
{
	/// <summary>The history and the buttons above it: all of the desktop's main window, and the Android app's first page</summary>
	public partial class MainView : UserControl
	{
		public static readonly StyledProperty<bool> IsCompactProperty = AvaloniaProperty.Register<MainView, bool>(nameof(IsCompact));


		public MainView()
		{
			InitializeComponent();								// fills in the named parts below, such as Messages, where messages and questions show
			Periods.AddHandler(KeyDownEvent, OnPeriodsKeyDown, RoutingStrategies.Tunnel);
		}


		/// <summary>The phone's layout, approved as sketched: a period's dates on lines of their own, room for fingers, and no space to spare at the edges</summary>
		public bool IsCompact
		{
			get => GetValue(IsCompactProperty);
			set => SetValue(IsCompactProperty, value);
		}


		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property != IsCompactProperty)
				return;

			var compact = IsCompact;
			Classes.Set("Compact", compact);
			OpenList.ItemTemplate = (IDataTemplate)Resources[compact ? "CompactPeriodCardTemplate" : "PeriodCardTemplate"]!;
			Periods.ItemTemplate = (IDataTemplate)Resources[compact ? "CompactPeriodListTemplate" : "PeriodListTemplate"]!;
			var edge = new GridLength(compact ? 8 : 20);
			Layout.ColumnDefinitions[0].Width = edge;
			Layout.ColumnDefinitions[2].Width = edge;
			Layout.MaxWidth = compact ? double.PositiveInfinity : 620;
			OpenList.Margin = compact ? new Thickness(0, 0, 0, 8) : new Thickness(4, 0, 22, 8);	// a phone's scroll bar floats over the list rather than beside it
			ScrollViewer.SetVerticalScrollBarVisibility(Periods, compact ? ScrollBarVisibility.Auto : ScrollBarVisibility.Visible);
		}


		protected override void OnDataContextChanged(EventArgs e)
		{
			base.OnDataContextChanged(e);
			if (_viewModel != null)
			{
				_viewModel.PropertyChanged -= OnViewModelChanged;
				_viewModel.ClosedPeriods.CollectionChanged -= OnPeriodsChanged;
			}

			_viewModel = DataContext as MainWindowViewModel;
			if (_viewModel == null)
				return;

			_viewModel.PropertyChanged += OnViewModelChanged;
			_viewModel.ClosedPeriods.CollectionChanged += OnPeriodsChanged;
			_dropClosesPeriod = _viewModel.HasOpenPeriod;
		}

		protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
		{
			base.OnAttachedToVisualTree(e);

			// What counts as past changes at midnight, even with the app left open
			_newDay?.Dispose();
			_newDay = DispatcherTimer.Run(() =>
			{
				if (IsEffectivelyVisible)
					_viewModel?.NoticeNewDay();
				return true;
			}, TimeSpan.FromMinutes(1));
		}

		protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
		{
			base.OnDetachedFromVisualTree(e);
			_newDay?.Dispose();
			_newDay = null;
		}


		private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(MainWindowViewModel.HasOpenPeriod))
				PopDropIfChanged();
		}

		/// <summary>Shows the newest periods whenever the list changes, as the WPF app does; here they're at the top</summary>
		private void OnPeriodsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
			Dispatcher.UIThread.Post(ScrollToNewest, DispatcherPriority.Background);

		/// <summary>Draws the eye to the wide button when what it does changes (E4), along with the fade between its faces in the styles</summary>
		private void PopDropIfChanged()
		{
			if (_viewModel == null || _viewModel.HasOpenPeriod == _dropClosesPeriod)
				return;

			_dropClosesPeriod = _viewModel.HasOpenPeriod;
			_ = DropPop.RunAsync(_dropClosesPeriod ? ClosePeriodDrop : NewPeriodDrop);
		}

		private static KeyFrame DropSize(double cue, double scale) => new()
		{
			Cue = new Cue(cue),
			Setters =
			{
				new Setter(ScaleTransform.ScaleXProperty, scale),
				new Setter(ScaleTransform.ScaleYProperty, scale),
			},
		};

		private void ScrollToNewest()
		{
			if (Periods.Scroll is ScrollViewer viewer)
				viewer.ScrollToHome();
		}

		/// <summary>
		/// Page Up and Page Down turn the list a screen at a time, wherever the focus is in it. A
		/// period's list of events would otherwise take them, and do nothing visible.
		/// </summary>
		private void OnPeriodsKeyDown(object? sender, KeyEventArgs e)
		{
			// An open calendar is outside the list's own tree, and uses them to change month
			if (e.KeyModifiers != KeyModifiers.None || Periods.Scroll is not ScrollViewer viewer
				|| e.Source is not Visual source || source != Periods && !Periods.IsVisualAncestorOf(source))
				return;

			if (e.Key == Key.PageUp)
				viewer.PageUp();
			else if (e.Key == Key.PageDown)
				viewer.PageDown();
			else
				return;
			e.Handled = true;
		}


		private static readonly Animation DropPop = new()
		{
			Duration = TimeSpan.FromMilliseconds(380),
			Easing = new CubicEaseOut(),
			Children = { DropSize(0, 0.6), DropSize(0.6, 1.25), DropSize(1, 1) },
		};

		private MainWindowViewModel? _viewModel;
		private bool _dropClosesPeriod;
		private IDisposable? _newDay;
	}
}
