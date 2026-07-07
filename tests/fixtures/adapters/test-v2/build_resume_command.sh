#!/usr/bin/env bash
set -euo pipefail

session_id="${COVE_SESSION_ID:-}"
cwd="${COVE_PANE_CWD:-$PWD}"
cat <<EOF
{"command":["test-v2","resume","${session_id}","--cwd","${cwd}"]}
EOF
