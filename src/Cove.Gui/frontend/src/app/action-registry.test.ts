import { describe, expect, it, vi } from "vitest";
import { ActionRegistry, parseCoveAction } from "./action-registry";

describe("ActionRegistry", () => {
  it("accepts only registered Cove action identifiers", () => {
    expect(parseCoveAction("shore.new")).toBe("shore.new");
    expect(parseCoveAction("bay.switch-4")).toBe("bay.switch-4");
    expect(parseCoveAction("unknown.action")).toBeNull();
  });

  it("dispatches one typed action path for menus and keys", async () => {
    const registry = new ActionRegistry();
    const run = vi.fn(async () => {});
    registry.register("workspace.new-shore", run);

    await expect(registry.dispatch("workspace.new-shore")).resolves.toBe(true);
    expect(run).toHaveBeenCalledOnce();
  });

  it("returns false for an unknown action", async () => {
    const registry = new ActionRegistry();

    await expect(registry.dispatch("missing")).resolves.toBe(false);
  });

  it("rejects duplicate owners and unregisters through its handle", async () => {
    const registry = new ActionRegistry();
    const handle = registry.register("workspace.close", () => {});

    expect(() => registry.register("workspace.close", () => {})).toThrow("workspace.close");
    handle.dispose();
    await expect(registry.dispatch("workspace.close")).resolves.toBe(false);
  });

  it("clears every action during disposal", async () => {
    const registry = new ActionRegistry();
    registry.register("one", () => {});
    registry.register("two", () => {});

    await registry.dispose();

    await expect(registry.dispatch("one")).resolves.toBe(false);
    expect(() => registry.register("three", () => {})).toThrow("ActionRegistry is disposed");
  });
});
