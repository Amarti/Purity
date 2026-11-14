using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;


namespace Purity.Desktop.Tests
{
	internal static class FindNamedExtension
	{
		/// <summary>
		/// The control of this name anywhere inside, whichever view holds it. A view keeps its names to
		/// itself, so a window's own lookup can't see the names in the view it shows.
		/// </summary>
		public static T? FindNamed<T>(this Visual root, string name) where T : Control =>
			Inside(root).OfType<T>().FirstOrDefault(el => el.Name == name);


		private static IEnumerable<object> Inside(Visual root) =>
			new object[] { root }.Concat(root is ILogical logical ? logical.GetLogicalDescendants() : Enumerable.Empty<ILogical>()).Concat(root.GetVisualDescendants());
	}
}
