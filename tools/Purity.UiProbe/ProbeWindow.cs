using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Purity.Core;


namespace Purity.UiProbe
{
	/// <summary>
	/// Engine text in the layout WPF uses, plus the controls the desktop app will need.
	/// Hebrew dates appear twice: A as WPF shows them, B reversed the way Avalonia 0.10 needed.
	/// Whichever reads correctly tells Phase 4 whether that workaround must stay.
	/// </summary>
	public sealed class ProbeWindow : Window
	{
		private readonly TextBlock _status = new() { Foreground = Brushes.DimGray, Text = "Waiting for you to try the controls" };
		private static readonly DateTime FirstShownDate = new(2027, 3, 14);
		private readonly CalendarDatePicker _picker = new() { SelectedDate = FirstShownDate, Width = 160 };
		private readonly CheckBox _night = new() { Content = "Night (after dusk)", VerticalAlignment = VerticalAlignment.Center };


		public ProbeWindow(bool auto, string output, string variant)
		{
			Title = "Purity check: " + variant;
			SizeToContent = SizeToContent.WidthAndHeight;
			WindowStartupLocation = WindowStartupLocation.CenterScreen;
			CanResize = false;

			var choose = new Button { Content = "Choose a file…" };
			choose.Click += async (_, _) => await ChooseFile();
			var close = new Button { Content = "Close" };
			close.Click += (_, _) => Close();
			_picker.SelectedDateChanged += (_, _) => ShowPicked();
			_night.IsCheckedChanged += (_, _) => ShowPicked();

			var panel = new StackPanel { Margin = new Thickness(20), Spacing = 10 };
			panel.Children.Add(Heading("Purity check"));
			panel.Children.Add(Text($"{RuntimeInformation.OSDescription}, {RuntimeInformation.ProcessArchitecture}"));
			panel.Children.Add(Text($"{RuntimeInformation.FrameworkDescription}, Avalonia {Program.AvaloniaVersion}"));
			panel.Children.Add(Heading("Engine text for made-up periods"));
			panel.Children.Add(EventTable());
			panel.Children.Add(Heading("Controls"));
			panel.Children.Add(Row(_picker, _night));
			panel.Children.Add(Row(choose, close));
			panel.Children.Add(_status);
			Content = new Border { Background = Brushes.White, Child = panel };

			Opened += (_, _) =>
			{
				Activate();
				Program.Report.Set("result", "window shown");
				Program.Report.Set("graphics", GraphicsName());
				Program.Report.Set("render scaling", RenderScaling);
				Program.Report.Save();
				if (auto)
					DispatcherTimer.RunOnce(() => SavePictureAndClose(output, variant), TimeSpan.FromSeconds(1.5));
			};
		}


		/// <summary>Three made-up periods early in 5787, a leap year, so events fall in both Adar I and Adar II</summary>
		private static IEnumerable<PurityEvent> MadeUpEvents()
		{
			var ledger = new PeriodLedger();
			foreach (var (begin, beginAtNight, end) in new[]
			{
				(new DateTime(2027, 1, 3), false, new DateTime(2027, 1, 8)),
				(new DateTime(2027, 1, 31), true, new DateTime(2027, 2, 5)),
				(new DateTime(2027, 3, 1), false, new DateTime(2027, 3, 6)),
			})
				ledger.AcceptPeriod(ledger.AddPeriod(Stamps.FromDate(begin, beginAtNight), Stamps.FromDate(end, false)));

			return ledger.Data.SelectMany(el => el.SubEvents);
		}

