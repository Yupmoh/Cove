# Changelog

All notable changes to Cove are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Headless-first daemon engine owning PTYs, sessions, workspaces, and SQLite state
- `cove://` control plane with framed protocol and source-generated JSON serialization
- Lossless flow-controlled PTY transport (credit/ack backpressure, ring buffer, replay-since-offset)
- Pane mosaic model (binary-tree splits, subtabs, resize, focus cycling, dock slots)
- Workspace model (workspaces, rooms, wings, collections, worktrees, run-commands)
- Agent & adapter system (manifest, install, launch-profiles, agent-definitions, skills, sigils)
- Task cards, kanban, dispatch sages, signaling, scheduling, loops, exports
- Knowledge domain (timeline, memory, notes, reviews, canvas, vault, library, captures)
- Browser pane chrome (open/navigate/back/forward/reload/close) + BrowserStore (history, annotations)
- CDP client library + browser automation verb logic (eval, navigate, click, fill, type, screenshot, snapshot, refs, cookies, console)
- Capture bundle format (CAP-N/ bundle dir + meta.json + captures.db)
- Config system (typed CoveConfig, [Setting] decoration, source-gen schema, hot-reload)
- Theme engine (6 built-in themes, WCAG contrast validation, custom themes)
- Keybinding engine (override/conflict/reserved-key, send-text, uri-command)
- Crash reporter (AppDomain + TaskScheduler handlers, path redaction, prune, zero egress)
- Telemetry subsystem (opt-in default-off, closed schema, 256-char limit)
- Notification policy engine (T0-T3 tiers, quiet-by-default, permanent dismissals)
- Control client (length-prefixed frames, req/res, sub/evt, pty/pty-in streaming)
- LSP host (subprocess, JSON-RPC, Content-Length framing, source-gen context)
- Git read model (porcelain-v2 status/diff/blame/log + stage/unstage)
- CLI branding (version banner, brand-glyph system, Spectre styled output, `--json` mode)

### Infrastructure
- Native-AOT compatible (zero reflection, source-gen everything)
- Central Package Management (Directory.Packages.props)
- `[CoveCommand]` source generator for command registration
- `[Setting]` source generator for config schema
- ZLogger source-generated structured logging
- Cross-platform PTY shim (forkpty/ConPTY, unix-socket/named-pipe seams)
