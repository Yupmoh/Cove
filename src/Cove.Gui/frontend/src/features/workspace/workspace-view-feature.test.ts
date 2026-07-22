import { Window } from "happy-dom";
import { describe, expect, it, vi } from "vitest";
import { WorkspaceStore } from "../../workspace/workspace-store";
import { renderNotepadNook } from "../../notepad-nook";
import {
  createWorkspaceViewFeature,
  type WorkspaceViewDependencies,
} from "./workspace-view-feature";

vi.mock("../../browser-nook", () => ({
  renderBrowserNook: vi.fn(() => new Promise<HTMLElement>(() => {})),
  reconcileBrowserBounds: vi.fn(),
}));
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
    invoke.mockClear();
    await feature.spawn({
      command: "",
      args: [],
      shellCommand: "npm install -g example",
      cwd: "",
      inheritCwdFrom: "",
    });
    expect(invoke).toHaveBeenCalledWith("app.nookSpawn", {
      command: "",
      args: [],
      shellCommand: "npm install -g example",
      cwd: "",
      inheritCwdFrom: "",
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
    const openingNook = grid.querySelector(".nook") as unknown as HTMLElement;
    expect(openingNook.classList.contains("nook-opening")).toBe(true);
    openingNook.dispatchEvent(new window.Event("animationend") as unknown as Event);
    expect(openingNook.classList.contains("nook-opening")).toBe(false);
    vi.spyOn(openingNook, "getBoundingClientRect")
      .mockReturnValueOnce({ left: 18, top: 24, right: 218, bottom: 224, width: 200, height: 200, x: 18, y: 24, toJSON: () => ({}) } as DOMRect)
      .mockReturnValueOnce({ left: 118, top: 64, right: 318, bottom: 264, width: 200, height: 200, x: 118, y: 64, toJSON: () => ({}) } as DOMRect);
    feature.render();
    const repositionedNook = grid.querySelector(".nook") as unknown as HTMLElement;
    expect(repositionedNook.classList.contains("nook-opening")).toBe(false);
    expect(repositionedNook.classList.contains("nook-repositioning")).toBe(true);
    expect(repositionedNook.style.getPropertyValue("--nook-shift-x")).toBe("-100px");
    expect(repositionedNook.style.getPropertyValue("--nook-shift-y")).toBe("-40px");
    const terminalHost = grid.querySelector(".term-host") as unknown as HTMLElement;
    const xterm = document.createElement("div") as unknown as HTMLElement;
    xterm.className = "xterm";
    const viewport = document.createElement("div") as unknown as HTMLElement;
    viewport.className = "xterm-viewport";
    xterm.appendChild(viewport);
    terminalHost.appendChild(xterm);
    const xtermMouseDown = vi.fn();
    xterm.addEventListener("mousedown", xtermMouseDown);
    const terminalFocus = vi.mocked(feature.nooks.get("nook-1")!.session.term.focus);
    terminalFocus.mockClear();
    viewport.dispatchEvent(new window.MouseEvent("mousedown", { bubbles: true }) as unknown as Event);
    expect(terminalFocus).not.toHaveBeenCalled();
    expect(xtermMouseDown).not.toHaveBeenCalled();
    terminalHost.dispatchEvent(new window.MouseEvent("mousedown", { bubbles: true }) as unknown as Event);
    expect(terminalFocus).toHaveBeenCalledOnce();
    mutate.mockClear();
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

  it("gives browser nook titles terminal-equivalent drag and drop behavior", () => {
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
      focusedNookId: "browser-1",
      shores: [{
        id: "shore-1",
        name: "Browser",
        zoomedNookId: null,
        layoutTree: {
          kind: "leaf",
          nookId: "browser-1",
          subtabs: [{
            documentId: "browser-1",
            nookType: "browser",
            title: "https://example.com",
          }],
          activeSubtab: 0,
        },
      }],
    });
    const nookDrag = { nookId: null as string | null };
    const paintDropOverlay = vi.fn();
    const clearDropOverlay = vi.fn();
    const applyNookMove = vi.fn(async () => {});
    const dependencies = {
      document,
      window,
      grid,
      shoreTabs: document.createElement("div"),
      leftSidebar: document.createElement("div"),
      workspace,
      workspaceController: { mutate: vi.fn(async () => ({})) },
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
      openSplitChooser: vi.fn(),
      closeNookById: vi.fn(async () => {}),
      closeFocused: vi.fn(async () => {}),
      closeOthers: vi.fn(async () => {}),
      paintDropOverlay,
      clearDropOverlay,
      applyNookMove,
      newShore: vi.fn(),
      openFileInEditor: vi.fn(async () => {}),
    } as unknown as WorkspaceViewDependencies;
    const feature = createWorkspaceViewFeature(dependencies);

    feature.render();
    const browserNook = grid.querySelector(".tool-nook") as unknown as HTMLElement;
    const header = browserNook.querySelector(".nook-header") as unknown as HTMLElement;
    const title = header.querySelector(".pt") as unknown as HTMLElement;
    const closeButton = header.querySelector(".pclose") as unknown as HTMLButtonElement;
    const content = browserNook.querySelector(".browser-nook-placeholder") as unknown as HTMLElement;

    expect(title.draggable).toBe(true);
    expect(closeButton.hasAttribute("draggable")).toBe(false);
    expect(content.hasAttribute("draggable")).toBe(false);

    const setData = vi.fn();
    const titleDrag = new window.Event("dragstart", { bubbles: true, cancelable: true });
    Object.defineProperty(titleDrag, "dataTransfer", {
      value: { setData, effectAllowed: "" },
    });
    title.dispatchEvent(titleDrag as unknown as Event);

    expect(setData).toHaveBeenCalledExactlyOnceWith("text/cove-nook", "browser-1");
    expect(nookDrag.nookId).toBe("browser-1");

    nookDrag.nookId = "nook-source";
    vi.spyOn(browserNook, "getBoundingClientRect").mockReturnValue({
      left: 0,
      top: 0,
      right: 200,
      bottom: 200,
      width: 200,
      height: 200,
      x: 0,
      y: 0,
      toJSON: () => ({}),
    } as DOMRect);
    const dataTransfer = {
      getData: vi.fn(() => "nook-source"),
      dropEffect: "",
    };
    const dragOver = new window.Event("dragover", { bubbles: true, cancelable: true });
    Object.defineProperties(dragOver, {
      clientX: { value: 1 },
      clientY: { value: 100 },
      dataTransfer: { value: dataTransfer },
    });
    browserNook.dispatchEvent(dragOver as unknown as Event);
    expect(dragOver.defaultPrevented).toBe(true);
    expect(paintDropOverlay).toHaveBeenCalledOnce();

    const drop = new window.Event("drop", { bubbles: true, cancelable: true });
    Object.defineProperties(drop, {
      clientX: { value: 1 },
      clientY: { value: 100 },
      dataTransfer: { value: dataTransfer },
    });
    browserNook.dispatchEvent(drop as unknown as Event);

    expect(applyNookMove).toHaveBeenCalledExactlyOnceWith({
      op: "moveNook",
      nookId: "nook-source",
      targetNookId: "browser-1",
      orientation: "row",
      dir: -1,
    }, "nook-source");
    expect(nookDrag.nookId).toBeNull();
    expect(clearDropOverlay).toHaveBeenCalled();
  });
});
