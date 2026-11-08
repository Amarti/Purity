using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace Purity.Core
{
	/// <summary>
	/// The pureData.json format. Text in, text out: file access belongs to the storage layer.
	/// Output is byte-identical on every platform, and to what the production app writes.
	/// </summary>
	public static class PurityFile
	{
		/// <exception cref="JsonException">The text isn't a valid data file</exception>
		public static List<PurityPeriod> Parse(string json) =>
			JsonSerializer.Deserialize(json, Context.ListPurityPeriod) ?? new List<PurityPeriod>();

		public static string Format(List<PurityPeriod> data)
		{
			var json = JsonSerializer.Serialize(data, Context.ListPurityPeriod);
			return json.Replace("\r\n", "\n").Replace("\n", NewLine);	// strings in JSON can't hold raw line breaks
		}

		/// <summary>The periods lengths CSV report, or an empty string when there is no data</summary>
		public static string PeriodLengthsReport(IReadOnlyList<PurityPeriod> data)
		{
			if (data.Count < 1)
				return string.Empty;

			var sb = new StringBuilder("Periods lengths").Append(NewLine);
			for (var i = 0; i < data.Count - 1; i++)
				sb.Append(PurityPeriod.GetFullPeriodLength(data[i], data[i + 1]).ToString("0.0", CultureInfo.InvariantCulture)).Append(NewLine);

			return sb.ToString();
		}


		private const string NewLine = "\r\n";

		private static readonly PurityJsonContext Context = new(new JsonSerializerOptions
		{
			WriteIndented = true,
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,	// default encoder escapes '<', '>' and other non-HTML-safe symbols
		});
	}


	/// <summary>Generated at compile time, because iPhone builds can't use runtime reflection.</summary>
	[JsonSerializable(typeof(List<PurityPeriod>))]
	internal partial class PurityJsonContext : JsonSerializerContext
	{}
}
