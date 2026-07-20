import { Window } from "happy-dom";
import { describe, expect, it, vi } from "vitest";
import { WorkspaceStore } from "../../workspace/workspace-store";
import { renderNotepadNook } from "../../notepad-nook";
import {
  createWorkspaceViewFeature,
  type WorkspaceViewDependencies,
} from "./workspace-view-feature";

vi.mock("../../editor-nook", () => ({
  renderEditorNook: vi.fn(async () => document.createElement("div")),
}));
vi.mock("../../diff-viewer-nook", () => ({
  renderDiffViewerNook: vi.fn(async () => document.createElement("div")),
}));
vi.mock("../../markdown-nook", () => ({
  renderMarkdownNook: vi.fn(async () => document.createElement("div")),
}));
vi.mock("../../notepad-nook", () => ({
  renderNotepadNook: vi.fn(async () => document.createElement("div")),
}));
vi.mock("../../terminal-resources", () => ({
  createTerminalResources: vi.fn(),
}));
vi.mock("../../terminal-session", () => ({
  TerminalSession: class {
    readonly term = {
      clear: vi.fn(),
      focus: vi.fn(),
      getSelection: vi.fn(() => ""),
      hasSelection: vi.fn(() => false),
    };
    readonly connected = false;
    readonly socketClosed = true;
    applySettings(): void {}
    captureViewport(): void {}
    connect(): void {}
    dispose(): void {}
    pause(): void {}
    scheduleFit(): void {}
  },
}));

