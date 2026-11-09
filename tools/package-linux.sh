#!/usr/bin/env bash
# Builds Debian packages of the desktop app, which also install on Ubuntu:
#   bash tools/package-linux.sh                 # amd64 and arm64
#   bash tools/package-linux.sh linux-x64       # or chosen runtime identifiers
# Writes out/<version>/purity_<version>_<arch>.deb, beside the other platforms' builds. Runs on
# Linux. On Windows the packaging step runs in WSL, since dpkg-deb only exists on Linux.
# tools/test-linux-package.sh tries a package out.
#
# The package puts the self-contained app in /opt/purity, a purity command in /usr/bin, and a menu
# entry with the lotus icon. Installing: sudo apt install ./purity_<version>_<arch>.deb
set -euo pipefail

root=$(cd "$(dirname "$0")/.." && pwd)

make_deb() { # publish-folder arch version deb
	local publish=$1 arch=$2 version=$3 deb=$4
	local packaging="$root/src/Purity.Desktop/Packaging/linux"
	local stage
	stage=$(mktemp -d)		# on a Linux file system, so the permissions below hold

	mkdir -p "$stage/DEBIAN" "$stage/opt/purity" "$stage/usr/bin" "$stage/usr/share/applications" "$stage/usr/share/icons/hicolor/256x256/apps"
	cp -R "$publish/." "$stage/opt/purity/"
	rm -f "$stage/opt/purity/"*.pdb
	find "$stage" -type d -exec chmod 755 {} +
	find "$stage/opt/purity" -type f -exec chmod 644 {} +
	chmod 755 "$stage/opt/purity/Purity"
	[ -f "$stage/opt/purity/createdump" ] && chmod 755 "$stage/opt/purity/createdump"
	ln -s /opt/purity/Purity "$stage/usr/bin/purity"

	tr -d '\r' < "$packaging/purity.desktop" > "$stage/usr/share/applications/purity.desktop"
	cp "$packaging/purity.png" "$stage/usr/share/icons/hicolor/256x256/apps/purity.png"
	chmod 644 "$stage/usr/share/applications/purity.desktop" "$stage/usr/share/icons/hicolor/256x256/apps/purity.png"

	local size
	size=$(du -sk --exclude=DEBIAN "$stage" | cut -f1)
	tr -d '\r' < "$packaging/control" | sed -e "s/@VERSION@/$version/" -e "s/@ARCH@/$arch/" -e "s/@SIZE@/$size/" > "$stage/DEBIAN/control"
	chmod 644 "$stage/DEBIAN/control"

	dpkg-deb --root-owner-group -Zxz --build "$stage" "$deb" > /dev/null
	rm -rf "$stage"
}

# A Windows path as WSL sees it: C:\x becomes /mnt/c/x
to_wsl() { cygpath -u "$1" | sed -E 's#^/([A-Za-z])/#/mnt/\L\1/#'; }

if [ "${1:-}" = --deb ]; then
	shift
	make_deb "$@"
	exit
fi

version=$(tr -d '\r' < "$root/src/Purity.Desktop/Purity.Desktop.csproj" | sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' | head -1)
out="$root/out/$version"
if [ $# -gt 0 ]; then rids=("$@"); else rids=(linux-x64 linux-arm64); fi
mkdir -p "$out"

for rid in "${rids[@]}"; do
	case $rid in
		linux-x64) arch=amd64 ;;
		linux-arm64) arch=arm64 ;;
		*) echo "No Debian architecture known for $rid" >&2; exit 1 ;;
	esac
	publish="$out/publish-$rid"
	deb="$out/purity_${version}_$arch.deb"
	echo "Building $(basename "$deb")"
	rm -rf "$publish" "$deb"
	dotnet publish "$root/src/Purity.Desktop/Purity.Desktop.csproj" -c Release -r "$rid" --self-contained -nologo -v quiet -o "$publish" > "$out/publish.log" 2>&1 ||
		{ cat "$out/publish.log"; exit 1; }

	if command -v dpkg-deb > /dev/null; then
		make_deb "$publish" "$arch" "$version" "$deb"
	elif command -v wsl.exe > /dev/null; then
		MSYS_NO_PATHCONV=1 wsl.exe -e bash "$(to_wsl "$root/tools/package-linux.sh")" --deb "$(to_wsl "$publish")" "$arch" "$version" "$(to_wsl "$deb")"
	else
		echo "dpkg-deb is needed: run this on Linux, or on Windows with WSL" >&2
		exit 1
	fi
	rm -rf "$publish"
	echo "  $(du -h "$deb" | cut -f1), ready in out/$version"
done
rm -f "$out/publish.log"
