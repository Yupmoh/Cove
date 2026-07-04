# ADR 0011 — Terminal renderer + the native-webview bet

Status: Proposed — AWAITING HUMAN GO/NO-GO

## Context

Cove renders every terminal pane with xterm.js inside Ryn's OS-native webview
(WKWebView on macOS), betting a ~5 MB NativeAOT binary can replace a bundled
Chromium (PL-88). xterm.js offers three renderers: WebGL (`@xterm/addon-webgl`),
Canvas (`@xterm/addon-canvas`), and the built-in DOM renderer. Per the Ryn
capability map §5, WebGL2 is hardware-accelerated on all backends and WebGPU is
not portable (never used). This ADR records measured behavior at 1–20 panes under
`yes`-spam and resize storms, then gates the whole native-webview strategy (TM-12).

## Methodology

- Harness: `src/Cove.Gui/frontend/src/perf/harness.ts`, auto-run via
  `COVE_GUI_PAGE=perf`. Synthetic in-page feeder (isolates the renderer from the
  PTY transport). The feeder is self-paced (backpressured by xterm's write
  callback), so achieved throughput is itself a measured result; 3 s per cell.
- Matrix: {webgl, canvas, dom} × {1,5,10,20 panes} × {idle, yesSpam, resizeStorm}
  plus a 20-pane / 4-visible hidden-pane-pause cell per renderer.
- Metrics: fps, frame-time p95 (jank), long-task ms (UI-thread blocking),
  throughput MB/s (self-paced), WebGL context losses.
- Live confirmation: one real `yes`-spam pane through the TP-10 relay, observed
  for wedge/corruption.
- Limitation: the WKWebView PerformanceObserver does not support the 'longtask'
  entry type, so the longtask(ms) column reads 0 in every cell on this engine.

## Test machine

machine: Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko)
webgl2Available: true
timestamp: 2026-07-04T04:05:13.336Z

## Results

| renderer | panes | visible | scenario | fps | frameP95(ms) | longtask(ms) | throughput(MB/s) | glLoss |
|---|---|---|---|---|---|---|---|---|
| webgl | 1 | 1 | idle | 30.4 | 34 | 0 | 0 | 0 |
| webgl | 1 | 1 | yesSpam | 30.3 | 43 | 0 | 0.8 | 0 |
| webgl | 1 | 1 | resizeStorm | 30.6 | 34 | 0 | 0 | 0 |
| webgl | 5 | 5 | idle | 30.5 | 34 | 0 | 0 | 0 |
| webgl | 5 | 5 | yesSpam | 17.9 | 62 | 0 | 2.9 | 0 |
| webgl | 5 | 5 | resizeStorm | 30.6 | 34 | 0 | 0 | 0 |
| webgl | 10 | 10 | idle | 30.3 | 34 | 0 | 0 | 0 |
| webgl | 10 | 10 | yesSpam | 8.9 | 122 | 0 | 9.5 | 0 |
| webgl | 10 | 10 | resizeStorm | 30.6 | 34 | 0 | 0 | 0 |
| webgl | 20 | 20 | idle | 30.3 | 34 | 0 | 0 | 4 |
| webgl | 20 | 20 | yesSpam | 4.5 | 255 | 0 | 10.1 | 4 |
| webgl | 20 | 20 | resizeStorm | 30.3 | 34 | 0 | 0 | 4 |
| webgl | 20 | 4 | yesSpamHidden | 30.3 | 60 | 0 | 9.6 | 4 |
| canvas | 1 | 1 | idle | 30.4 | 34 | 0 | 0 | 0 |
| canvas | 1 | 1 | yesSpam | 30.4 | 45 | 0 | 3.6 | 0 |
| canvas | 1 | 1 | resizeStorm | 30.3 | 34 | 0 | 0 | 0 |
| canvas | 5 | 5 | idle | 30.5 | 34 | 0 | 0 | 0 |
| canvas | 5 | 5 | yesSpam | 17.2 | 61 | 0 | 9.8 | 0 |
| canvas | 5 | 5 | resizeStorm | 30.5 | 34 | 0 | 0 | 0 |
| canvas | 10 | 10 | idle | 30.4 | 34 | 0 | 0 | 0 |
| canvas | 10 | 10 | yesSpam | 9 | 121 | 0 | 10 | 0 |
| canvas | 10 | 10 | resizeStorm | 30.5 | 34 | 0 | 0 | 0 |
| canvas | 20 | 20 | idle | 30.5 | 34 | 0 | 0 | 0 |
| canvas | 20 | 20 | yesSpam | 4.8 | 242 | 0 | 10.4 | 0 |
| canvas | 20 | 20 | resizeStorm | 30.6 | 39 | 0 | 0 | 0 |
| canvas | 20 | 4 | yesSpamHidden | 30.3 | 60 | 0 | 9.9 | 0 |
| dom | 1 | 1 | idle | 30.5 | 34 | 0 | 0 | 0 |
| dom | 1 | 1 | yesSpam | 30.5 | 44 | 0 | 3.7 | 0 |
| dom | 1 | 1 | resizeStorm | 30.4 | 34 | 0 | 0 | 0 |
| dom | 5 | 5 | idle | 30.4 | 34 | 0 | 0 | 0 |
| dom | 5 | 5 | yesSpam | 28.2 | 84 | 0 | 9.4 | 0 |
| dom | 5 | 5 | resizeStorm | 30.5 | 36 | 0 | 0 | 0 |
| dom | 10 | 10 | idle | 30.5 | 34 | 0 | 0 | 0 |
| dom | 10 | 10 | yesSpam | 11.4 | 98 | 0 | 9.7 | 0 |
| dom | 10 | 10 | resizeStorm | 30.5 | 44 | 0 | 0 | 0 |
| dom | 20 | 20 | idle | 30.6 | 34 | 0 | 0 | 0 |
| dom | 20 | 20 | yesSpam | 5.1 | 238 | 0 | 10.1 | 0 |
| dom | 20 | 20 | resizeStorm | 27.4 | 92 | 0 | 0 | 0 |
| dom | 20 | 4 | yesSpamHidden | 30.3 | 61 | 0 | 9.3 | 0 |

## Live confirmation

Deferred: engine pane.* live handlers not wired (M0 plan gap).

## Options considered

- WebGL renderer — GPU-accelerated; fastest glyph throughput; but each pane holds
  a WebGL2 context and browsers cap live contexts (~16), so 20 panes can force
  context loss / fallback (see `glLoss` column).
- Canvas renderer — CPU 2D; no context cap; middle ground.
- DOM renderer — most compatible; highest per-cell cost at scale.
- Hidden-pane render-pause + lazy unmount (TM-12): the `yesSpamHidden` cell shows
  the effect of not rendering/feeding off-screen panes.

## Decision — AWAITING HUMAN GO/NO-GO

To be decided by a human from the Results table: (1) the default renderer,
(2) the fallback order and the pane count at which to switch, (3) whether the
hidden-pane render-pause is mandatory, (4) GO or NO-GO on the ~5 MB
native-webview bet (PL-88).

PENDING

## Consequences

PENDING
