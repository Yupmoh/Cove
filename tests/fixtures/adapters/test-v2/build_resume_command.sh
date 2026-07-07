#!/usr/bin/env bash
set -euo pipefail

SESSION_ID="${1:?Usage: build_resume_command.sh <session_id> [flags_json]}"
FLAGS="${2:-"{}"}"

cat <<EOF
{"command":["test-v2","resume","${SESSION_ID}"]}
EOF
