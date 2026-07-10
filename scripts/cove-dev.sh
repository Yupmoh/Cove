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
  local pid="$1" tries=0
  while kill -0 "$pid" 2>/dev/null && [ "$tries" -lt 50 ]; do
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

if [ "$MODE" = "restart-all" ]; then
  if pid="$(daemon_pid)"; then
    echo "stopping daemon (pid $pid) — panes respawn, harness sessions need --resume"
    kill "$pid" 2>/dev/null || true
    wait_gone "$pid"
  fi
fi

if [ ! -x "$ENGINE" ] || [ ! -x "$GUI" ]; then
  echo "binaries missing — building solution first"
  dotnet build "$ROOT/Cove.slnx" -c Debug -v:q -clp:ErrorsOnly
fi

if daemon_pid > /dev/null; then
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
