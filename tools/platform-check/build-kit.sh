#!/usr/bin/env bash
# Builds the platform check kit: the desktop app and the test suites, self-contained, so the target
# machine needs nothing installed. Run anywhere:
#   bash tools/platform-check/build-kit.sh                  # Mac: Intel and Apple Silicon
#   bash tools/platform-check/build-kit.sh linux-x64        # or any runtime identifiers
# Writes out/platform-check/purity-check-<rid>/ and a zip of it.
set -euo pipefail

root=$(cd "$(dirname "$0")/../.." && pwd)
out="$root/out/platform-check"
if [ $# -gt 0 ]; then rids=("$@"); else rids=(osx-x64 osx-arm64); fi

publish() { # project rid folder
	echo "  $(basename "$1" .csproj)"
	dotnet publish "$root/$1" -c Release -f net10.0 -r "$2" --self-contained -nologo -v quiet -o "$3" > "$out/publish.log" 2>&1 ||
		{ cat "$out/publish.log"; exit 1; }
}

zip_folder() { # parent name
	rm -f "$1/$2.zip"
	if command -v zip > /dev/null; then
		(cd "$1" && zip -qr "$2.zip" "$2")
	elif [ -x /c/Windows/System32/tar.exe ]; then
		(cd "$1" && /c/Windows/System32/tar.exe -a -cf "$2.zip" "$2")
	else
		(cd "$1" && tar -czf "$2.tar.gz" "$2")
	fi
}

mkdir -p "$out"
for rid in "${rids[@]}"; do
	name="purity-check-$rid"
	kit="$out/$name"
	echo "Building $name"
	rm -rf "$kit"
	mkdir -p "$kit/bin"

	# The test suites share one folder: same packages, one copy of the runtime
	publish tests/Purity.Parity/Purity.Parity.csproj "$rid" "$kit/bin/tests"
	publish tests/Purity.Storage.Tests/Purity.Storage.Tests.csproj "$rid" "$kit/bin/tests"
	publish tests/Purity.Desktop.Tests/Purity.Desktop.Tests.csproj "$rid" "$kit/bin/tests"
	# Coverage tooling: the kit runs tests without collecting coverage. The folder is 30 MB of
	# Windows binaries, and the profiler libraries would look like a problem for older macOS
	# (one needs macOS 15) though they only load when coverage is collected.
	rm -rf "$kit/bin/tests/Microsoft.CodeCoverage"
	rm -f "$kit/bin/tests/"lib{CoverageInstrumentationMethod,InstrumentationEngine}.*

	# The app, as a Mac app bundle or a plain folder
	case $rid in
		osx-*)
			app="$kit/Purity.app/Contents/MacOS"
			publish src/Purity.Desktop/Purity.Desktop.csproj "$rid" "$app"
			mkdir -p "$kit/Purity.app/Contents/Resources"
			tr -d '\r' < "$root/src/Purity.Desktop/Packaging/macOS/Info.plist" > "$kit/Purity.app/Contents/Info.plist"
			cp "$root/src/Purity.Desktop/Packaging/macOS/Purity.icns" "$kit/Purity.app/Contents/Resources/"
			;;
		*)
			publish src/Purity.Desktop/Purity.Desktop.csproj "$rid" "$kit/app"
			;;
	esac
	dotnet run "$root/tools/make-sample-data.cs" -- "$kit/sample.json" > /dev/null

	# What the parity tests read. They find it by walking up to Purity.sln, so an empty one marks the root.
	: > "$kit/Purity.sln"
	mkdir -p "$kit/tests/Purity.Parity" "$kit/tests/Purity.Reference.V1" "$kit/tests/oracle"
	cp -R "$root/tests/Purity.Parity/Golden" "$kit/tests/Purity.Parity/"
	cp -R "$root/tests/Purity.Reference.V1/Source" "$kit/tests/Purity.Reference.V1/"
	cp "$root/tests/oracle/Purity.2022-05-06.dll" "$kit/tests/oracle/"

	for script in run-check.sh try-purity.command README.md; do
		tr -d '\r' < "$root/tools/platform-check/$script" > "$kit/$script"
	done
	printf 'rid: %s\ncommit: %s\nbuilt: %s\n' "$rid" "$(git -C "$root" describe --always --dirty)" "$(date -u '+%Y-%m-%d %H:%M UTC')" > "$kit/kit-info.txt"

	zip_folder "$out" "$name"
	echo "  $(du -sh "$kit" | cut -f1) unpacked, ready in out/platform-check"
done
rm -f "$out/publish.log"
