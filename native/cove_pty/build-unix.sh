#!/usr/bin/env bash
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUT="${1:?usage: build-unix.sh <output-dir>}"
mkdir -p "$OUT"

UNAME="$(uname -s)"
if [ "$UNAME" = "Darwin" ]; then
  LIB="$OUT/libcove_pty.dylib"
  clang -O2 -fPIC -shared -o "$LIB" "$HERE/cove_pty.c"
  clang -O2 -o "$OUT/covptygen" "$HERE/covptygen.c"
else
  LIB="$OUT/libcove_pty.so"
  cc -O2 -fPIC -shared -o "$LIB" "$HERE/cove_pty.c" -lutil
  cc -O2 -o "$OUT/covptygen" "$HERE/covptygen.c"
fi

echo "built: $LIB"
echo "built: $OUT/covptygen"
