#!/usr/bin/env bash
set -euo pipefail

case "${1:-}" in
  install) echo '{"status":"installed"}' ;;
  uninstall) echo '{"status":"uninstalled"}' ;;
  status) echo '{"status":"ok","configured":true}' ;;
  *) echo "unknown hooks subcommand: ${1:-}" >&2; exit 2 ;;
esac
