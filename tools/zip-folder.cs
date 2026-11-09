// Zips a folder so a Mac app or a Linux program still runs after unzipping, even when zipped on
// Windows. Each entry records Unix permissions, executable for programs and libraries, and the
// archive says it was made on Unix, which is what makes macOS's Archive Utility honor them.
// The folder itself becomes the top entry, as when zipping it from a file manager.
//
//   dotnet run tools/zip-folder.cs -- <folder> <zip file>

using System.Buffers.Binary;
using System.IO.Compression;

if (args.Length != 2 || !Directory.Exists(args[0]))
{
	Console.Error.WriteLine("Usage: dotnet run tools/zip-folder.cs -- <folder> <zip file>");
	return 2;
}

var folder = Path.GetFullPath(args[0]).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
var zipPath = Path.GetFullPath(args[1]);
var top = Path.GetFileName(folder);
File.Delete(zipPath);

const int FolderMode = 0x41ED;				// drwxr-xr-x
const int ProgramMode = 0x81ED;					// -rwxr-xr-x
const int PlainMode = 0x81A4;					// -rw-r--r--

var programs = 0;
using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
{
	foreach (var path in new[] { folder }.Concat(Directory.EnumerateDirectories(folder, "*", SearchOption.AllDirectories)).Order(StringComparer.Ordinal))
		zip.CreateEntry(EntryName(path) + "/").ExternalAttributes = FolderMode << 16 | 0x10;	// and the MS-DOS folder flag

	foreach (var path in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
	{
		var program = IsProgram(path);
		programs += program ? 1 : 0;
		zip.CreateEntryFromFile(path, EntryName(path), CompressionLevel.Optimal).ExternalAttributes = (program ? ProgramMode : PlainMode) << 16;
	}
}

MarkMadeOnUnix(zipPath);
Console.WriteLine($"Zipped {top} to {zipPath}, {programs} programs and libraries marked executable");
return 0;


string EntryName(string path)
{
	var relative = Path.GetRelativePath(folder, path).Replace('\\', '/');
	return relative == "." ? top : top + "/" + relative;
}

/// <summary>Mac and Linux programs and libraries, and scripts</summary>
static bool IsProgram(string path)
{
	using var file = File.OpenRead(path);
	Span<byte> head = stackalloc byte[4];
	if (file.Read(head) < 4)
		return false;

	var magic = BinaryPrimitives.ReadUInt32BigEndian(head);
	return magic is 0xCFFAEDFE or 0xCEFAEDFE or 0xCAFEBABE or 0x7F454C46		// Mach-O, universal Mach-O, ELF
		|| head[0] == '#' && head[1] == '!';
}

/// <summary>
/// Sets "made on Unix" in every central directory entry. .NET records the system it runs on, and
/// unzip tools ignore Unix permissions in an archive made on Windows.
/// </summary>
static void MarkMadeOnUnix(string path)
{
	var bytes = File.ReadAllBytes(path);
	var end = bytes.Length - 22;
	while (end >= 0 && BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(end)) != 0x06054B50)
		end--;
	if (end < 0)
		throw new InvalidDataException("The zip has no end of central directory record");

	var count = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(end + 10));
	var at = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(end + 16));
	for (var i = 0; i < count; i++)
	{
		if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at)) != 0x02014B50)
			throw new InvalidDataException("A central directory entry is out of place");

		bytes[at + 5] = 3;					// the high byte of "version made by": Unix
		at += 46 + BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(at + 28))
			+ BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(at + 30))
			+ BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(at + 32));
	}
	File.WriteAllBytes(path, bytes);
}
