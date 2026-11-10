using Purity.Core;
using Xunit;


namespace Purity.Reminders.Tests
{
	/// <summary>The choices every computer shares, as they're written into the calendar's description</summary>
	public sealed class SharedChoicesTests
	{
		[Fact]
		public void TheCodeIsShortAndHasNoWords() =>
			Assert.Equal("1234 d 0800 1700 1 he", Choices().Format());

		[Fact]
		public void EveryChoiceComesBackAsItWas()
		{
			var choices = new SharedChoices(new HashSet<PurityEventType> { PurityEventType.Mikveh, PurityEventType.OnaBeinonit }, ReminderWording.Detailed, new TimeOnly(9, 30), new TimeOnly(16, 0), false, ReminderLanguage.English);

			var read = SharedChoices.Parse(Line(choices));

			Assert.Equal(choices.Format(), read!.Format());
			Assert.Equal(new[] { PurityEventType.Mikveh, PurityEventType.OnaBeinonit }, read.Kinds.Order());
			Assert.Equal(ReminderWording.Detailed, read.Wording);
			Assert.Equal(new TimeOnly(9, 30), read.MikvehTime);
			Assert.Equal(new TimeOnly(16, 0), read.OtherTime);
			Assert.False(read.MoveOffShabbat);
			Assert.Equal(ReminderLanguage.English, read.Language);
		}

		[Fact]
		public void NoKindsAtAllCanBeWritten()
		{
			var none = Choices() with { Kinds = new HashSet<PurityEventType>() };

			Assert.Equal("- d 0800 1700 1 he", none.Format());
			Assert.Empty(SharedChoices.Parse(Line(none))!.Kinds);
		}

		[Fact]
		public void TheTagIsStillReadWithTheChoicesAfterIt() =>
			Assert.Equal(ReminderTag.Calendar, ReminderTag.FromNotes(Line(Choices())));

		[Fact]
		public void ThingsALaterVersionAddsAreSkipped() =>
			Assert.Equal(Choices().Format(), SharedChoices.Parse(Line(Choices()) + " x9 more")!.Format());

		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData("a note someone wrote")]
		public void NoChoicesAreFoundWhereThereAreNone(string? description) => Assert.Null(SharedChoices.Parse(description));

		[Fact]
		public void TheTagAloneHoldsNoChoices() => Assert.Null(SharedChoices.Parse(ReminderTag.NotesLine(ReminderTag.Calendar)));

		[Theory]
		[InlineData("1239 d 0800 1700 1 he")]					// no such kind
		[InlineData("12a d 0800 1700 1 he")]
		[InlineData("1234 z 0800 1700 1 he")]
		[InlineData("1234 d 2500 1700 1 he")]
		[InlineData("1234 d 0800 soon 1 he")]
		[InlineData("1234 d 0800 1700 2 he")]
		[InlineData("1234 d 0800 1700 1 fr")]
		[InlineData("1234 d 0800 1700 1")]
		public void AnythingItCantUnderstandCountsAsNone(string code) =>
			Assert.Null(SharedChoices.Parse(ReminderTag.NotesLine(ReminderTag.Calendar) + " " + code));

		[Fact]
		public void ChoicesOnAnotherTagAreNotOurs() =>
			Assert.Null(SharedChoices.Parse("ref:pr1spauvisupfo274ehddao 1234 d 0800 1700 1 he"));


		private static SharedChoices Choices() =>
			new(Enum.GetValues<PurityEventType>().ToHashSet(), ReminderWording.Discreet, new TimeOnly(8, 0), new TimeOnly(17, 0), true, ReminderLanguage.Hebrew);

		private static string Line(SharedChoices choices) => ReminderTag.NotesLine(ReminderTag.Calendar) + " " + choices.Format();
	}
}
