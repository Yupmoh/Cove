#!/usr/bin/env bash
set -euo pipefail

RID="${1:-}"
OUT_ARG="${2:-}"
VERSION="${COVE_VERSION:-}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
TEMP_ROOT=""

fail() {
  printf 'error: %s\n' "$1" >&2
  exit 1
}

cleanup() {
  if [ -n "$TEMP_ROOT" ] && [ -d "$TEMP_ROOT" ]; then
    rm -rf "$TEMP_ROOT"
  fi
}

trap cleanup EXIT INT TERM

[ "$RID" = "osx-arm64" ] || fail "RID must be osx-arm64"
[ -n "$OUT_ARG" ] || fail "output directory is required"
[ -n "$VERSION" ] || fail "COVE_VERSION is required"

case "$OUT_ARG" in
  /*) fail "output directory must be relative to the repository root" ;;
esac

case "/$OUT_ARG/" in
  */../*|*/./*) fail "output directory must not contain traversal components" ;;
esac

case "$VERSION" in
  *[!0-9A-Za-z._-]*|.*|-*|_*) fail "COVE_VERSION is unsafe for plist and artifact names" ;;
esac

OUTPUT_ROOT="$ROOT/$OUT_ARG"
[ "$OUTPUT_ROOT" != "$ROOT" ] || fail "output directory cannot be the repository root"

mkdir -p "$OUTPUT_ROOT"
OUTPUT_ROOT="$(cd "$OUTPUT_ROOT" && pwd -P)"
case "$OUTPUT_ROOT/" in
  "$ROOT"/*/) ;;
  *) fail "output directory resolved outside the repository root" ;;
esac

mkdir -p "$ROOT/artifacts"
TEMP_ROOT="$(mktemp -d "$ROOT/artifacts/.macos-package.XXXXXX")"
GUI_DIR="$TEMP_ROOT/gui"
ENGINE_DIR="$TEMP_ROOT/engine"
RID_STAGE="$TEMP_ROOT/result/$RID"
APP="$RID_STAGE/Cove.app"
MACOS="$APP/Contents/MacOS"
RESOURCES="$APP/Contents/Resources"
ARCHIVE_NAME="Cove-${VERSION}-${RID}.zip"
ARCHIVE_STAGE="$TEMP_ROOT/$ARCHIVE_NAME"

FRONTEND="$ROOT/src/Cove.Gui/frontend"
WWWROOT="$ROOT/src/Cove.Gui/wwwroot"
if [ ! -f "$WWWROOT/index.html" ] || [ -n "$(find "$FRONTEND/src" "$FRONTEND/index.html" "$FRONTEND/package.json" "$FRONTEND/vite.config.ts" -newer "$WWWROOT/index.html" -print -quit 2>/dev/null)" ]; then
  (cd "$FRONTEND" && npm run build)
fi

dotnet publish "$ROOT/src/Cove.Gui/Cove.Gui.csproj" \
  -c Release \
  -r "$RID" \
  --self-contained true \
  -p:PublishAot=true \
  -p:TreatWarningsAsErrors=true \
  -o "$GUI_DIR"

dotnet publish "$ROOT/src/Cove.Cli/Cove.Cli.csproj" \
  -c Release \
  -r "$RID" \
  --self-contained true \
  -p:PublishAot=true \
  -p:TreatWarningsAsErrors=true \
  -o "$ENGINE_DIR"

clang \
  -O2 \
  -fPIC \
  -shared \
  -pthread \
  -arch arm64 \
  -Wl,-no_uuid \
  -Wl,-install_name,@rpath/libcove_pty.dylib \
  -o "$ENGINE_DIR/libcove_pty.dylib" \
  "$ROOT/native/cove_pty/cove_pty.c"

mkdir -p "$MACOS" "$RESOURCES"
cp "$GUI_DIR/Cove" "$MACOS/Cove"
find "$GUI_DIR" -maxdepth 1 -type f -name '*.dylib' -exec cp {} "$MACOS"/ \;
cp "$ENGINE_DIR/cove" "$MACOS/cove-engine"

find "$ENGINE_DIR" -maxdepth 1 -type f -name '*.dylib' -exec cp {} "$MACOS"/ \;

cp -R "$GUI_DIR/wwwroot" "$RESOURCES/wwwroot"
cp -R "$GUI_DIR/assets" "$RESOURCES/assets"
cp "$GUI_DIR/appsettings.json" "$RESOURCES/appsettings.json"
cp "$GUI_DIR/ryn.json" "$RESOURCES/ryn.json"
cp -R "$ROOT/adapters" "$RESOURCES/adapters"
ln -s ../Resources/wwwroot "$MACOS/wwwroot"
ln -s ../Resources/assets "$MACOS/assets"
ln -s ../Resources/appsettings.json "$MACOS/appsettings.json"
ln -s ../Resources/ryn.json "$MACOS/ryn.json"
ln -s ../Resources/adapters "$MACOS/adapters"

