# 0005 — One framed, multiplexed, portable control protocol

Status: Accepted

## Context

The framework provides no CLI-to-app control channel, single-instance detection, or argument forwarding. Cove builds the control plane from scratch and must keep it portable across a Unix socket and a Windows named pipe.

## Decision

`IControlEndpoint` sits behind a Unix domain socket at `~/.cove/ipc/<channel>.sock` (mode 0600) on macOS/Linux and a named pipe `\\.\pipe\cove-<channel>` on Windows. The wire is length-prefixed binary frames: a fixed 24-byte header plus payload; frame types are Request, Response, Event, Error, StreamData, Credit, Resync, and StreamEnd. StreamData payloads are raw PTY bytes with an 8-byte offset prefix (never base64); Request/Response/Event payloads are source-generated System.Text.Json. One connection multiplexes the control stream and N per-session byte streams. Dispatch is two-tier: `cli`-source verbs run in-process in the `cove` binary; `core`-source verbs are socket-routed to the daemon. The channel suffix isolates stable/beta/dev instances.

## Consequences

- One portable protocol serves GUI, TUI, and CLI clients.
- JSON is zero-reflection via a source-generated context.
- The byte-exact frame layout is the normative wire contract; M0 wires `cove version` (cli-local) and `cove pane list` (core-routed).

## Inventory IDs

- PL-29 — Control-channel transport (Unix socket / named pipe)
- PL-31 — Two-tier dispatch (cli local vs core socket)
- TM-77 — Cross-platform PTY & control-channel portability

## Related

Enables 0001 (symmetric clients). Dispatch generated per 0007.
