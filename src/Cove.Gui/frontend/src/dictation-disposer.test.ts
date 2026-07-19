import { afterEach, describe, expect, it, vi } from "vitest";
import { Window } from "happy-dom";
import { setupDictation } from "./dictation";

class FakeWindow {
  readonly listeners = new Map<string, Set<EventListenerOrEventListenerObject>>();

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
  it("returns a handle that removes its window and typed engine listeners", async () => {
    const fakeWindow = new FakeWindow();
    vi.stubGlobal("window", fakeWindow);
    const engineListeners = new Map<string, Set<(data: unknown) => void>>();

    const feature = setupDictation({
      invoke: async () => undefined,
      events: {
        register: (channel, handler) => {
          const listeners = engineListeners.get(channel) ?? new Set<(data: unknown) => void>();
          listeners.add(handler as (data: unknown) => void);
          engineListeners.set(channel, listeners);
          return { dispose: () => { listeners.delete(handler as (data: unknown) => void); } };
        },
      },
      getFocusedNookId: () => null,
      writeNook: async () => undefined,
    });

    expect(fakeWindow.listenerCount("keydown")).toBe(1);
    expect(fakeWindow.listenerCount("keyup")).toBe(1);
    expect(fakeWindow.listenerCount("blur")).toBe(1);
    expect([...engineListeners.values()].map((listeners) => listeners.size)).toEqual([1, 1, 1]);

    await feature.dispose();

    expect(fakeWindow.listenerCount("keydown")).toBe(0);
    expect(fakeWindow.listenerCount("keyup")).toBe(0);
    expect(fakeWindow.listenerCount("blur")).toBe(0);
    expect([...engineListeners.values()].map((listeners) => listeners.size)).toEqual([0, 0, 0]);
  });

  it("removes its pill and cancels status timers on disposal", async () => {
    vi.useFakeTimers();
    const testWindow = new Window();
    vi.stubGlobal("window", testWindow);
    vi.stubGlobal("document", testWindow.document);
    vi.stubGlobal("localStorage", testWindow.localStorage);
    vi.stubGlobal("HTMLInputElement", testWindow.HTMLInputElement);
    vi.stubGlobal("HTMLTextAreaElement", testWindow.HTMLTextAreaElement);
    const handlers = new Map<string, (data: unknown) => void>();
    const feature = setupDictation({
      invoke: async () => undefined,
      events: {
        register: (channel, handler) => {
          handlers.set(channel, handler as (data: unknown) => void);
          return { dispose: () => { handlers.delete(channel); } };
        },
      },
      getFocusedNookId: () => null,
      writeNook: async () => undefined,
    });
    const lateModel = handlers.get("dictation.model")!;

    lateModel({ ready: true });
    expect(document.querySelector(".dictation-pill")).not.toBeNull();
    await feature.dispose();
    lateModel({ error: "late" });
    await vi.advanceTimersByTimeAsync(6000);

    expect(document.querySelector(".dictation-pill")).toBeNull();
    expect(vi.getTimerCount()).toBe(0);
    vi.useRealTimers();
  });
});
