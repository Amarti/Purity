#!/bin/bash
# Encrypts or decrypts the real-data test fixture. The repo is public, so only the encrypted
# file may ever be committed; tests/private/ is gitignored.
#
#   tools/fixture.sh encrypt   tests/private -> tests/fixtures/private.tar.gz.gpg
#   tools/fixture.sh decrypt   tests/fixtures/private.tar.gz.gpg -> tests/private
#
# The passphrase comes from PURITY_FIXTURE_PASSPHRASE and is never written anywhere. Use a long
# random one, keep it in a password manager, and store it as the GitHub Actions secret of the
# same name. If it ever leaks, the history can't be recalled from a public repo.
set -euo pipefail

cd "$(dirname "$0")/.."
fixture=tests/fixtures/private.tar.gz.gpg

if [ -z "${PURITY_FIXTURE_PASSPHRASE:-}" ]; then
	echo "Set PURITY_FIXTURE_PASSPHRASE first" >&2
	exit 1
fi

gpg_args=(--batch --yes --quiet --pinentry-mode loopback --passphrase-fd 3)

case "${1:-}" in
	encrypt)
		mkdir -p tests/fixtures
		tar -czf - -C tests private |
			gpg "${gpg_args[@]}" --symmetric --cipher-algo AES256 --s2k-digest-algo SHA512 --s2k-count 65011712 --output "$fixture" 3<<<"$PURITY_FIXTURE_PASSPHRASE"
		echo "Wrote $fixture"
		;;
	decrypt)
		gpg "${gpg_args[@]}" --decrypt "$fixture" 3<<<"$PURITY_FIXTURE_PASSPHRASE" | tar -xzf - -C tests
		echo "Decrypted to tests/private"
		;;
	*)
		echo "Usage: $0 encrypt|decrypt" >&2
		exit 1
		;;
esac
