#!/usr/bin/env bash
set -euo pipefail

APP_ARG="${1:-}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
[ -n "$APP_ARG" ] || { printf 'error: Cove.app path is required\n' >&2; exit 1; }

case "$APP_ARG" in
  /*) APP="$APP_ARG" ;;
  *) APP="$ROOT/$APP_ARG" ;;
esac

GUI="$APP/Contents/MacOS/Cove"
ENGINE="$APP/Contents/MacOS/cove-engine"
[ -x "$GUI" ] || { printf 'error: packaged GUI is missing\n' >&2; exit 1; }
[ -x "$ENGINE" ] || { printf 'error: packaged engine is missing\n' >&2; exit 1; }
BUNDLE_VERSION="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$APP/Contents/Info.plist")"
[ -n "$BUNDLE_VERSION" ] || { printf 'error: packaged bundle version is missing\n' >&2; exit 1; }

SMOKE_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/cove-macos-smoke.XXXXXX")"
DATA_DIR="$SMOKE_ROOT/data"
GUI_LOG="$SMOKE_ROOT/gui.log"
GUI_PID=""
ENGINE_PID=""
PRESERVE_LOGS=0
CLEAN_ENV=(env -u COVE -u COVE_CLI_PATH -u COVE_NOOK_ID -u COVE_NOOK_TOKEN -u COVE_BAY_ID -u COVE_SHORE_ID -u COVE_TASK_ID -u COVE_TASK_RUN_ID -u COVE_HOOK_PORT)

cleanup() {
  local result=$?
  trap - EXIT INT TERM
  if [ -n "$GUI_PID" ] && kill -0 "$GUI_PID" 2>/dev/null; then
    kill "$GUI_PID" 2>/dev/null || true
    wait "$GUI_PID" 2>/dev/null || true
  fi
  if [ -f "$DATA_DIR/ipc/dev.pid" ]; then
    ENGINE_PID="$(tr -d '[:space:]' < "$DATA_DIR/ipc/dev.pid")"
  fi
  if [ -n "$ENGINE_PID" ] && kill -0 "$ENGINE_PID" 2>/dev/null; then
    kill "$ENGINE_PID" 2>/dev/null || true
    for _ in $(seq 1 50); do
      kill -0 "$ENGINE_PID" 2>/dev/null || break
      sleep 0.1
    done
  fi
  if [ "$PRESERVE_LOGS" -eq 0 ]; then
    rm -rf "$SMOKE_ROOT"
  fi
  exit "$result"
}

trap cleanup EXIT INT TERM

if [ -n "${COVE_SMOKE_GUI_PORT:-}" ]; then
  GUI_PORT="$COVE_SMOKE_GUI_PORT"
  case "$GUI_PORT" in
    *[!0-9]*|'') printf 'error: COVE_SMOKE_GUI_PORT must be numeric\n' >&2; exit 1 ;;
  esac
  if lsof -nP -iTCP:"$GUI_PORT" -sTCP:LISTEN >/dev/null 2>&1; then
    printf 'error: requested smoke GUI port %s is already in use\n' "$GUI_PORT" >&2
    exit 1
  fi
else
  GUI_PORT=$((17420 + ($$ % 20000)))
  for _ in $(seq 1 200); do
    if ! lsof -nP -iTCP:"$GUI_PORT" -sTCP:LISTEN >/dev/null 2>&1; then
      break
    fi
    GUI_PORT=$((GUI_PORT + 1))
  done
  if lsof -nP -iTCP:"$GUI_PORT" -sTCP:LISTEN >/dev/null 2>&1; then
    printf 'error: no private smoke GUI port is available\n' >&2
    exit 1
  fi
fi

mkdir -p "$DATA_DIR"
"${CLEAN_ENV[@]}" COVE_DATA_DIR="$DATA_DIR" COVE_CHANNEL=dev COVE_ENGINE="$ENGINE" COVE_GUI_PORT="$GUI_PORT" "$GUI" >"$GUI_LOG" 2>&1 &
GUI_PID=$!

READY=0
for _ in $(seq 1 150); do
  if ! kill -0 "$GUI_PID" 2>/dev/null; then
    printf 'error: packaged GUI exited before readiness\n' >&2
    PRESERVE_LOGS=1
    exit 1
  fi
  if "${CLEAN_ENV[@]}" COVE_DATA_DIR="$DATA_DIR" COVE_CHANNEL=dev "$ENGINE" daemon status >/dev/null 2>&1; then
    READY=1
    break
  fi
  sleep 0.1
done

[ "$READY" -eq 1 ] || { printf 'error: packaged engine did not become ready\n' >&2; PRESERVE_LOGS=1; exit 1; }

VERSION_OUTPUT="$("${CLEAN_ENV[@]}" COVE_DATA_DIR="$DATA_DIR" COVE_CHANNEL=dev "$ENGINE" version)"
printf '%s\n' "$VERSION_OUTPUT" | grep -q 'daemon: connected'
printf '%s\n' "$VERSION_OUTPUT" | grep -Fq "cli: v$BUNDLE_VERSION (daemon: connected)"
"${CLEAN_ENV[@]}" COVE_DATA_DIR="$DATA_DIR" COVE_CHANNEL=dev "$ENGINE" daemon status >/dev/null
if CONTEXT_OUTPUT="$("${CLEAN_ENV[@]}" COVE_DATA_DIR="$DATA_DIR" COVE_CHANNEL=dev "$ENGINE" workspace context --json 2>&1)"; then
  [ -n "$CONTEXT_OUTPUT" ]
else
  printf '%s\n' "$CONTEXT_OUTPUT" | grep -q 'workspace has no target nook'
fi
lsof -nP -a -p "$GUI_PID" -iTCP:"$GUI_PORT" -sTCP:LISTEN >/dev/null

printf 'MACOS SMOKE OK\n'
