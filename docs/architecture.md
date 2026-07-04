# Architecture overview

Cove is headless-first. A single detached engine process (`cove daemon run`) owns the PTYs, sessions, workspaces, and persistence. The desktop GUI (a Ryn native-webview app), the terminal-mode client, and the `cove` CLI are all clients of that engine over a local control socket. This split is a day-one invariant, not a retrofit.

## Components

| Component | Role | Status |
|---|---|---|
| Engine (`cove daemon run`) | Owns PTYs, ring buffers, sessions, SQLite, control-socket server | Foundations in M0; built out in M1/M2 |
| Control plane (`cove://`) | Framed, multiplexed Unix-socket / named-pipe protocol | Wire contract settled in M0 |
| Frontend (Vite + TypeScript + xterm.js) | GUI panes in the Ryn webview | Perf proof in M0; built in M1 |
| CLI (`cove`) | Thin client + `cove daemon run` engine mode | Two-tier dispatch skeleton in M0; full tree in M4 |
| TUI (`cove tui`) | Terminal-mode client of the same engine | M9 |
| Persistence | SQLite (WAL, Dapper.AOT) + atomic flat-JSON | AOT proof in M0 |
| Adapter host | Speaks the MIT atrium adapter SDK | M3 |

## Cross-cutting standards

Latest .NET and C#; fully Native-AOT compatible with zero reflection (source-generated JSON, source-generated command dispatch, static provider init); performance-first; vertical-slice feature folders; dependency injection throughout; ZLogger for structured logging; exhaustive tests per slice.

## Decisions

The load-bearing decisions are recorded as ADRs in `adr/`. Each records why it beats the closest alternative and which product capabilities it enables.
