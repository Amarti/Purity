using Avalonia;
using Avalonia.Controls;
using Purity.Core;


namespace Purity.Desktop.Views
{
	/// <summary>
	/// The kind of a date, shown as a colored chip with one color per kind (tweak T4, approved for
	/// 2.2.0). The colors come from the styles, by the pseudo-class this sets.
	/// </summary>
	public sealed class EventChip : Border
	{
		public static readonly StyledProperty<PurityEventType> TypeProperty =
			AvaloniaProperty.Register<EventChip, PurityEventType>(nameof(Type));


		public PurityEventType Type
		{
			get => GetValue(TypeProperty);
			set => SetValue(TypeProperty, value);
		}


		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property != TypeProperty)
				return;

			var type = Type;
			PseudoClasses.Set(":mikveh", type == PurityEventType.Mikveh);
			PseudoClasses.Set(":ona-beinonit", type == PurityEventType.OnaBeinonit);
			PseudoClasses.Set(":veset-hodesh", type == PurityEventType.VesetHodesh);
			PseudoClasses.Set(":veset-aflaga", type == PurityEventType.VesetAflaga);
		}
	}
}
