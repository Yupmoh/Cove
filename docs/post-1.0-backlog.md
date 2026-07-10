# Cove Post-1.0 Backlog Register

This register folds every deferred FULL and BEYOND feature into a single tracked list. Items are grouped by blocker category. Each entry references its feature-inventory ID(s) and the packet that scoped it.

Revised 2026-07-09 after the full claims-vs-code audit: the original register mislabeled shipped Ryn 0.11.0 capabilities (MenuBar, Badge, GlobalShortcut, Notification, WebViewPane, frameless title bars) as upstream gaps, and listed the Monaco bundle, the daemon-to-GUI event push, and the TUI compositor as deferred after they had shipped. Those entries are corrected below; falsely-deferred items moved to section 8 (open, buildable now).

## 1. Genuine Ryn / platform upstream gaps

Verified against the Ryn source tree (Work/Ryn) on 2026-07-09. Only these remain upstream:

| Feature IDs | Packet | Description |
|---|---|---|
| UX-03 | MC-P03 | RESOLVED upstream: Ryn ships `SetBackdrop`/`GetBackdrop` with blur/acrylic/mica plus the `window.setBackdrop` IPC verb; the GUI launches with `BackdropMaterial.Blur` and exposes a backdrop toggle. Row kept for history — the 0.11-era "no vibrancy API" finding is stale. |
| BR-23-41 | M7-P10-P14 | CDP-fidelity browser automation — WKWebView/WebView2 expose no CDP. `webviewPane.eval/execute` enables an injected-JS subset (no screenshot, no httpOnly cookies, `isTrusted=false`); adopting it is an owner decision. |
| BR-53-57 | M7-P18 | CPU-throttle, occlusion, crash-recovery events — absent from WebViewPane. |
| BR-58-60 (part) | M7-P19 | UA override + extensions — absent from WebViewPane. Per-pane sessions (≈incognito) work today via ephemeral `StoragePath`. |
| BR-16-22 | M7-P09 | PiP, downloads, passkeys, deep links, codecs, file:// — beyond WebViewPane 0.11.0. Subtabs work today via multiple panes. |
| KN-80/97 | M7-P22 | Native screen-recorder sidecar (ScreenCaptureKit/WGC/PipeWire). |
| KN-81/82/84/102 | M7-P24 | Capture enrichment (chapters, redaction, live transcription). |
| UX-83 | MC-P16 | Cross-platform chrome verification — needs Windows/Linux hardware. |
| PL-76 | M9-P19 | TUI cross-platform hardening — needs Windows/Linux hardware. |

Superseded, never to be built: M7-P01/P03/P04/P05 (CEF hosting) — replaced by `Ryn.Plugins.WebViewPane` (native WKWebView/WebView2 panes, no Chromium ever).

## 2. External binaries / assets

| Feature IDs | Packet | Description |
|---|---|---|
| TM-62 | MP-P05 | LSP host end-to-end — protocol host built + tested; needs a real `typescript-language-server` binary wired. Blocks symbol-level editor features (go-to-def/find-refs/rename, symbol breadcrumbs). |
| KN-103 | M6-P22 | SKIPPED by owner decision (2026-07-09): semantic recall delegated to a pluggable external memory backend (see plans spec) instead of a bundled model; lexical+hotness is the shipping built-in. |
| KN-104 | M6-P13 | PNG export needs SkiaSharp or equivalent. |

## 3. CI secrets

| Feature IDs | Packet | Description |
|---|---|---|
| PL-76/77 | M8-P16 | Signed/notarized packaging (mac/win/linux) — needs Apple cert, Authenticode. |

## 4. Deferred TUI application surfaces (M9)

The compositor foundation (P01/P03/P04/P05), the input decoder (P06 backend), the multi-client viewport service (P16 backend), and `cove attach` (P17, runtime-unverified) are DONE. What remains is the milestone-sized TUI application layer, deferred as a block:

