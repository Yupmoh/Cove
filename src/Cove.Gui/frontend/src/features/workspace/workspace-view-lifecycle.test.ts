import { Window } from "happy-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { WorkspaceStore } from "../../workspace/workspace-store";
import type { NookContentHandle } from "../../app/lifecycle";
import { createWorkspaceViewFeature, type WorkspaceViewDependencies } from "./workspace-view-feature";
import { renderEditorNook } from "../../editor-nook";

vi.mock("../../editor-nook", () => ({ renderEditorNook: vi.fn() }));
vi.mock("../../monaco-loader", () => ({ MonacoLoader: { load: vi.fn() }, detectLanguage: vi.fn(), defineCoveMonacoTheme: vi.fn() }));
vi.mock("../../terminal-resources", () => ({ createTerminalResources: vi.fn() }));

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((done) => { resolve = done; });
  return { promise, resolve };
}

function createHarness(testWindow: Window) {
  const document = testWindow.document;
  const workspace = new WorkspaceStore();
  const grid = document.createElement("div");
  document.body.appendChild(grid);
  let overviewVisible = false;
  const dependencies = {
    document,
    window: testWindow,
    grid,
    shoreTabs: document.createElement("div"),
    leftSidebar: document.createElement("div"),
    workspace,
    workspaceController: { mutate: vi.fn(async () => ({})) },
    contextMenu: { openAt: vi.fn() },
    findFeature: { open: vi.fn() },
    settings: { fontFamily: "Mono", fontSize: 13, lineHeight: 1.4, letterSpacing: 0, cursorStyle: "block", cursorBlink: true, scrollback: 1000, padding: 8, backgroundOpacity: 1 },
    nookDrag: { nookId: null },
    invoke: vi.fn(async () => ({})),
    currentTermTheme: () => ({}),
    reload: vi.fn(async () => undefined),
    renderLauncher: () => document.createElement("div"),
    invalidateLauncherRecents: vi.fn(),
    refreshLauncherRecents: vi.fn(async () => undefined),
    launcherAdapters: () => [],
    launcherYolo: () => false,
    renderSidebar: vi.fn(),
    renderSidebarContent: vi.fn(),
    isSidebarModeVisible: () => false,
    rememberNookTitle: vi.fn(),
    acknowledgeAgentAttention: vi.fn(),
    syncAgentNookStateClasses: vi.fn(),
    sidebarBayBoxes: () => [],
    sidebarDefaultDirectory: () => "",
    renderShoreTabs: vi.fn(),
    shoreTabName: () => "",
    getOverviewVisible: () => overviewVisible,
    setOverviewVisible: (value: boolean) => { overviewVisible = value; },
    showInAppToast: vi.fn(),
    revealNook: vi.fn(),
    runAction: vi.fn(),
    openSplitChooser: vi.fn(),
    closeNookById: vi.fn(async () => undefined),
    closeFocused: vi.fn(async () => undefined),
    closeOthers: vi.fn(async () => undefined),
    paintDropOverlay: vi.fn(),
    clearDropOverlay: vi.fn(),
    applyNookMove: vi.fn(async () => undefined),
    newShore: vi.fn(),
    openFileInEditor: vi.fn(async () => undefined),
  } as unknown as WorkspaceViewDependencies;
  return { workspace, grid, dependencies, setOverview: (value: boolean) => { overviewVisible = value; } };
}

function editorLeaf(id: string) {
  return { kind: "leaf" as const, nookId: id, subtabs: [{ documentId: id, nookType: "editor", title: id }], activeSubtab: 0 };
}

describe("WorkspaceViewFeature child ownership", () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it("reuses unchanged pending content and disposes a stale completion exactly once", async () => {
    const testWindow = new Window();
    vi.stubGlobal("window", testWindow);
    vi.stubGlobal("document", testWindow.document);
    const pending = deferred<NookContentHandle>();
    vi.mocked(renderEditorNook).mockReturnValue(pending.promise);
    const harness = createHarness(testWindow);
    harness.workspace.applySnapshot({ schemaVersion: 1, id: "bay", name: "Bay", projectDir: "/repo", activeShoreId: "shore", focusedNookId: "editor", shores: [{ id: "shore", name: "Shore", zoomedNookId: null, layoutTree: editorLeaf("editor") }] });
    const feature = createWorkspaceViewFeature(harness.dependencies);

    feature.render();
    const firstPlaceholder = harness.grid.firstElementChild;
    feature.render();
    expect(renderEditorNook).toHaveBeenCalledTimes(1);
    expect(harness.grid.firstElementChild).toBe(firstPlaceholder);

    harness.setOverview(true);
    feature.render();
    const element = document.createElement("div");
    element.dataset.renderer = "stale";
    const dispose = vi.fn();
    pending.resolve({ element, dispose });
    await Promise.resolve();
    await Promise.resolve();

    expect(dispose).toHaveBeenCalledTimes(1);
    expect(document.querySelector("[data-renderer=stale]")).toBeNull();
    await feature.dispose();
    expect(dispose).toHaveBeenCalledTimes(1);
  });

  it("removes document split-drag handlers before rerender", async () => {
    const testWindow = new Window();
    vi.stubGlobal("window", testWindow);
    vi.stubGlobal("document", testWindow.document);
    vi.mocked(renderEditorNook).mockImplementation(async (id) => ({ element: document.createElement("div"), dispose: vi.fn() }));
    const harness = createHarness(testWindow);
    harness.workspace.applySnapshot({ schemaVersion: 1, id: "bay", name: "Bay", projectDir: "/repo", activeShoreId: "shore", focusedNookId: "a", shores: [{ id: "shore", name: "Shore", zoomedNookId: null, layoutTree: { kind: "split", orientation: "horizontal", ratio: 0.5, childA: editorLeaf("a"), childB: editorLeaf("b") } }] });
    const remove = vi.spyOn(document, "removeEventListener");
    const feature = createWorkspaceViewFeature(harness.dependencies);
    feature.render();
    const divider = harness.grid.querySelector(".divider") as unknown as HTMLElement;
    divider.dispatchEvent(new testWindow.MouseEvent("mousedown", { bubbles: true, clientX: 10 }) as unknown as Event);

    harness.setOverview(true);
    feature.render();
    document.dispatchEvent(new testWindow.MouseEvent("mousemove", { clientX: 100 }) as unknown as Event);

    expect(remove.mock.calls.filter(([type]) => type === "mousemove")).toHaveLength(1);
    expect(remove.mock.calls.filter(([type]) => type === "mouseup")).toHaveLength(1);
    await feature.dispose();
  });
});
