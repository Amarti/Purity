using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Purity.Core;


namespace Purity.Reminders
{
	/// <summary>
	/// The mark that says an entry in a calendar is one of ours, and which one. It comes only from
	/// the history, so every device works out the same tag for the same reminder, and it holds no
	/// words, language or date. Its letters are the ones Google allows in an event's ID.
	/// </summary>
	public static partial class ReminderTag
	{
		/// <summary>Marks a tag as ours, and which way of making tags it comes from</summary>
		public const string Prefix = "pr1";

		/// <summary>The tag of Purity's own calendar, which is how another device finds it</summary>
		public static string Calendar { get; } = Prefix + Encode(SHA256.HashData(Encoding.UTF8.GetBytes("calendar")));

		/// <summary>The tag of the one test reminder there can be</summary>
		public static string Test { get; } = Prefix + Encode(SHA256.HashData(Encoding.UTF8.GetBytes("test")));

		/// <param name="period">The period the reminder belongs to</param>
		/// <param name="item">The date it's about. Only what can't move or be translated goes in: a date that moves keeps its tag.</param>
		/// <param name="alert">Which alert of that date it is, counting from 1</param>
		public static string Of(PurityPeriod period, PurityEvent item, int alert = 1)
		{
			var identity = string.Create(CultureInfo.InvariantCulture, $"{period.Begin.UtcDateTime:yyyyMMddHH}|{(int)item.Type}|{item.Note}|{alert}");
			return Prefix + Encode(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
		}

		/// <summary>The line that carries the tag in an entry's notes, the one field every service keeps</summary>
		public static string NotesLine(string tag) => "ref:" + tag;


		/// <summary>The tag in an entry's notes, or null when the entry isn't one of ours</summary>
		public static string? FromNotes(string? notes)
		{
			if (notes == null)
				return null;

			var match = NotesPattern().Match(notes);
			return match.Success ? match.Groups[1].Value : null;
		}


		[GeneratedRegex("ref:(pr1[0-9a-v]{20})")]
		private static partial Regex NotesPattern();

		/// <summary>The first 100 bits, five at a time</summary>
		private static string Encode(ReadOnlySpan<byte> bytes)
		{
			var text = new char[Length];
			for (var i = 0; i < Length; i++)
			{
				var bit = i * 5;
				var value = ((bytes[bit / 8] << 8) | bytes[bit / 8 + 1]) >> (11 - bit % 8);
				text[i] = Alphabet[value & 31];
			}
			return new string(text);
		}


		private const int Length = 20;
		private const string Alphabet = "0123456789abcdefghijklmnopqrstuv";
	}
}