[ -x "$MACOS/Cove" ] || fail "Native-AOT GUI executable is missing"
[ -x "$MACOS/cove-engine" ] || fail "Native-AOT engine executable is missing"
[ -f "$MACOS/libcove_pty.dylib" ] || fail "RID-correct PTY library is missing"
[ -f "$MACOS/libe_sqlite3.dylib" ] || fail "RID-correct SQLite library is missing"

if [ -n "${COVE_RG_BINARY:-}" ]; then
  [ -x "$COVE_RG_BINARY" ] || fail "COVE_RG_BINARY is not executable"
  RESOURCE_RG_DIR="$RESOURCES/tools/rg/$RID"
  mkdir -p "$RESOURCE_RG_DIR"
  cp "$COVE_RG_BINARY" "$RESOURCE_RG_DIR/rg"
  chmod +x "$RESOURCE_RG_DIR/rg"
  ln -s ../Resources/tools "$MACOS/tools"
fi

if [ -f "$ROOT/src/Cove.Gui/assets/app-icon.png" ]; then
  ICONSET_ROOT="$(mktemp -d "$TEMP_ROOT/icon.XXXXXX")"
  ICONSET="$ICONSET_ROOT/AppIcon.iconset"
  mkdir -p "$ICONSET"
  for size in 16 32 128 256 512; do
    sips -z "$size" "$size" "$ROOT/src/Cove.Gui/assets/app-icon.png" --out "$ICONSET/icon_${size}x${size}.png" >/dev/null
    double=$((size * 2))
    sips -z "$double" "$double" "$ROOT/src/Cove.Gui/assets/app-icon.png" --out "$ICONSET/icon_${size}x${size}@2x.png" >/dev/null
  done
  iconutil -c icns "$ICONSET" -o "$RESOURCES/AppIcon.icns"
fi

cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "https://www.apple.com/DTDs/PropertyList-1.0.dtd">
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

plutil -lint "$APP/Contents/Info.plist" >/dev/null
chmod +x "$MACOS/Cove" "$MACOS/cove-engine"

validate_macho() {
  local path="$1"
  local description
  description="$(file -b "$path")"
  case "$description" in
    *Mach-O*) ;;
    *) return 0 ;;
  esac
  local architectures
  architectures="$(lipo -archs "$path")"
  if [ "$architectures" != "arm64" ]; then
    case " $architectures " in
      *" arm64 "*)
        lipo "$path" -thin arm64 -output "$path.arm64"
        mv "$path.arm64" "$path"
        architectures="$(lipo -archs "$path")"
        ;;
    esac
  fi
  [ "$architectures" = "arm64" ] || fail "Mach-O payload has wrong architecture: $path ($architectures)"
  if otool -L "$path" | grep -Eq 'libhostfxr|libcoreclr|libclrjit'; then
    fail "Native-AOT payload depends on a .NET runtime: $path"
  fi
}

while IFS= read -r -d '' payload; do
  validate_macho "$payload"
done < <(find "$APP" -type f -print0)

while IFS= read -r payload; do
  if [ "$payload" != "$MACOS/Cove" ] && file -b "$payload" | grep -q 'Mach-O'; then
    codesign --force --sign - --timestamp=none "$payload"
  fi
done < <(find "$APP" -type f -print | LC_ALL=C sort)

codesign --force --sign - --timestamp=none "$APP"
codesign --verify --deep --strict --verbose=2 "$APP"

find "$APP" -name '.DS_Store' -delete
find "$APP" -exec touch -h -t 202001010000 {} +

(cd "$RID_STAGE" && COPYFILE_DISABLE=1 find Cove.app -print | LC_ALL=C sort | zip -X -y -q "$ARCHIVE_STAGE" -@)

CHECKSUM_STAGE="$TEMP_ROOT/$ARCHIVE_NAME.sha256"
(cd "$TEMP_ROOT" && shasum -a 256 "$ARCHIVE_NAME" > "$ARCHIVE_NAME.sha256")
(cd "$TEMP_ROOT" && shasum -a 256 -c "$ARCHIVE_NAME.sha256")

FINAL_RID="$OUTPUT_ROOT/$RID"
PREVIOUS_RID="$TEMP_ROOT/previous-$RID"
if [ -e "$FINAL_RID" ]; then
  mv "$FINAL_RID" "$PREVIOUS_RID"
fi
mv "$RID_STAGE" "$FINAL_RID"
mv -f "$ARCHIVE_STAGE" "$OUTPUT_ROOT/$ARCHIVE_NAME"
mv -f "$CHECKSUM_STAGE" "$OUTPUT_ROOT/$ARCHIVE_NAME.sha256"

printf 'built %s\n' "$FINAL_RID/Cove.app"
printf 'artifact %s\n' "$OUTPUT_ROOT/$ARCHIVE_NAME"
printf 'checksum %s\n' "$OUTPUT_ROOT/$ARCHIVE_NAME.sha256"
