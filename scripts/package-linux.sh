#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
version="${1:-$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$root/src/TimeReport/TimeReport.csproj" | head -n 1)}"
binary="$root/dist/linux/TimeReport"
name="TimeReport-$version-linux-x86_64"
out="$root/dist/packages"
stage="$out/$name"

if [[ ! -x "$binary" ]]; then
  echo "Missing $binary. Publish the Linux executable first." >&2
  exit 1
fi

rm -rf "$stage"
mkdir -p "$stage/bin" "$stage/share/applications" "$stage/share/icons/hicolor/256x256/apps"
install -m 0755 "$binary" "$stage/bin/TimeReport"
install -m 0644 "$root/packaging/linux/timereport.desktop" "$stage/share/applications/timereport.desktop"
install -m 0644 "$root/src/TimeReport/Assets/timereport.png" "$stage/share/icons/hicolor/256x256/apps/timereport.png"
install -m 0644 "$root/LICENSE" "$stage/LICENSE"
install -m 0644 "$root/TERMS.md" "$stage/TERMS.md"
install -m 0644 "$root/THIRD-PARTY-NOTICES.md" "$stage/THIRD-PARTY-NOTICES.md"
install -m 0644 "$root/README.md" "$stage/README.md"

tar -C "$out" -czf "$out/$name.tar.gz" "$name"
(cd "$out" && sha256sum "$name.tar.gz" > "$name.tar.gz.sha256")

if command -v dpkg-deb >/dev/null 2>&1; then
  debroot="$out/debroot"
  rm -rf "$debroot"
  mkdir -p "$debroot/DEBIAN" "$debroot/usr/bin" "$debroot/usr/share/applications" \
    "$debroot/usr/share/icons/hicolor/256x256/apps" "$debroot/usr/share/doc/timereport"
  install -m 0755 "$binary" "$debroot/usr/bin/TimeReport"
  install -m 0644 "$root/packaging/linux/timereport.desktop" "$debroot/usr/share/applications/timereport.desktop"
  install -m 0644 "$root/src/TimeReport/Assets/timereport.png" "$debroot/usr/share/icons/hicolor/256x256/apps/timereport.png"
  install -m 0644 "$root/LICENSE" "$debroot/usr/share/doc/timereport/copyright"
  install -m 0644 "$root/TERMS.md" "$debroot/usr/share/doc/timereport/TERMS.md"
  install -m 0644 "$root/THIRD-PARTY-NOTICES.md" "$debroot/usr/share/doc/timereport/THIRD-PARTY-NOTICES.md"
  cat > "$debroot/DEBIAN/control" <<EOF
Package: timereport
Version: $version
Section: office
Priority: optional
Architecture: amd64
Maintainer: Roger Bergling
Description: Local-first time reporting
 A desktop time report for Linux and Windows with projects, entry types,
 Excel export and optional Dropbox sync.
Depends: libx11-6, libxrandr2, libxi6, libxcursor1, libxext6, libxrender1,
 libfontconfig1, libice6, libsm6, libxfixes3, libxinerama1, libxcomposite1,
 libxdamage1, libxcb-randr0, libxcb-render0, libxcb-shm0, libxcb-image0,
 libxcb-util1
EOF
  dpkg-deb --build --root-owner-group "$debroot" "$out/$name.deb" >/dev/null
  (cd "$out" && sha256sum "$name.deb" > "$name.deb.sha256")
  rm -rf "$debroot"
fi

rm -rf "$stage"
echo "Created packages in $out"
ls -lh "$out"/"$name"*
