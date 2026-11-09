using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;


namespace Purity.Desktop.Views
{
	/// <summary>
	/// A date's Day | Night switch, which the owner approved for 2.2.1 (T3b) in place of WPF's small
	/// icon button. It only shows what the period holds: pressing the half that's off runs the
	/// command, the same one WPF's button ran, and pressing the half that's on changes nothing.
	/// </summary>
	[TemplatePart("PART_Day", typeof(RadioButton))]
	[TemplatePart("PART_Night", typeof(RadioButton))]
	public sealed class DayNightSwitch : TemplatedControl
	{
		public static readonly StyledProperty<bool> IsNightProperty =
			AvaloniaProperty.Register<DayNightSwitch, bool>(nameof(IsNight));

		public static readonly StyledProperty<ICommand?> CommandProperty =
			AvaloniaProperty.Register<DayNightSwitch, ICommand?>(nameof(Command));


		public bool IsNight
		{
			get => GetValue(IsNightProperty);
			set => SetValue(IsNightProperty, value);
		}

		/// <summary>Moves the date to its other onah</summary>
		public ICommand? Command
		{
			get => GetValue(CommandProperty);
			set => SetValue(CommandProperty, value);
		}


		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);

			if (_day != null)
				_day.Click -= OnDayClick;
			if (_night != null)
				_night.Click -= OnNightClick;

			_day = e.NameScope.Find<RadioButton>("PART_Day");
			_night = e.NameScope.Find<RadioButton>("PART_Night");

			if (_day != null)
				_day.Click += OnDayClick;
			if (_night != null)
				_night.Click += OnNightClick;

			ShowIsNight();
		}

		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == IsNightProperty)
				ShowIsNight();
		}


		private void OnDayClick(object? sender, RoutedEventArgs e) => Choose(false);

		private void OnNightClick(object? sender, RoutedEventArgs e) => Choose(true);

		private void Choose(bool night)
		{
			if (night != IsNight)
				Command?.Execute(null);

			ShowIsNight();			// a press checks its half before the period has answered
		}

		private void ShowIsNight()
		{
			if (_day != null)
				_day.IsChecked = !IsNight;
			if (_night != null)
				_night.IsChecked = IsNight;
		}


		private RadioButton? _day;
		private RadioButton? _night;
	}
}
