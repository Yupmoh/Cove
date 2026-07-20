#!/usr/bin/env bash
set -euo pipefail

SESSION_ID="${1:?Usage: build_resume_command.sh <session_id> [flags_json]}"
FLAGS_JSON="${2:-}"
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
  local quoted_name
  quoted_name="$(printf '%q' "$name")"
  candidate="$("${SHELL:-/bin/zsh}" -lc "command -v $quoted_name" 2>/dev/null || true)"
  if [ -n "$candidate" ]; then printf '%s' "$candidate"; return 0; fi
  printf '%s' "$name"
}

flag_string() {
  printf '%s' "$FLAGS_JSON" | sed -n "s/.*\"$1\"[[:space:]]*:[[:space:]]*\"\([^\"]*\)\".*/\1/p" | head -1
}

json_escape() {
  local value="$1"
  value="${value//\\/\\\\}"
  value="${value//\"/\\\"}"
  printf '%s' "$value"
}

ADAPTER_DIR="${COVE_ADAPTER_DIR:-$(cd "$(dirname "$0")" && pwd)}"
bin="$(resolve_binary omp "$HOME/.bun/bin/omp" /opt/homebrew/bin/omp /usr/local/bin/omp)"
if [ "$session_known" -eq 1 ]; then
  args=("$bin" "--resume" "$SESSION_ID" "--allow-home" "--hook" "$ADAPTER_DIR/cove-hooks.ts")
else
  args=("$bin" "--allow-home" "--hook" "$ADAPTER_DIR/cove-hooks.ts")
fi
model="$(flag_string "model")"
if [ -n "$model" ] && [ "$model" != "default" ]; then
  args+=("--model" "$model")
fi
effort="$(flag_string "effort")"
if [ -n "$effort" ] && [ "$effort" != "default" ]; then
  args+=("--thinking" "$effort")
fi

out='{"command":['
for i in "${!args[@]}"; do
  [ "$i" -gt 0 ] && out+=','
  out+="\"$(json_escape "${args[$i]}")\""
done
out+=']}'
printf '%s\n' "$out"
