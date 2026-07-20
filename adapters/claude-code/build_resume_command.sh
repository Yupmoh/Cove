#!/usr/bin/env bash
set -euo pipefail

SESSION_ID="${1:?Usage: build_resume_command.sh <session_id> [flags_json]}"
FLAGS_JSON="${2:-}"
ADAPTER_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

flag_true() {
  printf '%s' "$FLAGS_JSON" | grep -q "\"$1\"[[:space:]]*:[[:space:]]*true"
}

flag_string() {
  printf '%s' "$FLAGS_JSON" | sed -n "s/.*\"$1\"[[:space:]]*:[[:space:]]*\"\([^\"]*\)\".*/\1/p" | head -1
}

resolve_binary() {
  local name="$1"; shift
  local candidate
  candidate="$(command -v "$name" 2>/dev/null || true)"
  if [ -n "$candidate" ]; then printf '%s' "$candidate"; return 0; fi
  for candidate in "$@"; do
    if [ -n "$candidate" ] && [ -x "$candidate" ]; then printf '%s' "$candidate"; return 0; fi
  done
  local quoted_name
  quoted_name="$(printf '%q' "$name")"
  candidate="$("${SHELL:-/bin/zsh}" -lc "command -v $quoted_name" 2>/dev/null || true)"
  if [ -n "$candidate" ]; then printf '%s' "$candidate"; return 0; fi
  printf '%s' "$name"
}

bin="$(resolve_binary claude "$HOME/.claude/local/claude" /opt/homebrew/bin/claude /usr/local/bin/claude)"

args=("$bin" "--resume" "$SESSION_ID")
if [ -f "$ADAPTER_DIR/hooks-settings.json" ]; then
  args+=("--settings" "$ADAPTER_DIR/hooks-settings.json")
fi
if flag_true "dangerouslySkipPermissions"; then
  args+=("--dangerously-skip-permissions")
fi
model="$(flag_string "model")"
if [ -n "$model" ] && [ "$model" != "default" ]; then
  args+=("--model" "$model")
fi
effort="$(flag_string "effort")"
if [ -n "$effort" ] && [ "$effort" != "default" ]; then
  args+=("--effort" "$effort")
fi

out='{"command":['
for i in "${!args[@]}"; do
  [ "$i" -gt 0 ] && out+=','
  out+="\"${args[$i]}\""
done
out+=']}'
printf '%s\n' "$out"
