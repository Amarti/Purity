using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;
using static Purity.Storage.Tests.History;


namespace Purity.Storage.Tests
{
	/// <summary>
	/// A real crash, not a simulated one: a separate process saves in a tight loop and is killed
	/// without warning. This proves the file survives the program dying at any moment. It can't
	/// prove what a power cut does to data still in the disk's own cache.
	/// </summary>
	public class CrashTests : IDisposable
	{
		private const int Rounds = 30;

		internal const string WriterVariable = "PURITY_CRASH_WRITER";

		internal static readonly byte[][] Versions = [FileFor(Cycles(800)), FileFor(Cycles(1200))];

		private readonly TestFolder _folder = new();


		public void Dispose() => _folder.Dispose();


		private Process StartWriter()
		{
			var host = Environment.ProcessPath!;
			var info = new ProcessStartInfo(host) { RedirectStandardOutput = true, UseShellExecute = false };
			if (Path.GetFileNameWithoutExtension(host) == "dotnet")
				info.ArgumentList.Add(Assembly.GetEntryAssembly()!.Location);

			info.Environment[WriterVariable] = _folder.DataFile;
			return Process.Start(info)!;
		}


		[Fact]
		public async Task KillingTheAppDuringSavesNeverDamagesTheFile()
		{
			await _folder.WriteDataAsync(Versions[0]);
			var random = new Random(20330103);

			for (var round = 0; round < Rounds; round++)
			{
				using var writer = StartWriter();
				await writer.StandardOutput.ReadLineAsync();
				await Task.Delay(random.Next(1, 40));
				writer.Kill(entireProcessTree: true);
				await writer.WaitForExitAsync();

				var content = await _folder.ReadDataAsync();
				Assert.True(Versions.Any(el => el.AsSpan().SequenceEqual(content)), $"round {round} left a damaged file");
			}
		}

		/// <summary>What a crash leaves behind must not stand in the way of the next run.</summary>
		[Fact]
		public async Task TheAppWorksNormallyAfterBeingKilled()
		{
			await _folder.WriteDataAsync(Versions[0]);
			using (var writer = StartWriter())
			{
				await writer.StandardOutput.ReadLineAsync();
				writer.Kill(entireProcessTree: true);
				await writer.WaitForExitAsync();
			}

			using var store = new FileDataStore(_folder.DataFile);
			var session = new DataSession(store, new LocalVault(_folder.VaultFolder));
			await session.OpenAsync();

			Assert.Equal(SessionState.Ready, session.State);
			session.Ledger.AcceptPeriod(session.Ledger.AddPeriod(Cycle(2000).Begin, Cycle(2000).End));
			Assert.Equal(SaveStatus.Saved, await session.SaveAsync());
		}
	}


	internal static class CrashWriter
	{
#pragma warning disable CA2255	// the one place this is wanted: it has to run before the test runner starts
		/// <summary>Turns this test program into the process that gets killed</summary>
		[ModuleInitializer]
		internal static void RunIfAsked()
#pragma warning restore CA2255
		{
			var path = Environment.GetEnvironmentVariable(CrashTests.WriterVariable);
			if (string.IsNullOrEmpty(path))
				return;

			using var store = new FileDataStore(path);
			var version = store.LoadAsync().GetAwaiter().GetResult()?.Version;
			var giveUp = DateTime.UtcNow.AddSeconds(30);	// if the test itself dies, don't run forever

			for (var round = 0; DateTime.UtcNow < giveUp; round++)
			{
				version = store.SaveAsync(CrashTests.Versions[round % 2], version).GetAwaiter().GetResult().Version;
				if (round == 0)
					Console.WriteLine("saving");
			}

			Environment.Exit(0);
		}
	}
}
