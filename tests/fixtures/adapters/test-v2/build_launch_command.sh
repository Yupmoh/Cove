#!/usr/bin/env bash
set -euo pipefail

cwd="${COVE_NOOK_CWD:-$PWD}"
cat <<EOF
{"command":["test-v2","--no-update","--cwd","${cwd}"]}
EOF
