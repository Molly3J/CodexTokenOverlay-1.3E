#!/usr/bin/env bash
set -euo pipefail

version="${1:-0.1.1}"
rid="linux-x64"
root="$(cd "$(dirname "$0")/.." && pwd)"
dist="$root/dist"
publish="$dist/$rid/publish"
stage="$dist/$rid/stage"
release="$dist/release"

rm -rf "$publish" "$stage"
mkdir -p "$publish" "$stage" "$release"
dotnet publish "$root/src/CodexTokenOverlay.Portable/CodexTokenOverlay.Portable.csproj" \
  -c Release -r "$rid" --self-contained true -o "$publish" --nologo
chmod +x "$publish/CodexTokenOverlay"

tarball="$release/CodexTokenOverlay-$version-linux-x64.tar.gz"
tar -C "$publish" -czf "$tarball" .

debroot="$stage/deb"
mkdir -p "$debroot/DEBIAN" "$debroot/opt/codex-token-overlay" "$debroot/usr/bin" \
  "$debroot/usr/share/applications" "$debroot/usr/share/icons/hicolor/256x256/apps"
cp -a "$publish/." "$debroot/opt/codex-token-overlay/"
install -m 755 "$root/packaging/linux/codex-token-overlay" "$debroot/usr/bin/codex-token-overlay"
install -m 644 "$root/packaging/linux/CodexTokenOverlay.desktop" "$debroot/usr/share/applications/codex-token-overlay.desktop"
install -m 644 "$root/assets/CodexTokenOverlay.png" "$debroot/usr/share/icons/hicolor/256x256/apps/codex-token-overlay.png"
sed "s/@VERSION@/$version/g" "$root/packaging/linux/deb-control" > "$debroot/DEBIAN/control"
dpkg-deb --root-owner-group --build "$debroot" "$release/codex-token-overlay_${version}_amd64.deb"

rpmtop="$stage/rpmbuild"
mkdir -p "$rpmtop/BUILD" "$rpmtop/BUILDROOT" "$rpmtop/RPMS" "$rpmtop/SOURCES/publish" "$rpmtop/SPECS" "$rpmtop/SRPMS"
cp -a "$publish/." "$rpmtop/SOURCES/publish/"
cp "$root/packaging/linux/codex-token-overlay" "$root/packaging/linux/CodexTokenOverlay.desktop" "$rpmtop/SOURCES/"
cp "$root/assets/CodexTokenOverlay.png" "$rpmtop/SOURCES/"
sed "s/@VERSION@/$version/g" "$root/packaging/linux/codex-token-overlay.spec" > "$rpmtop/SPECS/codex-token-overlay.spec"
rpmbuild --define "_topdir $rpmtop" -bb "$rpmtop/SPECS/codex-token-overlay.spec"
cp "$rpmtop"/RPMS/x86_64/*.rpm "$release/codex-token-overlay-$version.x86_64.rpm"

appdir="$stage/CodexTokenOverlay.AppDir"
mkdir -p "$appdir/usr/bin"
cp -a "$publish/." "$appdir/usr/bin/"
cp "$root/packaging/linux/AppRun" "$appdir/AppRun"
sed 's/Exec=codex-token-overlay/Exec=CodexTokenOverlay/' \
  "$root/packaging/linux/CodexTokenOverlay.desktop" > "$appdir/CodexTokenOverlay.desktop"
cp "$root/assets/CodexTokenOverlay.png" "$appdir/codex-token-overlay.png"
ln -s codex-token-overlay.png "$appdir/.DirIcon"
chmod +x "$appdir/AppRun" "$appdir/usr/bin/CodexTokenOverlay"

appimagetool="${APPIMAGETOOL:-$stage/appimagetool-x86_64.AppImage}"
if [[ ! -x "$appimagetool" ]]; then
  curl -fsSL "https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage" -o "$appimagetool"
  echo "a6d71e2b6cd66f8e8d16c37ad164658985e0cf5fcaa950c90a482890cb9d13e0  $appimagetool" | sha256sum -c -
  chmod +x "$appimagetool"
fi
ARCH=x86_64 APPIMAGE_EXTRACT_AND_RUN=1 "$appimagetool" "$appdir" "$release/CodexTokenOverlay-$version-linux-x86_64.AppImage"

sha256sum "$tarball" \
  "$release/codex-token-overlay_${version}_amd64.deb" \
  "$release/codex-token-overlay-$version.x86_64.rpm" \
  "$release/CodexTokenOverlay-$version-linux-x86_64.AppImage"
