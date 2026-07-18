import { describe, expect, it, vi } from "vitest";
import { EngineEventRouter } from "./engine-event-router";

describe("EngineEventRouter", () => {
  it("routes one typed subscription and disposes it", async () => {
    let listener: ((data: unknown) => void) | null = null;
    const unsubscribe = vi.fn();
    const handler = vi.fn();
    const router = new EngineEventRouter((next) => {
      listener = next;
      return unsubscribe;
    });
    router.register("config.changed", handler);

    router.start();
    listener!({ channel: "config.changed", payload: { key: "terminal.fontSize" } });

    expect(handler).toHaveBeenCalledWith({ key: "terminal.fontSize" });
    await router.dispose();
    expect(unsubscribe).toHaveBeenCalledOnce();
  });

  it("registers every consumer before the event source can emit", () => {
    const handler = vi.fn();
    const router = new EngineEventRouter((listener) => {
      listener({ channel: "notification.deliver", payload: { id: "notice-1" } });
      return () => {};
    });

    router.start(() => {
      router.register("notification.deliver", handler);
    });

    expect(handler).toHaveBeenCalledExactlyOnceWith({ id: "notice-1" });
  });
});
