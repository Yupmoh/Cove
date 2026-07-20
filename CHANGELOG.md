# Changelog

All notable changes to Cove are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed
- New Windows Claude Code, OMP, and Codex sessions now launch detected native executables instead of unusable `/c/...` paths returned by Git Bash
- Windows session resume now builds native Claude Code, OMP, and Codex command lines from detected executable paths instead of relying on Git Bash paths and script-only dependencies
- Windows Claude Code and OMP session discovery now scans native JSONL histories directly instead of silently returning no sessions when Git Bash lacks `jq`
- Windows adapter discovery now applies executable extensions when scanning `PATH`, allowing installed `.exe`, `.cmd`, and `.bat` agent harnesses to appear in the launcher
- Terminal relay now preserves absolute byte offsets, uses an explicit replay boundary, rejects stream gaps and stale generations, pauses hidden subscriptions, and reconnects from the last accepted offset
- Terminal rendering now defaults to Canvas, coordinates resize through one visibility-aware observer, and no longer ends replay or changes alternate-screen mode on the first keypress
- Removed temporary keyboard payload diagnostics from GUI logs
- Claude, Codex, and OMP session discovery now scans pre-existing native histories without Cove metadata, resolves the active layout's project path, upgrades legacy bundled scanner scripts, preserves distinct sessions with identical labels, and merges Codex threads across state databases; Codex and OMP capture session IDs so daemon restarts resume the correct conversation
- Restored and resynchronized terminals now follow replay to the live prompt without forcing ordinary reconnects away from the user's scroll position, and recover active alternate-screen, mouse, and keyboard modes after their original control sequences leave the PTY ring
- Terminal history persistence now stores atomic serialized xterm checkpoints with their exact raw PTY tails, restores original dimensions and private modes before replay, resynchronizes from checkpoints after ring overflow, and retains the full 8 MiB raw fallback instead of truncating daemon-restart history to 256 KiB

## [0.4.0] - 2026-07-13

### Added
- Bay / Shore / Nook workspace model across the app, protocol, storage, and adapter SDK
- In-shore launcher with harness cards, tool tiles, session resume dropdown, and recents
- Session resume scoped to the working directory, with user-given session names as labels
- Restore-on-boot: sessions, bypass-permissions choice, and hook wiring survive daemon restarts and reboots
- Bundled adapters (claude-code, codex, omp) seeded into the data dir at first boot
- Tools tab: adapter cards with live binary detection, re-scan, add-from-folder, guarded remove, retention chip
- Four-step first-run wizard (harness detection, permissions, appearance, sound)
- Editor panes wired to LSP (diagnostics, hover, definitions) with config-registrable servers
- Real updater flow with ECDSA-signed release assets and fail-closed verification
- Agent state chimes with a settings toggle and preview
- Settings dialog redesigned with a vertical navigation column
- Windows ConPTY terminal host (experimental) and Windows named-pipe control plane
- Comprehensive structured logging with a COVE_LOG_LEVEL switch and a GUI log file

### Fixed
- Hook port publication race between competing daemon instances
- Second daemon instance on Windows can no longer clobber a live daemon's state
- Workspace sidebar scrolling, pane title persistence, bay emoji icons

### Added (pre-0.4 foundation)
- Headless-first daemon engine owning PTYs, sessions, bays, and SQLite state
- `cove://` control plane with framed protocol and source-generated JSON serialization
- Lossless flow-controlled PTY transport (credit/ack backpressure, ring buffer, replay-since-offset)
- Nook mosaic model (binary-tree splits, subtabs, resize, focus cycling, dock slots)
- Bay model (bays, shores, wings, collections, worktrees, run-commands)
- Agent & adapter system (manifest, install, launch-profiles, agent-definitions, skills, sigils)
- Task cards, kanban, dispatch sagas, signaling, scheduling, loops, exports
- Knowledge domain (timeline, memory, notes, reviews, canvas, vault, library, captures)
- Browser nook chrome (open/navigate/back/forward/reload/close) + BrowserStore (history, annotations)
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
