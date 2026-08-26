#!/usr/bin/env bash
set -euo pipefail

version="${1:-1.4.0}"
rid="${2:?usage: package-macos.sh VERSION osx-x64|osx-arm64}"
case "$rid" in
  osx-x64) arch="x64" ;;
  osx-arm64) arch="arm64" ;;
  *) echo "unsupported macOS runtime: $rid" >&2; exit 2 ;;
esac

root="$(cd "$(dirname "$0")/.." && pwd)"
dist="$root/dist"
publish="$dist/$rid/publish"
stage="$dist/$rid/stage"
release="$dist/release"
app="$stage/CodexTokenOverlay.app"

rm -rf "$publish" "$stage"
mkdir -p "$publish" "$app/Contents/MacOS" "$app/Contents/Resources" "$release"

dotnet publish "$root/src/CodexTokenOverlay.Portable/CodexTokenOverlay.Portable.csproj" \
  -c Release -r "$rid" --self-contained true -o "$publish" --nologo
cp -R "$publish/." "$app/Contents/MacOS/"
chmod +x "$app/Contents/MacOS/CodexTokenOverlay"
sed "s/@VERSION@/$version/g" "$root/packaging/macos/Info.plist" > "$app/Contents/Info.plist"
cp "$root/LICENSE" "$root/PRIVACY.txt" "$app/Contents/Resources/"

iconset="$stage/CodexTokenOverlay.iconset"
mkdir -p "$iconset"
for size in 16 32 128 256 512; do
  sips -z "$size" "$size" "$root/assets/CodexTokenOverlay.png" --out "$iconset/icon_${size}x${size}.png" >/dev/null
  double=$((size * 2))
  sips -z "$double" "$double" "$root/assets/CodexTokenOverlay.png" --out "$iconset/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$iconset" -o "$app/Contents/Resources/CodexTokenOverlay.icns"
codesign --force --deep --sign - "$app"

dmg="$release/CodexTokenOverlay-$version-macos-$arch.dmg"
zip="$release/CodexTokenOverlay-$version-macos-$arch.zip"
rm -f "$dmg" "$zip"
hdiutil create -volname "Codex Token Overlay" -srcfolder "$app" -ov -format UDZO "$dmg"
ditto -c -k --sequesterRsrc --keepParent "$app" "$zip"

shasum -a 256 "$dmg" "$zip"
