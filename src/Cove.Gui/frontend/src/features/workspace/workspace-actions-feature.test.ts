import { Window } from "happy-dom";
import { describe, expect, it, vi } from "vitest";
import { WorkspaceStore, type BaySnapshot } from "../../workspace/workspace-store";
import { WorkspaceController } from "../../workspace/workspace-controller";
import {
  createWorkspaceActionsFeature,
  type WorkspaceActionsDependencies,
} from "./workspace-actions-feature";

function snapshot(): BaySnapshot {
  return {
    schemaVersion: 1,
    id: "bay-1",
    name: "Bay",
    projectDir: "/repo",
    activeShoreId: "shore-1",
    focusedNookId: "nook-1",
    shores: [{
      id: "shore-1",
      name: "One",
      zoomedNookId: null,
      layoutTree: {
        kind: "leaf",
        nookId: "nook-1",
        subtabs: [{ documentId: "nook-1", nookType: "terminal", title: "Shell" }],
        activeSubtab: 0,
      },
    }],
  };
}

describe("WorkspaceActionsFeature", () => {
  it("owns canonical reload and rejects replacement of live nooks", async () => {
    const window = new Window();
    const workspace = new WorkspaceStore();
    const render = vi.fn();
    const refreshRecents = vi.fn(async () => {});
    const invoke = vi.fn(async (command: string) => {
      if (command === "app.layoutGet") return snapshot();
      if (command === "app.nookList") return { nooks: [] };
      return {};
    });
    const dependencies = {
      document: window.document,
      workspace,
      workspaceController: { mutate: vi.fn() },
      workspaceView: {
        nooks: new Map(),
        render,
        refreshTitles: vi.fn(),
      },
      shoreTabsFeature: { render: vi.fn(), setActiveWing: vi.fn() },
      workspaceSidebar: { render: vi.fn() },
      launcherFeature: { refreshRecents },
      invoke,
      runAction: vi.fn(),
    } as unknown as WorkspaceActionsDependencies;
    const feature = createWorkspaceActionsFeature(dependencies);

    await feature.reload();
    expect(workspace.snapshot?.id).toBe("bay-1");
    expect(render).toHaveBeenCalledOnce();
    expect(refreshRecents).not.toHaveBeenCalled();
    expect(feature.safeReplaceTarget("shore-1", "nook-1")).toBeNull();

    feature.dispose();
  });

  it("moves a nook through the registered daemon operation", async () => {
    const window = new Window();
    const workspace = new WorkspaceStore();
    const state = snapshot();
    workspace.applySnapshot(state);
    const mutate = vi.fn(async () => ({}));
    const movedNook = window.document.createElement("div");
    const dependencies = {
      document: window.document,
      workspace,
      workspaceController: {
        mutate,
        transaction: vi.fn(async (work: () => Promise<unknown>) => work()),
      },
      workspaceView: {
        collectLeafIds: () => ["nook-1"],
        activeShore: () => state.shores[0],
        focus: vi.fn(),
        nooks: new Map([["nook-1", { el: movedNook }]]),
      },
      shoreTabsFeature: { render: vi.fn(), setActiveWing: vi.fn() },
      workspaceSidebar: { render: vi.fn() },
      launcherFeature: { refreshRecents: vi.fn(async () => {}) },
      invoke: vi.fn(async () => ({})),
      runAction: vi.fn(),
    } as unknown as WorkspaceActionsDependencies;
    const feature = createWorkspaceActionsFeature(dependencies);

    await feature.applyNookMove({
      op: "moveNook",
      nookId: "nook-1",
      targetNookId: "nook-2",
      orientation: "row",
      dir: 1,
    }, "nook-1");

    expect(mutate).toHaveBeenCalledExactlyOnceWith("moveNook", {
      shoreId: "shore-1",
      targetNookId: "nook-2",
      nookId: "nook-1",
      orientation: "row",
      dir: 1,
      newNookId: "",
      name: "",
    });
    expect(movedNook.classList.contains("nook-drop-settled")).toBe(true);
  });

  it("animates a drop preview only when it enters the host", () => {
    const window = new Window();
    const workspace = new WorkspaceStore();
    const host = window.document.createElement("div");
    const dependencies = {
      document: window.document,
      workspace,
      workspaceController: { mutate: vi.fn() },
      workspaceView: {},
      shoreTabsFeature: {},
      workspaceSidebar: {},
      launcherFeature: {},
      invoke: vi.fn(),
      runAction: vi.fn(),
    } as unknown as WorkspaceActionsDependencies;
    const feature = createWorkspaceActionsFeature(dependencies);

    feature.paintDropOverlay(host as unknown as HTMLElement, { kind: "center" });
    const overlay = host.querySelector(".drop-overlay");
    expect(overlay?.classList.contains("drop-overlay-entering")).toBe(true);

    overlay?.classList.remove("drop-overlay-entering");
    feature.paintDropOverlay(host as unknown as HTMLElement, { kind: "center" });
    expect(overlay?.classList.contains("drop-overlay-entering")).toBe(false);

  });

  it("closes native browser ownership through every nook close entry point", async () => {
    const window = new Window();
    const workspace = new WorkspaceStore();
    const state = snapshot();
    workspace.applySnapshot(state);
    const closeBrowserNook = vi.fn(async () => {});
    const invoke = vi.fn(async (command: string) => {
      if (command === "app.layoutGet") return state;
      if (command === "app.nookList") return { nooks: [] };
      return {};
    });
    const dependencies = {
      document: window.document,
      workspace,
      workspaceController: {
        mutate: vi.fn(async () => ({})),
        transaction: vi.fn(async (work: () => Promise<unknown>) => work()),
      },
      workspaceView: {
        nooks: new Map(),
        render: vi.fn(),
        refreshTitles: vi.fn(),
        disposeNook: vi.fn(),
        focus: vi.fn(),
        activeShore: () => state.shores[0],
        collectLeafIds: () => ["nook-1", "nook-2"],
      },
      shoreTabsFeature: { render: vi.fn(), setActiveWing: vi.fn() },
      workspaceSidebar: { render: vi.fn() },
      launcherFeature: { refreshRecents: vi.fn(async () => {}) },
      invoke,
      runAction: vi.fn(),
      closeBrowserNook,
      reconcileBrowserNooks: vi.fn(),
    } as unknown as WorkspaceActionsDependencies;
    const feature = createWorkspaceActionsFeature(dependencies);

    await feature.closeFocused();
    expect(closeBrowserNook).toHaveBeenCalledExactlyOnceWith("nook-1");

    closeBrowserNook.mockClear();
    await feature.closeOthers("nook-1");
    expect(closeBrowserNook).toHaveBeenCalledExactlyOnceWith("nook-2");
    expect(dependencies.workspaceController.transaction).toHaveBeenCalledTimes(2);
  });

  it("does not send PTY kill for a browser-owned nook", async () => {
    const window = new Window();
    const workspace = new WorkspaceStore();
    const state = snapshot();
    const leaf = state.shores[0].layoutTree;
    if (leaf.kind !== "leaf") throw new Error("expected leaf");
    leaf.subtabs[0].nookType = "browser";
    workspace.applySnapshot(state);
    const closeBrowserNook = vi.fn(async () => {});
    const invoke = vi.fn(async (command: string) => {
      if (command === "app.layoutGet") return state;
      if (command === "app.nookList") return { nooks: [] };
      return {};
    });
    const dependencies = {
      document: window.document,
      workspace,
      workspaceController: {
        mutate: vi.fn(async () => ({})),
        transaction: vi.fn(async (work: () => Promise<unknown>) => work()),
      },
      workspaceView: {
        nooks: new Map(),
        render: vi.fn(),
        refreshTitles: vi.fn(),
        disposeNook: vi.fn(),
        activeShore: () => state.shores[0],
        collectLeafIds: () => ["nook-1"],
      },
      shoreTabsFeature: { render: vi.fn(), setActiveWing: vi.fn() },
      workspaceSidebar: { render: vi.fn() },
      launcherFeature: { refreshRecents: vi.fn(async () => {}) },
      invoke,
      runAction: vi.fn(),
      closeBrowserNook,
      reconcileBrowserNooks: vi.fn(),
    } as unknown as WorkspaceActionsDependencies;
    const feature = createWorkspaceActionsFeature(dependencies);

    await feature.closeFocused();

    expect(closeBrowserNook).toHaveBeenCalledExactlyOnceWith("nook-1");
    expect(invoke).not.toHaveBeenCalledWith("app.nookKill", { nookId: "nook-1" });
  });

  it("reveals a subtab before its persisted mutation completes", async () => {
    const window = new Window();
    const workspace = new WorkspaceStore();
    const state = snapshot();
    const leaf = state.shores[0].layoutTree;
    if (leaf.kind !== "leaf") throw new Error("expected leaf");
    leaf.subtabs.push({ documentId: "nook-2", nookType: "terminal", title: "Two" });
    workspace.applySnapshot(state);
    let releaseActivation = (): void => {};
    const activation = new Promise<void>((resolve) => {
      releaseActivation = resolve;
    });
    const mutationOps: string[] = [];
    const workspaceController = new WorkspaceController(async (_command, args) => {
      const operation = (args as { op: string }).op;
      mutationOps.push(operation);
      if (operation === "activateSubtab") await activation;
      if (operation === "createShore") return { shoreId: "shore-2" };
      return {};
    });
    const mutate = vi.spyOn(workspaceController, "mutate");
    const dependencies = {
      document: window.document,
      workspace,
      workspaceController,
      workspaceView: {
        nooks: new Map(),
        render: vi.fn(),
        refreshTitles: vi.fn(),
        focus: vi.fn(),
        findNookLocation: vi.fn(() => ({ leaf, subtabIndex: 1 })),
      },
      shoreTabsFeature: {
        render: vi.fn(),
        setActiveWing: vi.fn(),
        nextName: vi.fn(() => "Two"),
        overviewVisible: false,
      },
      workspaceSidebar: {
        render: vi.fn(),
        acknowledgeAgentAttention: vi.fn(),
      },
      launcherFeature: { refreshRecents: vi.fn(async () => {}) },
      invoke: vi.fn(async (command: string) => {
        if (command === "app.layoutGet") return state;
        if (command === "app.nookList") return { nooks: [] };
        return {};
      }),
      runAction: vi.fn(),
    } as unknown as WorkspaceActionsDependencies;
    const feature = createWorkspaceActionsFeature(dependencies);

    const reveal = feature.revealNook("nook-2");
    await vi.waitFor(() => expect(mutationOps).toEqual(["activateSubtab"]));
    expect(dependencies.workspaceSidebar.acknowledgeAgentAttention)
      .toHaveBeenCalledExactlyOnceWith("nook-2");
    expect(dependencies.workspaceView.focus).toHaveBeenCalledExactlyOnceWith("nook-2");
    await reveal;
    const activationMutation = mutate.mock.results[0]?.value;
    if (!(activationMutation instanceof Promise)) throw new Error("subtab mutation was not started");
    releaseActivation();
    await activationMutation;
    expect(dependencies.invoke).not.toHaveBeenCalledWith("app.layoutGet", {});
    const create = feature.newShore();
    await create;
    expect(mutationOps).toEqual(["activateSubtab", "createShore"]);
    expect(dependencies.invoke).not.toHaveBeenCalledWith("app.layoutGet", {});
  });
});
