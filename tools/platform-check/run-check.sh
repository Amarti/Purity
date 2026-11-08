#!/bin/bash
# Purity platform check: runs the calculation and storage tests on this machine, opens the window
# of each candidate UI stack, and writes a report to send back. Usage:
#   bash run-check.sh              # everything, with a short hands-on check at the end
#   bash run-check.sh --auto-only  # no hands-on check, for machines without a person at them
# Works with macOS's bash 3.2 and on Linux. It changes nothing outside this folder apart from
# temporary files the tests remove, and it never touches the real data file.

set -u
kit=$(cd "$(dirname "$0")" && pwd)
stamp=$(date +%Y%m%d-%H%M%S)
results="$kit/results-$stamp"
report="$results/report.txt"
os=$(uname -s)
summary=""
first_window=""
manual=yes
[ "${1:-}" = "--auto-only" ] && manual=no
mkdir -p "$results"

# Keeps the home folder, which carries the user name, out of everything sent back
scrub() { sed -e "s#$HOME#~#g"; }
say() { printf '%s\n' "$*" | scrub | tee -a "$report"; }
note() { summary="$summary$(printf '  %-22s %s' "$1" "$2")
"; }

# True for programs and native libraries (Mach-O or ELF), which zip tools strip of the executable flag
is_native() {
	case $(head -c 4 "$1" | od -An -tx1 | tr -d ' \n') in
		cffaedfe|cafebabe|7f454c46) return 0 ;;
	esac
	return 1
}

# Runs a command with a time limit (macOS has no timeout command); returns its exit code, or 124 if stopped
run_limited() { # seconds log command...
	limit=$1 log=$2
	shift 2
	"$@" < /dev/null > "$log" 2>&1 &
	pid=$!
	waited=0
	while kill -0 $pid 2> /dev/null; do
		if [ $waited -ge "$limit" ]; then
			kill -9 $pid 2> /dev/null
			wait $pid 2> /dev/null
			echo "Stopped after $limit seconds" >> "$log"
			return 124
		fi
		sleep 2
		waited=$((waited + 2))
	done
	wait $pid
}

run_tests() { # name program
	if [ ! -f "$2" ]; then
		note "$1" "missing from the kit"
		return
	fi
	printf 'Running %s (a few minutes) ... ' "$1"
	start=$(date +%s)
	run_limited 1800 "$results/$1.log" "$2" -noColor
	code=$?
	seconds=$(($(date +%s) - start))
	counts=$(grep -E 'Total: [0-9]+' "$results/$1.log" | tail -1 | sed -e 's/^ *[^ ]* *//' -e 's/, Time:.*//')
	if [ $code -eq 0 ]; then result=passed; else result="FAILED (exit $code)"; fi
	echo "$result"
	note "$1" "$result in ${seconds}s. ${counts:-no test summary}"
}

run_window() { # variant
	program="$kit/bin/$1/Purity.UiProbe"
	if [ ! -f "$program" ]; then
		note "$1" "missing from the kit"
		return
	fi
	printf 'Opening %s (a window flashes up) ... ' "$1"
	run_limited 90 "$results/$1.log" "$program" --auto --out "$results" --variant "$1"
	code=$?
	result=$(sed -n 's/^result: //p' "$results/$1.txt" 2> /dev/null)
	if [ $code -eq 0 ] && [ "$result" = passed ]; then
		echo passed
		note "$1" "passed"
		[ -n "$first_window" ] || first_window=$1
	else
		echo "FAILED (exit $code)"
		note "$1" "FAILED (exit $code, ${result:-no report})"
	fi
}

ask() { # question
	answer=""
	while [ "$answer" != y ] && [ "$answer" != n ]; do
		printf '%s [y/n] ' "$1"
		read -r answer
	done
	echo "  $1 $answer" >> "$report"
	note "you: $answer" "$1"
}


# What this machine is
say "Purity platform check, $(date '+%Y-%m-%d %H:%M')"
if [ "$os" = Darwin ]; then
	say "macOS $(sw_vers -productVersion) ($(sw_vers -buildVersion)), $(sysctl -n hw.model)"
	say "Processor: $(sysctl -n machdep.cpu.brand_string 2> /dev/null)"
	[ "$(sysctl -n sysctl.proc_translated 2> /dev/null)" = 1 ] && say "This Terminal runs under Rosetta"
	system_profiler SPDisplaysDataType 2> /dev/null | grep -E 'Chipset Model|Metal' | sed 's/^ */Graphics: /' | scrub | tee -a "$report"
else
	say "$(. /etc/os-release 2> /dev/null && echo "${PRETTY_NAME:-}") $(uname -r)"
