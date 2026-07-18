import { Window } from "happy-dom";
import { describe, expect, it, vi } from "vitest";
import { ActionRegistry, type CoveAction } from "../app/action-registry";
import {
  createActionCoordinatorFeature,
  type ActionCoordinatorDependencies,
} from "./action-coordinator-feature";

describe("ActionCoordinatorFeature", () => {
  it("owns keyboard dispatch, menu synchronization, and disposal", async () => {
    const window = new Window();
    const run = vi.fn();
    const invoke = vi.fn(async (command: string) => {
      if (command === "cove://commands/keybind.list") return { bindings: [] };
      return {};
    });
    const actions = new ActionRegistry<CoveAction>();
    const feature = createActionCoordinatorFeature({
      window: window as unknown as globalThis.Window,
      actions,
      invoke,
      observe: vi.fn(() => () => {}),
      handlers: [["shore.new", run]],
      switchBayByIndex: vi.fn(async () => {}),
      isPaletteOpen: () => false,
      nativeMenuEventsBroken: true,
    } as ActionCoordinatorDependencies);

    feature.start();
    await feature.reloadKeymap();
    window.dispatchEvent(new window.KeyboardEvent("keydown", { metaKey: true, key: "t" }));
    await Promise.resolve();
    expect(run).toHaveBeenCalledOnce();
    expect(invoke).toHaveBeenCalledWith("menubar.setMenu", expect.any(Object));

    await feature.dispose();
    expect(() => actions.register("shore.new", run)).toThrow("ActionRegistry is disposed");
    window.dispatchEvent(new window.KeyboardEvent("keydown", { metaKey: true, key: "t" }));
    await Promise.resolve();
    expect(run).toHaveBeenCalledOnce();
  });
});
