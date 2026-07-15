# Terminal reliability

## Decision

Cove uses xterm.js as its terminal emulator and renderer. The terminal failures investigated on 2026-07-15 were caused by Cove's stream lifecycle and persistence behavior rather than by xterm's rendering core. Replacing xterm with libghostty would trade a proven renderer for an unstable C API and require Cove to build font rasterization, selection, search, IME, accessibility, and Canvas or WebGL rendering.

The reliability work therefore keeps xterm and makes restoration an explicit, offset-based contract between the daemon and GUI.

## Evidence

xterm.js is the terminal component used by VS Code, JupyterLab, Eclipse Theia, Tabby, Hyper, ttyd, Proxmox VE, Portainer, Azure Cloud Shell, and Teleport. Its official packages include the browser renderer, a Node headless state machine, fitting, search, WebGL, clipboard, image, and serialization addons.

VS Code keeps PTY processes in a separate host, feeds their output into `@xterm/headless`, and uses `@xterm/addon-serialize` to produce replayable VT state. The serializer captures the normal buffer, scrollback, alternate buffer, cursor, styles, and terminal modes. Its API recommends restoring into the original terminal dimensions before resizing.

Cove cannot adopt VS Code's headless process directly without shipping a Node runtime, which conflicts with Cove's small Native-AOT binary. Cove can use the same checkpoint-and-tail model with its existing browser xterm and C# daemon.

## Observed Cove failure modes

The live daemon retains 8 MiB of raw PTY output per nook. `NookRegistry.SnapshotRing` previously capped `scrollback.bin` at 262144 bytes, so daemon restart retained only the newest 256 KiB. Bytes discarded by that cap cannot be recovered by the renderer.

Raw replay also reconstructs terminal modes only when the retained byte range still contains the control sequence that established each mode. A wrapped ring can omit an earlier alternate-screen, mouse, or keyboard transition while retaining later output that assumes the mode is active.

Ordinary GUI reconnects and complete state restoration are different operations. A reconnect must continue from the last accepted absolute offset without resetting xterm. A complete restoration must reset once, establish the saved state, replay every byte after the checkpoint exactly once, and preserve the user's viewport unless an underrun makes an exact continuation impossible.

## Reliability contract

Every terminal stream uses monotonic absolute byte offsets.

The daemon owns process lifetime, the live byte ring, persisted raw history, and the latest accepted terminal checkpoint. The GUI owns rendered xterm state and can publish a serialized checkpoint only after all writes through the checkpoint offset have completed.

A checkpoint consists of:

- the serialized xterm VT state
- the original columns and rows
- the absolute PTY offset represented by the state
- the configured scrollback line limit

Raw PTY bytes received after the checkpoint form its tail. Checkpoint metadata and bytes must be committed atomically. The daemon must never associate a checkpoint with an offset newer than the state it represents.

Restoration creates xterm at the checkpoint dimensions, writes the checkpoint before exposing incomplete frames where practical, replays the raw tail beginning at the checkpoint offset, and resizes only after replay completes. An ordinary reconnect neither resets xterm nor reapplies a checkpoint.

The 8 MiB memory ring remains the fast reconnect path. Durable persistence must not silently replace the configured terminal history with a smaller fixed byte limit.

## Failure handling

A missing or invalid checkpoint falls back to raw replay. A checkpoint whose offset is outside the available raw range is rejected with contextual warning logging. Interrupted writes retain the previous complete checkpoint. Duplicate frames below the accepted offset are ignored. A gap at or above the accepted offset forces an explicit resynchronization rather than displaying corrupted state.

The daemon appends a tracked private-mode supplement after serialized state restoration because `@xterm/addon-serialize` 0.13.0 omits mouse encodings 1005, 1006, and 1015 and alternate-scroll mode 1007. The supplement excludes alternate-screen transitions 47, 1047, 1048, and 1049 because the serialized checkpoint already reconstructs the active screen.

## Implementation

Cove pins `@xterm/addon-serialize` 0.13.0 against xterm 5.5.0. The GUI publishes at most one checkpoint per second and only when xterm has completed every write through the declared PTY offset. Checkpoints are not published while replay, checkpoint restoration, or an xterm parse callback is in flight.

The daemon validates that each checkpoint offset remains inside the retained ring. It persists `terminal-state.bin` with a versioned magic value, checkpoint dimensions, scrollback limit, serialized state, and the exact raw tail after the checkpoint. The write uses a flushed temporary file followed by an atomic replacement. Legacy `scrollback.bin` remains the fallback for missing or invalid checkpoint state and no longer has a 256 KiB truncation.

Initial subscriptions and ring-underrun resynchronizations can carry checkpoint state, original dimensions, the filtered mode supplement, and raw bytes from the checkpoint offset. Browser xterm restores at the saved dimensions, applies the checkpoint and supplement as one ordered write, replays the tail, then fits to the current pane.

## Known limitations

xterm's serialization addon is used by VS Code but remains labeled experimental. Cove pins an exact compatible version and protects the required behavior with regression tests.

Sixel and iTerm images are not preserved by xterm serialization. Windows ConPTY output behavior is a separate platform issue. History discarded before this contract was deployed cannot be reconstructed.

## Acceptance

The terminal path is reliable only when one real long-running terminal survives all of the following without missing or duplicated output:

- output exceeding the live ring capacity
- scrolling away from the live prompt
- repeated pane resize and reflow
- hidden and restored shores
- alternate-screen entry and exit
- mouse and Kitty keyboard mode transitions
- GUI disconnect and reconnect
- clean GUI restart
- clean daemon restart
- unclean daemon termination and restart

After each transition, history, screen contents, cursor, terminal modes, viewport behavior, and input delivery must remain correct.

## Sources

- [xterm.js](https://github.com/xtermjs/xterm.js)
- [xterm serialization addon](https://github.com/xtermjs/xterm.js/tree/master/addons/addon-serialize)
- [xterm serialization API](https://github.com/xtermjs/xterm.js/blob/master/addons/addon-serialize/typings/addon-serialize.d.ts)
- [VS Code PTY service](https://github.com/microsoft/vscode/blob/main/src/vs/platform/terminal/node/ptyService.ts)
- [VS Code persistent terminals](https://code.visualstudio.com/docs/terminal/advanced)
