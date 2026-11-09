using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;


namespace Purity.Desktop.Views
{
	/// <summary>
	/// A picture of a window plus a short report beside it, for checks on machines CI can't reach.
	/// Only ever taken of a made-up history: see <see cref="StartOptions.PictureFile"/>.
	/// </summary>
	internal static class WindowPicture
	{
		/// <param name="window">The window to picture</param>
		/// <param name="file">Where the PNG goes. The report goes beside it as a .txt file</param>
		/// <param name="facts">What the window knows about itself, added to the report</param>
		/// <returns>Whether the picture was saved</returns>
		public static bool Save(Window window, string file, IEnumerable<(string Name, object? Value)> facts)
		{
			var report = new List<string>
			{
				$"os: {RuntimeInformation.OSDescription}",
				$"arch: {RuntimeInformation.ProcessArchitecture} process on {RuntimeInformation.OSArchitecture}",
				$"dotnet: {RuntimeInformation.FrameworkDescription}",
				$"avalonia: {AvaloniaVersion()}",
				$"graphics: {GraphicsName()}",
				$"render scaling: {window.RenderScaling}",
			};
			report.AddRange(facts.Select(el => $"{el.Name}: {el.Value}"));

			var saved = false;
			try
			{
				var scale = window.RenderScaling;
				var size = new PixelSize((int)Math.Ceiling(window.ClientSize.Width * scale), (int)Math.Ceiling(window.ClientSize.Height * scale));
				using (var bitmap = new RenderTargetBitmap(size, new Vector(96 * scale, 96 * scale)))
				{
					bitmap.Render(window);
					using var stream = File.Create(file);
					bitmap.Save(stream, PngBitmapEncoderOptions.Default);
				}
				report.Add("result: passed");
				saved = true;
			}
			catch (Exception e)
			{
				report.Add("result: failed");
				report.Add("error: " + e.ToString().ReplaceLineEndings("\n    "));
			}

			File.WriteAllLines(Path.ChangeExtension(file, ".txt"), report);
			return saved;
		}


		private static string AvaloniaVersion()
		{
			var version = typeof(AvaloniaObject).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
			return version.Split('+')[0];
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
	}
}
