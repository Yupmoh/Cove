import { partitionPinned, reorderShore, visibleShoreIds, buildWingModel, filterShoresByWing } from "../../shore-tabs";
import { iconForNookType, iconSvg } from "../../icons";
import type { WorkspaceStore, MosaicNode, ShoreSnapshot } from "../../workspace/workspace-store";
import type { WorkspaceController } from "../../workspace/workspace-controller";
import type { ContextMenuHost } from "../../shell/context-menu-host";
import type { TreeLeaf } from "../../bay-tree";
import { LifecycleScope, type ComponentHandle } from "../../app/lifecycle";
import { FrontendCommand } from "../../app/frontend-command";

export interface NookDragState {
  nookId: string | null;
}

interface ShoreTabNookView {
  readonly title: string;
  readonly customTitle: string;
}

export interface ShoreTabsDependencies {
  document: Document;
  window: Window;
  storage: Storage;
  root: HTMLElement;
  row: HTMLElement;
  workspace: WorkspaceStore;
  workspaceController: WorkspaceController;
  contextMenu: ContextMenuHost;
  nooks: ReadonlyMap<string, ShoreTabNookView>;
  nookDrag: NookDragState;
  invoke<T>(command: FrontendCommand, args: Record<string, unknown>): Promise<T>;
  renderShore(): void;
  focusNook(nookId: string): void;
  clearDropOverlay(): void;
  moveNookToShore(nookId: string, shoreId: string): Promise<void>;
  newShore(): void | Promise<void>;
  closeShore(shoreId: string): Promise<void>;
  firstLeafOf(shore: ShoreSnapshot): string | null | undefined;
  collectLeafIds(node: MosaicNode | null): string[];
  renderSidebar(): void;
  renderSidebarContent(): void;
  revealBays(): void;
  shoreLeaves(shore: ShoreSnapshot): TreeLeaf[];
}

export interface ShoreTabsFeature extends ComponentHandle {
  overviewVisible: boolean;
  render(): void;
  loadWings(): Promise<void>;
  tabName(shore: ShoreSnapshot): string;
  nextName(): string;
  reorder(fromId: string, toId: string): Promise<void>;
  setActiveWing(wingId: string): void;
  toggleActivePin(): void;
}

