import { Window } from "happy-dom";
import { describe, expect, it, vi } from "vitest";
import { WorkspaceStore, type BaySnapshot } from "../../workspace/workspace-store";
import { createShoreTabsFeature, type ShoreTabsDependencies } from "./shore-tabs-feature";

function snapshot(): BaySnapshot {
  return {
    schemaVersion: 1,
    id: "bay-1",
    name: "Bay",
    projectDir: "/repo",
    activeShoreId: "shore-1",
    focusedNookId: "nook-1",
    shores: [
      {
        id: "shore-1",
        name: "One",
        zoomedNookId: null,
        layoutTree: { kind: "leaf", nookId: "nook-1", subtabs: [], activeSubtab: 0 },
      },
      {
        id: "shore-2",
        name: "Two",
        zoomedNookId: null,
        layoutTree: { kind: "leaf", nookId: "nook-2", subtabs: [], activeSubtab: 0 },
      },
    ],
  };
}

describe("ShoreTabsFeature", () => {
  it("owns shore tab rendering and launcher navigation state", async () => {
    const window = new Window();
    const root = window.document.createElement("div");
    const row = window.document.createElement("div");
    const workspace = new WorkspaceStore();
    workspace.applySnapshot(snapshot());
    const renderShore = vi.fn();
    const revealBays = vi.fn();
    const dependencies = {
      document: window.document,
      window,
      storage: window.localStorage,
      root,
      row,
      workspace,
      workspaceController: { mutate: vi.fn(async () => ({})) },
      contextMenu: { openAt: vi.fn() },
      nooks: new Map(),
      nookDrag: { nookId: null },
      invoke: vi.fn(async (command: string) => command === "cove://commands/wing.list"
        ? { wings: [{ id: "main", name: "main" }] }
        : {
          shores: [
            { id: "shore-1", wingId: "main", pinned: false },
            { id: "shore-2", wingId: "main", pinned: false },
          ],
        }),
      reload: vi.fn(async () => {}),
      renderShore,
      focusNook: vi.fn(),
      clearDropOverlay: vi.fn(),
      moveNookToShore: vi.fn(async () => {}),
      newShore: vi.fn(),
      closeShore: vi.fn(async () => {}),
      firstLeafOf: vi.fn(() => "nook-1"),
      collectLeafIds: vi.fn(() => ["nook-1"]),
      renderSidebar: vi.fn(),
      renderSidebarContent: vi.fn(),
      revealBays,
      shoreLeaves: vi.fn(() => []),
    } as unknown as ShoreTabsDependencies;
    const feature = createShoreTabsFeature(dependencies);

    feature.render();
    expect(root.querySelectorAll(".rtab")).toHaveLength(2);
    (root.querySelector(".rbox-home") as unknown as HTMLElement).click();
    expect(feature.overviewVisible).toBe(true);
    expect(revealBays).toHaveBeenCalledOnce();
    expect(renderShore).toHaveBeenCalledOnce();

    await feature.loadWings();
    expect(dependencies.invoke).toHaveBeenCalledWith(
      "cove://commands/wing.list",
      { bayId: "bay-1" },
    );
    const updated = snapshot();
    updated.activeShoreId = "shore-3";
    updated.shores.push({
      id: "shore-3",
      name: "Three",
      zoomedNookId: null,
      wingId: "main",
      layoutTree: { kind: "leaf", nookId: "nook-3", subtabs: [], activeSubtab: 0 },
    });
    workspace.applySnapshot(updated);
    feature.render();
    expect(root.querySelectorAll(".rtab")).toHaveLength(3);

    vi.mocked(dependencies.invoke).mockClear();
    workspace.snapshot = null;
    await feature.loadWings();
    expect(dependencies.invoke).not.toHaveBeenCalled();

    await feature.dispose();
  });
});
