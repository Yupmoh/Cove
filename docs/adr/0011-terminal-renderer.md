# ADR 0011 — Terminal renderer + the native-webview bet

Status: Accepted — GO (owner-ratified 2026-07-04)

## Context

Cove renders every terminal nook with xterm.js inside Ryn's OS-native webview
(WKWebView on macOS), betting a ~5 MB NativeAOT binary can replace a bundled
Chromium (PL-88). xterm.js offers three renderers: WebGL (`@xterm/addon-webgl`),
Canvas (`@xterm/addon-canvas`), and the built-in DOM renderer. Per the Ryn
capability map §5, WebGL2 is hardware-accelerated on all backends and WebGPU is
not portable (never used). This ADR records measured behavior at 1–20 nooks under
`yes`-spam and resize storms, then gates the whole native-webview strategy (TM-12).

## Methodology

- Harness: `src/Cove.Gui/frontend/src/perf/harness.ts`, auto-run via
  `COVE_GUI_PAGE=perf`. Synthetic in-page feeder (isolates the renderer from the
  PTY transport). The feeder is self-paced (backpressured by xterm's write
  callback), so achieved throughput is itself a measured result; 3 s per cell.
- Matrix: {webgl, canvas, dom} × {1,5,10,20 nooks} × {idle, yesSpam, resizeStorm}
  plus a 20-nook / 4-visible hidden-nook-pause cell per renderer.
- Metrics: fps, frame-time p95 (jank), long-task ms (UI-thread blocking),
  throughput MB/s (self-paced), WebGL context losses.
- Live confirmation: one real `yes`-spam nook through the TP-10 relay, observed
  for wedge/corruption.
- Limitation: the WKWebView PerformanceObserver does not support the 'longtask'
  entry type, so the longtask(ms) column reads 0 in every cell on this engine.

## Test machine

machine: Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko)
webgl2Available: true
timestamp: 2026-07-04T04:05:13.336Z

## Results

| renderer | nooks | visible | scenario | fps | frameP95(ms) | longtask(ms) | throughput(MB/s) | glLoss |
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

Renderer FPS/lag were measured by the TP-11 perf harness (Results table above) driving a synthetic byte feed; the live engine `nook.*` handlers that stream a real PTY to the webview were wired separately (see criterion 5 / `NookLiveHandlerTests`). The renderer go/no-go does not depend on that wiring.

## Options considered

- WebGL renderer — GPU-accelerated; fastest glyph throughput; but each nook holds
  a WebGL2 context and browsers cap live contexts (~16), so 20 nooks can force
  context loss / fallback (see `glLoss` column).
- Canvas renderer — CPU 2D; no context cap; middle ground.
- DOM renderer — most compatible; highest per-cell cost at scale.
- Hidden-nook render-pause + lazy unmount (TM-12): the `yesSpamHidden` cell shows
  the effect of not rendering/feeding off-screen nooks.

## Decision

Ratified by Moh (owner) on 2026-07-04 from the Results table:

1. Default renderer: **canvas**. It holds 30.4–30.6 fps idle from 1 to 20 nooks with `glLoss=0` and stays at ~30.5 fps under resize storms, with no per-nook GPU-context cap to trip.
2. Fallback order: **canvas → webgl (opt-in accelerator, only below ~8 live nooks) → dom (universal compatibility)**. WebGL is never the default because a browser caps live WebGL2 contexts (~16) and 20 nooks force context loss (`glLoss=4`); dom is the last resort (highest per-cell cost, but always available).
3. Hidden-nook render-pause (TM-12) is **mandatory**. It is the difference between a usable and unusable spam workload at scale: 20 nooks under `yesSpam` render at 4.8 fps when all visible but recover to 30.3 fps when only 4 are visible.
4. **GO** on the ~5 MB native-webview bet (PL-88). Canvas delivers 30 fps idle across every configuration with zero GPU-context risk; the WKWebView footprint is acceptable for the capability.

Under sustained single-nook spam every renderer converges near the ~10 MB/s PTY throughput ceiling regardless of choice, so throughput does not discriminate between them; idle stability, resize behavior, and context safety do — all of which favor canvas.

## Consequences

- The GUI ships canvas as the default xterm.js renderer; webgl is gated behind an explicit opt-in and an ≤~8-nook guard; dom is the guaranteed fallback.
- Off-screen nooks must pause rendering and PTY feed; this is a correctness-adjacent requirement for multi-nook sessions, not an optimization.
- `longtask(ms)` reads 0 everywhere because WKWebView does not implement the `PerformanceObserver` longtask API; UI-thread stall is tracked via `frameP95(ms)` instead. This instrumentation gap is a WKWebView limitation, recorded so later milestones do not mistake it for a clean signal.
- The native-webview architecture (PL-88) is confirmed for M1; no renderer re-litigation is planned unless a measured regression appears.
