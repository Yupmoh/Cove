import { afterEach, describe, expect, it, vi } from "vitest";
import { setupDictation } from "./dictation";

class FakeWindow {
  readonly listeners = new Map<string, Set<EventListenerOrEventListenerObject>>();
  readonly __ryn = { on: vi.fn() };

  addEventListener(type: string, listener: EventListenerOrEventListenerObject): void {
    const listeners = this.listeners.get(type) ?? new Set<EventListenerOrEventListenerObject>();
    listeners.add(listener);
    this.listeners.set(type, listeners);
  }

  removeEventListener(type: string, listener: EventListenerOrEventListenerObject): void {
    this.listeners.get(type)?.delete(listener);
  }

  listenerCount(type: string): number {
    return this.listeners.get(type)?.size ?? 0;
  }
}

afterEach(() => vi.unstubAllGlobals());

describe("setupDictation", () => {
  it("returns a disposer that removes its window listeners", () => {
    const fakeWindow = new FakeWindow();
    vi.stubGlobal("window", fakeWindow);

    const dispose = setupDictation({
      invoke: async () => undefined,
      getFocusedNookId: () => null,
      writeNook: async () => undefined,
    });

    expect(fakeWindow.listenerCount("keydown")).toBe(1);
    expect(fakeWindow.listenerCount("keyup")).toBe(1);
    expect(fakeWindow.listenerCount("blur")).toBe(1);

    dispose();

    expect(fakeWindow.listenerCount("keydown")).toBe(0);
    expect(fakeWindow.listenerCount("keyup")).toBe(0);
    expect(fakeWindow.listenerCount("blur")).toBe(0);
  });
});
