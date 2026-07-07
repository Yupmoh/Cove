# M2 Workspace Model — Subsystem Docs

## Persistence tiers

**Tier 1 — flat JSON, atomic, git-snapshotted** (`AtomicJsonStore`):
- `state.json` — live app/session state (open/focused workspaces, geometry, clean-shutdown marker, auto-restore setting)
- `workspaces/{id}/workspace.json` — per-workspace layout snapshot (rooms, mosaic trees, pane records)
- `workspaces/{id}/panes/{paneId}/session.json` — per-pane session descriptor (command, args, cwd)
- `workspaces/{id}/panes/{paneId}/scrollback.bin` — per-pane scrollback ring snapshot (15s heartbeat)
- `run-commands/{id}.json` — run-command definitions
- `run-commands/{id}.json` — scoped to `COVE_WORKSPACE_ID`

All Tier-1 writes go through `AtomicJsonStore.Write` (temp-file-then-rename, fsync, chmod 0600, .bak fallback) or `WriteBytes` for binary.

**Tier 2 — SQLite + Dapper.AOT** for queryable domains (tasks, timeline, memory, transcripts — owned by later milestones).

## Restart cases

**Case A — client reconnect (GUI reload / TUI attach):** sessions never died (daemon kept them alive and losslessly draining). Client re-attaches over the control channel and replays each pane's ring buffer via `pane.subscribe` with `SinceOffset`. Zero session loss (WS-79).

**Case B — daemon restart (reboot / crash / quit+reopen):** full restoration sequence (WS-69):
1. `RestorationService.LoadState()` reads `state.json` for open/focused workspaces + geometry + clean-shutdown marker
2. `WorkspacePersistence.Load()` loads each workspace's `workspace.json` + session descriptors
3. Rebuilds mosaic trees per room
4. Materializes panes type-specifically: terminal panes relaunch fresh via `RespawnAs` with prior scrollback replayed (WS-74); agent panes enter the resume machine (T14); non-adapter in-flight commands are NOT resurrected (WS-78)
5. `RestoreProgressEvent` broadcast at each step (WS-70 launching screen)

**Clean-shutdown marker:** `state.json` carries `cleanShutdown: true` on graceful exit (written by `RestorationService.MarkCleanShutdown()`), cleared at launch (`MarkLaunching()`). Absence → unclean → RestoreChooser (WS-71) unless `autoRestoreOnLaunch` (WS-72).

## Worktree lifecycle

- `IGitRunner` (T10) wraps `git worktree add/remove/list --porcelain`
- Create: `git worktree add` + bind child workspace (`isWorktree`, `parentWorkspaceId`, `worktreeBranch`, inherit `collectionId`)
- Remove: `git worktree remove` + forget; active-worktree removal → parent focus (WS-46)
- Orphans: on-disk worktrees unbound to any Cove workspace; rescan on window-focus (WS-40)
- Real-time watching (WS-43): debounced `FileSystemWatcher` on git common dir (root `HEAD`+`packed-refs` + recursive `refs/`+`worktrees/`, `.lock` filtered)
- Path normalization: `PathRealpath.Normalize` resolves symlinks (macOS `/var`→`/private/var`) so bound paths match porcelain realpaths

## Run-commands

- Definitions persist to `run-commands/{id}.json`, scoped to workspace
- `RunCommandSession` wraps a daemon-owned kept-alive PTY (no pane binding) + ring buffer + lifecycle (`not-launched`/`running`/`stopped`)
- Idempotent start (WS-51): already-running → report + exit 0, no double-start
- Restart-in-place (WS-53): dead shell revives, scrollback preserved
- Inheritance (WS-52): `list` shows own + parent worktree commands
- Verbs: `workspace-command.create/edit/delete/list/status/start/stop/restart/logs/clear`

## Snapshot / Vault

- Two git repos via `IGitRunner`: `~/.cove/.git` (config/state auto-commit) + `snapshots/.git` (point-in-time captures)
- Commit triggers (WS-61): interval/shutdown/pre-update (skippable on hash match), pre-restore/manual/event (always commit)
- Dedup (WS-62): canonical content-hash; skippable triggers no-op on match
- Tiered retention: 24 most-recent within 24h + 1 per day for 6 days beyond, pinned exempt
- Restore (WS-63): pre-restore safety commit first, then `git checkout <commitId> -- .`
- Secret exclusion (WS-68): paths matching `secret`/`.env`/`credential`/`token`/`cookies`/`.key`/`.pem` filtered at capture time
- Verbs: `snapshot.take/list/restore/pin/prune`

## Control-plane verbs

- `workspace.*` — create/switch/list/delete/hide/reorder/set-icon/set-accent/copy-id/move-room
- `room.*` — create/switch/close/pin/rename/list/move-to-wing
- `wing.*` — list/create/rename/remove/switch/move-room/reorder/set-icon
- `collection.*` — list/create/rename/remove/switch/move-workspace
- `worktree.*` — create/list/remove/forget/adopt/orphans/prune
- `workspace-command.*` — create/edit/delete/list/status/start/stop/restart/logs/clear
- `restore.*` — chooser/confirm/undo
- `snapshot.*` — take/list/restore/pin/prune
- `state.*` — namespaced key-value bus get/set

## What does NOT resume (WS-78)

Explicit non-restored set — these are NOT resurrected on restart:

1. **Unsaved editor buffers** — flushed to disk on close, no separate journal
2. **In-flight HTTP/long-running foreground non-adapter commands** — killed on process death; last command kept for Replay
3. **Browser cookies across a webview-engine upgrade** — not portable
4. **Reaped adapter sessions** — fresh-launched (not resumed); launcher overrides reapplied

This contract is asserted by `NonRestoredContractTests`.
