#!/usr/bin/env bash
set -euo pipefail

CWD="${1:-$PWD}"
ROOT="${CLAUDE_CONFIG_DIR:-$HOME/.claude}/projects"
slug="$(printf '%s' "$CWD" | sed 's/[^a-zA-Z0-9]/-/g')"
dir="$ROOT/$slug"

emit_empty() { printf '%s\n' '{"sessions":[]}'; }

if ! command -v jq >/dev/null 2>&1 || [ ! -d "$dir" ]; then
  emit_empty
  exit 0
fi

stat_mtime() { stat -f %m "$1" 2>/dev/null || stat -c %Y "$1" 2>/dev/null || printf '0'; }
iso_from_epoch() { date -u -r "$1" +%Y-%m-%dT%H:%M:%SZ 2>/dev/null || date -u -d "@$1" +%Y-%m-%dT%H:%M:%SZ 2>/dev/null || printf ''; }
extract_title() { { grep -m1 "\"$2\"" "$1" 2>/dev/null || true; } | sed -n "s/.*\"$2\":\"\\([^\"]*\\)\".*/\\1/p" | head -1; }

tmp="$(mktemp)"
trap 'rm -f "$tmp"' EXIT

for f in "$dir"/*.jsonl; do
  [ -e "$f" ] || continue
  id="$(basename "$f" .jsonl)"
  epoch="$(stat_mtime "$f")"
  iso="$(iso_from_epoch "$epoch")"
  name="$(extract_title "$f" aiTitle)"
  [ -n "$name" ] || name="$(extract_title "$f" customTitle)"
  jq -cn --arg id "$id" --arg name "$name" --arg cwd "$CWD" --arg la "$iso" \
    '{id:$id,name:$name,cwd:$cwd,lastActive:$la}' >> "$tmp"
done

if [ -s "$tmp" ]; then
  jq -s '{sessions: .}' "$tmp"
else
  emit_empty
fi
