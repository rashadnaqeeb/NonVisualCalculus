#!/bin/sh
# Builds the macOS installer, releases/NonVisualCalculusInstaller.dmg: the Loadstone mod manager
# branded as a single-mod installer by installer/loadstone-profile.json, which pins it to this
# repo's loadstone-packages/ registry. The image holds "Non-Visual Calculus Installer.app"
# (universal) and an Applications shortcut. Loadstone is a checkout beside this repo
# (../loadstone), or wherever LOADSTONE_DIR points; the first run builds wxWidgets once into
# ~/Library/Caches/loadstone. Run on a Mac, from anywhere.
#
# The app and the image are signed with a Developer ID Application identity (hardened runtime,
# secure timestamp), notarized, and stapled, so Gatekeeper opens a downloaded copy without
# asking. NVC_SIGN_IDENTITY picks the identity (a name or SHA-1 from
# `security find-identity -v -p codesigning`), else the keychain's first Developer ID Application
# one. NVC_NOTARY_PROFILE names the notarytool keychain profile (default "notary"; create it with
# `xcrun notarytool store-credentials`).
set -eu

repo="$(cd "$(dirname "$0")" && pwd)"
loadstone="${LOADSTONE_DIR:-$(dirname "$repo")/loadstone}"
[ -x "$loadstone/scripts/build_macos_app.sh" ] || {
    echo "Loadstone checkout not found: $loadstone (clone github.com/bradjrenshaw/loadstone beside this repo or set LOADSTONE_DIR)" >&2
    exit 1
}
name="Non-Visual Calculus Installer"
out="$repo/obj/mac/installer"
app="$out/$name.app"
dmg="$repo/releases/NonVisualCalculusInstaller.dmg"
notary_profile="${NVC_NOTARY_PROFILE:-notary}"

identity="${NVC_SIGN_IDENTITY:-}"
if [ -z "$identity" ]; then
    identity="$(security find-identity -v -p codesigning \
        | sed -n 's/^[^)]*) \([0-9A-F]\{40\}\) "Developer ID Application:.*"$/\1/p' | head -n 1)"
fi
[ -n "$identity" ] || { echo "No Developer ID Application identity in the keychain (set NVC_SIGN_IDENTITY)" >&2; exit 1; }
xcrun notarytool history --keychain-profile "$notary_profile" >/dev/null || {
    echo "notarytool keychain profile \"$notary_profile\" does not work (set NVC_NOTARY_PROFILE)" >&2
    exit 1
}

notarize() {
    xcrun notarytool submit "$1" --keychain-profile "$notary_profile" --wait --output-format json \
        > "$out/notary.json"
    status="$(python3 -I -c 'import json,sys; print(json.load(open(sys.argv[1]))["status"])' "$out/notary.json")"
    if [ "$status" != Accepted ]; then
        id="$(python3 -I -c 'import json,sys; print(json.load(open(sys.argv[1]))["id"])' "$out/notary.json")"
        echo "Notarization of $(basename "$1") ended $status:" >&2
        xcrun notarytool log "$id" --keychain-profile "$notary_profile" >&2
        exit 1
    fi
}

"$loadstone/scripts/build_macos_app.sh" \
    --profile "$repo/installer/loadstone-profile.json" \
    --name "$name" \
    --bundle-id com.rashad.nonvisualcalculus.installer \
    --out "$out"

echo "== Signing $name.app"
codesign --force --deep --options runtime --timestamp --sign "$identity" "$app"
codesign --verify --strict --deep "$app"

echo "== Notarizing $name.app"
ditto -c -k --keepParent "$app" "$out/app.zip"
notarize "$out/app.zip"
rm "$out/app.zip"
xcrun stapler staple -q "$app"

# The app beside an Applications shortcut, as Loadstone's own --dmg builds it.
echo "== Building $(basename "$dmg")"
image_root="$(mktemp -d "${TMPDIR:-/tmp}/nvc-dmg-XXXXXX")"
ditto "$app" "$image_root/$name.app"
ln -s /Applications "$image_root/Applications"
mkdir -p "$repo/releases"
rm -f "$dmg"
hdiutil create -quiet -volname "$name" -srcfolder "$image_root" -fs HFS+ -format UDZO -ov "$dmg"
rm -rf "$image_root"
codesign --force --timestamp --sign "$identity" "$dmg"

echo "== Notarizing $(basename "$dmg")"
notarize "$dmg"
xcrun stapler staple -q "$dmg"
rm "$out/notary.json"
spctl --assess --type open --context context:primary-signature -v "$dmg"
echo "Installer: $dmg"
