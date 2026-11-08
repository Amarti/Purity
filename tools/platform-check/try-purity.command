#!/bin/bash
# Opens Purity with the made-up history, keeping everything it writes inside this folder, so it
# can't touch the real data file. On a Mac, double-click this file. On Linux: bash try-purity.command
# Delete the "try" folder to start again from the original made-up history.
kit=$(cd "$(dirname "$0")" && pwd)
try="$kit/try"

if [ -f "$kit/Purity.app/Contents/MacOS/Purity" ]; then
	program="$kit/Purity.app/Contents/MacOS/Purity"
else
	program="$kit/app/Purity"
fi
if [ ! -x "$program" ]; then
	echo "Run run-check.sh once first: it prepares the app to run on this machine."
	exit 1
fi

mkdir -p "$try"
[ -f "$try/sample.json" ] || cp "$kit/sample.json" "$try/sample.json"
PURITY_APP_DATA="$try/app data" "$program" --data "$try/sample.json"
