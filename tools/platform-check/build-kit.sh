#!/usr/bin/env bash
# Builds the platform check kit: the calculation and storage test suites and a UI probe per
# candidate stack, self-contained, so the target machine needs nothing installed. Run anywhere:
#   bash tools/platform-check/build-kit.sh                  # Mac: Intel and Apple Silicon
#   bash tools/platform-check/build-kit.sh linux-x64        # or any runtime identifiers
# Writes out/platform-check/purity-check-<rid>/ and a zip of it.
set -euo pipefail

root=$(cd "$(dirname "$0")/../.." && pwd)
out="$root/out/platform-check"
if [ $# -gt 0 ]; then rids=("$@"); else rids=(osx-x64 osx-arm64); fi

publish() { # project framework rid folder
	echo "  $(basename "$1" .csproj) for $2"
	dotnet publish "$root/$1" -c Release -f "$2" -r "$3" --self-contained -nologo -v quiet -o "$4" > "$out/publish.log" 2>&1 ||
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

	# Both test suites share one folder per framework: same packages, one copy of the runtime
	for framework in net10.0 net8.0; do
		short=${framework%.0}
		publish tests/Purity.Parity/Purity.Parity.csproj "$framework" "$rid" "$kit/bin/tests-$short"
		publish tests/Purity.Storage.Tests/Purity.Storage.Tests.csproj "$framework" "$rid" "$kit/bin/tests-$short"
		publish tools/Purity.UiProbe/Purity.UiProbe.csproj "$framework" "$rid" "$kit/bin/ui-avalonia12-$short"
		# Coverage tooling: the kit runs tests without collecting coverage. The folder is 30 MB of
		# Windows binaries, and the profiler libraries would look like a problem for older macOS
		# (one needs macOS 15) though they only load when coverage is collected.
		rm -rf "$kit/bin/tests-$short/Microsoft.CodeCoverage"
		rm -f "$kit/bin/tests-$short/"lib{CoverageInstrumentationMethod,InstrumentationEngine}.*
	done
	publish tools/Purity.UiProbe.Avalonia11/Purity.UiProbe.Avalonia11.csproj net8.0 "$rid" "$kit/bin/ui-avalonia11-net8"

	# What the parity tests read. They find it by walking up to Purity.sln, so an empty one marks the root.
	: > "$kit/Purity.sln"
	mkdir -p "$kit/tests/Purity.Parity" "$kit/tests/Purity.Reference.V1" "$kit/tests/oracle"
	cp -R "$root/tests/Purity.Parity/Golden" "$kit/tests/Purity.Parity/"
	cp -R "$root/tests/Purity.Reference.V1/Source" "$kit/tests/Purity.Reference.V1/"
	cp "$root/tests/oracle/Purity.2022-05-06.dll" "$kit/tests/oracle/"

	tr -d '\r' < "$root/tools/platform-check/run-check.sh" > "$kit/run-check.sh"
	tr -d '\r' < "$root/tools/platform-check/README.md" > "$kit/README.md"
	printf 'rid: %s\ncommit: %s\nbuilt: %s\n' "$rid" "$(git -C "$root" describe --always --dirty)" "$(date -u '+%Y-%m-%d %H:%M UTC')" > "$kit/kit-info.txt"

	zip_folder "$out" "$name"
	echo "  $(du -sh "$kit" | cut -f1) unpacked, ready in out/platform-check"
done
rm -f "$out/publish.log"
