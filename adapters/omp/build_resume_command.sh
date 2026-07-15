#!/usr/bin/env bash
set -euo pipefail

SESSION_ID="${1:?Usage: build_resume_command.sh <session_id> [flags_json]}"
ROOT="${PI_CODING_AGENT_DIR:-$HOME/.omp/agent}/sessions"

session_known=0
if find "$ROOT" -type f -name "*_${SESSION_ID}.jsonl" -print -quit 2>/dev/null | grep -q .; then
  session_known=1
fi

resolve_binary() {
  local name="$1"; shift
  local candidate
  candidate="$(command -v "$name" 2>/dev/null || true)"
  if [ -n "$candidate" ]; then printf '%s' "$candidate"; return 0; fi
  for candidate in "$@"; do
    if [ -n "$candidate" ] && [ -x "$candidate" ]; then printf '%s' "$candidate"; return 0; fi
  done
  candidate="$("${SHELL:-/bin/zsh}" -lc "command -v $name" 2>/dev/null || true)"
  if [ -n "$candidate" ]; then printf '%s' "$candidate"; return 0; fi
  printf '%s' "$name"
}

ADAPTER_DIR="${COVE_ADAPTER_DIR:-$(cd "$(dirname "$0")" && pwd)}"
bin="$(resolve_binary omp "$HOME/.bun/bin/omp" /opt/homebrew/bin/omp /usr/local/bin/omp)"
if [ "$session_known" -eq 1 ]; then
  printf '{"command":["%s","--resume","%s","--hook","%s/cove-hooks.ts"]}\n' "$bin" "$SESSION_ID" "$ADAPTER_DIR"
else
  printf '{"command":["%s","--hook","%s/cove-hooks.ts"]}\n' "$bin" "$ADAPTER_DIR"
fi