		private static Grid EventTable()
		{
			var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,Auto,Auto,Auto") };
			var rows = new List<string[]> { new[] { "Event", "A: as WPF", "B: old workaround", "Date", "Onah", "Note" } };
			rows.AddRange(MadeUpEvents().Select(el => new[]
			{
				el.TypeRepr, el.StampHebRepr, el.StampHebReprAvalonia, el.StampGrgRepr,
				el.IsFullDay ? "full day" : el.IsAfterDusk ? "night" : "day", el.Note ?? string.Empty,
			}));

			for (var row = 0; row < rows.Count; row++)
			{
				grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
				for (var column = 0; column < rows[row].Length; column++)
				{
					var cell = new TextBlock { Text = rows[row][column], Margin = new Thickness(0, 2, 18, 2) };
					if (row == 0)
						cell.FontWeight = FontWeight.SemiBold;
					Grid.SetRow(cell, row);
					Grid.SetColumn(cell, column);
					grid.Children.Add(cell);
				}
			}

			return grid;
		}

		private void ShowPicked()
		{
			if (_picker.SelectedDate is not { } date)
				return;

			var shown = new PurityEvent(PurityEventType.Mikveh, Stamps.FromDate(date, _night.IsChecked == true));
			_status.Text = $"Picked {shown.StampGrgRepr.Trim()} {(shown.IsAfterDusk ? "night" : "day")}: {shown.StampHebRepr}";
			// The controls also raise change events while they set up, so only a changed value counts as a person's action
			if (date.Date == FirstShownDate && _night.IsChecked != true)
				return;
			Program.Report.Set("date picker", "picked a date");
			Program.Report.Save();
		}

		private async Task ChooseFile()
		{
			try
			{
				var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Pick any file (it isn't opened)" });
				// Only the outcome is kept: a file name could be personal
				var outcome = files.Count == 0 ? "cancelled" : "picked a file";
				_status.Text = "File chooser: " + outcome;
				Program.Report.Set("file chooser", outcome);
			}
			catch (Exception e)
			{
				_status.Text = "File chooser failed: " + e.Message;
				Program.Report.Set("file chooser", "failed: " + e);
			}
			Program.Report.Save();
		}

		private void SavePictureAndClose(string output, string variant)
		{
			try
			{
				var content = (Control)Content!;
				var scale = RenderScaling;
				var size = new PixelSize((int)Math.Ceiling(content.Bounds.Width * scale), (int)Math.Ceiling(content.Bounds.Height * scale));
				using (var bitmap = new RenderTargetBitmap(size, new Vector(96 * scale, 96 * scale)))
				{
					bitmap.Render(content);
#pragma warning disable CS0618 // Avalonia 12 prefers an overload that 11.3 doesn't have
					bitmap.Save(Path.Combine(output, variant + ".png"));
#pragma warning restore CS0618
				}
				Program.Report.Set("picture", variant + ".png");
				Program.Report.Set("result", "passed");
			}
			catch (Exception e)
			{
				Program.Report.Set("result", "failed");
				Program.Report.Set("error", "saving the picture failed: " + e);
			}
			Program.Report.Save();
			Close();
		}

		/// <summary>Which GPU route Avalonia chose (Metal, OpenGL or none). Internal, so looked up by name and best effort only.</summary>
		private static string GraphicsName()
		{
			try
			{
				var assembly = typeof(AvaloniaObject).Assembly;
				var locator = assembly.GetType("Avalonia.AvaloniaLocator")?.GetProperty("Current")?.GetValue(null);
				var service = assembly.GetType("Avalonia.Platform.IPlatformGraphics");
				if (locator == null || service == null)
					return "unknown";
				var graphics = locator.GetType().GetMethod("GetService", new[] { typeof(Type) })?.Invoke(locator, new object[] { service });
				return graphics?.GetType().FullName ?? "none (software rendering)";
			}
			catch (Exception e)
			{
				return "unknown: " + e.GetType().Name;
			}
		}

		private static TextBlock Heading(string text) => new() { Text = text, FontSize = 16, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 6, 0, 0) };

		private static TextBlock Text(string text) => new() { Text = text, Foreground = Brushes.DimGray };

		private static StackPanel Row(params Control[] children)
		{
			var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
			row.Children.AddRange(children);
			return row;
		}
	}
}
