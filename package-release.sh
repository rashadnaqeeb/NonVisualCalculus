#!/bin/sh
# Writes releases/loadstone-release.json, the release manifest the installer reads: Loadstone's
# `package` command freezes loadstone-packages/non-visual-calculus.json for this version, hashes
# both mod zips (Windows and macOS, each tagged with its platform) and stamps both installers so
# running copies can update themselves. The release notes are the version's CHANGELOG.md section.
# Run on a Mac after build_release.ps1, build-installer.ps1, build_release.sh and
# build-installer.sh have filled releases/. Loadstone is a checkout beside this repo
# (../loadstone), or wherever LOADSTONE_DIR points.
set -eu

repo="$(cd "$(dirname "$0")" && pwd)"
loadstone="${LOADSTONE_DIR:-$(dirname "$repo")/loadstone}"
version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$repo/Directory.Build.props" | head -1)"
[ -n "$version" ] || { echo "Could not read Version from Directory.Build.props" >&2; exit 1; }
[ -f "$loadstone/Cargo.toml" ] || {
    echo "Loadstone checkout not found: $loadstone (clone github.com/bradjrenshaw/loadstone beside this repo or set LOADSTONE_DIR)" >&2
    exit 1
}

releases="$repo/releases"
win_zip="$releases/NonVisualCalculus-v$version.zip"
mac_zip="$releases/NonVisualCalculus-macOS-v$version.zip"
win_installer="$releases/NonVisualCalculusInstaller.exe"
mac_installer="$releases/NonVisualCalculusInstaller.dmg"
for required in "$win_zip" "$mac_zip" "$win_installer" "$mac_installer"; do
    [ -f "$required" ] || { echo "Release artifact not found: $required" >&2; exit 1; }
done

work="$repo/obj/mac/loadstone"
rm -rf "$work"
mkdir -p "$work"

# The section between "## V<version>" and the next "##" heading, as create-release.ps1 reads it.
awk -v heading="## V$version" '
    $0 == heading { found = 1; next }
    found && /^##[ \t]/ { exit }
    found { print }
' "$repo/CHANGELOG.md" > "$work/notes.md"
grep -q '[^[:space:]]' "$work/notes.md" || { echo "CHANGELOG.md has no non-empty '## V$version' section" >&2; exit 1; }

# The committed manifest plus a packaging block naming this version's zips: the CLI's --artifact
# flag cannot attach the platform condition each zip needs.
python3 -I - "$repo/loadstone-packages/non-visual-calculus.json" "$work/non-visual-calculus.json" \
    "$win_zip" "$mac_zip" <<'PY'
import json, sys
source, out, win_zip, mac_zip = sys.argv[1:]
with open(source, encoding="utf-8") as f:
    manifest = json.load(f)
manifest["packaging"] = {"artifacts": [
    {"file": win_zip, "when": {"platform": "windows"}},
    {"file": mac_zip, "when": {"platform": "macos"}},
]}
with open(out, "w", encoding="utf-8") as f:
    json.dump(manifest, f, indent=2)
PY

(cd "$loadstone" && cargo build -q --release -p loadstone-cli)
cli="$loadstone/target/release/loadstone"
"$cli" package --manifest "$work/non-visual-calculus.json" --scope "$repo/loadstone-packages" \
    --version "v$version" --installer "$win_installer" --installer "$mac_installer" \
    --notes-file "$work/notes.md" --out "$releases/loadstone-release.json"
"$cli" validate --scope "$repo/loadstone-packages" "$releases/loadstone-release.json"
rm -rf "$work"
