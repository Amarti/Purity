using Xunit;
using static Purity.Storage.Tests.History;


namespace Purity.Storage.Tests
{
	public class HistoryMergeTests
	{
		private static readonly PeriodRecord[] Base = Cycles(3);


		private static PeriodRecord[] With(params PeriodRecord[] extra) => Base.Concat(extra).ToArray();

		private static PeriodRecord[] Replacing(int index, PeriodRecord changed)
		{
			var copy = Base.ToArray();
			copy[index] = changed;
			return copy;
		}

		private static void AssertMerged(MergeResult result, params PeriodRecord[] expected)
		{
			Assert.Empty(result.Conflicts);
			Assert.True(SameInputs(expected, result.Merged), "merged history differs");
		}


		[Fact]
		public void NothingChangedStaysTheSame()
		{
			AssertMerged(HistoryMerge.Merge(Base, Base, Base), Base);
		}

		[Fact]
		public void EmptyHistoriesMergeToNothing()
		{
			AssertMerged(HistoryMerge.Merge([], [], []));
		}

		[Fact]
		public void AnAdditionOnEitherSideIsKept()
		{
			var added = With(Cycle(3));

			AssertMerged(HistoryMerge.Merge(Base, added, Base), added);
			AssertMerged(HistoryMerge.Merge(Base, Base, added), added);
		}

		[Fact]
		public void AdditionsInDifferentCyclesAreBothKeptInOrder()
		{
			var result = HistoryMerge.Merge(Base, With(Cycle(4)), With(Cycle(3)));

			AssertMerged(result, With(Cycle(3), Cycle(4)));
		}

		[Fact]
		public void TheSameAdditionOnBothSidesIsKeptOnce()
		{
			AssertMerged(HistoryMerge.Merge(Base, With(Cycle(3)), With(Cycle(3))), With(Cycle(3)));
		}

		/// <summary>The case that decides the design: keeping both would shift every later date.</summary>
		[Fact]
		public void SamePeriodRecordedOneOnahApartIsAConflict()
		{
			var day = Cycle(3);
			var night = Cycle(3, night: true);

			var result = HistoryMerge.Merge(Base, With(day), With(night));

			var conflict = Assert.Single(result.Conflicts);
			Assert.True(day.SameAs(Assert.Single(conflict.Local)));
			Assert.True(night.SameAs(Assert.Single(conflict.Remote)));
			Assert.Empty(conflict.Baseline);
			Assert.True(SameInputs(Base, result.Merged), "only the agreed periods may be merged");
		}

		[Fact]
		public void AnEditOnThisDeviceIsTaken()
		{
			var edited = Replacing(1, Cycle(1, length: 7));

			AssertMerged(HistoryMerge.Merge(Base, edited, Base), edited);
		}

		/// <summary>A correction made elsewhere and a stale copy reverting one look exactly the same.</summary>
		[Fact]
		public void AClosedPeriodChangedInTheStoredFileNeedsADecision()
		{
			var result = HistoryMerge.Merge(Base, Base, Replacing(1, Cycle(1, length: 7)));

			var conflict = Assert.Single(result.Conflicts);
			Assert.True(Base[1].SameAs(Assert.Single(conflict.Local)));
			Assert.True(Cycle(1, length: 7).SameAs(Assert.Single(conflict.Remote)));
		}

		[Fact]
		public void AClosedPeriodReopenedInTheStoredFileNeedsADecision()
		{
			Assert.Single(HistoryMerge.Merge(Base, Base, Replacing(2, Base[2] with { Closed = false })).Conflicts);
		}

		/// <summary>The everyday case: a period opened on one device and closed on another.</summary>
		[Fact]
		public void ClosingAnOpenPeriodInTheStoredFileIsTaken()
		{
			var open = Replacing(2, Base[2] with { End = DateTimeOffset.MinValue, Closed = false });

			AssertMerged(HistoryMerge.Merge(open, open, Base), Base);
		}

		/// <summary>
		/// Two devices each saved their own version to their own replica, one onah apart, and then
		/// sync delivered one over the other. Each device's baseline holds its own version.
		/// </summary>
		[Fact]
		public void AnotherDevicesVersionOfASavedPeriodNeedsADecision()
		{
			var mine = With(Cycle(3));
			var theirs = With(Cycle(3, night: true));

			Assert.Single(HistoryMerge.Merge(mine, mine, theirs).Conflicts);
		}