export function createShoreTabsFeature(dependencies: ShoreTabsDependencies): ShoreTabsFeature {
  const lifecycle = new LifecycleScope();
  const document = dependencies.document;
  const window = dependencies.window;
  const localStorage = dependencies.storage;
  const shoreTabsEl = dependencies.root;
  const shoresRowEl = dependencies.row;
  const workspace = dependencies.workspace;
  const workspaceController = dependencies.workspaceController;
  const contextMenu = dependencies.contextMenu;
  const nooks = dependencies.nooks;
  const nookDrag = dependencies.nookDrag;
  const invoke = dependencies.invoke;
  const renderShore = dependencies.renderShore;
  const focusNook = dependencies.focusNook;
  const clearDropOverlay = dependencies.clearDropOverlay;
  const moveNookToShore = dependencies.moveNookToShore;
  const newShore = dependencies.newShore;
  const closeShore = dependencies.closeShore;
  const firstLeafOf = dependencies.firstLeafOf;
  const collectLeafIds = dependencies.collectLeafIds;
  const workspaceSidebar = {
    render: dependencies.renderSidebar,
    renderContent: (_side: string) => dependencies.renderSidebarContent(),
    reveal: (_mode: string) => dependencies.revealBays(),
    shoreLeaves: dependencies.shoreLeaves,
  };
  let bayOverviewVisible = false;
  let tabSpringTimer: number | null = null;
  let tabSpringShoreId: string | null = null;

const pinnedShoreIds = new Set<string>(JSON.parse(localStorage.getItem("cove.pinnedShores") ?? "[]"));

function savePinnedShores(): void { localStorage.setItem("cove.pinnedShores", JSON.stringify([...pinnedShoreIds])); }

interface WingInfo { id: string; name: string; }

let wings: WingInfo[] = [];

let activeWingId: string | null = "main";

let wingSwitcherExpanded = false;

async function loadWings(): Promise<void> {
  const wsId = workspace.snapshot?.id;
  if (!wsId) {
    console.warn("wing load skipped without an active bay");
    wings = [{ id: "main", name: "main" }];
    return;
  }
  try {
    const res = await invoke<{ wings: { id: string; name: string }[] }>(FrontendCommand.WingList, { bayId: wsId });
    wings = res.wings ?? [{ id: "main", name: "main" }];
  } catch (error) {
    console.warn("wing list failed", { bayId: wsId, error });
    wings = [{ id: "main", name: "main" }];
  }
}

async function switchWingActive(wingId: string): Promise<void> {
  const bayId = workspace.snapshot?.id;
  if (!bayId) {
    console.warn("wing switch skipped without an active bay", wingId);
    return;
  }
  activeWingId = wingId;
  try {
    await invoke(FrontendCommand.WingSwitch, { bayId, wingId });
  } catch (error) {
    console.warn("wing switch failed", { bayId, wingId, error });
  }
  await loadWings();
  renderShoreTabs();
}

function shoreTabName(shore: ShoreSnapshot): string {
  if (shore.name.trim().length > 0) return shore.name;
  const leaves = collectLeafIds(shore.layoutTree);
  const first = leaves[0] ? nooks.get(leaves[0]) : undefined;
  return (first && first.title) || "Shore";
}

function nextShoreName(): string {
  return "Shore " + ((workspace.snapshot?.shores.length ?? 0) + 1);
}

function renderShoreTabs(): void {
  const activeRename = shoreTabsEl.querySelector(".rtab-rename-input");
  if (activeRename && activeRename === document.activeElement) return;
  shoreTabsEl.innerHTML = "";
  const allShores = workspace.snapshot?.shores ?? [];
  const liveShoreWingSummaries = allShores.map((shore) => ({
    id: shore.id,
    wingId: shore.wingId ?? "main",
    pinned: shore.pinned ?? false,
  }));
  const wingModel = buildWingModel(wings, liveShoreWingSummaries, activeWingId);
  const visibleIds = visibleShoreIds(wingModel);
  const shores = visibleIds.length > 0 || wings.length > 1 ? filterShoresByWing(allShores, visibleIds) : allShores;
  if (shores.length === 0) { shoreTabsEl.style.display = "none"; shoresRowEl.style.display = "none"; return; }
  shoresRowEl.style.display = "flex";
  shoreTabsEl.style.display = "flex";

  const { pinned, unpinned } = partitionPinned(shores.map((r) => ({ id: r.id, name: r.name, pinned: pinnedShoreIds.has(r.id) })));
  const shoreMap = new Map(shores.map((r) => [r.id, r]));

  let dragSrcId: string | null = null;

  const makeTab = (shoreId: string): HTMLElement => {
    const shore = shoreMap.get(shoreId);
    if (!shore) return document.createElement("div");
    const isPinned = pinnedShoreIds.has(shoreId);
    const tab = document.createElement("div");
    tab.className = "rtab" + (shoreId === workspace.activeShoreId ? " active" : "") + (isPinned ? " pinned" : "");
    tab.draggable = false;
    tab.title = shoreTabName(shore);

    const glyph = document.createElement("span");
    glyph.className = "rtab-glyph";
    glyph.innerHTML = iconForNookType(workspaceSidebar.shoreLeaves(shore)[0]?.nookType ?? "terminal");
    glyph.draggable = true;
    glyph.title = "Drag to reorder";
    tab.appendChild(glyph);

    const nameEl = document.createElement("span");
    nameEl.className = "rtab-name";
    nameEl.textContent = shoreTabName(shore);
    tab.appendChild(nameEl);

    const closeEl = document.createElement("span");
    closeEl.className = "rtab-close";
    closeEl.innerHTML = "&times;";
    closeEl.title = "Close";
    tab.appendChild(closeEl);

    let clickCount = 0;
    tab.addEventListener("click", (e) => {
      if ((e.target as HTMLElement).classList.contains("rtab-close")) {
        if (isPinned) return;
        void closeShore(shoreId);
        return;
      }
      if (bayOverviewVisible) {
        bayOverviewVisible = false;
        workspace.activeShoreId = shoreId;
        const f = firstLeafOf(shore);
        if (f) workspace.focusedNookId = f;
        renderShore();
        renderShoreTabs();
        workspaceSidebar.render();
        if (f) focusNook(f);
        return;
      }
      if (shoreId === workspace.activeShoreId) {
        clickCount++;
        if (clickCount >= 2) {
          startRename(shoreId, nameEl);
          clickCount = 0;
        } else {
          setTimeout(() => { clickCount = 0; }, 400);
        }
      } else {
        workspace.activeShoreId = shoreId;
        const f = firstLeafOf(shore);
        if (f) workspace.focusedNookId = f;
        renderShore();
        renderShoreTabs();
        workspaceSidebar.render();
        if (f) focusNook(f);
      }
    });
    tab.addEventListener("contextmenu", (e) => {
      const pinned = pinnedShoreIds.has(shoreId);
      contextMenu.openAt(e, [
        { id: "rename", label: "Rename" },
        { id: "pin", label: pinned ? "Unpin" : "Pin" },
        { id: "sep", label: "", separator: true },
        { id: "close", label: "Close", danger: true, disabled: pinned },
        { id: "close-others", label: "Close Others" },
      ], (id) => {
        if (id === "rename") startRename(shoreId, tab.querySelector(".rtab-name") as HTMLElement);
        else if (id === "pin") { if (pinned) pinnedShoreIds.delete(shoreId); else pinnedShoreIds.add(shoreId); savePinnedShores(); renderShoreTabs(); }
        else if (id === "close") void closeShore(shoreId);
        else if (id === "close-others") void closeOtherShores(shoreId);
      });
    });
    tab.addEventListener("dragstart", () => { dragSrcId = shoreId; tab.classList.add("dragging"); });
    tab.addEventListener("dragend", () => { tab.classList.remove("dragging"); dragSrcId = null; });
    tab.addEventListener("dragover", (e) => {
      e.preventDefault();
      if (nookDrag.nookId) {
        tab.classList.add("nook-drop-target");
        if (shoreId !== workspace.activeShoreId && tabSpringShoreId !== shoreId) {
          if (tabSpringTimer !== null) window.clearTimeout(tabSpringTimer);
          tabSpringShoreId = shoreId;
          tabSpringTimer = window.setTimeout(() => {
            tabSpringTimer = null;
            tabSpringShoreId = null;
            if (!nookDrag.nookId) return;
            workspace.activeShoreId = shoreId;
            renderShore();
            renderShoreTabs();
            workspaceSidebar.render();
          }, 550);
        }
        return;
      }
      tab.classList.add("drag-over");
    });
    tab.addEventListener("dragleave", () => {
      tab.classList.remove("drag-over");
      tab.classList.remove("nook-drop-target");
      if (tabSpringShoreId === shoreId && tabSpringTimer !== null) {
        window.clearTimeout(tabSpringTimer);
        tabSpringTimer = null;
        tabSpringShoreId = null;
      }
    });
    tab.addEventListener("drop", (e) => {
      e.preventDefault();
      tab.classList.remove("drag-over");
      tab.classList.remove("nook-drop-target");
      if (tabSpringTimer !== null) { window.clearTimeout(tabSpringTimer); tabSpringTimer = null; tabSpringShoreId = null; }
      const nookSrc = e.dataTransfer?.getData("text/cove-nook") || nookDrag.nookId;
      if (nookSrc) {
        nookDrag.nookId = null;
        clearDropOverlay();
        void moveNookToShore(nookSrc, shoreId);
        return;
      }
      if (dragSrcId && dragSrcId !== shoreId) {
        void reorderShores(dragSrcId, shoreId);
      }
    });
    return tab;
  };

  const homeBtn = document.createElement("div");
  homeBtn.className = "rbox-ctl rbox-home" + (bayOverviewVisible ? " active" : "");
  homeBtn.innerHTML = iconSvg("home");
  homeBtn.title = "Bay launcher";
  homeBtn.addEventListener("click", () => {
    bayOverviewVisible = true;
    workspaceSidebar.reveal("bays");
    renderShore();
    renderShoreTabs();
  });
  shoreTabsEl.appendChild(homeBtn);

  for (const id of pinned) shoreTabsEl.appendChild(makeTab(id));
  if (pinned.length > 0 && unpinned.length > 0) {
    const divider = document.createElement("div");
    divider.className = "rtab-divider";
    shoreTabsEl.appendChild(divider);
  }
  for (const id of unpinned) shoreTabsEl.appendChild(makeTab(id));

  const addBtn = document.createElement("div");
  addBtn.className = "rbox-ctl rbox-add";
  addBtn.innerHTML = iconSvg("plus");
  addBtn.title = "New shore (Cmd T)";
  addBtn.addEventListener("click", () => void newShore());
  shoreTabsEl.appendChild(addBtn);

  if (wings.length > 1 || wingSwitcherExpanded) {
    const switcher = document.createElement("div");
    switcher.style.marginLeft = "auto";
    switcher.id = "wing-switcher";
    if (!wingSwitcherExpanded) {
      const toggle = document.createElement("div");
      toggle.className = "wing-btn";
      toggle.textContent = "\u27e8";
      toggle.title = "Wings";
      toggle.addEventListener("click", () => { wingSwitcherExpanded = true; renderShoreTabs(); });
      switcher.appendChild(toggle);
    } else {
      for (const wing of wings) {
        const btn = document.createElement("div");
        btn.className = "wing-btn" + (wing.id === activeWingId ? " active" : "");
        btn.textContent = wing.name;
        btn.addEventListener("click", () => void switchWingActive(wing.id));
        switcher.appendChild(btn);
      }
      const collapse = document.createElement("div");
      collapse.className = "wing-btn";
      collapse.textContent = "\u27e9";
      collapse.title = "Collapse wings";
      collapse.addEventListener("click", () => { wingSwitcherExpanded = false; renderShoreTabs(); });
      switcher.appendChild(collapse);
    }
    shoreTabsEl.appendChild(switcher);
  }

  updateEdgeFade();
}

function updateEdgeFade(): void {
  shoreTabsEl.classList.remove("edge-fade-left", "edge-fade-right");
  if (shoreTabsEl.scrollWidth > shoreTabsEl.clientWidth) {
    if (shoreTabsEl.scrollLeft > 2) shoreTabsEl.classList.add("edge-fade-left");
    if (shoreTabsEl.scrollLeft + shoreTabsEl.clientWidth < shoreTabsEl.scrollWidth - 2) shoreTabsEl.classList.add("edge-fade-right");
  }
}

lifecycle.listen(shoreTabsEl, "scroll", updateEdgeFade);

async function reorderShores(fromId: string, toId: string): Promise<void> {
  if (!workspace.snapshot) return;
  const ids = workspace.snapshot.shores.map((r) => r.id);
  const fromIdx = ids.indexOf(fromId);
  const toIdx = ids.indexOf(toId);
  if (fromIdx < 0 || toIdx < 0) return;
  const reordered = reorderShore(workspace.snapshot.shores, fromIdx, toIdx);
  const newOrder = reordered.map((r) => r.id);
  workspace.reorderShores(newOrder);
  renderShoreTabs();
  workspaceSidebar.renderContent("left");
  try {
    await workspaceController.mutate("reorder", { shoreIds: newOrder, shoreId: "", targetNookId: "", newNookId: "", orientation: "", name: "", nookId: "", dir: 0 });
  } catch (err) { console.warn("shore reorder failed", err); }
}

function startRename(shoreId: string, nameEl: HTMLElement): void {
  const shore = workspace.snapshot?.shores.find((r) => r.id === shoreId);
  if (!shore) return;
  const input = document.createElement("input");
  input.className = "rtab-rename-input";
  input.value = shoreTabName(shore);
  input.spellcheck = false;
  nameEl.replaceWith(input);
  input.focus();
  input.select();
  const commit = async () => {
    const newName = input.value.trim() || shore.name;
    if (newName !== shore.name) {
      shore.name = newName;
      try {
        await workspaceController.mutate("rename", { shoreId, name: newName, nookId: "", targetNookId: "", newNookId: "", orientation: "", dir: 0 });
        return;
      } catch (err) { console.warn("shore rename failed", shoreId, err); }
    }
    renderShoreTabs();
    workspaceSidebar.render();
  };
  input.addEventListener("blur", commit);
  input.addEventListener("keydown", (e) => {
    e.stopPropagation();
    if (e.key === "Enter") input.blur();
    if (e.key === "Escape") { input.value = shore.name; input.blur(); }
  });
}

async function closeOtherShores(keepShoreId: string): Promise<void> {
  if (!workspace.snapshot) return;
  const toClose = workspace.snapshot.shores.filter((r) => r.id !== keepShoreId);
  for (const shore of toClose) {
    await closeShore(shore.id);
  }
}

  return {
    get overviewVisible() { return bayOverviewVisible; },
    set overviewVisible(value: boolean) { bayOverviewVisible = value; },
    render: renderShoreTabs,
    loadWings,
    tabName: shoreTabName,
    nextName: nextShoreName,
    reorder: reorderShores,
    setActiveWing(wingId: string) { activeWingId = wingId; },
    toggleActivePin() {
      const shoreId = workspace.activeShoreId;
      if (!shoreId) {
        console.warn("pin requested with no active shore");
        return;
      }
      if (pinnedShoreIds.has(shoreId)) pinnedShoreIds.delete(shoreId);
      else pinnedShoreIds.add(shoreId);
      savePinnedShores();
      renderShoreTabs();
    },
    async dispose() {
      if (tabSpringTimer !== null) window.clearTimeout(tabSpringTimer);
      tabSpringTimer = null;
      tabSpringShoreId = null;
      await lifecycle.dispose();
    },
  };
}
