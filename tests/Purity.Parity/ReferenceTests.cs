using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using Xunit;


namespace Purity.Parity
{
	/// <summary>Proves the oracle itself: it can't be edited, and it matches the 2022 production binary.</summary>
	public class ReferenceTests
	{
		private static readonly Dictionary<string, string> SourceHashes = new()
		{
			["CultureHolder.cs"] = "a4ed07ceec1482a81a74eb5560d9f01d4e8a5790aced39d99887c5af1d9f441c",
			["DataSerializer.cs"] = "a6b9374eda5a1772ab0a98212fa53a1a401e0eeb9d0015189d6ba78681e937e1",
			["LogEntry.cs"] = "b9308208503aca3b020e75d329a4f28f47261f413a674794d1e853b1da149101",
			["PurityEvent.cs"] = "10e004600ca6fab4ed22f04d05d005221316e342c27c96082044ddf120dd2891",
			["Settings.cs"] = "605631913973733be585163a774406a29332ffd54862d8883dca140fc96276d7",
			["ViewModels/IMainWindowViewModel.cs"] = "778c8c9b77d4951a2d31bd625e4649cced96e7a5c3e378b432da0af05ac50ef7",
			["ViewModels/IPurityPeriodViewModel.cs"] = "4434ce1ebec4ed42c54c759828631faeebf95dadb064fab4ed65b53b31f517a9",
			["ViewModels/MainWindowViewModelImpl.cs"] = "380f0c8793d340078ad64bc5a2b5cef691fa9f0f827af97e8f84a2b6a463be73",
			["ViewModels/PurityPeriodViewModelImpl.cs"] = "e6360c809cce27147513a0a9667b09f8da31d49d588c7923e9909c25dbb52659",
		};
		private const string OracleBinaryHash = "513096faefe5b5b7308318b059f61775ca0fd1e476f724818a33fe9295fd80fc";


		[Fact]
		public void ReferenceSourceIsUnchanged()
		{
			var found = Directory.GetFiles(RepoPaths.ReferenceSource, "*.cs", SearchOption.AllDirectories)
				.Select(el => Path.GetRelativePath(RepoPaths.ReferenceSource, el).Replace('\\', '/'))
				.Order(StringComparer.Ordinal);
			Assert.Equal(SourceHashes.Keys.Order(StringComparer.Ordinal), found);

			foreach (var (file, hash) in SourceHashes)
				Assert.True(hash == Sha256(Path.Combine(RepoPaths.ReferenceSource, file)), $"{file} was modified");
		}

		[Fact]
		public void OracleBinaryIsUnchanged() => Assert.Equal(OracleBinaryHash, Sha256(RepoPaths.OracleBinary));

		/// <summary>Shows the source copy behaves exactly like the binary built for production in 2022.</summary>
		[Fact]
		public void ReferenceMatchesThe2022ProductionBinary()
		{
			var reference = Engines.Reference;
			var assembly = new AssemblyLoadContext("oracle-2022").LoadFromAssemblyPath(RepoPaths.OracleBinary);
			var periodType = assembly.GetType("Purity.PurityPeriod")!;
			var eventType = assembly.GetType("Purity.PurityEvent")!;
			var commit = periodType.GetMethod("Commit")!;
			var subEvents = periodType.GetProperty("SubEvents")!;
			var calendar = new HebrewCalendar();
			var tracked = new[] { 37, 33 };
			var includeText = Platform.ReferenceTextIsTruth;

			Culture.Run(Culture.Production, () =>
			{
				for (var day = new DateTimeOffset(1990, 1, 1, 0, 0, 0, TimeSpan.Zero); day.Year < 2060; day = day.AddDays(1))
					foreach (var hours in new[] { 0, 12 })
					{
						var begin = day.AddHours(hours);
						var end = day.AddDays(6);

						var period = Activator.CreateInstance(periodType, begin, end)!;
						commit.Invoke(period, [calendar, tracked.ToList()]);
						var binary = ((System.Collections.IEnumerable)subEvents.GetValue(period)!).Cast<object>().Select(el => Line(eventType, el, includeText)).ToList();

						var source = reference.CommitSingle(new PeriodInput(begin, end, true, false), tracked, includeText)
							.Select(el => $"{el.Type}|{el.Stamp}|{el.Note}|{el.HebrewText}|{el.GregorianText}").ToList();

						Assert.Equal(source, binary);
					}
				return 0;
			});
		}


		private static string Line(Type eventType, object e, bool includeText)
		{
			object? Read(string name) => eventType.GetProperty(name)!.GetValue(e);
			var text = includeText ? $"{Read("StampHebRepr")}|{Read("StampGrgRepr")}" : "|";
			return $"{Convert.ToInt32(Read("Type"))}|{(DateTimeOffset)Read("Stamp")!:o}|{Read("Note")}|{text}";
		}

		private static string Sha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
	}
}
