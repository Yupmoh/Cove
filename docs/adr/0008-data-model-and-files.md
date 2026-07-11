# 0008 — Data directory layout + in-memory ring-buffer substrate

Status: Accepted

## Context

M0 must establish where Cove keeps state and the substrate the lossless transport replays from, before any feature milestone writes to disk.

## Decision

`Cove.Platform` resolves `~/.cove/` on macOS/Linux and `%USERPROFILE%\.cove\` on Windows, plus the `~/.cove-beta` / `~/.cove-dev` channel roots, honoring `COVE_DATA_DIR`. It scaffolds the canonical tree (`ipc/ logs/ bays/ themes/ bin/ cache/` and more) and writes an internal `.gitignore` that splits versioned intent from regenerable cache (`ipc/`, `cache/`, `bin/`, `logs/`, `*.db*` ignored). Each PTY session gets a bounded in-memory ring buffer with a monotonic total offset and per-client cursors — the substrate for replay-since-offset. Persisting scrollback to disk is a later milestone; M0 proves the in-memory replay contract.

## Consequences

- Portable path resolution from day one.
- The data directory is safe to place under version control (cache excluded).
- The ring-buffer replay contract is proven in-memory in M0.

## Inventory IDs

- PL-38 — `~/.cove` root + channel roots + `COVE_DATA_DIR`
- PL-39 — Full data-directory layout map + `.gitignore` split
- TM-04 — Engine-side ring buffer + replay-since-offset

## Related

Feeds 0002 (transport replay) and 0006 (SQLite file locations).
