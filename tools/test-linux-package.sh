#!/usr/bin/env bash
# Installs a Purity package in a clean Debian or Ubuntu container and opens the app on a made-up
# history, on a virtual screen. Proves the package's dependencies are enough. As CI runs it:
#   docker run --rm -v "$PWD:/repo" -w /repo debian:13 \
#     bash tools/test-linux-package.sh out/<version>/purity_<version>_amd64.deb out/sample.json
# Needs root, so only ever run it in a throwaway container. A third argument copies the
# picture and report of the window there.
set -euo pipefail
deb=$1 sample=$2 keep=${3:-}
system=$(. /etc/os-release && echo "$PRETTY_NAME")
export DEBIAN_FRONTEND=noninteractive

# Says what went wrong, with the end of the log that shows it. On GitHub the same text becomes a
# public annotation, so a failure can be read without signing in. Everything here is made up.
fail() { # message [log]
	local text=$1
	[ -n "${2:-}" ] && [ -f "$2" ] && text="$text"$'\n'"$(tail -n 25 "$2")"
	echo "$text"
	text=${text//'%'/'%25'}
	text=${text//$'\r'/}
	text=${text//$'\n'/'%0A'}
	echo "::error title=Linux package on $system::$text"
	exit 1
}

echo "Installing $(basename "$deb") on $system"
apt-get update -qq > /tmp/apt.log 2>&1 || fail "apt-get update failed" /tmp/apt.log
apt-get install -y -qq "./$deb" xvfb xauth desktop-file-utils > /tmp/apt.log 2>&1 || fail "Installing the package failed" /tmp/apt.log
desktop-file-validate /usr/share/applications/purity.desktop > /tmp/desktop.log 2>&1 || fail "The menu entry isn't valid" /tmp/desktop.log
[ -x /usr/bin/purity ] || fail "The package has no purity command"

work=$(mktemp -d)
cp "$sample" "$work/sample.json"
PURITY_APP_DATA="$work/app data" xvfb-run -a -s "-screen 0 1280x1024x24" purity --data "$work/sample.json" --picture "$work/app.png" > "$work/app.log" 2>&1 ||
	fail "The app failed with exit code $?" "$work/app.log"
cat "$work/app.txt"
[ -n "$keep" ] && mkdir -p "$keep" && cp "$work/app.png" "$work/app.txt" "$keep/"

grep -q '^result: passed' "$work/app.txt" || fail "The app couldn't picture its window" "$work/app.txt"
grep -q '^self-test: passed' "$work/app.txt" || fail "The app's check of its date calculations didn't pass" "$work/app.txt"
grep -q '^font: Heebo' "$work/app.txt" || fail "The app isn't using its built-in Heebo" "$work/app.txt"
cmp -s "$sample" "$work/sample.json" || fail "Opening the app changed the data file"
echo "The package works on $system"
