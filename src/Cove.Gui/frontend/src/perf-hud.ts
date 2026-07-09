export interface HudState {
  enabled: boolean;
  lastTs: number | null;
  samples: number[];
  maxSamples: number;
}

export interface HudMetrics {
  fps: number;
  frameMs: number;
  worstFrameMs: number;
  sampleCount: number;
}

export interface JsHeapProbe {
  usedJSHeapSize: number;
}

export interface HudLine {
  label: string;
  value: string;
}

export function initHud(maxSamples = 120): HudState {
  return { enabled: false, lastTs: null, samples: [], maxSamples: Math.max(1, maxSamples) };
}

export function setHudEnabled(state: HudState, enabled: boolean): HudState {
  if (enabled && !state.enabled) return { ...state, enabled: true, lastTs: null, samples: [] };
  return { ...state, enabled };
}

export function toggleHud(state: HudState): HudState {
  return setHudEnabled(state, !state.enabled);
}

export function recordFrame(state: HudState, ts: number): HudState {
  if (state.lastTs === null) return { ...state, lastTs: ts };
  const delta = ts - state.lastTs;
  if (delta <= 0) return { ...state, lastTs: ts };
  const samples = [...state.samples, delta];
  while (samples.length > state.maxSamples) samples.shift();
  return { ...state, lastTs: ts, samples };
}

export function hudMetrics(state: HudState): HudMetrics {
  const count = state.samples.length;
  if (count === 0) return { fps: 0, frameMs: 0, worstFrameMs: 0, sampleCount: 0 };
  let sum = 0;
  let worst = 0;
  for (const sample of state.samples) {
    sum += sample;
    if (sample > worst) worst = sample;
  }
  const average = sum / count;
  return {
    fps: average > 0 ? Math.round(1000 / average) : 0,
    frameMs: roundTenths(average),
    worstFrameMs: roundTenths(worst),
    sampleCount: count,
  };
}

export function readJsHeapBytes(heap: JsHeapProbe | null | undefined): number | null {
  if (!heap || typeof heap.usedJSHeapSize !== "number") return null;
  return heap.usedJSHeapSize;
}

export function formatBytes(bytes: number): string {
  const units = ["B", "KB", "MB", "GB", "TB"];
  let value = bytes;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit++;
  }
  if (unit === 0) return `${Math.round(value)} B`;
  return `${value.toFixed(1)} ${units[unit]}`;
}

export function hudLines(metrics: HudMetrics, heapBytes: number | null): HudLine[] {
  const measured = metrics.sampleCount > 0;
  return [
    { label: "FPS", value: measured ? String(metrics.fps) : "—" },
    { label: "Frame", value: measured ? `${metrics.frameMs} ms` : "—" },
    { label: "Worst", value: measured ? `${metrics.worstFrameMs} ms` : "—" },
    { label: "JS heap", value: heapBytes === null ? "n/a" : formatBytes(heapBytes) },
  ];
}

function roundTenths(value: number): number {
  return Math.round(value * 10) / 10;
}