		[Fact]
		public void APeriodJoiningAKeptOneInTheSameCycleIsTaken()
		{
			var first = Cycle(0);
			var second = first with { Begin = first.Begin.AddDays(10), End = first.End.AddDays(10) };

			AssertMerged(HistoryMerge.Merge([first], [first], [first, second]), first, second);
		}

		[Fact]
		public void TheSameEditOnBothSidesIsTaken()
		{
			var edited = Replacing(1, Cycle(1, length: 7));

			AssertMerged(HistoryMerge.Merge(Base, edited, edited), edited);
		}

		[Fact]
		public void DifferentEditsOfOnePeriodAreAConflict()
		{
			var result = HistoryMerge.Merge(Base, Replacing(1, Cycle(1, length: 6)), Replacing(1, Cycle(1, length: 7)));

			var conflict = Assert.Single(result.Conflicts);
			Assert.True(Base[1].SameAs(Assert.Single(conflict.Baseline)));
		}

		[Fact]
		public void ChangesToDifferentPeriodsAreBothTaken()
		{
			var open = Replacing(2, Base[2] with { End = DateTimeOffset.MinValue, Closed = false });
			var local = new[] { Cycle(0, length: 6), Base[1], open[2] };

			AssertMerged(HistoryMerge.Merge(open, local, Base), Cycle(0, length: 6), Base[1], Base[2]);
		}

		[Fact]
		public void ClosingOnOneSideAndSkippingOnTheOtherIsAConflict()
		{
			var open = Base[2] with { Closed = false };
			var baseline = new[] { Base[0], Base[1], open };
			var closed = new[] { Base[0], Base[1], open with { Closed = true } };
			var skipped = new[] { Base[0], Base[1], open with { SkipStreak = true } };

			Assert.Single(HistoryMerge.Merge(baseline, closed, skipped).Conflicts);
		}

		[Fact]
		public void MovingABeginOnThisDeviceIsTaken()
		{
			var moved = Replacing(1, Cycle(1, night: true));

			AssertMerged(HistoryMerge.Merge(Base, moved, Base), moved);
		}

		[Fact]
		public void ABeginMovedInTheStoredFileNeedsADecision()
		{
			Assert.Single(HistoryMerge.Merge(Base, Base, Replacing(1, Cycle(1, night: true))).Conflicts);
		}

		[Fact]
		public void MovingABeginAgainstAnEditIsAConflict()
		{
			var result = HistoryMerge.Merge(Base, Replacing(1, Cycle(1, night: true)), Replacing(1, Cycle(1, length: 7)));

			Assert.Single(result.Conflicts);
		}

		[Fact]
		public void TheSameMomentWithAnotherOffsetIsADifferentStamp()
		{
			var original = Cycle(1);
			var shifted = original with { Begin = original.Begin.ToOffset(TimeSpan.FromHours(3)) };

			Assert.False(original.SameAs(shifted));
			Assert.Single(HistoryMerge.Merge(Base, Replacing(1, shifted), Replacing(1, Cycle(1, length: 7))).Conflicts);
		}

		[Fact]
		public void ARemovalOnThisDeviceIsTaken()
		{
			AssertMerged(HistoryMerge.Merge(Base, Base[..2], Base), Base[..2]);
		}

		[Fact]
		public void ARemovalOnThisDeviceKeepsTheOtherSidesAddition()
		{
			AssertMerged(HistoryMerge.Merge(Base, Base[..2], With(Cycle(4))), Base[0], Base[1], Cycle(4));
		}

		/// <summary>
		/// A stale copy written over a newer one looks exactly like this: the old app does it when
		/// it closes after the newest entry was made elsewhere.
		/// </summary>
		[Fact]
		public void TheLastPeriodMissingFromTheStoredFileNeedsADecision()
		{
			var result = HistoryMerge.Merge(Base, Base, Base[..2]);

			var conflict = Assert.Single(result.Conflicts);
			Assert.True(Base[2].SameAs(Assert.Single(conflict.Local)));
			Assert.Empty(conflict.Remote);
			Assert.True(SameInputs(Base[..2], result.Merged), "only the agreed periods may be merged");
		}

		[Fact]
		public void AnEarlierPeriodMissingFromTheStoredFileNeedsADecision()
		{
			var result = HistoryMerge.Merge(Base, Base, [Base[0], Base[2]]);

			var conflict = Assert.Single(result.Conflicts);
			Assert.True(Base[1].SameAs(Assert.Single(conflict.Local)));
			Assert.Empty(conflict.Remote);
		}

