#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENGINE="$ROOT/src/Cove.Cli/bin/Debug/net10.0/cove"
GUI="$ROOT/src/Cove.Gui/bin/Debug/net10.0/Cove"

if [ "${1:-}" = "restart" ]; then
  echo "restarting daemon and gui (panes respawn, harness sessions need --resume)"
  pkill -f "bin/Debug/net10.0/Cove$" 2>/dev/null || true
  pkill -f "cove daemon run" 2>/dev/null || true
  sleep 1.5
fi

if [ ! -x "$ENGINE" ] || [ ! -x "$GUI" ]; then
  echo "binaries missing — building solution first"
  dotnet build "$ROOT/Cove.slnx" -c Debug -v:q -clp:ErrorsOnly
fi

if ! pgrep -f "cove daemon run" > /dev/null; then
  nohup "$ENGINE" daemon run --channel dev > /tmp/cove-daemon-dev.log 2>&1 &
  sleep 1
  echo "daemon started (log: /tmp/cove-daemon-dev.log)"
else
  echo "daemon already running — sessions will reattach"
fi

if pgrep -f "bin/Debug/net10.0/Cove$" > /dev/null; then
  echo "gui already running — focusing it"
  osascript -e 'tell application "System Events" to tell process "Cove" to set frontmost to true' 2>/dev/null || true
  exit 0
fi

COVE_CHANNEL=dev COVE_ENGINE="$ENGINE" nohup "$GUI" > /tmp/cove-gui-dev.log 2>&1 &
echo "cove launched (log: /tmp/cove-gui-dev.log)"
