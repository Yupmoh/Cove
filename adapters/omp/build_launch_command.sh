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
  local quoted_name
  quoted_name="$(printf '%q' "$name")"
  candidate="$("${SHELL:-/bin/zsh}" -lc "command -v $quoted_name" 2>/dev/null || true)"
  if [ -n "$candidate" ]; then printf '%s' "$candidate"; return 0; fi
  printf '%s' "$name"
}

FLAGS_JSON="${1:-}"

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
custom_command="$(flag_string "command")"
custom_command="${custom_command/#~\//$HOME/}"
if [ -n "$custom_command" ]; then
  bin="$(resolve_binary "$custom_command")"
else
  bin="$(resolve_binary omp "$HOME/.bun/bin/omp" /opt/homebrew/bin/omp /usr/local/bin/omp)"
fi
args=("$bin" "--allow-home" "--hook" "$ADAPTER_DIR/cove-hooks.ts")
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
