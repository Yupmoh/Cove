#!/usr/bin/env bash
set -euo pipefail

SESSION_ID="${1:?Usage: build_resume_command.sh <session_id> [flags_json]}"
FLAGS="${2:-"{}"}"

if printf '%s' "$FLAGS" | grep -q '"dangerouslySkipPermissions"[[:space:]]*:[[:space:]]*true'; then
  cat <<EOF
{"command":["test-v2","resume","${SESSION_ID}","--dangerously-skip-permissions"]}
EOF
else
  cat <<EOF
{"command":["test-v2","resume","${SESSION_ID}"]}
EOF
fi
