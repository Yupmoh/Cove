#!/usr/bin/env bash
set -euo pipefail

normalize_path() { if [ "$1" = "/" ]; then printf '/'; else printf '%s' "${1%/}"; fi; }
CWD="$(normalize_path "${1:-$PWD}")"
ROOT="${CODEX_HOME:-$HOME/.codex}"

emit_empty() { printf '%s\n' '{"sessions":[]}'; }

if ! command -v jq >/dev/null 2>&1 || ! command -v sqlite3 >/dev/null 2>&1 || [ ! -d "$ROOT" ]; then
  emit_empty
  exit 0
fi

tmp="$(mktemp)"
trap 'rm -f "$tmp"' EXIT

for database in "$ROOT"/state_*.sqlite; do
  [ -e "$database" ] || continue
  sqlite3 -json "$database" "SELECT id, COALESCE(title, '') AS name, cwd, updated_at AS updatedAt, strftime('%Y-%m-%dT%H:%M:%SZ', updated_at, 'unixepoch') AS lastActive FROM threads WHERE archived = 0;" 2>/dev/null \
    | jq -c '.[]' >> "$tmp" 2>/dev/null || true
done

if [ ! -s "$tmp" ]; then
  emit_empty
  exit 0
fi

jq -s --arg cwd "$CWD" '
  def normalized: if . == "/" then . else sub("/+$"; "") end;
  {
    sessions: [
      sort_by(.updatedAt)
      | group_by(.id)[]
      | last
      | select(((.cwd // "") | normalized) == $cwd)
      | {id, name, cwd: (.cwd | normalized), lastActive}
    ]
  }
' "$tmp"
