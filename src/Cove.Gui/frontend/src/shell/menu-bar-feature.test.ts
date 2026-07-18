import { describe, expect, it, vi } from "vitest";
import { MenuBarFeature } from "./menu-bar-feature";

describe("MenuBarFeature", () => {
  it("routes native menu identifiers and unsubscribes on disposal", async () => {
    const subscription: { listener: ((data: unknown) => void) | null } = { listener: null };
    const unsubscribe = vi.fn();
    const runAction = vi.fn();
    const feature = new MenuBarFeature({
      invoke: vi.fn(async () => {}),
      observe: (_event, callback) => {
        subscription.listener = callback;
        return unsubscribe;
      },
      actionChords: () => [{ action: "app.settings", chord: "Cmd+," }],
      runAction,
      nativeEventsBroken: false,
    });

    feature.start();
    await Promise.resolve();
    subscription.listener?.("settings");
    expect(runAction).toHaveBeenCalledWith("app.settings");

    await feature.dispose();
    expect(unsubscribe).toHaveBeenCalledOnce();
  });
});
