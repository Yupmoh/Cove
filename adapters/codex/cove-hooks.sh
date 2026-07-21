#!/usr/bin/env bash
set -euo pipefail

if [ "${COVE:-}" != "1" ]; then
  exit 0
fi

if [ -z "${COVE_CLI_PATH:-}" ] || [ -z "${COVE_NOOK_ID:-}" ]; then
  printf '%s\n' 'cove codex hook missing COVE_CLI_PATH or COVE_NOOK_ID' >&2
  exit 0
fi

payload="$(cat)"

if ! command -v jq >/dev/null 2>&1; then
  printf '%s\n' 'cove codex hook requires jq to inspect the event envelope' >&2
  exit 0
fi

if ! printf '%s' "$payload" | jq -e . >/dev/null 2>&1; then
  printf '%s\n' 'cove codex hook received invalid JSON' >&2
  exit 0
fi

session_id="$(printf '%s' "$payload" | jq -r '.session_id // ""')"
if [ -z "$session_id" ]; then
  printf '%s\n' 'cove codex hook received no session_id' >&2
  exit 0
fi

hook_event_name="$(printf '%s' "$payload" | jq -r '.hook_event_name // ""')"
if [ -z "$hook_event_name" ]; then
  printf '%s\n' 'cove codex hook received no hook_event_name' >&2
  exit 0
fi

case "$hook_event_name" in
  SessionStart) event="session-start" ;;
  UserPromptSubmit) event="user-prompt-submit" ;;
  PreToolUse) event="pre-tool-use" ;;
  PostToolUse) event="post-tool-use" ;;
  PermissionRequest) event="permission-request" ;;
  SubagentStart) event="subagent-start" ;;
  SubagentStop) event="subagent-stop" ;;
  Stop) event="stop" ;;
  *)
    printf 'cove codex hook received unknown hook_event_name=%s\n' "$hook_event_name" >&2
    exit 0
    ;;
esac

if ! printf '%s' "$payload" | "$COVE_CLI_PATH" hook emit "$event" --adapter codex --nook-id "$COVE_NOOK_ID"; then
  printf 'cove codex hook failed event=%s session_id=%s nook_id=%s\n' "$event" "$session_id" "$COVE_NOOK_ID" >&2
fi

exit 0
