import { Window } from "happy-dom";
import { describe, expect, it, vi } from "vitest";
import {
  createWorkspaceSidebarFeature,
  type WorkspaceSidebarDependencies,
} from "./workspace-sidebar-feature";

vi.mock("../notepad/notepad-feature", () => ({
  createNotepadFeature: ({ document }: { document: Document }) => ({
    render(container: HTMLElement) {
      const button = document.createElement("button");
      button.textContent = "New note";
      container.replaceChildren(button);
    },
    reload: vi.fn(),
    load: vi.fn(),
    dispose: vi.fn(),
  }),
}));

function fixture() {
  const window = new Window();
  const document = window.document;
  const leftRail = document.createElement("div");
  const leftSidebar = document.createElement("div");
  const leftContent = document.createElement("div");
  leftSidebar.appendChild(leftContent);
  document.body.append(leftRail, leftSidebar);
  const dependencies = {
    document,
    window,

    storage: window.localStorage,
    leftRail,
    leftSidebar,
    leftContent,
    workspace: { snapshot: null, activeShoreId: null, focusedNookId: null },
    workspaceController: { mutate: vi.fn() },
    contextMenu: { openAt: vi.fn() },
    launcherFeature: { open: vi.fn() },
    nooks: new Map(),
    invoke: vi.fn(async () => ({ cards: [], bays: [], notes: [] })),
    reload: vi.fn(async () => {}),
    focusNook: vi.fn(),
    revealNook: vi.fn(),
    spawnNook: vi.fn(async () => ({ nookId: "nook-1" })),
    openFileInEditor: vi.fn(async () => {}),
    openNote: vi.fn(async () => {}),
    showInAppToast: vi.fn(),
    switchBay: vi.fn(async () => {}),
    renderShore: vi.fn(),
    renderShoreTabs: vi.fn(),
    openBayLauncher: vi.fn(async () => {}),
    closeFocused: vi.fn(async () => {}),
    closeShore: vi.fn(async () => {}),
    disposeNook: vi.fn(),
    firstLeafOf: vi.fn(() => null),
    collectLeafIds: vi.fn(() => []),
    shoreTabName: vi.fn(() => ""),
    reorderShores: vi.fn(async () => {}),
    newBay: vi.fn(async () => {}),
    newShore: vi.fn(async () => {}),
    syncTitlebarWorkspaceOffset: vi.fn(),
    fitAll: vi.fn(),
  } as unknown as WorkspaceSidebarDependencies;
  return { window, document, leftSidebar, leftContent, dependencies };
}

describe("WorkspaceSidebarFeature", () => {
  it("owns sidebar mode rendering and resize listener disposal", async () => {
    const { window, document, leftSidebar, leftContent, dependencies } = fixture();
    const feature = createWorkspaceSidebarFeature(dependencies);
    const handle = document.createElement("div");

    feature.reveal("notepad");
    expect(leftSidebar.classList.contains("collapsed")).toBe(false);
    expect(leftContent.textContent).toContain("New note");

    feature.wireResize(handle as unknown as HTMLElement, "left");
    vi.mocked(dependencies.fitAll).mockClear();
    handle.dispatchEvent(new window.MouseEvent("mousedown", { clientX: 100 }));
    document.dispatchEvent(new window.MouseEvent("mousemove", { clientX: 120 }));
    const fitCallsBeforeDispose = vi.mocked(dependencies.fitAll).mock.calls.length;
    expect(handle.classList.contains("dragging")).toBe(true);
    await feature.dispose();
    document.dispatchEvent(new window.MouseEvent("mousemove", { clientX: 140 }));
    const fitCallsAfterDispose = vi.mocked(dependencies.fitAll).mock.calls.length;
    const draggingAfterDispose = handle.classList.contains("dragging");
    document.dispatchEvent(new window.MouseEvent("mouseup"));

    expect(draggingAfterDispose).toBe(false);
    expect(fitCallsAfterDispose).toBe(fitCallsBeforeDispose);
    handle.dispatchEvent(new window.MouseEvent("mousedown"));
    expect(handle.classList.contains("dragging")).toBe(false);
  });
});
