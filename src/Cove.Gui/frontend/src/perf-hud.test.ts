import { describe, it, expect } from "vitest";
import { initHud, toggleHud, setHudEnabled, recordFrame, hudMetrics, readJsHeapBytes, formatBytes, hudLines } from "./perf-hud";

describe("initHud", () => {
  it("starts disabled with no samples", () => {
    const s = initHud();
    expect(s.enabled).toBe(false);
    expect(s.samples).toEqual([]);
    expect(s.lastTs).toBeNull();
  });
  it("clamps maxSamples to at least one", () => {
    expect(initHud(0).maxSamples).toBe(1);
  });
});

describe("toggleHud", () => {
  it("enables from disabled and clears prior samples", () => {
    const primed = recordFrame(recordFrame(initHud(), 0), 16);
    const on = toggleHud(primed);
    expect(on.enabled).toBe(true);
    expect(on.samples).toEqual([]);
    expect(on.lastTs).toBeNull();
  });
  it("disables from enabled", () => {
    const off = toggleHud({ ...initHud(), enabled: true });
    expect(off.enabled).toBe(false);
  });
});

describe("setHudEnabled", () => {
  it("resets sampling window when enabling", () => {
    const primed = recordFrame(recordFrame(initHud(), 0), 16);
    const on = setHudEnabled(primed, true);
    expect(on.enabled).toBe(true);
    expect(on.samples).toEqual([]);
  });
  it("keeps samples when already enabled", () => {
    const primed = recordFrame(recordFrame({ ...initHud(), enabled: true }, 0), 16);
    const still = setHudEnabled(primed, true);
    expect(still.samples).toEqual([16]);
  });
});

describe("recordFrame", () => {
  it("stores the timestamp but no duration on the first frame", () => {
    const s = recordFrame(initHud(), 1000);
    expect(s.lastTs).toBe(1000);
    expect(s.samples).toEqual([]);
  });
  it("appends the elapsed duration between frames", () => {
    const s = recordFrame(recordFrame(initHud(), 1000), 1016);
    expect(s.samples).toEqual([16]);
    expect(s.lastTs).toBe(1016);
  });
  it("ignores non-positive deltas but advances the timestamp", () => {
    const s = recordFrame(recordFrame(initHud(), 1000), 1000);
    expect(s.samples).toEqual([]);
    expect(s.lastTs).toBe(1000);
  });
  it("caps the sample window at maxSamples", () => {
    let s = initHud(3);
    let ts = 0;
    for (let i = 0; i < 6; i++) { ts += 10; s = recordFrame(s, ts); }
    expect(s.samples.length).toBe(3);
    expect(s.samples).toEqual([10, 10, 10]);
  });
});

describe("hudMetrics", () => {
  it("returns zeros with no samples", () => {
    expect(hudMetrics(initHud())).toEqual({ fps: 0, frameMs: 0, worstFrameMs: 0, sampleCount: 0 });
  });
  it("computes fps from the average frame duration", () => {
    let s = initHud();
    let ts = 0;
    for (let i = 0; i < 6; i++) { ts += 16; s = recordFrame(s, ts); }
    const m = hudMetrics(s);
    expect(m.sampleCount).toBe(5);
    expect(m.fps).toBe(63);
    expect(m.frameMs).toBe(16);
  });
  it("reports the worst frame in the window", () => {
    let s = recordFrame(initHud(), 0);
    s = recordFrame(s, 10);
    s = recordFrame(s, 60);
    const m = hudMetrics(s);
    expect(m.worstFrameMs).toBe(50);
  });
});

describe("readJsHeapBytes", () => {
  it("returns null when the heap probe is absent", () => {
    expect(readJsHeapBytes(undefined)).toBeNull();
    expect(readJsHeapBytes(null)).toBeNull();
  });
  it("returns the used heap size when present", () => {
    expect(readJsHeapBytes({ usedJSHeapSize: 12345 })).toBe(12345);
  });
});

describe("formatBytes", () => {
  it("formats byte magnitudes", () => {
    expect(formatBytes(512)).toBe("512 B");
    expect(formatBytes(1536)).toBe("1.5 KB");
    expect(formatBytes(5 * 1024 * 1024)).toBe("5.0 MB");
  });
});

describe("hudLines", () => {
  it("shows placeholders before any frame and when the heap is unavailable", () => {
    const lines = hudLines(hudMetrics(initHud()), null);
    const byLabel = Object.fromEntries(lines.map((l) => [l.label, l.value]));
    expect(byLabel.FPS).toBe("—");
    expect(byLabel["JS heap"]).toBe("n/a");
  });
  it("renders measured values once sampling has data", () => {
    let s = initHud();
    let ts = 0;
    for (let i = 0; i < 4; i++) { ts += 20; s = recordFrame(s, ts); }
    const lines = hudLines(hudMetrics(s), 2 * 1024 * 1024);
    const byLabel = Object.fromEntries(lines.map((l) => [l.label, l.value]));
    expect(byLabel.FPS).toBe("50");
    expect(byLabel.Frame).toBe("20 ms");
    expect(byLabel["JS heap"]).toBe("2.0 MB");
  });
});
