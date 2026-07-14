#!/usr/bin/env bash
set -euo pipefail

EVENT="${1:?Usage: hooks.sh <install|uninstall>}"
ADAPTER_DIR="${COVE_ADAPTER_DIR:-$(cd "$(dirname "$0")" && pwd)}"
CODEX_ROOT="${CODEX_HOME:-$HOME/.codex}"
HOOKS_FILE="$CODEX_ROOT/hooks.json"
COMMAND="COVE_HOOK_MARKER=cove-runtime-hook '$ADAPTER_DIR/cove-hooks.sh'"

if ! command -v jq >/dev/null 2>&1; then
  printf '%s\n' 'cove codex hook installation requires jq' >&2
  exit 1
fi

mkdir -p "$CODEX_ROOT"
if [ ! -f "$HOOKS_FILE" ]; then
  printf '%s\n' '{"hooks":{}}' > "$HOOKS_FILE"
fi

tmp="$(mktemp "$CODEX_ROOT/hooks.json.XXXXXX")"
trap 'rm -f "$tmp"' EXIT

case "$EVENT" in
  install)
    jq --arg command "$COMMAND" '
      def cove_owned: ((.command // "") | contains("COVE_HOOK_MARKER=cove-runtime-hook"));
      .hooks = (.hooks // {})
      | .hooks.SessionStart = (
          [(.hooks.SessionStart // [])[]
            | select(any(.hooks[]?; cove_owned) | not)]
          + [{matcher:"startup|resume",hooks:[{type:"command",command:$command,timeout:10}]}]
        )
    ' "$HOOKS_FILE" > "$tmp"
    ;;
  uninstall)
    jq --arg command "$COMMAND" '
      def cove_owned: ((.command // "") | contains("COVE_HOOK_MARKER=cove-runtime-hook"));
      .hooks = (.hooks // {})
      | .hooks.SessionStart = [(.hooks.SessionStart // [])[] | select(any(.hooks[]?; cove_owned) | not)]
    ' "$HOOKS_FILE" > "$tmp"
    ;;
  *)
    printf 'unsupported cove codex hook event: %s\n' "$EVENT" >&2
    exit 1
    ;;
esac

mv "$tmp" "$HOOKS_FILE"
trap - EXIT
