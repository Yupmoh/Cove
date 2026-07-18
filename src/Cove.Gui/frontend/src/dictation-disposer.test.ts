import { afterEach, describe, expect, it, vi } from "vitest";
import { setupDictation } from "./dictation";

class FakeWindow {
  readonly listeners = new Map<string, Set<EventListenerOrEventListenerObject>>();
  readonly bridgeListeners = new Map<string, Set<(data: unknown) => void>>();
  readonly __ryn = {
    on: (event: string, callback: (data: unknown) => void): void => {
      const callbacks = this.bridgeListeners.get(event) ?? new Set<(data: unknown) => void>();
      callbacks.add(callback);
      this.bridgeListeners.set(event, callbacks);
    },
    off: (event: string, callback: (data: unknown) => void): void => {
      this.bridgeListeners.get(event)?.delete(callback);
    },
  };

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

  bridgeListenerCount(event: string): number {
    return this.bridgeListeners.get(event)?.size ?? 0;
  }
}

afterEach(() => vi.unstubAllGlobals());

describe("setupDictation", () => {
  it("returns a disposer that removes its window and bridge listeners", () => {
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
    expect(fakeWindow.bridgeListenerCount("engine.event")).toBe(1);

    dispose();

    expect(fakeWindow.listenerCount("keydown")).toBe(0);
    expect(fakeWindow.listenerCount("keyup")).toBe(0);
    expect(fakeWindow.listenerCount("blur")).toBe(0);
    expect(fakeWindow.bridgeListenerCount("engine.event")).toBe(0);
  });
});
