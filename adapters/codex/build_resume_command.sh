#!/usr/bin/env bash
set -euo pipefail

SESSION_ID="${1:?Usage: build_resume_command.sh <session_id> [flags_json]}"
FLAGS_JSON="${2:-}"
ROOT="${CODEX_HOME:-$HOME/.codex}"
ADAPTER_DIR="${COVE_ADAPTER_DIR:-$(cd "$(dirname "$0")" && pwd)}"

if ! "$ADAPTER_DIR/hooks.sh" install; then
  printf '%s\n' 'cove codex resume failed to reconcile hooks' >&2
  exit 1
fi

session_known=0
if command -v sqlite3 >/dev/null 2>&1; then
  for database in "$ROOT"/state_*.sqlite; do
    [ -e "$database" ] || continue
    if [ "$(sqlite3 "$database" "SELECT 1 FROM threads WHERE id = '$(printf '%s' "$SESSION_ID" | sed "s/'/''/g")' LIMIT 1;" 2>/dev/null || true)" = "1" ]; then
      session_known=1
      break
    fi
  done
fi
if [ "$session_known" -eq 0 ] && find "$ROOT/sessions" "$ROOT/archived_sessions" -type f -name "*-${SESSION_ID}.jsonl" -print -quit 2>/dev/null | grep -q .; then
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
  local quoted_name
  quoted_name="$(printf '%q' "$name")"
  candidate="$("${SHELL:-/bin/zsh}" -lc "command -v $quoted_name" 2>/dev/null || true)"
  if [ -n "$candidate" ]; then printf '%s' "$candidate"; return 0; fi
  printf '%s' "$name"
}

json_escape() {
  local value="$1"
  value="${value//\\/\\\\}"
  value="${value//\"/\\\"}"
  printf '%s' "$value"
}


flag_true() {
  printf '%s' "$FLAGS_JSON" | grep -q "\"$1\"[[:space:]]*:[[:space:]]*true"
}

flag_string() {
  printf '%s' "$FLAGS_JSON" | sed -n "s/.*\"$1\"[[:space:]]*:[[:space:]]*\"\([^\"]*\)\".*/\1/p" | head -1
}

bin="$(resolve_binary codex /opt/homebrew/bin/codex /usr/local/bin/codex)"
args=("$bin" "--dangerously-bypass-hook-trust")
if flag_true "dangerouslySkipPermissions"; then
  args+=("--yolo")
fi
model="$(flag_string "model")"
if [ -n "$model" ] && [ "$model" != "default" ]; then
  args+=("--model" "$model")
fi
effort="$(flag_string "effort")"
if [ -n "$effort" ] && [ "$effort" != "default" ]; then
  args+=("--config" "model_reasoning_effort=\"$effort\"")
fi
if [ "$session_known" -eq 1 ]; then
  args+=("resume" "$SESSION_ID")
fi

out='{"command":['
for i in "${!args[@]}"; do
  [ "$i" -gt 0 ] && out+=','
  out+="\"$(json_escape "${args[$i]}")\""
done
out+=']}'
printf '%s\n' "$out"
