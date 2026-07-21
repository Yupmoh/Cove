import { Window } from "happy-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createSurfaceMotion } from "./surface-motion";

afterEach(() => {
  vi.useRealTimers();
});

describe("SurfaceMotion", () => {
  it("keeps a closing surface rendered until its finite exit completes", () => {
    vi.useFakeTimers();
    const window = new Window();
    const root = window.document.createElement("div") as unknown as HTMLElement;
    const motion = createSurfaceMotion(root, 140);

    motion.open();
    motion.close();

    expect(root.classList.contains("open")).toBe(false);
    expect(root.classList.contains("closing")).toBe(true);
    vi.advanceTimersByTime(139);
    expect(root.classList.contains("closing")).toBe(true);
    vi.advanceTimersByTime(1);
    expect(root.classList.contains("closing")).toBe(false);
  });

  it("cancels a pending exit when the surface reopens", () => {
    vi.useFakeTimers();
    const window = new Window();
    const root = window.document.createElement("div") as unknown as HTMLElement;
    const motion = createSurfaceMotion(root, 140);

    motion.open();
    motion.close();
    motion.open();
    vi.runAllTimers();

    expect(root.classList.contains("open")).toBe(true);
    expect(root.classList.contains("closing")).toBe(false);
  });

  it("finishes an exit on the root animation event and disposes without motion", () => {
    vi.useFakeTimers();
    const window = new Window();
    const root = window.document.createElement("div") as unknown as HTMLElement;
    const motion = createSurfaceMotion(root, 140);

    motion.open();
    motion.close();
    root.dispatchEvent(new window.AnimationEvent("animationend", { bubbles: true }) as unknown as Event);
    expect(root.classList.contains("closing")).toBe(false);

    motion.open();
    motion.dispose();
    expect(root.classList.contains("open")).toBe(false);
    expect(root.classList.contains("closing")).toBe(false);
  });
});
