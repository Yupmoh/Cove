# Architecture Decision Records

Each record captures one decision: its context, the decision, its consequences, and the product capabilities it enables. Records are immutable once accepted; a superseding decision gets a new record that references the old one.

| # | Decision | Status |
|---|---|---|
| [0001](0001-process-topology-daemon-client-split.md) | Out-of-process daemon; GUI/TUI/CLI are symmetric clients | Accepted |
| [0002](0002-lossless-pty-transport.md) | Losslessness is an engine property: ring buffer + windowed credit | Accepted |
| [0003](0003-engine-pty-host-native-shim.md) | Portable PTY host via a native forkpty/ConPTY shim | Accepted |
| [0004](0004-trusted-local-terminal.md) | Trusted local terminal security posture | Accepted |
| [0005](0005-control-channel-framed-protocol.md) | One framed, multiplexed, portable control protocol | Accepted |
| [0006](0006-persistence-sqlite-dapper-aot.md) | Engine-owned SQLite, WAL, Dapper.AOT, single-writer | Accepted |
| [0007](0007-serialization-sourcegen-logging.md) | Source-generated JSON, command dispatch, and ZLogger | Accepted |
| [0008](0008-data-model-and-files.md) | Data directory layout + in-memory ring-buffer substrate | Accepted |
| [0009](0009-repo-build-conventions.md) | Repo and build conventions mirror Ryn | Accepted |
| [0010](0010-license-apache-2.0.md) | License: Apache-2.0 | Accepted |