fi
say "Machine: $(uname -m)"
say "Kit: $(tr '\n' ' ' < "$kit/kit-info.txt")"

kit_rid=$(sed -n 's/^rid: //p' "$kit/kit-info.txt")
case "$kit_rid:$(uname -m)" in
	*-x64:x86_64 | *-arm64:arm64 | *-arm64:aarch64) ;;
	osx-x64:arm64) say "Note: this is the Intel kit on Apple Silicon, so it runs under Rosetta. The arm64 kit checks this Mac natively." ;;
	*)
		say "This kit is for $kit_rid and doesn't run on this machine ($(uname -m)). Use the other kit."
		exit 1
		;;
esac

# .NET won't start on Linux without ICU unless told to do without it (macOS always has ICU)
if [ "$os" = Linux ] && ! { /sbin/ldconfig -p 2> /dev/null || ldconfig -p 2> /dev/null; } | grep -q 'libicuuc\.so'; then
	export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
	say "ICU: not installed, so everything runs without it. The app will need the libicu package here, or to run the same way."
fi

if [ -f "$kit/private/pureData.json" ]; then
	export PURITY_PRIVATE_DATA="$kit/private" PURITY_REQUIRE_REAL_DATA=1
	say "Real data: included (the tests use a temporary copy)"
else
	say "Real data: not included, so those tests skip"
fi
say ""

# Downloads are quarantined, and zip tools drop the executable flag. The kit is built on Windows,
# so on a Mac its programs also get an ad hoc signature, which is valid on this machine only.
[ "$os" = Darwin ] && xattr -dr com.apple.quarantine "$kit" 2> /dev/null
signed=0
while IFS= read -r file; do
	is_native "$file" || continue
	chmod +x "$file"
	if [ "$os" = Darwin ] && ! codesign -v "$file" 2> /dev/null; then
		codesign -f -s - "$file" >> "$results/prepare.log" 2>&1 && signed=$((signed + 1))
	fi
done < <(find "$kit/bin" -type f ! -name '*.dll' ! -name '*.json' ! -name '*.pdb' ! -name '*.xml')
[ "$os" = Darwin ] && say "Signed $signed programs and libraries for this Mac"

# Calculations and storage, on both runtimes
run_tests parity-net10 "$kit/bin/tests-net10/Purity.Parity"
run_tests parity-net8 "$kit/bin/tests-net8/Purity.Parity"
run_tests storage-net10 "$kit/bin/tests-net10/Purity.Storage.Tests"
run_tests storage-net8 "$kit/bin/tests-net8/Purity.Storage.Tests"

# Each UI candidate, in order of preference
run_window ui-avalonia12-net10
run_window ui-avalonia12-net8
run_window ui-avalonia11-net8

# A person tries the best candidate that opened
if [ "$manual" = yes ] && [ -n "$first_window" ]; then
	echo ""
	echo "Last step: a window opens for you to try ($first_window). In it:"
	echo "  1. Look at the table. Column A is the Hebrew date as the Windows app shows it;"
	echo "     column B is an old workaround and may look scrambled."
	echo "  2. Click the date box, pick any date, then tick Night."
	echo "  3. Click \"Choose a file...\", then cancel, or pick any file (it isn't opened)."
	echo "  4. Click Close. The questions follow."
	printf 'Press Return to open the window ... '
	read -r _
	"$kit/bin/$first_window/Purity.UiProbe" --out "$results" --variant "$first_window-manual" > "$results/$first_window-manual.log" 2>&1
	echo "Answers:" >> "$report"
	ask "Did the window look right, with sharp text and nothing cut off?"
	ask "Does column A read correctly?"
	ask "Does column B read correctly?"
	ask "Did the date picker open and let you pick a date?"
	ask "Did the file chooser open?"
fi

say ""
say "Summary"
printf '%s' "$summary" | tee -a "$report"
[ "${DOTNET_SYSTEM_GLOBALIZATION_INVARIANT:-}" = 1 ] && say "Without ICU most calculation tests skip, so this run doesn't check the calculations fully."

for log in "$results"/*.log "$results"/*.txt; do
	[ -f "$log" ] && scrub < "$log" > "$log.tmp" && mv "$log.tmp" "$log"
done
if [ "$os" = Darwin ]; then
	ditto -c -k --keepParent "$results" "$results.zip"
	sent="$results.zip"
else
	(cd "$kit" && tar -czf "results-$stamp.tar.gz" "results-$stamp")
	sent="$kit/results-$stamp.tar.gz"
fi
echo ""
say "Done. Please send back $sent"
