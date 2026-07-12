#!/usr/bin/env bash
set -euo pipefail

RID="${1:-osx-arm64}"
OUT="${2:-artifacts}"
VERSION="${COVE_VERSION:-0.1.1-dev}"

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
APP="$OUT/Cove.app"
MACOS="$APP/Contents/MacOS"

rm -rf "$APP"
mkdir -p "$MACOS"

dotnet publish "$ROOT/src/Cove.Gui/Cove.Gui.csproj" -c Release -r "$RID" --self-contained true -o "$MACOS"

ENGINE_DIR="$(mktemp -d)"
dotnet publish "$ROOT/src/Cove.Cli/Cove.Cli.csproj" -c Release -r "$RID" -p:PublishAot=true --self-contained true -o "$ENGINE_DIR"
bash "$ROOT/native/cove_pty/build-unix.sh" "$ENGINE_DIR"
cp "$ENGINE_DIR/cove" "$MACOS/cove-engine"
cp "$ENGINE_DIR/libcove_pty.dylib" "$MACOS/libcove_pty.dylib"
rm -rf "$ENGINE_DIR"

rm -rf "$MACOS/adapters"
cp -R "$ROOT/adapters" "$MACOS/adapters"

RG_DIR="$MACOS/tools/rg/$RID"
if [ ! -x "$RG_DIR/rg" ]; then
  RG_SYS="$(command -v rg || true)"
  if [ -n "$RG_SYS" ]; then
    mkdir -p "$RG_DIR"
    cp "$RG_SYS" "$RG_DIR/rg"
    chmod +x "$RG_DIR/rg"
  else
    echo "warning: no rg binary found to vendor; packaged search requires system ripgrep" >&2
  fi
fi

find "$MACOS" -maxdepth 1 -name '*.pdb' -delete
find "$MACOS" -maxdepth 1 -name '*.dbg' -delete
rm -f "$MACOS/covptygen"
rm -rf "$MACOS/cove.dSYM"
chmod +x "$MACOS/Cove" "$MACOS/cove-engine"

if [ -f "$ROOT/src/Cove.Gui/assets/app-icon.png" ]; then
  RESOURCES="$APP/Contents/Resources"
  mkdir -p "$RESOURCES"
  ICONSET="$(mktemp -d)/AppIcon.iconset"
  mkdir -p "$ICONSET"
  for size in 16 32 128 256 512; do
    sips -z "$size" "$size" "$ROOT/src/Cove.Gui/assets/app-icon.png" --out "$ICONSET/icon_${size}x${size}.png" >/dev/null
    double=$((size * 2))
    sips -z "$double" "$double" "$ROOT/src/Cove.Gui/assets/app-icon.png" --out "$ICONSET/icon_${size}x${size}@2x.png" >/dev/null
  done
  iconutil -c icns "$ICONSET" -o "$RESOURCES/AppIcon.icns"
  rm -rf "$(dirname "$ICONSET")"
fi

cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key>
  <string>Cove</string>
  <key>CFBundleDisplayName</key>
  <string>Cove</string>
  <key>CFBundleIdentifier</key>
  <string>com.yupmoh.cove</string>
  <key>CFBundleVersion</key>
  <string>${VERSION}</string>
  <key>CFBundleShortVersionString</key>
  <string>${VERSION}</string>
  <key>CFBundleExecutable</key>
  <string>Cove</string>
  <key>CFBundleIconFile</key>
  <string>AppIcon</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>LSMinimumSystemVersion</key>
  <string>13.0</string>
  <key>NSHighResolutionCapable</key>
  <true/>
  <key>LSApplicationCategoryType</key>
  <string>public.app-category.developer-tools</string>
  <key>NSAppTransportSecurity</key>
  <dict>
    <key>NSAllowsLocalNetworking</key>
    <true/>
  </dict>
</dict>
</plist>
PLIST

echo "built $APP ($(du -sh "$APP" | cut -f1)) version ${VERSION}"
