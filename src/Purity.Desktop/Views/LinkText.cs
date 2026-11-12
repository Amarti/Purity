using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Path = Avalonia.Controls.Shapes.Path;


namespace Purity.Desktop.Views
{
	/// <summary>
	/// Text whose parts in square brackets are links within it, each with the usual mark for a page
	/// that opens outside the app. The links are real buttons, so the keyboard and screen readers
	/// reach them, and they open in the person's browser.
	/// </summary>
	public sealed partial class LinkText : TextBlock
	{
		public static readonly StyledProperty<string?> MarkedProperty = AvaloniaProperty.Register<LinkText, string?>(nameof(Marked));

		public static readonly StyledProperty<IReadOnlyList<Uri>?> LinksProperty = AvaloniaProperty.Register<LinkText, IReadOnlyList<Uri>?>(nameof(Links));


		/// <summary>The text, its links in square brackets</summary>
		public string? Marked
		{
			get => GetValue(MarkedProperty);
			set => SetValue(MarkedProperty, value);
		}

		/// <summary>Where each link goes, in order. A link without one is shown as plain text.</summary>
		public IReadOnlyList<Uri>? Links
		{
			get => GetValue(LinksProperty);
			set => SetValue(LinksProperty, value);
		}


		protected override Type StyleKeyOverride => typeof(TextBlock);

		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == MarkedProperty || change.Property == LinksProperty || change.Property == IsVisibleProperty)
				Build();
		}

		/// <summary>
		/// A control within text stands on the baseline whatever it's told, so a link, itself a line
		/// of text, would stand above the line by the text's descent and make that line taller. Each
		/// link hangs below the baseline by that much instead, its words on the line's own baseline.
		/// </summary>
		protected override Size MeasureOverride(Size availableSize)
		{
			if (_links.Count > 0)
			{
				using var probe = new TextLayout("x", new Typeface(FontFamily, FontStyle, FontWeight), FontSize, null);
				var scale = LayoutHelper.GetLayoutScale(this);
				var room = Math.Floor(probe.TextLines[0].Baseline * scale) / scale;		// above the baseline, in whole pixels, as layout rounds
				foreach (var link in _links)
				{
					link.Measure(Size.Infinity);
					var height = link.DesiredSize.Height - link.Margin.Top - link.Margin.Bottom;
					var above = link.BorderThickness.Top + link.Padding.Top;
					var hang = new Thickness(0, -above, 0, -Math.Max(0, height - above - room));
					if (link.Margin != hang)
						link.Margin = hang;
				}
			}
			return base.MeasureOverride(availableSize);
		}


		/// <summary>
		/// Hidden, the links are plain words. Avalonia still draws text hidden this way, and a link in it
		/// is laid out then, in the middle of drawing, which fails: opening the Reminders window on
		/// iCloud already connected crashed on a Mac. Text in a hidden panel isn't drawn, so is safe.
		/// </summary>
		private void Build()
		{
			var inlines = new InlineCollection();
			var text = Marked ?? string.Empty;
			var at = 0;
			var index = 0;
			_links.Clear();
			foreach (Match link in LinkPattern().Matches(text))
			{
				if (link.Index > at)
					inlines.Add(new Run(text[at..link.Index]));

				var words = link.Groups[1].Value;
				if (IsVisible && Links is { } links && index < links.Count)
				{
					var button = Link(words, links[index]);
					_links.Add(button);
					inlines.Add(new InlineUIContainer(button));
				}
				else
					inlines.Add(new Run(words));
				index++;
				at = link.Index + link.Length;
			}
			if (at < text.Length)
				inlines.Add(new Run(text[at..]));
			Inlines = inlines;
		}

		private static HyperlinkButton Link(string words, Uri page)
		{
			var button = new HyperlinkButton { NavigateUri = page, Padding = new Thickness(0) };
			var mark = OutsideMark();
			mark.Bind(Shape.StrokeProperty, button.GetObservable(HyperlinkButton.ForegroundProperty));
			button.Content = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				Spacing = 3,
				Children = { new TextBlock { Text = words, TextDecorations = Avalonia.Media.TextDecorations.Underline }, mark },
			};
			AutomationProperties.SetName(button, words);
			ToolTip.SetTip(button, page.Host);
			return button;
		}

		/// <summary>A box with an arrow leaving it, the usual mark for a page that opens outside</summary>
		private static Path OutsideMark()
		{
			var mark = new Path
			{
				Data = Geometry.Parse("M5,2 H2 V10 H10 V7 M7,2 H10 V5 M10,2 L5.5,6.5"),
				StrokeThickness = 1.2,
				StrokeLineCap = PenLineCap.Round,
				StrokeJoin = PenLineJoin.Round,
				Width = 10,
				Height = 10,
				Stretch = Stretch.Uniform,
				VerticalAlignment = VerticalAlignment.Center,
			};
			return mark;
		}


		[GeneratedRegex(@"\[([^\]]+)\]")]
		private static partial Regex LinkPattern();


		private readonly List<HyperlinkButton> _links = new();
	}
}
