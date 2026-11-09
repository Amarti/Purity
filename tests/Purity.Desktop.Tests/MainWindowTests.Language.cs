using System.Globalization;
using Avalonia.Media;
using Purity.Core;
using Purity.Desktop.Localization;
using Purity.Desktop.ViewModels;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>The language: it shows as soon as it's chosen, OK keeps it, and leaving any other way puts it back</summary>
	public sealed partial class MainWindowTests
	{
		[Fact]
		public Task ChoosingALanguageShowsItAtOnceAndOkKeepsIt() => BackToEnglishAfter(async () =>
		{
			var window = await OpenFromSettingsWithOneChangeAsync();
			_dialogs.InSettings = settings =>
			{
				Assert.Equal(Language.English, settings.Language);
				Assert.True(settings.IsEnglish);

				settings.IsHebrew = true;

				Assert.Equal(Language.Hebrew, Loc.Instance.Language);
				Assert.Equal(FlowDirection.RightToLeft, Loc.Instance.FlowDirection);
				Assert.Equal(UiText.Hebrew.NewPeriod, window.AddOrCloseLabel);
				Assert.Equal(UiText.Hebrew.Light, settings.AppearanceChoices[1]);
				Assert.Equal(Appearance.System, settings.Appearance);		// the list was filled again without losing its choice
				Assert.Equal(Language.English, DesktopSettings.Load().Language);	// nothing is saved before OK
				return true;
			};

			await window.OpenSettingsCommand.ExecuteAsync(null);

			Assert.Equal(Language.Hebrew, Loc.Instance.Language);
			Assert.Equal(Language.Hebrew, DesktopSettings.Load().Language);
			Assert.Equal(_dataFile, DesktopSettings.Load().DataFilePath);
			Assert.Empty(_dialogs.Shown);

			Loc.Instance.Use(Language.English);
			using var next = new MainWindowViewModel(_dialogs, null, new FixedClock(), _pending.Enqueue);
			Assert.Equal(Language.Hebrew, Loc.Instance.Language);
		});

		[Fact]
		public Task CancelPutsTheLanguageBack() => BackToEnglishAfter(async () =>
		{
			var window = await OpenFromSettingsWithOneChangeAsync();
			_dialogs.InSettings = settings =>
			{
				settings.IsHebrew = true;
				Assert.Equal(Language.Hebrew, Loc.Instance.Language);
				return false;
			};

			await window.OpenSettingsCommand.ExecuteAsync(null);

			Assert.Equal(Language.English, Loc.Instance.Language);
			Assert.Equal(UiText.English.NewPeriod, window.AddOrCloseLabel);
			Assert.Equal(Language.English, DesktopSettings.Load().Language);
		});

		[Fact]
		public Task WhatTheWindowShowsFollowsTheLanguage() => BackToEnglishAfter(async () =>
		{
			var window = await OpenFromSettingsWithOneChangeAsync();
			var first = window.Periods[1];
			var englishBegin = first.BeginText;
			_dialogs.InSettings = settings =>
			{
				settings.IsHebrew = true;
				return true;
			};

			await window.OpenSettingsCommand.ExecuteAsync(null);

			Assert.NotEqual(englishBegin, first.BeginText);
			Assert.Equal(first.BeginDate!.Value.ToString("dddd, d MMMM yyyy", HebrewDates.Culture), first.BeginText);
			Assert.StartsWith(UiText.Hebrew.Skip, first.SkipLabel);
			Assert.All(first.EventRows, row =>
			{
				Assert.Equal(UiText.Hebrew.EventType(row.Event.Type), row.TypeText);
				Assert.Equal(row.Event.GregorianText(HebrewDates.ShortMonths), row.GregorianText);
			});
		});

		[Fact]
		public void AnUnknownSavedLanguageMeansEnglish() =>
			Assert.Equal(Language.English, new DesktopSettings { LanguageText = "Klingon" }.Language);

		[Fact]
		public void TheEnglishTextIsTheEnginesOwn()
		{
			foreach (var type in Enum.GetValues<PurityEventType>())
				Assert.Equal(DateText.EventType(type), UiText.English.EventType(type));
		}

		/// <summary>The language is only what's shown: the engine's text and the approved results don't move</summary>
		[Fact]
		public Task TheCalculationsDontKnowTheLanguage() => BackToEnglishAfter(() =>
		{
			var september = new PurityEvent(PurityEventType.OnaBeinonit, new DateTimeOffset(2033, 9, 5, 0, 0, 0, TimeSpan.Zero));
			var march = new PurityEvent(PurityEventType.OnaBeinonit, new DateTimeOffset(2033, 3, 1, 0, 0, 0, TimeSpan.Zero));
			var mikveh = new PurityEvent(PurityEventType.Mikveh, new DateTimeOffset(2033, 9, 5, 12, 0, 0, TimeSpan.Zero), "(35)");
			var english = (september.StampGrgRepr, september.StampHebRepr, september.TypeRepr);

			Loc.Instance.Use(Language.Hebrew);

			Assert.Equal(english, (september.StampGrgRepr, september.StampHebRepr, september.TypeRepr));
			Assert.True(SelfTest.Run().Passed);
			Assert.Equal("4-5 בספט׳ ", Loc.Instance.GregorianText(september));
			Assert.Equal("28 בפבר׳ - 1 במרץ ", Loc.Instance.GregorianText(march));
			Assert.Equal("5 בספט׳ (35)", Loc.Instance.GregorianText(mikveh));
			return Task.CompletedTask;
		});

		/// <summary>A date field reads its own text back when it loses focus, so every date has to survive the trip</summary>
		[Fact]
		public void EveryHebrewDateCanBeReadBack()
		{
			const string format = "dddd, d MMMM yyyy";
			Assert.Equal("יום שני, 7 בנובמבר 2033", new DateTime(2033, 11, 7).ToString(format, HebrewDates.Culture));
			Assert.Equal("נובמבר 2033", new DateTime(2033, 11, 7).ToString("Y", HebrewDates.Culture));

			for (var day = new DateTime(2032, 1, 1); day.Year < 2034; day = day.AddDays(1))
				Assert.Equal(day, DateTime.ParseExact(day.ToString(format, HebrewDates.Culture), format, HebrewDates.Culture));
		}

		[Fact]
		public void EveryTextIsInHebrew()
		{
			var checkedCount = 0;
			foreach (var property in typeof(UiText).GetProperties())
			{
				var value = property.GetValue(UiText.Hebrew);
				var text = value switch
				{
					string plain => plain,
					Func<string, string> oneName => oneName("x"),
					Func<int, string> oneCount => oneCount(3),
					Func<string, string, string> twoNames => twoNames("x", "y"),
					Func<string, bool, string> nameAndChoice => nameAndChoice("x", true),
					Func<int, string, string> countAndName => countAndName(3, "x"),
					Func<int, int, string> twoCounts => twoCounts(3, 2),
					_ => throw new InvalidOperationException($"{property.Name} has a type this test doesn't know"),
				};

				Assert.True(text.Any(el => el is >= 'א' and <= 'ת'), $"{property.Name} has no Hebrew in it");
				checkedCount++;
			}
			Assert.True(checkedCount > 100, $"only {checkedCount} texts were found");
		}


		private static Task BackToEnglishAfter(Func<Task> test) => Ui.Run(async () =>
		{
			try
			{
				await test();
			}
			finally
			{
				Loc.Instance.Use(Language.English);
			}
		});
	}
}
