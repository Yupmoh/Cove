# Cove documentation

Cove is a free, open-source, AI-native terminal workspace: a headless engine that owns your terminals, sessions, and workspaces, with a native-webview GUI and a full terminal-mode client as symmetric clients over one local control plane.

Created & maintained by Moh (github.com/Yupmoh).

## Reading order

1. `architecture.md` — the subsystem map and how the pieces fit.
2. `terminal-core.md` — the M1 terminal engine: mosaic, PTY lifecycle, crash recovery, persistence, cross-platform PTY.
3. `pty-flow-control.md` — the credit flow-control and lossless-delivery contract.
4. `adr/` — the architecture decision records. Start at `adr/README.md`.
5. `ryn-upstream.md` — the Ryn framework capabilities Cove depends on and their status.

Per-subsystem reference docs (protocol, persistence, adapters, CLI, TUI) land with their owning milestones and are linked from `architecture.md` as they arrive.
