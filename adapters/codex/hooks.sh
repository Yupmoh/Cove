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
      def without_cove:
        [(. // [])[]
          | .hooks = [(.hooks // [])[] | select(cove_owned | not)]
          | select(.hooks | length > 0)];
      def reconcile($matcher): without_cove + [({hooks:[{type:"command",command:$command,timeout:10}]} + $matcher)];
      .hooks = (.hooks // {})
      | .hooks.SessionStart = (.hooks.SessionStart | reconcile({matcher:"startup|resume"}))
      | .hooks.UserPromptSubmit = (.hooks.UserPromptSubmit | reconcile({}))
      | .hooks.PreToolUse = (.hooks.PreToolUse | reconcile({matcher:"*"}))
      | .hooks.PostToolUse = (.hooks.PostToolUse | reconcile({matcher:"*"}))
      | .hooks.PermissionRequest = (.hooks.PermissionRequest | reconcile({matcher:"*"}))
      | .hooks.SubagentStart = (.hooks.SubagentStart | reconcile({matcher:"*"}))
      | .hooks.SubagentStop = (.hooks.SubagentStop | reconcile({matcher:"*"}))
      | .hooks.Stop = (.hooks.Stop | reconcile({}))
    ' "$HOOKS_FILE" > "$tmp"
    ;;
  uninstall)
    jq '
      def cove_owned: ((.command // "") | contains("COVE_HOOK_MARKER=cove-runtime-hook"));
      def without_cove:
        [(. // [])[]
          | .hooks = [(.hooks // [])[] | select(cove_owned | not)]
          | select(.hooks | length > 0)];
      .hooks = (.hooks // {})
      | .hooks.SessionStart = (.hooks.SessionStart | without_cove)
      | .hooks.UserPromptSubmit = (.hooks.UserPromptSubmit | without_cove)
      | .hooks.PreToolUse = (.hooks.PreToolUse | without_cove)
      | .hooks.PostToolUse = (.hooks.PostToolUse | without_cove)
      | .hooks.PermissionRequest = (.hooks.PermissionRequest | without_cove)
      | .hooks.SubagentStart = (.hooks.SubagentStart | without_cove)
      | .hooks.SubagentStop = (.hooks.SubagentStop | without_cove)
      | .hooks.Stop = (.hooks.Stop | without_cove)
    ' "$HOOKS_FILE" > "$tmp"
    ;;
  *)
    printf 'unsupported cove codex hook event: %s\n' "$EVENT" >&2
    exit 1
    ;;
esac

mv "$tmp" "$HOOKS_FILE"
trap - EXIT
