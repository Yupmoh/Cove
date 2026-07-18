import { Window } from "happy-dom";
import { describe, expect, it, vi } from "vitest";
import { PerformanceHudFeature } from "./performance-hud-feature";

describe("PerformanceHudFeature", () => {
  it("owns frame scheduling and cancels it on disposal", () => {
    const window = new Window();
    const root = window.document.createElement("div");
    const cancelFrame = vi.fn();
    const feature = new PerformanceHudFeature({
      document: window.document as unknown as Document,
      root: root as unknown as HTMLElement,
      readHeap: () => null,
      requestFrame: vi.fn(() => 7),
      cancelFrame,
      onToggled: vi.fn(),
    });

    feature.toggle();
    expect(feature.enabled).toBe(true);
    expect(root.classList.contains("open")).toBe(true);
    feature.dispose();
    expect(cancelFrame).toHaveBeenCalledWith(7);
    expect(root.classList.contains("open")).toBe(false);
  });
});
