# M1 — Terminal core

M1 makes Cove a working terminal workspace: an engine-authoritative pane mosaic, lossless PTY streaming with credit flow-control, byte-exact reattach, and deterministic cold-restart. The engine owns every terminal; the GUI is a thin renderer of the engine's layout tree.

## Ownership model

The daemon (`cove daemon run`) owns all terminal state. The GUI holds no authoritative layout — it fetches the tree via `app.layoutGet`, mutates it via `app.layoutMutate`, and renders the result. This keeps a single source of truth so the TUI and CLI observe the same mosaic, and so a GUI restart replays the engine's state rather than reconstructing its own.

## Layout mosaic

`Cove.Engine.Layout.LayoutService` holds each room's layout as a binary space partition tree of `MosaicNode`s: a node is either a `split` (orientation Row/Column, a ratio, two children) or a `leaf` (a `paneId` plus its stacked `subtabs`). Structural operations — `SplitPane`, `ClosePane`, `FocusPane`, `CycleFocus`, `SetZoom`, `RenameRoom`, `AddSubtab`, `ActivateSubtab`, `ReplaceLeaf` — mutate the tree and raise `OnChanged`. The GUI renders leaves as xterm instances inside nested flex containers with draggable dividers; a zoomed leaf renders full-room.

Control routes (`app.layoutGet`, `app.layoutMutate`, `app.paneSpawn`, `app.paneWrite`, `app.paneKill`, `app.paneList`, `app.sessionState`) are dispatched by the `[CoveCommand]` source generator — adding a verb needs only the attribute, no hand-wired table.

## PTY lifecycle

`Cove.Engine.Pty.PaneRegistry` spawns a login shell per leaf through the `Cove.Platform` PTY seam. Output is captured by `PtySessionReader` into a `PtyRingBuffer` (a power-of-two byte ring). Each subscriber gets a `PtyStreamSender` that replays the ring from an offset and honours a credit window so a slow client never drops bytes and never blows memory — the lossless contract proven by `CreditBackpressureE2ETests` and `PtyStreamSenderTests`.

Working directory is tracked from the shell's OSC 7 emissions (`ShellIntegration`), so new splits inherit the focused pane's live cwd (`SpawnParams.InheritCwdFrom`).

## Crash recovery

Three independent recovery paths, each verified live:

- **Reattach** — the daemon outlives the GUI. On GUI relaunch, `GuiEngineLauncher` dials the existing control socket, and each pane's `PtyStreamSender` replays its ring byte-exact from offset 0. No respawn.
- **Cold restart** — daemon and GUI both die. On next launch `DaemonHost` loads `workspace.json`, respawns a fresh shell per persisted leaf under its original `paneId` (`PaneRegistry.RespawnAs`), and pre-seeds each fresh ring with the pane's last `scrollback.bin` snapshot so prior output shows as history above the new prompt.
- **Per-pane fault** — one shell exits while others live. `PtySessionReader` detects the exit, `PtyStreamSender.MarkChildExited` emits a stream-end frame carrying the exit code after the last data byte, and the GUI renders `[process exited: N]` in that pane only.

## Persistence

Per workspace under `$COVE_DATA_DIR/workspaces/default/`:

- `workspace.json` — the layout snapshot (rooms, trees, focus, zoom), written atomically on every `OnChanged`.
- `panes/<paneId>/session.json` — the pane's spawn descriptor (command, args, cwd) for respawn.
- `panes/<paneId>/scrollback.bin` — a periodic (15 s) ring snapshot plus a final snapshot on clean shutdown, capped, used to restore prior history on cold restart.

All JSON goes through source-generated `System.Text.Json` (`CoveJsonContext`) and the atomic temp-then-rename store; scrollback is raw bytes written the same atomic way.

## GUI surface

Command palette (⌘K), split (⌘D / ⌘⇧D), zoom (⌘Z), new terminal (⌘T), per-pane header with inline rename and an overflow menu, copy-or-SIGINT / select-all / paste keybinds, terminal settings panel (⌘,) with 9 `terminal.*` keys (fontFamily/fontSize/lineHeight/cursorStyle/cursorBlink/ligatures/scrollbackLines/padding/backgroundOpacity) live-applied to xterm and persisted to engine `config.json` via `config.get`/`config.set` routes, scrollback find bar (⌘F), and a launcher overlay (⌘L). Leaves can stack multiple terminals as subtabs with a tab strip.

## Cross-platform PTY

The PTY is behind `Cove.Platform`'s `IPtyHost` seam. On macOS and Linux a small native shim (`native/cove_pty/cove_pty.c`, built by `build-unix.sh` into `libcove_pty.dylib` / `libcove_pty.so`) provides `forkpty`; the shim is declared as a copy-to-output/copy-to-publish item in `Cove.Platform.csproj` so it flows transitively into any consuming project's `publish` output. Windows uses managed ConPTY interop — no native shim. The control endpoint is a Unix domain socket on POSIX and a named pipe on Windows, selected by `ControlEndpointFactory`.

## Running the tests and harnesses

```
dotnet build Cove.slnx -c Debug -v:q -clp:ErrorsOnly     # whole solution
dotnet test Cove.slnx                                     # all suites
dotnet test tests/Cove.Engine.Tests/Cove.Engine.Tests.csproj
```

The engine suite covers the mosaic, layout routes, ring buffer, credit flow-control end-to-end, spawn descriptors, scrollback round-trip, and search. The lossless-PTY proof harness and the renderer perf spike from M0 remain runnable and gate the byte-exact and frame-budget invariants.
