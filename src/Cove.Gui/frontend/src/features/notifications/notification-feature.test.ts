import { describe, expect, it, vi } from "vitest";
import { EngineEventRouter } from "../../app/engine-event-router";
import { createNotificationFeature } from "./notification-feature";

describe("NotificationFeature", () => {
  it("owns engine delivery, native activation, and disposal", async () => {
    let emitEngine: (data: unknown) => void = () => {};
    const nativeListeners = new Map<string, (data: unknown) => void>();
    const router = new EngineEventRouter((listener) => {
      emitEngine = listener;
      return () => { emitEngine = () => {}; };
    });
    const send = vi.fn();
    async function invoke<T>(): Promise<T> {
      return true as T;
    }
    async function invokeNative<T>(
      command: string,
      args: Record<string, unknown>,
    ): Promise<T> {
      send(command, args);
      return undefined as T;
    }
    const reveal = vi.fn();
    const feature = createNotificationFeature({
      engineEvents: router,
      observe: (event, listener) => {
        nativeListeners.set(event, listener);
        return () => { nativeListeners.delete(event); };
      },
      invoke,
      invokeNative,
      reveal,
      toast: vi.fn(),
      warn: vi.fn(),
    });

    feature.start();
    router.start();
    emitEngine({
      channel: "notification.deliver",
      payload: { id: "notification-1", title: "Done", body: "Ready", nookId: "nook-1" },
    });
    await Promise.resolve();
    await Promise.resolve();
    expect(send).toHaveBeenCalledWith("notification.sendWithId", {
      id: "notification-1",
      title: "Done",
      body: "Ready",
    });

    nativeListeners.get("notification.activated")?.("notification-1");
    expect(reveal).toHaveBeenCalledWith("nook-1");

    await feature.dispose();
    expect(nativeListeners.size).toBe(0);
    await router.dispose();
  });
});