describe("WorkspaceViewFeature", () => {
  it("owns workspace rendering, spawning, and disposal", async () => {
    const window = new Window();
    const document = window.document;
    const grid = document.createElement("div");
    const launcher = document.createElement("div");
    launcher.dataset.owner = "launcher";
    const invoke = vi.fn(async (command: string) => (
      command === "app.nookSpawn" ? { nookId: "nook-1" } : {}
    ));
    let overviewVisible = true;
    const dependencies = {
      document,
      window,
      grid,
      shoreTabs: document.createElement("div"),
      leftSidebar: document.createElement("div"),
      workspace: new WorkspaceStore(),
      workspaceController: { mutate: vi.fn() },
      contextMenu: { openAt: vi.fn() },
      findFeature: { open: vi.fn() },
      settings: {
        fontFamily: "Mono",
        fontSize: 13,
        lineHeight: 1.4,
        letterSpacing: 0,
        cursorStyle: "block",
        cursorBlink: true,
        scrollback: 1000,
        padding: 8,
        backgroundOpacity: 1,
      },
      nookDrag: { nookId: null },
      invoke,
      currentTermTheme: () => ({}),
      reload: vi.fn(async () => {}),
      renderLauncher: () => launcher,
      invalidateLauncherRecents: vi.fn(),
      refreshLauncherRecents: vi.fn(async () => {}),
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
      setOverviewVisible: vi.fn(),
      showInAppToast: vi.fn(),
      revealNook: vi.fn(),
      runAction: vi.fn(),
      openSplitChooser: vi.fn(),
      closeNookById: vi.fn(async () => {}),
      closeFocused: vi.fn(async () => {}),
      closeOthers: vi.fn(async () => {}),
      paintDropOverlay: vi.fn(),
      clearDropOverlay: vi.fn(),
      applyNookMove: vi.fn(async () => {}),
      newShore: vi.fn(),
      openFileInEditor: vi.fn(async () => {}),
    } as unknown as WorkspaceViewDependencies;
    const feature = createWorkspaceViewFeature(dependencies);

    feature.render();
    expect(grid.firstElementChild).toBe(launcher);
    await expect(feature.spawn({ command: "" })).resolves.toEqual({ nookId: "nook-1" });
    expect(invoke).toHaveBeenCalledWith("app.nookSpawn", { command: "", cwd: "" });

    vi.mocked(renderNotepadNook).mockResolvedValue(
      document.createElement("div") as unknown as HTMLElement,
    );
    dependencies.workspace.applySnapshot({
      schemaVersion: 1,
      id: "bay-42",
      name: "Bay",
      projectDir: "/repo",
      activeShoreId: "shore-1",
      focusedNookId: "note-1",
      shores: [{
        id: "shore-1",
        name: "Notes",
        zoomedNookId: null,
        layoutTree: {
          kind: "leaf",
          nookId: "note-1",
          subtabs: [{
            documentId: "note-1",
            nookType: "notepad",
            title: "Notes",
          }],
          activeSubtab: 0,
        },
      }],
    });
    overviewVisible = false;
    feature.render();
    await vi.waitFor(() =>
      expect(renderNotepadNook).toHaveBeenCalledExactlyOnceWith("bay-42")
    );

    await feature.dispose();
    expect(feature.nooks.size).toBe(0);
  });

  it("keeps split controls clickable without starting a nook drag", () => {
    const window = new Window();
    const document = window.document;
    const grid = document.createElement("div");
    const workspace = new WorkspaceStore();
    workspace.applySnapshot({
      schemaVersion: 1,
      id: "bay-42",
      name: "Bay",
      projectDir: "/repo",
      activeShoreId: "shore-1",
      focusedNookId: "nook-1",
      shores: [{
        id: "shore-1",
        name: "Shell",
        zoomedNookId: null,
        layoutTree: {
          kind: "leaf",
          nookId: "nook-1",
          subtabs: [{
            documentId: "nook-1",
            nookType: "terminal",
            title: "Shell",
          }],
          activeSubtab: 0,
        },
      }],
    });
    const nookDrag = { nookId: null as string | null };
    const mutate = vi.fn(async () => ({}));
    const openSplitChooser = vi.fn();
    const dependencies = {
      document,
      window,
      grid,
      shoreTabs: document.createElement("div"),
      leftSidebar: document.createElement("div"),
      workspace,
      workspaceController: { mutate },
      contextMenu: { openAt: vi.fn() },
      findFeature: { open: vi.fn() },
      settings: {
        fontFamily: "Mono",
        fontSize: 13,
        lineHeight: 1.4,
        letterSpacing: 0,
        cursorStyle: "block",
        cursorBlink: true,
        scrollback: 1000,
        padding: 8,
        backgroundOpacity: 1,
      },
      nookDrag,
      invoke: vi.fn(async () => ({})),
      currentTermTheme: () => ({}),
      renderLauncher: () => document.createElement("div"),
      invalidateLauncherRecents: vi.fn(),
      refreshLauncherRecents: vi.fn(async () => {}),
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
      getOverviewVisible: () => false,
      setOverviewVisible: vi.fn(),
      showInAppToast: vi.fn(),
      revealNook: vi.fn(),
      runAction: vi.fn(),
      openSplitChooser,
      closeNookById: vi.fn(async () => {}),
      closeFocused: vi.fn(async () => {}),
      closeOthers: vi.fn(async () => {}),
      paintDropOverlay: vi.fn(),
      clearDropOverlay: vi.fn(),
      applyNookMove: vi.fn(async () => {}),
      newShore: vi.fn(),
      openFileInEditor: vi.fn(async () => {}),
    } as unknown as WorkspaceViewDependencies;
    const feature = createWorkspaceViewFeature(dependencies);

    feature.render();
    const header = grid.querySelector(".nook-header") as unknown as HTMLElement;
    const split = grid.querySelector(".psplit") as unknown as HTMLButtonElement;
    const splitIcon = split.querySelector("svg") ?? split;
    const controlDrag = new window.Event("dragstart", { bubbles: true, cancelable: true });
    splitIcon.dispatchEvent(controlDrag as unknown as Event);

    expect(controlDrag.defaultPrevented).toBe(true);
    expect(nookDrag.nookId).toBeNull();

    splitIcon.dispatchEvent(new window.MouseEvent("mousedown", { bubbles: true }) as unknown as Event);
    expect(mutate).not.toHaveBeenCalled();

    split.dispatchEvent(new window.MouseEvent("click", { bubbles: true }) as unknown as Event);
    expect(openSplitChooser).toHaveBeenCalledExactlyOnceWith(expect.anything(), "row");
    expect(mutate).toHaveBeenCalledExactlyOnceWith("focus", expect.objectContaining({
      nookId: "nook-1",
    }));

    const title = header.querySelector(".pt") as unknown as HTMLElement;
    const setData = vi.fn();
    const titleDrag = new window.Event("dragstart", { bubbles: true, cancelable: true });
    Object.defineProperty(titleDrag, "dataTransfer", {
      value: { setData, effectAllowed: "" },
    });
    title.dispatchEvent(titleDrag as unknown as Event);

    expect(setData).toHaveBeenCalledExactlyOnceWith("text/cove-nook", "nook-1");
    expect(nookDrag.nookId).toBe("nook-1");
  });
});
