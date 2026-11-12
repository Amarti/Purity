using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;


namespace Purity.Reminders.CalDav
{
	/// <summary>One resource in a WebDAV multi-status answer</summary>
	/// <param name="Url">Its address</param>
	/// <param name="Properties">Each property returned, with the status the server gave it</param>
	internal sealed record DavResource(Uri Url, IReadOnlyList<(int Status, XElement Property)> Properties)
	{
		/// <summary>The property, if the server returned it as found</summary>
		public XElement? Found(XName name) => Properties.FirstOrDefault(el => el.Status == 200 && el.Property.Name == name).Property;

		/// <summary>The status the server gave the property, or 0 when it didn't mention it</summary>
		public int StatusOf(XName name) => Properties.FirstOrDefault(el => el.Property.Name == name).Status;
	}


	/// <summary>The WebDAV and CalDAV calls (RFC 4918 and RFC 4791) as Purity uses them</summary>
	internal static class CalDavXml
	{
		public static readonly XNamespace Dav = "DAV:";
		public static readonly XNamespace CalDav = "urn:ietf:params:xml:ns:caldav";

		public static readonly HttpMethod PropFind = new("PROPFIND");
		public static readonly HttpMethod PropPatch = new("PROPPATCH");
		public static readonly HttpMethod Report = new("REPORT");
		public static readonly HttpMethod MkCalendar = new("MKCALENDAR");

		public const string XmlType = "application/xml";


		public static string PropFindBody(params XName[] properties) =>
			Text(new XElement(Dav + "propfind", Namespaces, new XElement(Dav + "prop", properties.Select(el => new XElement(el)))));

		/// <summary>Every event in a calendar, with its text</summary>
		public static string EventsQuery() => Text(new XElement(CalDav + "calendar-query", Namespaces,
			new XElement(Dav + "prop", new XElement(Dav + "getetag"), new XElement(CalDav + "calendar-data")),
			new XElement(CalDav + "filter", new XElement(CalDav + "comp-filter", new XAttribute("name", "VCALENDAR"), new XElement(CalDav + "comp-filter", new XAttribute("name", "VEVENT"))))));

		/// <summary>A calendar for events only, with its name and description</summary>
		public static string MakeCalendarBody(string name, string description) => Text(new XElement(CalDav + "mkcalendar", Namespaces,
			new XElement(Dav + "set", new XElement(Dav + "prop",
				new XElement(Dav + "displayname", name),
				new XElement(CalDav + "calendar-description", description),
				new XElement(CalDav + "supported-calendar-component-set", new XElement(CalDav + "comp", new XAttribute("name", "VEVENT")))))));

		public static string SetDescriptionBody(string description) => Text(new XElement(Dav + "propertyupdate", Namespaces,
			new XElement(Dav + "set", new XElement(Dav + "prop", new XElement(CalDav + "calendar-description", description)))));

		/// <summary>The resources in a multi-status answer, their addresses made whole against the one asked</summary>
		public static async Task<IReadOnlyList<DavResource>> ReadAsync(HttpResponseMessage response, CancellationToken cancellation)
		{
			var asked = response.RequestMessage?.RequestUri ?? throw new InvalidOperationException("The answer has no request");
			XDocument document;
			try
			{
				await using var body = await response.Content.ReadAsStreamAsync(cancellation);
				document = await XDocument.LoadAsync(body, LoadOptions.None, cancellation);
			}
			catch (XmlException e)
			{
				throw new CalendarServiceException(CalendarProblem.Refused, "The server's answer isn't XML: " + e.Message);
			}

			var resources = new List<DavResource>();
			foreach (var item in document.Root?.Elements(Dav + "response") ?? Enumerable.Empty<XElement>())
			{
				if (item.Element(Dav + "href")?.Value.Trim() is not { Length: > 0 } href || !Uri.TryCreate(asked, href, out var url))
					continue;

				var properties = new List<(int, XElement)>();
				foreach (var found in item.Elements(Dav + "propstat"))
				{
					var status = StatusCode(found.Element(Dav + "status")?.Value);
					foreach (var property in found.Element(Dav + "prop")?.Elements() ?? Enumerable.Empty<XElement>())
						properties.Add((status, property));
				}
				resources.Add(new DavResource(url, properties));
			}
			return resources;
		}

		/// <summary>An address that names a folder, as a calendar or a calendar home does, ends in a slash</summary>
		public static Uri AsFolder(Uri url) => url.AbsolutePath.EndsWith('/') ? url : new UriBuilder(url) { Path = url.AbsolutePath + "/" }.Uri;

		/// <summary>The same address, whichever way a server wrote it</summary>
		public static bool SameAddress(Uri first, Uri second) =>
			Uri.Compare(AsFolder(first), AsFolder(second), UriComponents.HttpRequestUrl, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0;


		/// <summary>"HTTP/1.1 200 OK" gives 200</summary>
		private static int StatusCode(string? line)
		{
			var parts = line?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			return parts is { Length: >= 2 } && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var code) ? code : 0;
		}

		private static string Text(XElement root) => root.ToString(SaveOptions.DisableFormatting);

		private static readonly object[] Namespaces =
		{
			new XAttribute(XNamespace.Xmlns + "D", Dav.NamespaceName),
			new XAttribute(XNamespace.Xmlns + "C", CalDav.NamespaceName),
		};
	}
}
