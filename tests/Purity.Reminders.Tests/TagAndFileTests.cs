using System.Text;
using Purity.Core;
using Xunit;


namespace Purity.Reminders.Tests
{
	/// <summary>The tag that marks a reminder as ours, and the calendar file. Every date is made up.</summary>
	public sealed class TagAndFileTests
	{
		[Fact]
		public void ATagLooksTheSameEverywhere()
		{
			var tag = ReminderTag.Of(Period(), Item());

			Assert.Matches("^pr1[0-9a-v]{20}$", tag);						// also the letters Google allows in an event's ID
			Assert.Equal("ref:" + tag, ReminderTag.NotesLine(tag));
		}

		/// <summary>Reminders already in calendars are found by this. If it ever has to change, the prefix must change with it.</summary>
		[Fact]
		public void TheWayOfMakingTagsDoesNotDriftByAccident() =>
			Assert.Equal("pr1spauvisupfo274ehddao", ReminderTag.Of(Period(), Item()));

		[Fact]
		public void TheLanguageAndWordingPlayNoPart()
		{
			var period = Period();
			period.SubEvents.Add(Item());
			var tags = new HashSet<string>();

			foreach (var language in Enum.GetValues<ReminderLanguage>())
				foreach (var wording in Enum.GetValues<ReminderWording>())
					tags.Add(ReminderPlanner.Plan(new[] { period }, Options() with { Language = language, Wording = wording }, Long).Single().Tag);

			Assert.Single(tags);
		}

		[Fact]
		public void EachReminderHasItsOwnTag()
		{
			var tags = new[]
			{
				ReminderTag.Of(Period(), Item()),
				ReminderTag.Of(Period(), Item(PurityEventType.VesetHodesh)),
				ReminderTag.Of(Period(), Item(PurityEventType.VesetAflaga, "(42)")),
				ReminderTag.Of(Period(), Item(PurityEventType.VesetAflaga, "(49)")),
				ReminderTag.Of(Period(), Item(), alert: 2),
				ReminderTag.Of(Period(begin: 3), Item()),
			};

			Assert.Equal(tags.Length, tags.Distinct().Count());
		}

		[Fact]
		public void ADateThatMovesKeepsItsTag()
		{
			var moved = Item();
			moved.Stamp = moved.Stamp.AddDays(4);
			var period = Period();
			period.End = period.End.AddDays(2);

			Assert.Equal(ReminderTag.Of(Period(), Item()), ReminderTag.Of(period, moved));
		}


		[Fact]
		public void TheFileHasWhatACalendarNeeds()
		{
			var file = IcsFile.Write(new[] { Reminder("Appointment today") }, "Personal", Made);
			var lines = Unfolded(file);

			Assert.Equal("BEGIN:VCALENDAR", lines[0]);
			Assert.Equal("END:VCALENDAR", lines[^1]);
			Assert.Contains("VERSION:2.0", lines);
			Assert.Contains("X-WR-CALNAME:Personal", lines);
			Assert.Contains("UID:pr1abc", lines);
			Assert.Contains("DTSTAMP:20310101T120000Z", lines);
			Assert.Contains("DTSTART:20310302T080000", lines);
			Assert.Contains("DTEND:20310302T081500", lines);
			Assert.Contains("SUMMARY:Appointment today", lines);
			Assert.Contains("DESCRIPTION:ref:pr1abc", lines);
			Assert.Contains("TRIGGER:PT0S", lines);
			Assert.Contains("CLASS:PRIVATE", lines);
			Assert.DoesNotContain("Purity", file, StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public void TheFileEscapesWhatWouldBreakALine()
		{
			var lines = Unfolded(IcsFile.Write(new[] { Reminder("Reminder H, tonight; a\\b") }, "A, B", Made));

			Assert.Contains("SUMMARY:Reminder H\\, tonight\\; a\\\\b", lines);
			Assert.Contains("X-WR-CALNAME:A\\, B", lines);
		}

		[Fact]
		public void EveryLineEndsTheRightWay()
		{
			var file = IcsFile.Write(new[] { Reminder("Appointment today") }, "Personal", Made);

			Assert.EndsWith("\r\n", file);
			Assert.DoesNotContain("\n", file.Replace("\r\n", string.Empty));
		}

		[Fact]
		public void ALongHebrewTitleContinuesWithoutBreakingALetter()
		{
			const string title = "וסת ההפלגה ביום ראשון בשעות היום, וסת ההפלגה ביום ראשון בשעות היום, יממה שלמה";

			var file = IcsFile.Write(new[] { Reminder(title) }, "אישי", Made);

			Assert.All(file.Split("\r\n"), el => Assert.True(Encoding.UTF8.GetByteCount(el) <= 75, $"Too long: {el}"));
			Assert.Contains(file.Split("\r\n"), el => el.StartsWith(' '));
			Assert.Contains("SUMMARY:" + title.Replace(",", "\\,"), Unfolded(file));
		}

		[Fact]
		public void NoRemindersStillMakesAValidFile()
		{
			var lines = Unfolded(IcsFile.Write(Array.Empty<Reminder>(), "Personal", Made));

			Assert.Equal("END:VCALENDAR", lines[^1]);
			Assert.DoesNotContain("BEGIN:VEVENT", lines);
		}


		private static readonly DateTime Made = new(2031, 1, 1, 12, 0, 0, DateTimeKind.Utc);
		private static readonly DateTime Long = new(2030, 1, 1);

		private static PurityPeriod Period(int begin = 2) =>
			new(new DateTimeOffset(2031, 2, begin, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2031, 2, 7, 0, 0, 0, TimeSpan.Zero)) { Closed = true };

		private static PurityEvent Item(PurityEventType kind = PurityEventType.Mikveh, string? note = null) =>
			new(kind, new DateTimeOffset(2031, 3, 3, 12, 0, 0, TimeSpan.Zero), note);

		private static ReminderOptions Options() => new() { Kinds = Enum.GetValues<PurityEventType>().ToHashSet() };

		private static Reminder Reminder(string title) => new("pr1abc", new DateTime(2031, 3, 2, 8, 0, 0), title, PurityEventType.Mikveh);

		private static string[] Unfolded(string file) => file.Replace("\r\n ", string.Empty).TrimEnd().Split("\r\n");
	}
}
