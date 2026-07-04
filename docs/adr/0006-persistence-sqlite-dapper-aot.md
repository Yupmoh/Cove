# 0006 — Engine-owned SQLite, WAL, Dapper.AOT, single-writer

Status: Accepted

## Context

The framework provides no persistence. Plain Dapper is reflection-based and is banned by the zero-reflection standard. Cove needs task, timeline, and full-text-search stores under Native AOT.

## Decision

Use Microsoft.Data.Sqlite.Core + SQLitePCLRaw.bundle_e_sqlite3 (native e_sqlite3 built with FTS5) + Dapper.AOT (source-generated interceptors) under Native AOT. Register the provider statically with `Batteries_V2.Init()` (the module-initializer auto-registration can be trimmed under AOT). Use WAL, mode 0600, and atomic temp-then-rename for the flat-JSON tier. Because only the daemon touches the database files, SQLite is single-writer and cross-process WAL write contention is entirely avoided. The recorded fallback, if Dapper.AOT proves immature, is hand-written Microsoft.Data.Sqlite reader loops with source-generated row mapping; the standing choice is Dapper.AOT.

## Consequences

- Zero-reflection persistence, proven on all three RIDs in CI.
- Single-writer design sidesteps WAL contention.
- Cove owns the storage wiring end to end.

## Inventory IDs

- PL-49 — SQLite+WAL persistence tier (Dapper.AOT, source-generated)
- WS-83 — Cross-platform portable state storage

## Related

Depends on 0001 (only the daemon writes). Paths from 0008.
