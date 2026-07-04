# 0002 — Losslessness is an engine property: ring buffer + windowed credit

Status: Accepted

## Context

The framework's event batcher silently drops chunks when its queue is full, slicing ANSI escape sequences and desyncing the emulator. The reference product's reader stops draining mid-burst, so the child wedges on a blocked write and the terminal freezes on a fast startup burst. Losslessness cannot be a property of any single hop.

## Decision

The PTY master is drained unconditionally and always into a per-session ring buffer, never gated on client, IPC, or render readiness — so the child never blocks. Delivery is a separate consumer: each attached client holds a byte offset (cursor) into the ring, and the engine sends frames only up to that client's granted credit (a fixed 256 KiB window, replenished every 128 KiB). A slow client's cursor simply lags while its bytes wait in the ring. If a client falls so far behind that the ring evicts its unread bytes, the engine emits exactly one explicit Resync (reset your grid, continue from this offset) instead of splicing a corrupted mid-stream. The high-volume output firehose bypasses the framework's lossy event path via a same-origin loopback WebSocket relay.

## Consequences

- Incoming bytes are never dropped; the only failure mode is an explicit, correct grid resync.
- Backpressure extends end-to-end to xterm.js through the credit model.
- The upstream framework event-batcher fix is filed (see `../ryn-upstream.md`, RYN-01) but does not block Cove.

## Inventory IDs

- TM-02 — Lossless, flow-controlled PTY transport (flagship)
- TM-03 — Unconditional PTY drain loop
- TM-04 — Engine-side ring buffer + replay-since-offset

## Related

Builds on 0001 (engine owns PTYs). Wire frames defined in 0005.