		[Fact]
		public void OnePeriodOfACycleMissingFromTheStoredFileNeedsADecision()
		{
			var first = Cycle(0);
			var second = first with { Begin = first.Begin.AddDays(10), End = first.End.AddDays(10) };

			Assert.Single(HistoryMerge.Merge([first, second], [first, second], [first]).Conflicts);
		}

		/// <summary>What a failed load followed by a blind save does to a file in the old app.</summary>
		[Fact]
		public void AWipedHistoryLosesNothingWithoutADecision()
		{
			var result = HistoryMerge.Merge(Base, Base, []);

			Assert.Equal(Base.Length, result.Conflicts.Count);
			Assert.Empty(result.Merged);
			Assert.True(SameInputs(Base, result.Resolve(_ => MergeSide.Local)), "keeping this device's side must restore everything");
		}

		[Fact]
		public void RemovingOnBothSidesIsAgreement()
		{
			AssertMerged(HistoryMerge.Merge(Base, [Base[0], Base[2]], [Base[0], Base[2]]), Base[0], Base[2]);
		}

		[Fact]
		public void EditAgainstRemovalIsAConflict()
		{
			var result = HistoryMerge.Merge(Base, Replacing(2, Cycle(2, length: 7)), Base[..2]);

			Assert.Single(result.Conflicts);
		}

		[Theory]
		[InlineData(14.0, 0)]
		[InlineData(13.5, 1)]
		public void CyclesSplitAtExactlyTheThreshold(double gapDays, int conflicts)
		{
			var first = Cycle(0);
			var second = first with { Begin = first.Begin.AddDays(gapDays), End = first.End.AddDays(gapDays) };

			Assert.Equal(conflicts, HistoryMerge.Merge([], [first], [second]).Conflicts.Count);
		}

		[Fact]
		public void ResolveAppliesEachChoiceAndKeepsOrder()
		{
			var result = HistoryMerge.Merge(Base, Replacing(0, Cycle(0, length: 6)), Replacing(0, Cycle(0, length: 7)));

			var local = result.Resolve(_ => MergeSide.Local);
			var remote = result.Resolve(_ => MergeSide.Remote);

			Assert.True(SameInputs([Cycle(0, length: 6), Base[1], Base[2]], local), "local choice");
			Assert.True(SameInputs([Cycle(0, length: 7), Base[1], Base[2]], remote), "remote choice");
		}

		/// <summary>Only changes to closed periods treat the sides differently, so additions must not care.</summary>
		[Fact]
		public void SidesCanBeSwappedWhenBothOnlyAdd()
		{
			var random = new Random(20330101);
			for (var round = 0; round < 500; round++)
			{
				var baseline = Cycles(random.Next(0, 8));
				var (local, remote) = (Mutate(baseline, random, onlyAdd: true), Mutate(baseline, random, onlyAdd: true));

				var forward = HistoryMerge.Merge(baseline, local, remote);
				var backward = HistoryMerge.Merge(baseline, remote, local);

				Assert.True(SameInputs(forward.Merged, backward.Merged), $"round {round}");
				Assert.Equal(forward.Conflicts.Count, backward.Conflicts.Count);
			}
		}

		/// <summary>Whatever two devices do, a period neither of them touched can't disappear.</summary>
		[Fact]
		public void APeriodNeitherSideTouchedIsNeverLost()
		{
			var random = new Random(20330102);
			for (var round = 0; round < 500; round++)
			{
				var baseline = Cycles(random.Next(1, 10));
				var (local, remote) = (Mutate(baseline, random), Mutate(baseline, random));

				var merged = HistoryMerge.Merge(baseline, local, remote);
				var resolved = merged.Resolve(_ => MergeSide.Local);

				foreach (var period in baseline.Where(el => local.Any(el.SameAs) && remote.Any(el.SameAs)))
					Assert.Contains(resolved, el => el.SameAs(period));
			}
		}


		private static PeriodRecord[] Mutate(PeriodRecord[] history, Random random, bool onlyAdd = false)
		{
			var result = history.ToList();
			for (var change = random.Next(0, 3); change > 0; change--)
			{
				switch (random.Next(3))
				{
					case 0 when result.Count > 0 && !onlyAdd:
						var i = random.Next(result.Count);
						result[i] = result[i] with { End = result[i].End.AddDays(random.Next(1, 4)) };
						break;
					case 1 when result.Count > 0 && !onlyAdd:
						result.RemoveAt(result.Count - 1);
						break;
					default:
						result.Add(Cycle(result.Count + random.Next(0, 3), night: random.Next(2) == 0));
						break;
				}
			}

			return result.OrderBy(el => el.Begin).ToArray();
		}
	}
}
