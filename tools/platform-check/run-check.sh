#!/bin/bash
# Purity platform check: runs the test suites on this machine, opens the desktop app with a made-up
# history, and writes a report to send back. Usage:
#   bash run-check.sh              # everything, with a short hands-on check at the end
#   bash run-check.sh --auto-only  # no hands-on check, for machines without a person at them
# Works with macOS's bash 3.2 and on Linux. It changes nothing outside this folder apart from
# temporary files the tests remove, and it never touches the real data file.

set -u
kit=$(cd "$(dirname "$0")" && pwd)
stamp=$(date +%Y%m%d-%H%M%S)
results="$kit/results-$stamp"
report="$results/report.txt"
check="$kit/.check-$stamp"
os=$(uname -s)
summary=""
manual=yes
[ "${1:-}" = "--auto-only" ] && manual=no
mkdir -p "$results" "$check"

if [ -d "$kit/Purity.app" ]; then
	app="$kit/Purity.app/Contents/MacOS/Purity"
else
	app="$kit/app/Purity"
fi

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

# Opens the app on a copy of the made-up history; it saves a picture of its window and closes
run_app_picture() {
	if [ ! -f "$app" ]; then
		note "app window" "missing from the kit"
		return 1
	fi
	printf 'Opening the app (its window flashes up) ... '
	cp "$kit/sample.json" "$check/picture.json"
	run_limited 90 "$results/app.log" env PURITY_APP_DATA="$check/app data" "$app" --data "$check/picture.json" --picture "$results/app.png"
	code=$?
	result=$(sed -n 's/^result: //p' "$results/app.txt" 2> /dev/null)
	selftest=$(sed -n 's/^self-test: //p' "$results/app.txt" 2> /dev/null)
	if [ $code -eq 0 ] && [ "$result" = passed ] && [ "$selftest" = passed ]; then
		echo passed
		note "app window" "passed, $(sed -n 's/^window: //p' "$results/app.txt")"
		note "app self-test" "passed"
		return 0
	fi
	echo "FAILED (exit $code)"
	note "app window" "FAILED (exit $code, ${result:-no report})"
	note "app self-test" "${selftest:-not run}"
	return 1
}

periods_in() { # data file
	count=$(grep -c '"Begin"' "$1" 2> /dev/null)
	echo "${count:-0}"
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
	system_profiler SPDisplaysDataType 2> /dev/null | grep -E 'Chipset Model|Metal|Resolution' | sed 's/^ */Graphics: /' | scrub | tee -a "$report"
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

# The test suites need ICU on Linux unless told to do without it (macOS always has it). The app
# itself never uses it.
if [ "$os" = Linux ] && ! { /sbin/ldconfig -p 2> /dev/null || ldconfig -p 2> /dev/null; } | grep -q 'libicuuc\.so'; then
	export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
	say "ICU: not installed, so the tests run without it and most calculation tests skip. The app doesn't need it."
fi

if [ -f "$kit/private/pureData.json" ]; then
	export PURITY_PRIVATE_DATA="$kit/private" PURITY_REQUIRE_REAL_DATA=1
	say "Real data: included (the tests use a temporary copy)"
else
	say "Real data: not included, so those tests skip"
fi
say ""

# Downloads are quarantined, and zip tools drop the executable flag. The kit is built on Windows,
# so on a Mac anything unsigned also gets an ad hoc signature, which is valid on this machine only.
[ "$os" = Darwin ] && xattr -dr com.apple.quarantine "$kit" 2> /dev/null
chmod +x "$kit/try-purity.command" 2> /dev/null
signed=0
while IFS= read -r file; do
	is_native "$file" || continue
	chmod +x "$file"
	if [ "$os" = Darwin ] && ! codesign -v "$file" 2> /dev/null; then
		codesign -f -s - "$file" >> "$results/prepare.log" 2>&1 && signed=$((signed + 1))
	fi
done < <(find "$kit/bin" "$kit/Purity.app" "$kit/app" -type f ! -name '*.dll' ! -name '*.json' ! -name '*.pdb' ! -name '*.xml' 2> /dev/null)
[ "$os" = Darwin ] && say "Signed $signed programs and libraries for this Mac"

# Calculations, storage and the app's behavior
run_tests parity "$kit/bin/tests/Purity.Parity"
run_tests storage "$kit/bin/tests/Purity.Storage.Tests"
run_tests desktop "$kit/bin/tests/Purity.Desktop.Tests"

# The app itself
if run_app_picture && [ "$manual" = yes ]; then
	cp "$kit/sample.json" "$check/try.json"
	before=$(periods_in "$check/try.json")
	echo ""
	echo "Last step: the app opens with a made-up history for you to try. In it:"
	echo "  1. Scroll the list with the mouse wheel or trackpad, keeping the pointer over the"
	echo "     date boxes. No date should change."
	echo "  2. Click the pink drop at the top to add a period. Click the little calendar in"
	echo "     its End box and pick a date, then click the sun next to it so it shows a moon."
	echo "  3. Click the tick on the right of the new period to accept it. Its dates appear under it."
	echo "  4. Click the gear at the top left, then \"...\" next to the data file path. Cancel"
	echo "     the file chooser, then press Cancel in Settings."
	echo "  5. Close the window. The questions follow."
	printf 'Press Return to open the app ... '
	read -r _
	PURITY_APP_DATA="$check/app data" "$app" --data "$check/try.json" > "$results/app-try.log" 2>&1
	after=$(periods_in "$check/try.json")
	if [ "$after" -gt "$before" ]; then saved="saved the new period"; else saved="saved no new period"; fi
	note "app, by hand" "$saved ($before periods before, $after after)"

	echo "Answers:" >> "$report"
	ask "Did the whole window fit on the screen, with sharp text and nothing cut off?"
	ask "Do the Hebrew dates read correctly, as in the Windows app?"
	ask "Did scrolling over the date boxes leave every date unchanged?"
	ask "Did the calendar open and let you pick a date?"
	ask "Did the file chooser open from Settings?"
	printf 'Anything else that looked wrong? Type a short note, or just press Return: '
	read -r remark
	[ -n "$remark" ] && echo "  Note: $remark" >> "$report"
fi
rm -rf "$check"

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
[ -f "$app" ] && echo "To try the app again later, double-click try-purity.command in this folder."
