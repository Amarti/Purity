// Fails when a Mac program or library in a folder needs a newer macOS than the limit given.
//
// Hosted CI has no Monterey Mac, so this is how an update to Avalonia, SkiaSharp or .NET that drops
// macOS 12 gets noticed before the app reaches one. It reads the minimum version that each Mach-O
// file records, which macOS checks before loading it. A file that passes can still call something
// newer at run time, so the platform check kit stays the real test after an upgrade.
//
//   dotnet run tools/check-macos-minimum.cs -- <folder> <version, such as 12.0>

using System.Buffers.Binary;
using Microsoft.Win32.SafeHandles;

if (args.Length != 2 || !Version.TryParse(args[1], out var limitVersion))
{
	Console.Error.WriteLine("Usage: dotnet run tools/check-macos-minimum.cs -- <folder> <version, such as 12.0>");
	return 2;
}
var folder = args[0];
if (!Directory.Exists(folder))
{
	Console.Error.WriteLine($"There is no folder {folder}");
	return 2;
}

var limit = MachO.Pack(limitVersion);
var problems = new List<string>();
var files = 0;
uint newest = 0;
foreach (var path in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
{
	var name = Path.GetRelativePath(folder, path).Replace('\\', '/');
	List<MachO.Slice>? slices;
	try
	{
		slices = MachO.Read(path);
	}
	catch (Exception e) when (e is InvalidDataException or IOException)
	{
		problems.Add($"{name} can't be read: {e.Message}");
		continue;
	}
	if (slices == null)
		continue;

	files++;
	foreach (var slice in slices)
	{
		var minimum = slice.Minimum is { } m ? "macOS " + MachO.Format(m) : "no minimum";
		Console.WriteLine($"{name,-72} {slice.Arch,-7} {minimum}");
		if (slice.Minimum is not { } version)
			problems.Add($"{name} ({slice.Arch}) records no minimum macOS version");
		else if (slice.Platform != MachO.PlatformMacOS)
			problems.Add($"{name} ({slice.Arch}) is built for platform {slice.Platform}, not macOS");
		else
		{
			newest = Math.Max(newest, version);
			if (version > limit)
				problems.Add($"{name} ({slice.Arch}) needs macOS {MachO.Format(version)}");
		}
	}
}

Console.WriteLine();
var annotate = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";
if (files == 0 && problems.Count == 0)
{
	Console.WriteLine((annotate ? "::error::" : "") + $"No Mac programs or libraries were found in {folder}, so nothing was checked");
	return 1;
}
if (problems.Count == 0)
{
	Console.WriteLine($"Checked {files} Mac files. The newest minimum is macOS {MachO.Format(newest)}, within the limit of {MachO.Format(limit)}.");
	return 0;
}

Console.WriteLine($"Some files won't load on macOS {MachO.Format(limit)}:");
foreach (var problem in problems)
	Console.WriteLine((annotate ? "::error::" : "  ") + problem);
return 1;


/// <summary>Just enough of the Mach-O format to find each architecture's minimum macOS version</summary>
static class MachO
{
	public const uint PlatformMacOS = 1;
	private const uint Magic64 = 0xFEEDFACF, Magic32 = 0xFEEDFACE, FatMagic = 0xCAFEBABE, FatMagic64 = 0xCAFEBABF;
	private const uint LoadVersionMinMacOS = 0x24, LoadBuildVersion = 0x32;

	/// <summary>One architecture in a file. Platform and Minimum are null when the file doesn't record them.</summary>
	public sealed record Slice(string Arch, uint? Platform, uint? Minimum);

	/// <summary>The architectures in a Mach-O file, or null when the file isn't one</summary>
	public static List<Slice>? Read(string path)
	{
		using var handle = File.OpenHandle(path);
		var length = RandomAccess.GetLength(handle);
		if (length < 8)
			return null;
		var head = ReadAt(handle, 0, 8, length);
		var magic = BinaryPrimitives.ReadUInt32LittleEndian(head);
		if (magic is Magic64 or Magic32)
			return [ReadSlice(handle, 0, length)];

		// A universal file: a big-endian table of slices, one per architecture
		magic = BinaryPrimitives.ReadUInt32BigEndian(head);
		if (magic is not (FatMagic or FatMagic64))
			return null;
		var count = (int)BinaryPrimitives.ReadUInt32BigEndian(head.AsSpan(4));
		if (count is 0 or > 20) // Java class files start with the same number, followed by a large version
			return null;
		var entrySize = magic == FatMagic64 ? 32 : 20;
		var table = ReadAt(handle, 8, entrySize * count, length);
		var slices = new List<Slice>();
		for (var i = 0; i < count; i++)
		{
			var entry = table.AsSpan(i * entrySize + 8);
			var offset = magic == FatMagic64 ? (long)BinaryPrimitives.ReadUInt64BigEndian(entry) : BinaryPrimitives.ReadUInt32BigEndian(entry);
			slices.Add(ReadSlice(handle, offset, length));
		}
		return slices;
	}

	private static Slice ReadSlice(SafeFileHandle handle, long start, long length)
	{
		var header = ReadAt(handle, start, 28, length);
		var magic = BinaryPrimitives.ReadUInt32LittleEndian(header);
		if (magic is not (Magic64 or Magic32))
			throw new InvalidDataException("a slice of the universal file isn't Mach-O");
		var cpu = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4));
		var commandCount = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(16));
		var commandBytes = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(20));
		var commands = ReadAt(handle, start + (magic == Magic64 ? 32 : 28), checked((int)commandBytes), length);

		// A file built for both macOS and Mac Catalyst records both, so prefer the macOS entry
		uint? platform = null, minimum = null;
		var at = 0;
		for (var i = 0; i < commandCount; i++)
		{
			if (at + 8 > commands.Length)
				throw new InvalidDataException("its load commands are cut short");
			var command = BinaryPrimitives.ReadUInt32LittleEndian(commands.AsSpan(at));
			var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(commands.AsSpan(at + 4));
			if (size < 8 || at + size > commands.Length)
				throw new InvalidDataException("a load command has a wrong size");

			if (platform != PlatformMacOS && command == LoadBuildVersion && size >= 16)
			{
				platform = BinaryPrimitives.ReadUInt32LittleEndian(commands.AsSpan(at + 8));
				minimum = BinaryPrimitives.ReadUInt32LittleEndian(commands.AsSpan(at + 12));
			}
			else if (platform != PlatformMacOS && command == LoadVersionMinMacOS && size >= 12)
			{
				platform = PlatformMacOS;
				minimum = BinaryPrimitives.ReadUInt32LittleEndian(commands.AsSpan(at + 8));
			}
			at += size;
		}

		return new Slice(Arch(cpu), platform, minimum);
	}

	private static byte[] ReadAt(SafeFileHandle handle, long offset, int count, long length)
	{
		if (offset < 0 || offset + count > length)
			throw new InvalidDataException("the file is shorter than its header says");
		var buffer = new byte[count];
		var done = 0;
		while (done < count)
		{
			var read = RandomAccess.Read(handle, buffer.AsSpan(done), offset + done);
			if (read == 0)
				throw new InvalidDataException("the file ended early");
			done += read;
		}
		return buffer;
	}

	private static string Arch(uint cpu) => cpu switch
	{
		0x01000007 => "x64",
		0x0100000C => "arm64",
		0x00000007 => "x86",
		0x0000000C => "arm",
		_ => $"cpu 0x{cpu:x}",
	};

	/// <summary>Mach-O stores a version as 16 bits of major, then 8 of minor and 8 of patch</summary>
	public static uint Pack(Version version) =>
		(uint)version.Major << 16 | (uint)version.Minor << 8 | (uint)Math.Max(version.Build, 0);

	public static string Format(uint version) =>
		$"{version >> 16}.{(version >> 8) & 0xFF}" + ((version & 0xFF) != 0 ? $".{version & 0xFF}" : "");
}
