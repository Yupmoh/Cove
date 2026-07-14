import { describe, it, expect } from "vitest";
import { isPaneFittable, shouldResize } from "./terminal-fit";

describe("isPaneFittable", () => {
  it("is fittable when connected, visible, and sized", () => {
    expect(isPaneFittable(140, 35, true, true)).toBe(true);
  });

  it("is not fittable when disconnected", () => {
    expect(isPaneFittable(140, 35, false, true)).toBe(false);
  });

  it("is not fittable when hidden", () => {
    expect(isPaneFittable(140, 35, true, false)).toBe(false);
  });

  it("is not fittable with zero width", () => {
    expect(isPaneFittable(0, 35, true, true)).toBe(false);
  });

  it("is not fittable with zero height", () => {
    expect(isPaneFittable(140, 0, true, true)).toBe(false);
  });
});

describe("shouldResize", () => {
  it("resizes the first time a visible pane is measured", () => {
    expect(shouldResize({ cols: 140, rows: 35 }, null, true)).toBe(true);
  });

  it("does not resize a hidden pane", () => {
    expect(shouldResize({ cols: 140, rows: 35 }, null, false)).toBe(false);
  });

  it("does not resize when dimensions are unchanged", () => {
    expect(shouldResize({ cols: 140, rows: 35 }, { cols: 140, rows: 35 }, true)).toBe(false);
  });

  it("resizes when columns change", () => {
    expect(shouldResize({ cols: 80, rows: 35 }, { cols: 140, rows: 35 }, true)).toBe(true);
  });

  it("resizes when rows change", () => {
    expect(shouldResize({ cols: 140, rows: 24 }, { cols: 140, rows: 35 }, true)).toBe(true);
  });

  it("rejects a degenerate two-by-one collapse", () => {
    expect(shouldResize({ cols: 0, rows: 0 }, { cols: 140, rows: 35 }, true)).toBe(false);
  });
});
