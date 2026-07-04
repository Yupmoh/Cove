import { describe, it, expect } from "vitest";
import { computeStats, fps, throughputMBs } from "./instrument";

describe("instrument", () => {
  it("computes stats on a known set", () => {
    const s = computeStats([10, 20, 30, 40, 50]);
    expect(s.min).toBe(10);
    expect(s.max).toBe(50);
    expect(s.mean).toBe(30);
    expect(s.p50).toBe(30);
    expect(s.p95).toBe(40);
  });
  it("returns zeros for empty input", () => {
    expect(computeStats([])).toEqual({ mean: 0, p50: 0, p95: 0, min: 0, max: 0 });
  });
  it("derives fps from frame deltas", () => {
    expect(fps([16, 16, 16, 16])).toBeCloseTo(62.5, 1);
    expect(fps([])).toBe(0);
  });
  it("computes throughput MB/s", () => {
    expect(throughputMBs(1024 * 1024, 1000)).toBeCloseTo(1, 5);
    expect(throughputMBs(0, 0)).toBe(0);
  });
});
