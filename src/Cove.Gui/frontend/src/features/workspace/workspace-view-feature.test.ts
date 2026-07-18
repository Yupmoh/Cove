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
});
