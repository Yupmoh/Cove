import { Window } from "happy-dom";
import { describe, expect, it, vi } from "vitest";
import { FrontendCommand } from "../../app/frontend-command";
import { createPaletteFeature, type PaletteFeatureDependencies } from "./palette-feature";

function fixture() {
  const window = new Window();
  const root = window.document.createElement("div");
  const input = window.document.createElement("input");
  const list = window.document.createElement("div");
  root.append(input, list);
  window.document.body.appendChild(root);
  const dispatchAction = vi.fn();
  const invoke = vi.fn(async <T>(
    _command: FrontendCommand,
    _args: unknown,
  ) => ({ bays: [], cards: [], matches: [] } as T));
  const dependencies: PaletteFeatureDependencies = {
    document: window.document as unknown as Document,
    storage: window.localStorage as unknown as Storage,
    root: root as unknown as HTMLElement,
    input: input as unknown as HTMLInputElement,
    list: list as unknown as HTMLElement,
    commandActions: () => [{ kind: "action", label: "Run", icon: ">", action: "shore.new" }],
    shoreActions: () => [],
    nooks: () => [],
    invoke: invoke as PaletteFeatureDependencies["invoke"],
    switchBay: vi.fn(),
    openTask: vi.fn(),
    openFile: vi.fn(),
    splitActive: vi.fn(),
    focusActiveNook: vi.fn(),
    dispatchAction,
  };
  return { window, root, input, dispatchAction, invoke, feature: createPaletteFeature(dependencies) };
}

describe("PaletteFeature", () => {
  it("routes command actions through the shared action dispatcher", async () => {
    const { window, root, input, dispatchAction, feature } = fixture();

    feature.open();
    await vi.waitFor(() => expect(root.querySelector(".pal-item")).not.toBeNull());
    input.dispatchEvent(new window.KeyboardEvent("keydown", { key: "Enter" }));
    expect(dispatchAction).toHaveBeenCalledExactlyOnceWith("shore.new");
    expect(root.classList.contains("open")).toBe(false);

    feature.open();
    input.dispatchEvent(new window.KeyboardEvent("keydown", { key: "Escape" }));
    expect(root.classList.contains("open")).toBe(false);

    await feature.dispose();
    root.classList.add("open");
    input.dispatchEvent(new window.KeyboardEvent("keydown", { key: "Escape" }));
    expect(root.classList.contains("open")).toBe(true);
  });

  it("loads palette tasks from the default scope", async () => {
    const { feature, invoke } = fixture();

    feature.open();

    await vi.waitFor(() => {
      expect(invoke).toHaveBeenCalledWith(FrontendCommand.TaskList, { bayId: "default" });
    });
    await feature.dispose();
  });

  it("searches palette files in the default scope", async () => {
    const { window, feature, input, invoke } = fixture();

    feature.open();
    input.value = "/readme";
    input.dispatchEvent(new window.Event("input"));

    await vi.waitFor(() => {
      expect(invoke).toHaveBeenCalledWith(FrontendCommand.SearchQuery, {
        query: "readme",
        bayId: "default",
      });
    });
    await feature.dispose();
  });

  it("rejects a debounced file search from an earlier open generation", async () => {
    vi.useFakeTimers();
    const { window, feature, input, invoke } = fixture();

    try {
      feature.open();
      input.value = "/readme";
      input.dispatchEvent(new window.Event("input"));
      feature.close();
      feature.open();

      await vi.advanceTimersByTimeAsync(200);
      expect(invoke.mock.calls.filter(([command]) => command === FrontendCommand.SearchQuery))
        .toHaveLength(0);

      input.value = "/readme";
      input.dispatchEvent(new window.Event("input"));
      await vi.advanceTimersByTimeAsync(200);
      expect(invoke.mock.calls.filter(([command]) => command === FrontendCommand.SearchQuery))
        .toEqual([[
          FrontendCommand.SearchQuery,
          { query: "readme", bayId: "default" },
        ]]);
    } finally {
      await feature.dispose();
      vi.useRealTimers();
    }
  });
});