| Feature IDs | Packet | Description |
|---|---|---|
| TM-38-42 | M9-P07 | Mosaic layout engine + focus/split rendering |
| UX-82 | M9-P08 | Chrome surfaces (status line, room tabs, sidebars) |
| PL-84 | M9-P09 | Terminal capability + color/glyph degradation (detection built; rendering pending) |
| UX-43-49 | M9-P10 | Theme parity resolver |
| AG-39/UX-26 | M9-P11 | Launcher + command palette (TUI forms) |
| AG-138 | M9-P12 | Agent surface (TUI) |
| TA-76 | M9-P13 | Task board (TUI) |
| KN-96 | M9-P14 | Knowledge surfaces (TUI) |
| TM-60/72/75 | M9-P15 | GUI-only degradation stubs |
| UX-82 | M9-P18 | Reduced-motion, accessibility, SR-linear mode |
| PL-84 | M9-P20 | Cross-cutting test harnesses (VT conformance corpus, PTY soak) |
| UX-82 | MC-P17 | TUI analog contract handshake (follows the surfaces above) |
| UX-38/39 (part) | M9-P06 | Raw-mode terminal setup + keymap/prefix resolver (decoder done) |
| WS-81 (part) | M9-P16 | Per-client view-state persistence + detach/reattach polish (service done) |

## 5. Runtime-verification debt (shipped but never exercised end-to-end)

Not features — verification work. These shipped code paths have unit tests but no live exercise:

- `cove attach` streaming (M9-P17): needs a live daemon + PTY session.
- Monaco editor/diff/markdown panes + browser pane: type-checked + unit-tested; never visually confirmed in a running GUI.
- Update flow (M8-P14): mock-verified only; `Ryn.Plugins.Updater` (real apply/relaunch) is not referenced.
- CI matrix: main is ~215 commits ahead of origin — no CI run has validated any of this work. Push and watch a full run.
- M1 gate 7 deviation: restored scrollback replays through the live xterm parser instead of rendering as a static grid.

## 6. Corrected on 2026-07-09 — no longer deferred (open, buildable now)

Everything here was falsely listed as blocked/deferred; the capability exists and is wired. See the milestone LEDGERs for precise remaining work:

| Packet | Was blamed on | Reality |
|---|---|---|
| MC-P01 | "Ryn frameless gap" | `TitleBarStyle{Hidden,Overlay,Frameless}` set in Program.cs + `data-webview-drag/resize/close/minimize/maximize/ignore` attributes (docs/custom-title-bars.md) |
| MC-P02 | "Requires Ryn GUI" | Pure frontend once MC-P01 lands |
| MC-P04 | "Ryn CMP-03 gap" | `Ryn.Plugins.MenuBar` shipped + wired; main.ts already defines a menu + itemClicked switch |
| MC-P05 | "Requires Ryn GUI" | Pure frontend HTML/CSS |
| MC-P06 | "Requires Ryn GUI" | Pure frontend; toggle-zen case already in main.ts |
| MC-P13 | "Ryn CMP-03 gap" | `Ryn.Plugins.GlobalShortcut` shipped + wired; MenuBar accelerators cover menu chords |
| M8-P10 | "Ryn notification.* gap" | `Ryn.Plugins.Notification` exists (Send/SendWithSound/SendWithIcon/IsSupported/RequestPermission); policy engine built (14 tests); needs package ref + bridge |
| M8-P11 | "no app-icon-badge API" | `Ryn.Plugins.Badge` shipped + wired (Set/SetCount/Clear, mac+win backends) |
| MP-P02/P03/P04/P13/P17 | "Monaco is Human effort" | Monaco shipped (`bcf65c9`/`5fe96da`); panes use it |
| MP-P06/P08/P10/P14/P18/P19/P20 | "Requires Monaco/GUI" | Monaco + GUI exist; remaining work itemized in M-Panes LEDGER |
| M8-P01 | "no daemon→GUI push channel" | DONE (`132cefe`): FrameType.Event + EngineEventForwarder → `engine.event` |
| M7-P25/P26 | "Requires Ryn engine" | Backend done; frontend-only work in the existing settings window |
| M9-P01/P03/P04/P05 | "human-judgment infrastructure" | DONE: compositor + VT emulator + Spectre backend, AOT-clean, 77 tests |

## Summary

- **722 buildable features** total (excluding 21 SKIP).
- Genuinely deferred: sections 1-4 (upstream API gaps, external binaries/assets, CI secrets, TUI application layer).
- Section 5 is verification debt on shipped code, not new features.
- Section 6 documents the 2026-07-09 correction — those items are open work, buildable with what Ryn 0.11.0 ships today.
