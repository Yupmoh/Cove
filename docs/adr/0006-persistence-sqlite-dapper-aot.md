# 0006 — Engine-owned SQLite, WAL, Dapper.AOT, single-writer

Status: Accepted — DP-12: Dapper.AOT fallback adopted (owner-ratified 2026-07-04)

## Context

The framework provides no persistence. Plain Dapper is reflection-based and is banned by the zero-reflection standard. Cove needs task, timeline, and full-text-search stores under Native AOT.

## Decision

Use Microsoft.Data.Sqlite.Core + SQLitePCLRaw.bundle_e_sqlite3 (native e_sqlite3 built with FTS5) + Dapper.AOT (source-generated interceptors) under Native AOT. Register the provider statically with `Batteries_V2.Init()` (the module-initializer auto-registration can be trimmed under AOT). Use WAL, mode 0600, and atomic temp-then-rename for the flat-JSON tier. Because only the daemon touches the database files, SQLite is single-writer and cross-process WAL write contention is entirely avoided. The recorded fallback, if Dapper.AOT proves immature, is hand-written Microsoft.Data.Sqlite reader loops with source-generated row mapping; the standing choice is Dapper.AOT.

DP-12 update: under warnings-as-errors ILC, vanilla `Dapper.dll` (which `Dapper.AOT` still references) emits IL2104/IL3053 because its `SqlMapper` is reflection-based, and `Dapper.AOT 1.0.52` does not expose the Dapper API transitively. The recorded fallback was therefore adopted for the AOT-published path: `Dapper.dll` is kept out of the ILC closure and the store uses the hand-written zero-reflection `Microsoft.Data.Sqlite` reader with source-generated row mapping (`ProbeStoreFallback`). The AOT probe publishes clean and reports `PROBE OK mode=fallback journal_mode=wal fts5_hits=1` on all three RIDs; the JIT test suite continues to prove both store implementations (5/5). Criterion 6 is satisfied in this amended form: an AOT binary opens WAL, runs insert/select, and answers an FTS5 query with zero reflection warnings — via the source-generated fallback command rather than Dapper.AOT.

## Consequences

- Zero-reflection persistence, proven on all three RIDs in CI.
- Single-writer design sidesteps WAL contention.
- Cove owns the storage wiring end to end.

## Inventory IDs

- PL-49 — SQLite+WAL persistence tier (Dapper.AOT, source-generated)
- WS-83 — Cross-platform portable state storage

## Related

Depends on 0001 (only the daemon writes). Paths from 0008.
