#!/usr/bin/env bash
# Installs a Purity package in a clean Debian or Ubuntu container and opens the app on a made-up
# history, on a virtual screen. Proves the package's dependencies are enough. As CI runs it:
#   docker run --rm -v "$PWD:/repo" -w /repo debian:13 \
#     bash tools/test-linux-package.sh out/linux/purity_2.0.0_amd64.deb out/linux/sample.json
# Needs root, so only ever run it in a throwaway container. A third argument copies the
# picture and report of the window there.
set -euo pipefail
deb=$1 sample=$2 keep=${3:-}
system=$(. /etc/os-release && echo "$PRETTY_NAME")
export DEBIAN_FRONTEND=noninteractive

echo "Installing $(basename "$deb") on $system"
apt-get update -qq
apt-get install -y -qq "./$deb" xvfb xauth desktop-file-utils > /tmp/apt.log 2>&1 || { cat /tmp/apt.log; exit 1; }
desktop-file-validate /usr/share/applications/purity.desktop
test -x /usr/bin/purity

work=$(mktemp -d)
cp "$sample" "$work/sample.json"
PURITY_APP_DATA="$work/app data" xvfb-run -a -s "-screen 0 1280x1024x24" purity --data "$work/sample.json" --picture "$work/app.png"
cat "$work/app.txt"
[ -n "$keep" ] && mkdir -p "$keep" && cp "$work/app.png" "$work/app.txt" "$keep/"

grep -q '^result: passed' "$work/app.txt"
grep -q '^font: Liberation Sans' "$work/app.txt" || { echo "The package didn't bring in Liberation Sans"; exit 1; }
cmp -s "$sample" "$work/sample.json" || { echo "Opening the app changed the data file"; exit 1; }
echo "The package works on $system"
