using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using Purity.Core;
using Purity.Desktop.Views;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>
	/// Scrolling past a date field must never change it. Avalonia's date picker moves its date a day
	/// per wheel notch while the pointer is over it, which nobody would notice before accepting.
	/// </summary>
	[Collection(AppDataCollection.Name)]
	public sealed class DateFieldTests : IDisposable
	{
		public DateFieldTests()
		{
			Directory.CreateDirectory(_root);
			Environment.SetEnvironmentVariable("PURITY_APP_DATA", Path.Combine(_root, "app data"));
		}

		public void Dispose()
		{
			Environment.SetEnvironmentVariable("PURITY_APP_DATA", null);
			try
			{
				Directory.Delete(_root, true);
			}
			catch (IOException)
			{
				// a watcher may hold the folder for a moment; temp gets cleaned anyway
			}
		}


		[Fact]
		public Task TheWheelScrollsPastADateFieldWithoutChangingIt() => Ui.Run(() =>
		{
			var date = new DateTime(2031, 3, 2);
			var field = new DateField { SelectedDate = date, Width = 150, Height = 25 };
			var stock = new CalendarDatePicker { SelectedDate = date, Width = 150, Height = 25 };
			var list = new StackPanel { Spacing = 10 };
			list.Children.Add(stock);
			list.Children.Add(field);
			list.Children.Add(new Border { Height = 1000 });
			var scroller = new ScrollViewer { Content = list };
			var window = new Window { Content = scroller, Width = 300, Height = 200 };
			window.Show();

			// Avalonia's own picker does move, so this test would notice the problem coming back
			window.MouseWheel(Center(stock, window), new Vector(0, -1));
			Assert.NotEqual(date, stock.SelectedDate);
			Assert.Equal(0, scroller.Offset.Y);

			window.MouseWheel(Center(field, window), new Vector(0, -1));
			Assert.Equal(date, field.SelectedDate);
			Assert.True(scroller.Offset.Y > 0, "The wheel should go on to scroll the list");

			window.Close();
		});

		[Fact]
		public Task TheMainWindowsDateFieldsIgnoreTheWheel() => Ui.Run(async () =>
		{
			var dataFile = Path.Combine(_root, "pureData.json");
			var content = Encoding.UTF8.GetBytes(History());
			File.WriteAllBytes(dataFile, content);

			var window = new MainWindow(new StartOptions(dataFile));
			window.Show();
			var viewModel = (ViewModels.MainWindowViewModel)window.DataContext!;
			var periods = window.FindControl<ListBox>("Periods")!;
			await Ui.WaitUntil(() => viewModel.Periods.Count > 0 && periods.GetVisualDescendants().OfType<CalendarDatePicker>().Any());
			await Task.Delay(200);
			await Ui.WaitUntil(() => true);					// the list scrolls to the newest periods first

			string Stamps() => string.Join(", ", viewModel.Periods.Select(el => $"{el.Period.Begin:O} {el.Period.End:O}"));
			var stamps = Stamps();
			Assert.All(periods.GetVisualDescendants().OfType<CalendarDatePicker>(), el => Assert.IsType<DateField>(el));

			// Scroll up the list one notch at a time, always with the pointer over a date field. The
			// list builds periods as they come near, so the periods themselves are what's checked.
			var offsets = new HashSet<double>();
			for (var notch = 0; notch < 6; notch++)
			{
				var field = periods.GetVisualDescendants().OfType<CalendarDatePicker>()
					.Where(el => el.IsEffectivelyVisible && el.Bounds.Height > 0)
					.Select(el => (Field: el, At: Center(el, window)))
					.Where(el => el.At.Y > 0 && el.At.Y < window.Bounds.Height - 60)
					.OrderBy(el => el.At.Y)
					.First();
				offsets.Add(periods.Scroll!.Offset.Y);

				window.MouseWheel(field.At, new Vector(0, 1));
				await Ui.WaitUntil(() => true);
				Assert.Equal(stamps, Stamps());
			}

			offsets.Add(periods.Scroll!.Offset.Y);
			Assert.True(offsets.Count > 1, "The wheel should go on to scroll the list");
			window.Close();
			await Ui.WaitUntil(() => !window.IsVisible);
			Assert.Equal(content, File.ReadAllBytes(dataFile));
		});


		private static Point Center(Control control, Visual root) =>
			control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), root)!.Value;

		private static string History()
		{
			var ledger = new PeriodLedger();
			for (var begin = new DateTime(2031, 1, 5); begin.Year == 2031; begin = begin.AddDays(29))
				ledger.AcceptPeriod(ledger.AddPeriod(Stamps.FromDate(begin, false), Stamps.FromDate(begin.AddDays(5), false)));
			return PurityFile.Format(ledger.Data);
		}


		private readonly string _root = Path.Combine(Path.GetTempPath(), $"purity-desktop-{Guid.NewGuid():N}");
	}


	/// <summary>Tests that point PURITY_APP_DATA somewhere, which is one setting for the whole process, so they take turns</summary>
	[CollectionDefinition(Name)]
	public sealed class AppDataCollection
	{
		public const string Name = "App data";
	}
}
