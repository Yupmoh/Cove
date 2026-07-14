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
The relay is itself a Leg B client of the engine. Every browser binary message preserves the
engine frame's absolute offset as `offset u64 LE + raw bytes`. The browser requires that offset
to equal its expected next offset before writing the bytes to xterm. Exact duplicate frames are
discarded; gaps and overlaps close the subscription and reconnect from the last accepted offset.

The relay begins with `{ "t": "base", "off": baseOffset, "head": replayUntilOffset }`. The
browser suppresses xterm-generated input until the write callback reaches `head`, then enters
live mode. Keyboard input never ends replay and never injects terminal mode changes.

Hidden terminals close their WebSocket after retaining the last accepted offset. The engine
continues draining the PTY into its ring without spending browser render time or holding an
off-screen renderer. Showing the terminal creates a new subscription from that offset.

#### xterm.js ack-shim contract
- On browser data `(offset, raw)`, require `offset == expectedOffset`, advance the expected
  offset, and feed `raw` to `terminal.write(raw)`.
- When xterm's write callback fires, publish that frame's end offset as consumed.
- Send `Credit(consumed)` after at least 128 KiB of progress and once more when the render queue
  goes idle.
- On `Resync(newBaseOffset)`, call `terminal.reset()`, set both expected and consumed offsets to
  `newBaseOffset`, and leave replay mode.
- On `StreamEnd(finalOffset, exitCode)`, stop crediting and render the process exit.
- The shim never buffers unbounded data of its own; xterm's write queue and the credit window are
  the only browser-side buffers.

## The only loss mode: explicit resync
If a client falls so far behind that the ring evicts the next in-order byte it still needs
(`sentOffset < ring.Tail`), the engine sends one `Resync` frame with
`newBaseOffset = ring.Tail`, resets its cursor there, and continues from that offset. The
browser resets its terminal before accepting the new base, so stale and post-loss bytes are
never silently combined.

The ring tail is a byte boundary, not a serialized terminal parser checkpoint. A deep underrun
therefore makes exact screen reconstruction impossible and is surfaced as a reset. Under normal
hide/show and reconnect flow the client resumes from its preserved offset before the default
8 MiB ring is overtaken.

Invariant: incoming bytes always buffer at the reader; a lagging consumer either catches up or
receives an explicit reset with a new absolute base.

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
