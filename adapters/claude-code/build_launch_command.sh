#!/usr/bin/env bash
set -euo pipefail

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

bin="$(resolve_binary claude "$HOME/.claude/local/claude" /opt/homebrew/bin/claude /usr/local/bin/claude)"
printf '{"command":["%s"]}\n' "$bin"
