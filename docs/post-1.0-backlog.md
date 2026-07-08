# Cove Post-1.0 Backlog Register

This register folds every deferred FULL and BEYOND feature into a single tracked list. Items are grouped by blocker category. Each entry references its feature-inventory ID(s) and the packet that scoped it.

## 1. Ryn / CEF upstream (browser engine, native chrome)

These require the Ryn framework to ship `Ryn.Plugins.Browser` (CEF hosting), window-effects, native menu bar, and notification APIs. Cove cannot build them autonomously.

| Feature IDs | Packet | Description |
|---|---|---|
| BR-68, TM-78 | M7-P03 | Ryn.Plugins.Browser engine host + on-demand CEF download |
| BR-26-32 | M7-P04 | CDP transport + input forwarding through hosted engine |
| BR-58-60 | M7-P05 | Per-pane incognito/extensions/UA + CDP nav events |
| BR-16-22 | M7-P09 | Subtabs, PiP, downloads, passkeys, deep links, codecs, file:// |
| BR-53-57 | M7-P18 | CPU-throttle, occlusion, crash-recovery, coalescing |
| BR-58-60 | M7-P19 | Engine config UI (incognito/extensions/UA in Settings) |
| BR-56 | M7-P20 | CEF Crashpad local minidumps |
| BR-43-48 | M7-P15 | Comment mode pin-anchoring (needs live CEF element/rect anchoring) |
| KN-80/97 | M7-P22 | Native screen-recorder sidecar (ScreenCaptureKit/WGC/PipeWire) |
| KN-81/82/84/102 | M7-P24 | Capture enrichment (chapters, redaction, live transcription) |
| UX-01/83 | MC-P01 | Frameless window + custom title bar + macOS traffic-light repositioning |
| UX-03 | MC-P03 | Window vibrancy + portable opaque fallback |
| UX-04 | MC-P04 | Native menu bar shell + accelerator sync (Ryn CMP-03) |
| UX-83 | MC-P16 | Cross-platform chrome portability (per-OS title bar, Windows menu, Linux) |
| UX-63 | M8-P11 | Dock badge via Ryn app-icon-badge API |
| UX-61 | M8-P10 | OS notification delivery (Ryn notification.* cap-map §2) |

## 2. Monaco bundle (editor surface)

The Monaco Vite bundle + lazy-load + same-origin workers requires Vite build wiring and binary embedding. A textarea-based interim editor exists but cannot meet fold/undo/find-replace-chords criteria.

| Feature IDs | Packet | Description |
|---|---|---|
| TM-60 | MP-P02 | Monaco Vite bundle + lazy-load + embedded + same-origin workers |
| TM-60 | MP-P03 | Editor pane core (fold/undo/format-on-save/large-file/read-only) |
| TM-61 | MP-P04 | Editor find/replace + navigation + symbols (go-to-def/find-refs/rename) |
| TM-63 | MP-P06 | Editor chrome (breadcrumbs/minimap/word-wrap/agent-edit chip) |
| TM-64 | MP-P08 | Editor git decorations + inline blame |
| TM-65 | MP-P13 | Markdown pane (Lexical RTE ↔ Monaco source) |
| TM-66 | MP-P14 | Markdown inline comments (MDAST directives) + image paste |
| TM-73 | MP-P17 | Diff viewer pane |

## 3. LSP server binary (editor diagnostics)

The LSP protocol host is built and tested, but end-to-end TS/JSON/CSS/HTML diagnostics + hovers + go-to-def require a real LSP server binary (typescript-language-server or equivalent) to be installed and wired.

| Feature IDs | Packet | Description |
|---|---|---|
| TM-62 | MP-P05 | LSP host + ESLint sidecar (end-to-end diagnostics/hovers/go-to-def) |

## 4. Model2Vec / PNG export (M6 remaining)

| Feature IDs | Packet | Description |
|---|---|---|
| KN-103 | M6-P22 | SemanticEmbedder needs Model2Vec model asset |
| KN-104 | M6-P13 | PNG export needs SkiaSharp/System.Drawing |

## 5. Signed packaging (CI secrets)

| Feature IDs | Packet | Description |
|---|---|---|
| PL-76/77 | M8-P16 | Signed/notarized packaging (mac/win/linux) — needs Apple cert, Authenticode |

## 6. Daemon→GUI event push (settings repaint)

| Feature IDs | Packet | Description |
|---|---|---|
| UX-59 | M8-P01 | Settings client repaint — no daemon→GUI event-push channel; needs shared sub/evt transport extension |

## 7. Human-judgment TUI infrastructure (M9)

The TUI second frontend requires deep infrastructure that needs owner design calls and performance tuning.

| Feature IDs | Packet | Description |
|---|---|---|
| PL-84 | M9-P01 | Compositor benchmark + Spectre AOT + VT emulator decision |
| PL-84 | M9-P03 | Cell compositor (double-buffered, damage-tracked) |
| TM-01 | M9-P04 | Embedded VT emulator (xterm-subset parser) |
| PL-84 | M9-P05 | Spectre cell-region backend + AOT gate |
| UX-38/39 | M9-P06 | Input layer + keymap/prefix model (raw mode, CSI-u/kitty, SGR-1006) |
| WS-81 | M9-P16 | Multi-client + per-client viewport |
| PL-76 | M9-P19 | Cross-platform hardening (Windows named-pipe, Linux console) |

## Summary

- **722 buildable features** total (excluding 21 SKIP).
- Items above are deferred to post-1.0 because they depend on upstream (Ryn/CEF/Monaco), external binaries (LSP server, Model2Vec), CI secrets, or require human-judgment infrastructure decisions.
- All other features are either DONE or have their backend logic built and tested.
