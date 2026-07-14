#!/usr/bin/env bash
set -euo pipefail

normalize_path() { if [ "$1" = "/" ]; then printf '/'; else printf '%s' "${1%/}"; fi; }
CWD="$(normalize_path "${1:-$PWD}")"
ROOT="${PI_CODING_AGENT_DIR:-$HOME/.omp/agent}/sessions"

emit_empty() { printf '%s\n' '{"sessions":[]}'; }

if ! command -v jq >/dev/null 2>&1 || [ ! -d "$ROOT" ]; then
  emit_empty
  exit 0
fi

stat_mtime() { stat -f %m "$1" 2>/dev/null || stat -c %Y "$1" 2>/dev/null || printf '0'; }
iso_from_epoch() { date -u -r "$1" +%Y-%m-%dT%H:%M:%SZ 2>/dev/null || date -u -d "@$1" +%Y-%m-%dT%H:%M:%SZ 2>/dev/null || printf ''; }

tmp="$(mktemp)"
trap 'rm -f "$tmp"' EXIT

for f in "$ROOT"/*/*.jsonl; do
  [ -e "$f" ] || continue
  iso="$(iso_from_epoch "$(stat_mtime "$f")")"
  jq -sc --arg cwd "$CWD" --arg la "$iso" '
    def normalized: if . == "/" then . else sub("/+$"; "") end;
    ([.[] | select(.type == "session")][0] // {}) as $session
    | if ((($session.cwd // "") | normalized) == $cwd and ($session.id // "") != "") then
        {
          id: $session.id,
          name: ([.[] | select((.type == "title" or .type == "title_change") and (.title | type == "string")) | .title] | last // ""),
          cwd: ($session.cwd | normalized),
          lastActive: $la
        }
      else empty end
  ' "$f" 2>/dev/null >> "$tmp" || true
done

if [ -s "$tmp" ]; then
  jq -s '{sessions: .}' "$tmp"
else
  emit_empty
fi
