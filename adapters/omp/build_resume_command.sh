#!/usr/bin/env bash
set -euo pipefail

SESSION_ID="${1:?Usage: build_resume_command.sh <session_id> [flags_json]}"

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
printf '{"command":["%s","--resume","%s","--hook","%s/cove-hooks.ts"]}\n' "$bin" "$SESSION_ID" "$ADAPTER_DIR"
