# 0001 — Out-of-process daemon; GUI/TUI/CLI are symmetric clients

Status: Accepted

## Context

A native-webview terminal session dies when its webview/window tears down, and the framework's dev loop relaunches the whole app on every backend edit, losing all state. An engine living inside the GUI process could never let a session survive a client detaching, and every backend iteration would kill every terminal.

## Decision

Ship two executables: `cove` (the CLI, the `cove daemon run` engine mode, and later `cove tui`) and `Cove.Gui` (the webview desktop app). The engine runs as its own detached process. The GUI, TUI, and CLI are symmetric clients of that engine over the `cove://` control socket. On launch, whoever comes first connects to the channel endpoint or spawns the daemon detached, then connects; the socket bind is the single-instance gate. The app is one main window with nooks as DOM; popouts and settings are in-window overlays, never secondary native windows (which sidesteps the macOS secondary-window first-paint bug).

## Consequences

- Sessions survive a client detach and reattach by replaying the engine ring buffer.
- Backend iteration restarts only the GUI shell; PTYs keep running in the daemon.
- Cove builds its own control-socket server; the framework provides none.

## Inventory IDs

- PL-85 — Headless-first daemon/client split
- TM-05 — Daemon/client session ownership decoupled from any frontend
- WS-80 — Bay/session survival across restarts
- AG-138 — Adapter/agent session continuity across client detach

## Related

Enabled by 0005 (control channel). Enables 0002 (ring-buffer replay), 0006 (single-writer SQLite).
