#!/usr/bin/env bash
# Builds the desktop app for Windows, macOS and Linux, each self-contained so the computer it goes
# to needs nothing installed:
#   bash tools/build-apps.sh                  # all three
#   bash tools/build-apps.sh windows mac      # or any of: windows mac linux
# Writes out/<version>/. Runs on Windows (Git Bash; the Linux packages need WSL), macOS or
# Linux. The version comes from <Version> in src/Purity.Desktop/Purity.Desktop.csproj.
set -euo pipefail

root=$(cd "$(dirname "$0")/.." && pwd)
version=$(tr -d '\r' < "$root/src/Purity.Desktop/Purity.Desktop.csproj" | sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' | head -1)
out="$root/out/$version"
stage="$out/.stage"
if [ $# -gt 0 ]; then platforms=("$@"); else platforms=(windows mac linux); fi

publish() { # rid folder
	dotnet publish "$root/src/Purity.Desktop/Purity.Desktop.csproj" -c Release -r "$1" --self-contained -p:DebugType=none \
		-nologo -v quiet -o "$2" > "$stage/publish.log" 2>&1 || { cat "$stage/publish.log"; exit 1; }
	find "$2" -name '*.pdb' -delete				# debug symbols, such as those SkiaSharp ships for Windows
}

zip_folder() { # folder zip
	dotnet run "$root/tools/zip-folder.cs" -- "$1" "$2" > "$stage/zip.log" 2>&1 || { cat "$stage/zip.log"; exit 1; }
	echo "  $(basename "$2"), $(du -h "$2" | cut -f1)"
}

rm -rf "$stage"
mkdir -p "$out" "$stage"
echo "Purity $version"

for platform in "${platforms[@]}"; do
	case $platform in
		windows)
			for arch in x64 arm64; do
				publish "win-$arch" "$stage/win-$arch"			# a single Purity.exe, as the project file sets for Windows
				cp "$stage/win-$arch/Purity.exe" "$out/Purity-$version-windows-$arch.exe"
				echo "  Purity-$version-windows-$arch.exe, $(du -h "$out/Purity-$version-windows-$arch.exe" | cut -f1)"
			done
			;;
		mac)
			for arch in x64 arm64; do
				app="$stage/osx-$arch/Purity.app"
				publish "osx-$arch" "$app/Contents/MacOS"
				mkdir -p "$app/Contents/Resources"
				tr -d '\r' < "$root/src/Purity.Desktop/Packaging/macOS/Info.plist" | sed "s/@VERSION@/$version/g" > "$app/Contents/Info.plist"
				cp "$root/src/Purity.Desktop/Packaging/macOS/Purity.icns" "$app/Contents/Resources/"
				if [ "$arch" = x64 ]; then name=intel; else name=apple-silicon; fi
				zip_folder "$app" "$out/Purity-$version-macos-$name.zip"
			done
			;;
		linux)
			bash "$root/tools/package-linux.sh" linux-x64 linux-arm64 > "$stage/linux.log" 2>&1 || { cat "$stage/linux.log"; exit 1; }
			for arch in amd64 arm64; do
				cp "$root/out/linux/purity_${version}_$arch.deb" "$out/"
				echo "  purity_${version}_$arch.deb, $(du -h "$out/purity_${version}_$arch.deb" | cut -f1)"
			done
			;;
		*)
			echo "Unknown platform '$platform': use windows, mac or linux" >&2
			exit 1
			;;
	esac
done

rm -rf "$stage"
echo "Ready in out/$version"
