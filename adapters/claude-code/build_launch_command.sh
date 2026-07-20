#!/usr/bin/env bash
set -euo pipefail

FLAGS_JSON="${1:-}"
ADAPTER_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

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

flag_true() {
  printf '%s' "$FLAGS_JSON" | grep -q "\"$1\"[[:space:]]*:[[:space:]]*true"
}

flag_string() {
  printf '%s' "$FLAGS_JSON" | sed -n "s/.*\"$1\"[[:space:]]*:[[:space:]]*\"\([^\"]*\)\".*/\1/p" | head -1
}

custom_command="$(flag_string "command")"
custom_command="${custom_command/#~\//$HOME/}"
if [ -n "$custom_command" ]; then
  bin="$(resolve_binary "$custom_command")"
else
  bin="$(resolve_binary claude "$HOME/.claude/local/claude" /opt/homebrew/bin/claude /usr/local/bin/claude)"
fi

args=("$bin")
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
extra_args="$(flag_string "extraArgs")"
if [ -n "$extra_args" ]; then
  read -r -a extra_parts <<< "$extra_args"
  args+=("${extra_parts[@]}")
fi

out='{"command":['
for i in "${!args[@]}"; do
  [ "$i" -gt 0 ] && out+=','
  out+="\"${args[$i]}\""
done
out+=']}'
printf '%s\n' "$out"
