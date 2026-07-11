# Ryn upstream work items

Cove is built on the Ryn framework, which is maintained by the same author. Where a capability Cove needs is framework-shaped and benefits any Ryn app, it is built upstream in Ryn and consumed as a Ryn release, keeping Cove's codebase clean. This register tracks those items, why Cove needs each, how Cove behaves until the item lands, and who owns it. None of these block M0.

| ID | Item | Needed by | Blocking? |
|---|---|---|---|
| RYN-01 | Backpressure-aware event delivery + flow-control primitive in the shell PTY plugin | M1 terminal | No |
| RYN-02 | Native application menu bar (macOS App/Edit/Window) | M8 chrome | **SHIPPED — Ryn.Plugins.MenuBar 0.11.0** |
| RYN-03 | Global shortcuts / hotkeys plugin | M8 chrome | **SHIPPED — Ryn.Plugins.GlobalShortcut 0.11.0** |
| RYN-04 | Windows Authenticode signing in the bundler | M8 release | No |
| RYN-05 | Linux GUI verification on real hardware + fixes | All GUI milestones | No |
| RYN-06 | Multiple webviews per window / native nook embedding | M7 browser | **SHIPPED — Ryn.Plugins.WebViewPane 0.11.0** |
| RYN-07 | HTTP Range + off-thread static serving | M6/M7 media | No |

## RYN-01 — Lossless event delivery + shell flow control

Owner: Moh (Ryn maintainer). The framework's event batcher drops chunks on overflow, which corrupts terminal state. Cove routes the PTY firehose around it via an engine-owned ring buffer and a same-origin loopback WebSocket relay, so Cove is already lossless (see `adr/0002-lossless-pty-transport.md`). The upstream fix makes the batcher coalesce-and-credit instead of drop, benefiting non-PTY events and every Ryn app. Not a Cove blocker.

## RYN-02 — Native application menu bar

**SHIPPED in Ryn 0.11.0** as `Ryn.Plugins.MenuBar`: `SetMenu`/`Reset`, `MenuItemClicked`/`RoleActivated`, role items dispatching through the macOS responder chain, accelerator parsing, `CreateDefault`/`ExpandTopLevelRoles`. Wired in Cove's Program.cs (`AddRynMenuBar`); main.ts already defines a menu. Remaining Cove work: full menu IA + keybinding-engine accelerator sync (MC-P04).

## RYN-03 — Global shortcuts / hotkeys

**SHIPPED in Ryn 0.11.0** as `Ryn.Plugins.GlobalShortcut`: `Register`/`Unregister`/`IsRegistered`/`UnregisterAll` with cross-platform accelerator parsing. Wired in Cove's Program.cs (`AddRynGlobalShortcut`). Remaining Cove work: bind the global show/hide toggle + default chord map dispatch (MC-P13).

## RYN-04 — Windows Authenticode signing

Owner: Moh (Ryn maintainer). The bundler signs macOS artifacts but has no Windows Authenticode path. Needed by the M8 release milestone. Until then, the release pipeline stubs the signing step or self-manages signtool.

## RYN-05 — Linux GUI verification

Owner: Moh (Ryn maintainer). The Linux GUI is unverified on real hardware and the tray is menu-only. Cove treats Linux as experimental, schedules explicit Linux GUI verification in CI, and upstreams fixes. Ships opaque fallbacks where vibrancy or tray-click gaps exist.

## RYN-06 — Multiple webviews per window

**SHIPPED in Ryn 0.11.0** as `Ryn.Plugins.WebViewPane`: secondary native webviews (WKWebView/WebView2) positioned as rectangles inside the window — open/close/list/navigate/back/forward/reload/setBounds/setZoom/setDevTools/url/eval/execute + navigated/titleChanged/loadStateChanged/domReady/faviconChanged/closed events, per-nook sessions via `StoragePath`. This replaced the CEF plan entirely (M7-P01/P03/P04/P05 obsolete). Still absent at the nook level: screenshot capture, UA override, extensions, CDP, throttle/occlusion/crash events (see post-1.0 backlog §1).

## RYN-07 — HTTP Range + off-thread static serving

Owner: Moh (Ryn maintainer). Static serving reads whole files synchronously on the UI thread with no HTTP Range, so media seeking is broken and large assets stutter. Needed if M6/M7 nooks embed media.
