# Control channel

Cove's daemon and its clients (GUI, TUI, CLI) speak one binary framed protocol over a single `cove://` control socket. The daemon owns every terminal; clients are thin renderers that drive it through request/response verbs and subscribe to streaming output.

## Transport

The control endpoint is a Unix domain socket on POSIX and a named pipe on Windows, selected by `ControlEndpointFactory` behind the `IControlEndpoint` seam. Each client opens one connection, performs a `hello` handshake, then exchanges frames. The daemon serves multiple connections concurrently and outlives any single client — a GUI restart reconnects and replays state rather than respawning it.

## Frame wire format

Every frame is a 24-byte header followed by a payload of exactly `Length` bytes.

```
 offset  size  field
 0       4     magic            "COVE" (0x43 0x4F 0x56 0x45)
 4       1     wire version     1
 5       1     frame type       see FrameType
 6       2     reserved         must be 0x00 0x00
 8       8     stream id        u64 LE — 0 for control frames, >0 for stream frames
 16      4     seq              u32 LE — stream sequence number (0 for control)
 20      4     length           u32 LE — payload byte count (≤ MaxFramePayload)
```

Constants (`ProtocolConstants`): `HeaderSize = 24`, `MaxFramePayload = 16 MiB`, `WireVersion = 1`, `SemanticProtocolVersion = 1`. A frame whose magic, version, reserved bytes, stream-id/role consistency, or length is invalid is rejected with a matching error string (`malformed_frame`, `unsupported_version`, `unknown_frame_type`, `frame_too_large`).

## Frame types

| Type | Byte | StreamId | Direction | Payload |
|---|---|---|---|---|
| `Request` | 0x01 | 0 | both | JSON `ControlRequest` |
| `Response` | 0x02 | 0 | both | JSON `ControlResponse` |
| `Event` | 0x03 | 0 | daemon→client | JSON `ControlEvent` |
| `Error` | 0x04 | 0 | both | JSON `ControlErrorFrame` |
| `StreamData` | 0x05 | >0 | daemon→client | raw PTY bytes (base64-free) |
| `Credit` | 0x06 | >0 | client→daemon | JSON `CreditFrame` |
| `Resync` | 0x07 | >0 | client→daemon | JSON `ResyncFrame` |
| `StreamEnd` | 0x08 | >0 | daemon→client | JSON `StreamEndFrame` |

Control frames (`Request`/`Response`/`Event`/`Error`) must carry `StreamId = 0`; stream frames must carry `StreamId > 0`. The codec enforces this — a mismatch is a `malformed_frame`.

## Handshake

A client's first frame must be a `Request` to `cove://commands/hello` with `HelloParams` (`ProtocolVersion`, `ClientKind`, `ClientVersion`, `Channel`). The daemon replies `HelloResult` (`ProtocolVersion`, `EngineVersion`, `EnginePid`, `Channel`) and marks the connection ready. No other command is accepted before `hello` succeeds. `ReadinessTimeoutMs = 5 s`.

## Request/response

`ControlRequest` is `{ Id, Uri, Params?, Source?, CallerNookId? }`. `Uri` is the verb (`cove://commands/nook.spawn`). The daemon routes it via the `[CoveCommand]` source generator — each verb is a method attributed `[CoveCommand("cove://commands/<verb>")]`, compiled into `CoveCommandRegistry.Handlers`, so adding a verb needs no hand-wired dispatch table. The response is `ControlResponse` `{ Id, Ok, Data?, Error? }` correlated by `Id`. A 30 s timeout applies to control requests.

All JSON is source-generated through `CoveJsonContext` (`JsonSerializerContext`) — no reflection, AOT-safe.

## PTY streams

A `nook.spawn` response carries a `StreamId > 0`. The daemon then streams that nook's PTY output as `StreamData` frames keyed by `StreamId`, replaying the ring from the subscriber's offset so a reconnect loses no bytes. Backpressure is credit-based:

- Each `StreamData` consumes the client's flow window (`FlowWindow = 256 KiB`).
- When the client's remaining credit drops to `CreditReplenishThreshold = 128 KiB`, it sends a `Credit` frame toping the daemon back up. A slow client never drops bytes and never forces the daemon to buffer unboundedly.
- A client may send `Resync` to re-anchor at a different ring offset (e.g. after truncation).
- When the nook's process exits, the daemon sends `StreamEnd` carrying the exit code after the final data byte. The GUI renders the exit state in that nook only; siblings stay alive.

The ring (`PtyRingBuffer`, power-of-two byte ring) is the lossless store backing replay; the credit window is the live transport contract. Both are proven by `CreditBackpressureE2ETests` and `PtyStreamSenderTests`.

## Events

The daemon pushes unsolicited `Event` frames (channel + JSON payload) for state changes the client did not request — layout mutations from another client, cwd changes via OSC 7, nook exits, etc. Clients subscribe to channels they care about; events carry the channel name so a client can filter.

## Error model

`ControlError` is `{ Code, Message }` on a failed `Response`. Stream-level errors use `ControlErrorFrame` with an optional `StreamId`. Codes are stable strings: `not_found`, `invalid_params`, `not_ready`, `handler_error`, `malformed_frame`, etc. A missing-data early return logs a `Warning` with context before responding, never silently swallowing.

## CLI parity

The `cove` CLI speaks the same framed protocol over the socket. Verbs surfaced as CLI subcommands (`cove config get/set`, `cove nook list`, ...) map to the same `cove://commands/*` URIs, so the CLI, GUI, and TUI observe identical engine state.
