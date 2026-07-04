# PTY flow control and lossless delivery

Cove never drops terminal output. Losslessness is a property of the engine's per-session
ring buffer, not of any single hop. This document is the contract every client
(GUI relay, TUI, CLI) implements.

## Three legs

### Leg A — PTY child to engine ring (drain)
A dedicated thread reads the PTY master into the session's ring buffer forever. It never
waits on a client, a subscription, credit, or render readiness. When the ring is full the
oldest bytes are evicted; incoming bytes are never dropped at the reader. Because the reader
never stops, the child never wedges on a blocked write. This is the fix for the class of
terminal freeze where a reader stops draining mid-burst.

### Leg B — engine ring to client (windowed credit)
Each subscribed client has a byte cursor into the ring and a fixed 256 KiB credit window.
The engine sends `StreamData` frames only while `sentOffset - ackOffset < 256 KiB`, in chunks
of up to 64 KiB. When the window is full the engine pauses that stream's sender. The client
sends a `Credit` frame carrying the highest offset it has consumed; the engine advances
`ackOffset` and resumes. A slow or backgrounded client simply stops sending credit: its
`ackOffset` stalls, the window fills, the engine pauses, and the bytes wait in the ring. The
client is behind in delivery position, never in capture.

### Leg C — engine relay to xterm.js (webview)
The GUI process relays Leg B to the terminal emulator over a same-origin loopback WebSocket.
The relay is itself a Leg B client of the engine. It forwards bytes to a thin JS shim that
feeds xterm.js and acknowledges consumed bytes; the relay forwards only up to the shim's
acknowledged credit and also watches the socket's buffered amount as a second backpressure
signal. When the page is backgrounded and the shim stops acking, the relay stops forwarding,
its own engine cursor lags, it stops granting engine credit, and bytes wait in the engine
ring. One elastic buffer, backpressure all the way down.

#### xterm.js ack-shim contract (implemented in TP-10)
- On `StreamData(offset, raw)`: feed `raw` to `terminal.write(raw)`; when xterm's write
  callback fires, add `raw.length` to a running consumed counter.
- Send a `Credit(consumed)` frame once the consumed counter has advanced at least 128 KiB
  since the last credit, and once more when the render queue goes idle.
- On `Resync(newBaseOffset)`: call `terminal.reset()` (RIS), set the expected next offset to
  `newBaseOffset`, and set the credit base to `newBaseOffset`.
- On `StreamEnd(finalOffset, exitCode)`: the session is finished; stop crediting.
- The shim never buffers unbounded data of its own; xterm's own write backpressure plus the
  credit window are the only buffers.

## The only failure mode: explicit resync
If a client falls so far behind that the ring evicts the next in-order byte it still needs
(`sentOffset < ring.Tail`), the engine sends exactly one `Resync` frame with
`newBaseOffset = ring.Tail`, resets its cursor there, and continues sending from that offset.
It never splices a corrupted mid-stream. A resync means "your position was lost; reset your
grid and continue from here." One resync per underrun event.

Invariant: incoming bytes always buffer at the reader; a lagging consumer either catches up
or is resynced, never spliced.

## Constants
| Name | Value |
|---|---|
| Credit window | 256 KiB (262144) |
| Credit replenish threshold | 128 KiB (131072) |
| StreamData max raw bytes | 64 KiB (65536) |
| Ring capacity (default per session) | 8 MiB (8388608) |

## Wire frames
| Frame | Direction | Payload |
|---|---|---|
| StreamData | engine to client | offset u64 LE + raw bytes |
| Credit | client to engine | ackOffset u64 LE |
| Resync | engine to client | newBaseOffset u64 LE |
| StreamEnd | engine to client | finalOffset u64 LE + exitCode i32 LE |
