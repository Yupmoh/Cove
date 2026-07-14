#!/usr/bin/env bash
set -euo pipefail

if [ "${COVE:-}" != "1" ]; then
  exit 0
fi

payload="$(cat)"

if [ -z "${COVE_CLI_PATH:-}" ] || [ -z "${COVE_NOOK_ID:-}" ]; then
  printf '%s\n' 'cove codex hook missing COVE_CLI_PATH or COVE_NOOK_ID' >&2
  exit 0
fi

if ! command -v jq >/dev/null 2>&1; then
  printf '%s\n' 'cove codex hook requires jq to capture the session id' >&2
  exit 0
fi

session_id="$(printf '%s' "$payload" | jq -r '.session_id // ""' 2>/dev/null || printf '')"
if [ -z "$session_id" ]; then
  printf '%s\n' 'cove codex hook received no session_id' >&2
  exit 0
fi

hook_payload="$(jq -cn --arg session_id "$session_id" '{session_id:$session_id}')"
if ! printf '%s' "$hook_payload" | "$COVE_CLI_PATH" hook emit session-start --adapter codex --nook-id "$COVE_NOOK_ID"; then
  printf 'cove codex hook failed session_id=%s nook_id=%s\n' "$session_id" "$COVE_NOOK_ID" >&2
fi

exit 0
