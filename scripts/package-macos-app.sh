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
cp "$ENGINE_DIR/cove" "$MACOS/cove"
cp "$ENGINE_DIR/libcove_pty.dylib" "$MACOS/libcove_pty.dylib"
rm -rf "$ENGINE_DIR"

find "$MACOS" -maxdepth 1 -name '*.pdb' -delete
find "$MACOS" -maxdepth 1 -name '*.dbg' -delete
rm -f "$MACOS/covptygen"
rm -rf "$MACOS/cove.dSYM"
chmod +x "$MACOS/Cove" "$MACOS/cove"

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
