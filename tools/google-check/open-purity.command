#!/bin/bash
# Opens Purity with the made-up history. Its settings and its copy of the history stay in the
# "this computer" folder beside this file, apart from any real Purity; delete that folder to start
# afresh. The Google sign-in goes where the system keeps secrets: the login keychain on a Mac, the
# desktop's keyring on Linux.
#   Mac: in Terminal, type bash and a space, drag this file in, and press Return.
#   Linux: bash open-purity.command
set -u
kit=$(cd "$(dirname "$0")" && pwd)
computer="$kit/this computer"
os=$(uname -s)
if [ -d "$kit/Purity.app" ]; then
	program="$kit/Purity.app/Contents/MacOS/Purity"
else
	program="$kit/app/Purity"
fi

# True for programs and native libraries (Mach-O or ELF)
is_native() {
	case $(head -c 4 "$1" | od -An -tx1 | tr -d ' \n') in
		cffaedfe|cafebabe|7f454c46) return 0 ;;
	esac
	return 1
}

# Once: downloads are quarantined, and the kit is built on Windows, so on a Mac anything unsigned
# gets an ad hoc signature, which is valid on this machine only
if [ ! -f "$kit/.prepared" ]; then
	echo "Preparing the app to run on this computer (only the first time) ..."
	[ "$os" = Darwin ] && xattr -dr com.apple.quarantine "$kit" 2> /dev/null
	while IFS= read -r file; do
		is_native "$file" || continue
		chmod +x "$file"
		if [ "$os" = Darwin ] && ! codesign -v "$file" 2> /dev/null; then
			codesign -f -s - "$file" > /dev/null 2>&1
		fi
	done < <(find "$kit/Purity.app" "$kit/app" -type f ! -name '*.dll' ! -name '*.json' ! -name '*.pdb' ! -name '*.xml' 2> /dev/null)
	touch "$kit/.prepared"
fi

if [ "$os" = Linux ] && ! command -v secret-tool > /dev/null; then
	echo "Note: secret-tool isn't installed, so Purity can't remember the sign-in. To add it: sudo apt install libsecret-tools"
fi

mkdir -p "$computer"
[ -f "$computer/history.json" ] || cp "$kit/history.json" "$computer/history.json"
PURITY_APP_DATA="$computer/app data" "$program" --data "$computer/history.json"
