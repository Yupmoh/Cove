export interface Stats { mean: number; p50: number; p95: number; min: number; max: number; }

export function computeStats(xs: number[]): Stats {
  if (xs.length === 0) return { mean: 0, p50: 0, p95: 0, min: 0, max: 0 };
  const s = [...xs].sort((a, b) => a - b);
  const q = (p: number) => s[Math.min(s.length - 1, Math.floor(p * (s.length - 1)))];
  const mean = xs.reduce((a, b) => a + b, 0) / xs.length;
  return { mean, p50: q(0.5), p95: q(0.95), min: s[0], max: s[s.length - 1] };
}

export function fps(frameDeltasMs: number[]): number {
  if (frameDeltasMs.length === 0) return 0;
  const avg = frameDeltasMs.reduce((a, b) => a + b, 0) / frameDeltasMs.length;
  return avg > 0 ? 1000 / avg : 0;
}

export function throughputMBs(bytes: number, durationMs: number): number {
  if (durationMs <= 0) return 0;
  return bytes / (1024 * 1024) / (durationMs / 1000);
}

export function round1(x: number): number { return Math.round(x * 10) / 10; }
