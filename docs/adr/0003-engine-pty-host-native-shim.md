# 0003 — Portable PTY host via a native forkpty/ConPTY shim

Status: Accepted

## Context

Running managed code after `fork()` is not async-signal-safe. The framework proved a native C forkpty shim and deliberately removed its managed fallback, failing loud if the shim is missing rather than being silently unsafe.

## Decision

`IPtyHost` abstracts the platform. On macOS and Linux, a native `cove-pty` C shim does `forkpty()` + `execvp()` entirely in C — no managed code runs post-fork — and ships as a native asset next to the binary, throwing `PlatformNotSupportedException` if absent. On Windows the host uses ConPTY. Resize is clamped to 1..500 rows/cols; writes are capped at 8 MiB. M0 proves the macOS path end to end; the ConPTY path and Linux forkpty are skeletoned and scheduled for CI verification.

## Consequences

- Spawn is async-signal-safe on unix.
- A missing shim fails loud, never silently unsafe.
- Windows and Linux paths are scheduled, not assumed (Linux GUI verification is RYN-05).

## Inventory IDs

- TM-01 — PTY spawn/read/write/resize/kill
- TM-77 — Cross-platform PTY & control-channel portability

## Related

Feeds the ring buffer in 0002.
