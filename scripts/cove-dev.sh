#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENGINE="$ROOT/src/Cove.Cli/bin/Debug/net10.0/cove"
GUI="$ROOT/src/Cove.Gui/bin/Debug/net10.0/Cove"
PIDFILE="$HOME/.cove-dev/ipc/dev.pid"
GUI_PORT=7420

daemon_pid() {
  [ -f "$PIDFILE" ] || return 1
  local pid
  pid="$(tr -d '[:space:]' < "$PIDFILE")"
  [ -n "$pid" ] || return 1
  kill -0 "$pid" 2>/dev/null || return 1
  echo "$pid"
}

gui_pid() {
  lsof -ti "tcp:$GUI_PORT" -sTCP:LISTEN 2>/dev/null | head -1
}

wait_gone() {
  local pid="$1" tries=0 max="${2:-50}"
  while kill -0 "$pid" 2>/dev/null && [ "$tries" -lt "$max" ]; do
    sleep 0.1
    tries=$((tries + 1))
  done
}

MODE="${1:-}"

if [ "$MODE" = "restart" ] || [ "$MODE" = "restart-all" ]; then
  if pid="$(gui_pid)" && [ -n "$pid" ]; then
    echo "stopping gui (pid $pid) — daemon keeps sessions alive"
    kill "$pid" 2>/dev/null || true
    wait_gone "$pid"
  fi
fi

HANDOFF_FROM=""
if [ "$MODE" = "restart-all" ]; then
  if pid="$(daemon_pid)"; then
    echo "daemon (pid $pid) stays up — successor will take over live sessions"
    HANDOFF_FROM="$pid"
  fi
fi

FRONTEND="$ROOT/src/Cove.Gui/frontend"
WWWROOT="$ROOT/src/Cove.Gui/wwwroot"
frontend_stale() {
  [ -f "$WWWROOT/index.html" ] || return 0
  [ -n "$(find "$FRONTEND/src" "$FRONTEND/package.json" "$FRONTEND/vite.config.ts" -newer "$WWWROOT/index.html" -print -quit 2>/dev/null)" ]
}

if frontend_stale; then
  echo "frontend sources newer than bundle — rebuilding frontend"
  (cd "$FRONTEND" && npm run build)
fi

echo "building solution (incremental)"
dotnet build "$ROOT/Cove.slnx" -c Debug -v:q -clp:ErrorsOnly

if [ -n "$HANDOFF_FROM" ]; then
  echo "starting successor daemon with live handoff"
  COVE_HANDOFF=1 nohup "$ENGINE" daemon run --channel dev > /tmp/cove-daemon-dev.log 2>&1 &
  wait_gone "$HANDOFF_FROM" 200
  if kill -0 "$HANDOFF_FROM" 2>/dev/null; then
    echo "handoff did not complete — predecessor still running, keeping it"
  else
    echo "handoff complete (log: /tmp/cove-daemon-dev.log)"
  fi
elif daemon_pid > /dev/null; then
  echo "daemon already running — sessions will reattach"
else
  nohup "$ENGINE" daemon run --channel dev > /tmp/cove-daemon-dev.log 2>&1 &
  sleep 1
  echo "daemon started (log: /tmp/cove-daemon-dev.log)"
fi

if pid="$(gui_pid)" && [ -n "$pid" ]; then
  echo "gui already running — focusing it"
  osascript -e 'tell application "System Events" to tell process "Cove" to set frontmost to true' 2>/dev/null || true
  exit 0
fi

COVE_CHANNEL=dev COVE_ENGINE="$ENGINE" nohup "$GUI" > /tmp/cove-gui-dev.log 2>&1 &
echo "cove launched (log: /tmp/cove-gui-dev.log)"
