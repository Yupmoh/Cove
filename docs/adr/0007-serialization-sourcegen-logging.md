# 0007 — Source-generated JSON, command dispatch, and ZLogger

Status: Accepted

## Context

The zero-reflection standard forbids reflection-based serialization, dispatch, and logging. Every serialization, every command route, and every log message must be resolved at compile time.

## Decision

Serialize every DTO through a source-generated System.Text.Json `JsonSerializerContext`. A single Roslyn incremental generator, `Cove.SourceGen`, turns `[CoveCommand]`-attributed static methods into both dispatch surfaces: the in-process `cli` table in the `cove` binary and the `core` handler registry in the daemon — so adding a command needs only the attribute, with no hand-wiring. Standardize logging on ZLogger with source-generated `[ZLoggerMessage]` methods; the engine logs to `~/.cove/logs/`. A disabled hot-path log call allocates zero managed bytes.

## Consequences

- No runtime reflection anywhere in serialization, dispatch, or logging.
- New verbs are attribute-only.
- Hot-path logging is zero-allocation, proven by an allocation test.

## Inventory IDs

- PL-12 — Full CLI tree via source-generated dispatcher
- PL-31 — Two-tier dispatch (cli local vs core socket)

## Related

Emits the dispatch for 0005. Enforced by the AOT build gate.
