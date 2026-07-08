# Cove

A free, open-source, AI-native terminal workspace — a permissively licensed alternative to atrium, built in C# on the Ryn framework, shipping as a small Native-AOT native-webview binary.

Cove is **headless-first**: a `cove daemon` engine owns PTYs, sessions, and SQLite state; the Ryn GUI, the TUI, and the `cove` CLI are all thin clients of one `cove://` control socket.

## Documentation

- [Architecture overview](architecture.md) — the subsystem map and how the pieces fit
- [Terminal core](terminal-core.md) — the M1 terminal engine: mosaic, PTY lifecycle, crash recovery, persistence
- [PTY flow control](pty-flow-control.md) — the credit flow-control and lossless-delivery contract
- [Control channel](control-channel.md) — the framed `cove://` protocol
- [TUI guide](tui-guide.md) — terminal-mode client, keymap, SSH, degradation matrix
- [CLI reference](cli-reference.md) — every `cove` command
- [Configuration reference](config-reference.md) — every config key, grouped by tab
- [Ryn upstream](ryn-upstream.md) — Ryn framework capabilities and their status
- [Post-1.0 backlog](post-1.0-backlog.md) — deferred features and their blockers
- [ADRs](adr/) — architecture decision records

## Community

- [Contributing](../CONTRIBUTING.md)
- [Security policy](../SECURITY.md)
- [Code of conduct](../CODE_OF_CONDUCT.md)
- [Changelog](../CHANGELOG.md)

## License

Apache-2.0
