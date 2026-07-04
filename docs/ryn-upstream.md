# Ryn upstream work items

Cove is built on the Ryn framework, which is maintained by the same author. Where a capability Cove needs is framework-shaped and benefits any Ryn app, it is built upstream in Ryn and consumed as a Ryn release, keeping Cove's codebase clean. This register tracks those items, why Cove needs each, how Cove behaves until the item lands, and who owns it. None of these block M0.

| ID | Item | Needed by | Blocking? |
|---|---|---|---|
| RYN-01 | Backpressure-aware event delivery + flow-control primitive in the shell PTY plugin | M1 terminal | No |
| RYN-02 | Native application menu bar (macOS App/Edit/Window) | M8 chrome | No |
| RYN-03 | Global shortcuts / hotkeys plugin | M8 chrome | No |
| RYN-04 | Windows Authenticode signing in the bundler | M8 release | No |
| RYN-05 | Linux GUI verification on real hardware + fixes | All GUI milestones | No |
| RYN-06 | Multiple webviews per window / native pane embedding | M7 browser | No |
| RYN-07 | HTTP Range + off-thread static serving | M6/M7 media | No |

## RYN-01 — Lossless event delivery + shell flow control

Owner: Moh (Ryn maintainer). The framework's event batcher drops chunks on overflow, which corrupts terminal state. Cove routes the PTY firehose around it via an engine-owned ring buffer and a same-origin loopback WebSocket relay, so Cove is already lossless (see `adr/0002-lossless-pty-transport.md`). The upstream fix makes the batcher coalesce-and-credit instead of drop, benefiting non-PTY events and every Ryn app. Not a Cove blocker.

## RYN-02 — Native application menu bar

Owner: Moh (Ryn maintainer). The framework has tray menus but no application menu bar; on macOS this is below the platform baseline. Needed by the M8 chrome milestone. Until then, Cove exposes commands through the in-window command palette.

## RYN-03 — Global shortcuts / hotkeys

Owner: Moh (Ryn maintainer). No app-wide keybinds exist outside the focused webview. Needed by M8 for a global show/hide toggle. In-page keybinds work today.

## RYN-04 — Windows Authenticode signing

Owner: Moh (Ryn maintainer). The bundler signs macOS artifacts but has no Windows Authenticode path. Needed by the M8 release milestone. Until then, the release pipeline stubs the signing step or self-manages signtool.

## RYN-05 — Linux GUI verification

Owner: Moh (Ryn maintainer). The Linux GUI is unverified on real hardware and the tray is menu-only. Cove treats Linux as experimental, schedules explicit Linux GUI verification in CI, and upstreams fixes. Ships opaque fallbacks where vibrancy or tray-click gaps exist.

## RYN-06 — Multiple webviews per window

Owner: Moh (Ryn maintainer). One webview per window means a real embedded-browser pane is not achievable without upstream work. Needed by the M7 browser milestone, which picks a concrete strategy there.

## RYN-07 — HTTP Range + off-thread static serving

Owner: Moh (Ryn maintainer). Static serving reads whole files synchronously on the UI thread with no HTTP Range, so media seeking is broken and large assets stutter. Needed if M6/M7 panes embed media.
