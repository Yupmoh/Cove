import { createCoalescer } from "../../refresh-coalescer";
import { initialSidebarModel, selectLeftMode, toggleSide, setCollapsed, setWidth, collapsedOf, widthOf, SIDEBAR_MODES, SIDEBAR_RAIL_MODES, SIDEBAR_MODE_META, type SidebarModel, type SidebarSide, type SidebarMode } from "../../sidebar-model";
import { nextBayName, type BayBoxInput } from "../../bay-boxes";
import { buildBayTree, bayTreeEmptyMessage, NOOK_TYPE_LABELS, type TreeLeaf, type TreeShoreInput, type TreeRow } from "../../bay-tree";
import { buildAgentRows, mapAgentState, agentCardsEqual, AGENT_STATE_META, type AgentCard, type AgentState } from "../../agents-model";
import { resolveActiveBayId, bayAccent, sortFsEntries, joinPath, mergeFsStatus, scmChipText, parseCollapsedCardIds, serializeCollapsedCardIds, toggleCardCollapsed, type FsEntry, type FsStatusEntry, type BayCardEntry, type ScmSummary } from "../../bay-cards";
import { BAY_ICON_CHOICES, bayGlyph } from "../../bay-icons";
import { buildEmptyState } from "../../empty-states";
import { detectChimes, playChime, chimesEnabledFrom, chimePrefValue, AGENT_CHIMES_STORAGE_KEY } from "../../chime";
import { adapterIconSvg, fileIcon, iconSvg, iconForNookType } from "../../icons";
import { clampMenuPosition } from "../../context-menu";
import { closeBrowserWebview } from "../../browser-nook";
import { createNotepadFeature } from "../notepad/notepad-feature";
import type { WorkspaceStore, BaySnapshot, MosaicNode, ShoreSnapshot } from "../../workspace/workspace-store";
import type { WorkspaceController } from "../../workspace/workspace-controller";
import type { ContextMenuHost } from "../../shell/context-menu-host";
import type { LauncherFeature } from "../launcher/launcher-feature";
import { LifecycleScope, type ComponentHandle } from "../../app/lifecycle";
import { FrontendCommand } from "../../app/frontend-command";

interface SidebarNookView {
  readonly el: HTMLElement;
  readonly title: string;
  readonly customTitle: string;
}

export interface WorkspaceSidebarDependencies {
  document: Document;
  window: Window;
  storage: Storage;
  leftRail: HTMLElement;
  leftSidebar: HTMLElement;
  leftContent: HTMLElement;
  workspace: WorkspaceStore;
  workspaceController: WorkspaceController;
  contextMenu: ContextMenuHost;
  launcherFeature: LauncherFeature;
  nooks: ReadonlyMap<string, SidebarNookView>;
  invoke<T>(command: FrontendCommand, args: Record<string, unknown>): Promise<T>;
  focusNook(nookId: string): void;
  revealNook(nookId: string): void;
  spawnNook(input: Record<string, unknown>): Promise<{ nookId: string }>;
  openFileInEditor(path: string): Promise<void>;
  openNote(noteId: string, bayId: string): Promise<void>;
  showInAppToast(title: string, body: string, action?: () => void): void;
  switchBay(bayId: string, targetShoreId?: string | null, targetNookId?: string | null, showLauncher?: boolean): Promise<void>;
  renderShore(): void;
  renderShoreTabs(): void;
  openBayLauncher(bayId: string): Promise<void>;
  closeFocused(): Promise<void>;
  closeShore(shoreId: string): Promise<void>;
  disposeNook(nookId: string): void;
  firstLeafOf(shore: ShoreSnapshot): string | null | undefined;
  collectLeafIds(node: MosaicNode | null): string[];
  shoreTabName(shore: ShoreSnapshot): string;
  reorderShores(sourceId: string, targetId: string): Promise<void>;
  newBay(): void | Promise<void>;
  newShore(): Promise<void>;
  syncTitlebarWorkspaceOffset(): void;
  fitAll(): void;
}

export interface WorkspaceSidebarFeature extends ComponentHandle {
  readonly model: SidebarModel;
  readonly bayBoxes: BayBoxInput[];
  readonly defaultDirectory: string;
  readonly needsInputCount: number;
  render(): void;
  renderContent(side: SidebarSide): void;
  applyModel(): void;
  toggleLeft(): void;
  reveal(mode: SidebarMode): void;
  rememberNookTitle(nookId: string, title: string): void;
  acknowledgeAgentAttention(nookId: string): void;
  syncAgentNookStateClasses(): void;
  buildBayIconGrid(selected: string | null, onSelect: (icon: string | null) => void): HTMLElement;
  closeBayIconPopover(): void;
  agentChimesEnabled(): boolean;
  setAgentChimesEnabled(enabled: boolean): void;
  refreshAgents(): Promise<void>;
  agentsVisible(): boolean;
  loadBayBoxes(): Promise<void>;
  setDefaultDirectory(value: string): void;
  loadModel(): Promise<void>;
  wireResize(handle: HTMLElement, side: SidebarSide): void;
  startAgentPolling(): void;
  isModeVisible(mode: SidebarMode): boolean;
  setChromeVisibility(leftHidden: boolean, rightHidden: boolean): void;
  addNeedsInput(nookId: string): void;
  removeNeedsInput(nookId: string): void;
  clearNeedsInput(): void;
  shoreLeaves(shore: ShoreSnapshot): TreeLeaf[];
}

export function createWorkspaceSidebarFeature(dependencies: WorkspaceSidebarDependencies): WorkspaceSidebarFeature {
  const lifecycle = new LifecycleScope();
  const document = dependencies.document;
  const window = dependencies.window;
  const localStorage = dependencies.storage;
  const leftRailEl = dependencies.leftRail;
  const leftSidebarEl = dependencies.leftSidebar;
  const leftContentEl = dependencies.leftContent;
  const workspace = dependencies.workspace;
  const workspaceController = dependencies.workspaceController;
  const contextMenu = dependencies.contextMenu;
  const launcherFeature = dependencies.launcherFeature;
  const nooks = dependencies.nooks;
  const invoke = dependencies.invoke;
  const focusNook = dependencies.focusNook;
  const revealNook = dependencies.revealNook;
  const spawnNook = dependencies.spawnNook;
  const openFileInEditor = dependencies.openFileInEditor;
  const openNote = dependencies.openNote;
  const showInAppToast = dependencies.showInAppToast;
  const switchBay = dependencies.switchBay;
  const renderShore = dependencies.renderShore;
  const renderShoreTabs = dependencies.renderShoreTabs;
  const openBayLauncher = dependencies.openBayLauncher;
  const closeFocused = dependencies.closeFocused;
  const closeShore = dependencies.closeShore;
  const disposeNook = dependencies.disposeNook;
  const firstLeafOf = dependencies.firstLeafOf;
  const collectLeafIds = dependencies.collectLeafIds;
  const shoreTabName = dependencies.shoreTabName;
  const reorderShores = dependencies.reorderShores;
  const newBay = dependencies.newBay;
  const newShore = dependencies.newShore;
  const syncTitlebarWorkspaceOffset = dependencies.syncTitlebarWorkspaceOffset;
  const fitAll = dependencies.fitAll;

let sidebarModel: SidebarModel = initialSidebarModel();

const sidebarScrollOffsets = new Map<SidebarMode, number>();

const collapsedTreeShores = new Set<string>(JSON.parse(localStorage.getItem("cove.tree.collapsedShores") ?? "[]"));

let agentCards: AgentCard[] = [];

const acknowledgedDoneNooks = new Set<string>();

const needsInputNooks = new Set<string>();

let agentPollTimer: ReturnType<typeof setInterval> | null = null;

let activeResizeCleanup: (() => void) | null = null;

  lifecycle.own(() => {
    activeResizeCleanup?.();
    activeResizeCleanup = null;
  });

let bayBoxItems: BayBoxInput[] = [];

let baysDefaultDir = "";

const notepadFeature = createNotepadFeature({
  document,
  storage: localStorage,
  invoke,
  isVisible: () => sidebarModel.leftMode === "notepad" && !collapsedOf(sidebarModel, "left"),
  rerenderSidebar: () => renderSidebarContent("left"),
  sidebarHead,
  spawnNook,
  createShore: (nookId) => workspaceController.mutate<{ shoreId: string }>("createShore", {
    newNookId: nookId,
    name: "Note",
    shoreId: "",
    targetNookId: "",
    orientation: "",
    nookId: "",
    dir: 0,
    nookType: "notepad",
  }),
  selectShore: (shoreId) => { workspace.activeShoreId = shoreId; },
  focusNook,
  openNote,
});

async function loadBayBoxes(): Promise<void> {
  try {
    const res = await invoke<{ bays: { id: string; name: string; projectDir?: string; iconKind?: string | null; iconValue?: string | null }[] }>(FrontendCommand.BayList, {});
    bayBoxItems = (res.bays ?? []).map((w) => ({ id: w.id, name: w.name, projectDir: w.projectDir, icon: w.iconKind ? { kind: w.iconKind, value: w.iconValue ?? "" } : null }));
  } catch { bayBoxItems = []; }
  renderSidebarContent("left");
}

let draggingBayId: string | null = null;

async function reorderBays(fromId: string, toId: string): Promise<void> {
  const ids = bayBoxItems.map((w) => w.id);
  const fromIdx = ids.indexOf(fromId);
  const toIdx = ids.indexOf(toId);
  if (fromIdx < 0 || toIdx < 0) { console.warn("bay reorder with unknown ids", fromId, toId); return; }
  ids.splice(toIdx, 0, ids.splice(fromIdx, 1)[0]);
  try { await invoke(FrontendCommand.BayReorder, { orderedIds: ids }); } catch (err) { console.warn("bay reorder failed", err); }
  await loadBayBoxes();
}

let treeDragShoreId: string | null = null;

function startShoreRename(shoreId: string, labelEl: HTMLElement | null, currentName: string): void {
  if (!labelEl) { console.warn("shore rename: label element missing", shoreId); return; }
  const input = document.createElement("input");
  input.className = "prename";
  input.value = currentName;
  input.spellcheck = false;
  labelEl.textContent = "";
  labelEl.appendChild(input);
  input.focus();
  input.select();
  let done = false;
  const commit = async (save: boolean) => {
    if (done) return;
    done = true;
    const newName = input.value.trim();
    if (save && newName && newName !== currentName) {
      try { await workspaceController.mutate("rename", { shoreId, name: newName, nookId: "", targetNookId: "", newNookId: "", orientation: "", dir: 0 }); }
      catch (e) { console.warn("shore rename failed", shoreId, e); }
      return;
    }
    renderSidebarContent("left");
  };
  input.addEventListener("blur", () => void commit(true));
  input.addEventListener("keydown", (e) => { e.stopPropagation(); if (e.key === "Enter") void commit(true); else if (e.key === "Escape") void commit(false); });
  input.addEventListener("click", (e) => e.stopPropagation());
}

function startBayRename(wsId: string, boxEl: HTMLElement, currentName: string): void {
  const input = document.createElement("input");
  input.className = "prename";
  input.value = currentName;
  input.spellcheck = false;
  boxEl.textContent = "";
  boxEl.appendChild(input);
  input.focus();
  input.select();
  let done = false;
  const commit = async (save: boolean) => {
    if (done) return;
    done = true;
    const newName = nextBayName(input.value, currentName);
    if (save && newName !== currentName) {
      try { await invoke(FrontendCommand.BayRename, { id: wsId, name: newName }); }
      catch (e) { console.warn("bay.rename failed", wsId, e); }
      await loadBayBoxes();
      return;
    }
    renderSidebarContent("left");
  };
  input.addEventListener("blur", () => void commit(true));
  input.addEventListener("keydown", (e) => {
    e.stopPropagation();
    if (e.key === "Enter") { e.preventDefault(); void commit(true); }
    else if (e.key === "Escape") { e.preventDefault(); void commit(false); }
  });
}

async function deleteBay(wsId: string): Promise<void> {
  try {
    await invoke(FrontendCommand.BayDelete, { id: wsId });
    await loadBayBoxes();
  } catch (e) { console.warn("bay.delete failed", wsId, e); }
}

function sideEl(_side: SidebarSide): { root: HTMLElement; content: HTMLElement } {
  return { root: leftSidebarEl, content: leftContentEl };
}

function renderSidebar(): void {
  renderSidebarContent("left");
}

function applySidebarModel(): void {
  const { root, content } = sideEl("left");
  root.classList.toggle("collapsed", collapsedOf(sidebarModel, "left"));
  content.style.width = `${widthOf(sidebarModel, "left")}px`;
  syncTitlebarWorkspaceOffset();
  renderSidebarContent("left");
  renderLeftRail();
  fitAll();
}

function renderLeftRail(): void {
  leftRailEl.innerHTML = "";
  const activeMode = sidebarModel.leftMode;
  for (const meta of SIDEBAR_RAIL_MODES) {
    const btn = document.createElement("div");
    btn.className = "sb-mode" + (meta.mode === activeMode ? " active" : "") + (meta.functional ? "" : " stub");
    btn.innerHTML = iconSvg(meta.mode);
    btn.title = meta.label;
    btn.setAttribute("role", "button");
    btn.setAttribute("aria-label", meta.label);
    btn.addEventListener("click", () => onRailClick(meta.mode));
    leftRailEl.appendChild(btn);
  }
}

function onRailClick(mode: SidebarMode): void {
  const wasActive = sidebarModel.leftMode === mode;
  const wasCollapsed = collapsedOf(sidebarModel, "left");
  if (wasActive && !wasCollapsed) {
    sidebarModel = toggleSide(sidebarModel, "left");
  } else {
    sidebarModel = selectLeftMode(sidebarModel, mode);
  }
  persistSidebarModel();
  applySidebarModel();
}

function toggleLeftSidebar(): void {
  sidebarModel = toggleSide(sidebarModel, "left");
  persistSidebarModel();
  applySidebarModel();
}

function revealSidebarMode(mode: SidebarMode): void {
  sidebarModel = selectLeftMode(sidebarModel, mode);
  persistSidebarModel();
  applySidebarModel();
}

function renderSidebarContent(side: SidebarSide): void {
  if (side !== "left") return;
  const { content } = sideEl(side);
  const previousMode = content.dataset.sidebarMode as SidebarMode | undefined;
  const previousScroller = content.querySelector<HTMLElement>(".sb-list");
  if (previousMode && previousScroller) sidebarScrollOffsets.set(previousMode, previousScroller.scrollTop);
  if (collapsedOf(sidebarModel, side)) { content.innerHTML = ""; return; }
  content.innerHTML = "";
  const mode = sidebarModel.leftMode;
  content.dataset.sidebarMode = mode;
  if (mode === "bays") renderBaysContent(content);
  else if (mode === "notepad") notepadFeature.render(content);
  else renderStubContent(content, mode);
  const nextScroller = content.querySelector<HTMLElement>(".sb-list");
  if (nextScroller) nextScroller.scrollTop = sidebarScrollOffsets.get(mode) ?? 0;
}

function sidebarHead(title: string, actions: { icon: string; title: string; run: () => void }[]): HTMLElement {
  const head = document.createElement("div");
  head.className = "sb-head";
  const label = document.createElement("span");
  label.textContent = title;
  head.appendChild(label);
  if (actions.length > 0) {
    const wrap = document.createElement("div");
    wrap.className = "sb-head-actions";
    for (const a of actions) {
      const act = document.createElement("span");
      act.className = "sb-act";
      if (a.icon.startsWith("<svg")) act.innerHTML = a.icon;
      else act.textContent = a.icon;
      act.title = a.title;
      act.addEventListener("click", (e) => { e.stopPropagation(); a.run(); });
      wrap.appendChild(act);
    }
    head.appendChild(wrap);
  }
  return head;
}

function renderStubContent(container: HTMLElement, mode: SidebarMode): void {
  container.appendChild(sidebarHead(SIDEBAR_MODE_META[mode].label, []));
  const empty = buildEmptyState({ message: `${SIDEBAR_MODE_META[mode].label} is coming soon.`, actionLabel: "", actionIcon: "" });
  container.appendChild(empty);
}

function shoreLeaves(shore: ShoreSnapshot): TreeLeaf[] {
  const collect = (node: MosaicNode): TreeLeaf[] => {
    if (node.kind === "leaf") {
      const subs = node.subtabs.length > 0 ? node.subtabs : [{ documentId: node.nookId, nookType: "terminal", title: null }];
      return subs.map((s) => {
        const pv = nooks.get(s.documentId);
        return { nookId: s.documentId, nookType: s.nookType, title: (pv && (pv.customTitle || pv.title)) || nookTitleCache.get(s.documentId) || s.title || "" };
      });
    }
    return [...collect(node.childA), ...collect(node.childB)];
  };
  return collect(shore.layoutTree);
}

const fsExpandedDirs = new Set<string>(JSON.parse(localStorage.getItem("cove.files.expanded") ?? "[]"));

let filesExpandedWs = parseCollapsedCardIds(localStorage.getItem("cove.files.expandedWs"));

let collapsedBayCards = parseCollapsedCardIds(localStorage.getItem("cove.bays.collapsedCards"));

const fsDirCache = new Map<string, { entries: FsEntry[]; truncated: boolean }>();

const fsDirLoading = new Set<string>();

const scmSummaryCache = new Map<string, ScmSummary>();

const scmSummaryFetchedAt = new Map<string, number>();

const scmSummaryFetching = new Set<string>();

const SCM_SUMMARY_TTL_MS = 10000;

function requestScmSummary(dir: string): void {
  if (!dir || scmSummaryFetching.has(dir)) return;
  if (Date.now() - (scmSummaryFetchedAt.get(dir) ?? 0) < SCM_SUMMARY_TTL_MS) return;
  scmSummaryFetching.add(dir);
  void invoke<ScmSummary>(FrontendCommand.AppGitSummary, { path: dir })
    .then((r) => {
      const prev = scmSummaryCache.get(dir);
      scmSummaryCache.set(dir, r);
      scmSummaryFetchedAt.set(dir, Date.now());
      scmSummaryFetching.delete(dir);
      if (JSON.stringify(prev ?? null) !== JSON.stringify(r)) {
        for (const cachedDir of fsDirCache.keys()) {
          if (cachedDir === dir || cachedDir.startsWith(`${dir}/`)) fsDirCache.delete(cachedDir);
        }
        renderSidebarContent("left");
      }
    })
    .catch((err) => {
      console.warn("git summary failed", dir, err);
      scmSummaryFetchedAt.set(dir, Date.now());
      scmSummaryFetching.delete(dir);
    });
}

function loadNookTitleCache(): Map<string, string> {
  try {
    return new Map(Object.entries(JSON.parse(localStorage.getItem("cove.nookTitles") ?? "{}") as Record<string, string>));
  } catch (err) {
    console.warn("nook title cache unreadable, starting empty", err);
    return new Map();
  }
}

const nookTitleCache = loadNookTitleCache();

function rememberNookTitle(nookId: string, title: string): void {
  if (!title) return;
  if (nookTitleCache.get(nookId) === title) return;
  nookTitleCache.set(nookId, title);
  while (nookTitleCache.size > 300) nookTitleCache.delete(nookTitleCache.keys().next().value!);
  localStorage.setItem("cove.nookTitles", JSON.stringify(Object.fromEntries(nookTitleCache)));
}

const wsSnapshotCache = new Map<string, BaySnapshot>();

const wsSnapshotFetchedAt = new Map<string, number>();

const wsSnapshotFetching = new Set<string>();

const WS_SNAPSHOT_TTL_MS = 10000;

function requestBaySnapshot(wsId: string): void {
  if (!wsId || wsSnapshotFetching.has(wsId)) return;
  if (Date.now() - (wsSnapshotFetchedAt.get(wsId) ?? 0) < WS_SNAPSHOT_TTL_MS) return;
  wsSnapshotFetching.add(wsId);
  void invoke<BaySnapshot>(FrontendCommand.LayoutGet, { bayId: wsId })
    .then((snap) => {
      if (snap.id !== wsId) {
        console.warn("bay snapshot id mismatch (daemon predates layout.get bayId)", wsId, snap.id);
        wsSnapshotFetchedAt.set(wsId, Date.now());
        wsSnapshotFetching.delete(wsId);
        return;
      }
      const prev = wsSnapshotCache.get(wsId);
      wsSnapshotCache.set(wsId, snap);
      wsSnapshotFetchedAt.set(wsId, Date.now());
      wsSnapshotFetching.delete(wsId);
      if (JSON.stringify(prev ?? null) !== JSON.stringify(snap)) renderSidebarContent("left");
    })
    .catch((err) => {
      console.warn("bay snapshot fetch failed", wsId, err);
      wsSnapshotFetchedAt.set(wsId, Date.now());
      wsSnapshotFetching.delete(wsId);
    });
}

function requestFsDir(path: string): void {
  if (fsDirCache.has(path) || fsDirLoading.has(path)) return;
  fsDirLoading.add(path);
  void invoke<{ entries: FsEntry[]; truncated: boolean; error: string | null }>(FrontendCommand.AppFsList, { path })
    .then((r) => {
      if (r.error) console.warn("fs list failed", path, r.error);
      fsDirCache.set(path, { entries: sortFsEntries(r.entries ?? []), truncated: !!r.truncated });
    })
    .catch((err) => {
      console.warn("fs list failed", path, err);
      fsDirCache.set(path, { entries: [], truncated: false });
    })
    .finally(() => {
      fsDirLoading.delete(path);
      renderSidebarContent("left");
    });
}

function renderFsLevel(host: HTMLElement, rootDir: string, dir: string, depth: number, statuses: FsStatusEntry[]): void {
  const cached = fsDirCache.get(dir);
  if (!cached) {
    requestFsDir(dir);
    const loading = document.createElement("div");
    loading.className = "fs-row fs-note";
    loading.style.paddingLeft = `${10 + depth * 14}px`;
    loading.textContent = "loading…";
    host.appendChild(loading);
    return;
  }
  const relativeDir = dir === rootDir ? "" : dir.slice(rootDir.length).replace(/^\/+/, "");
  for (const entry of mergeFsStatus(cached.entries, relativeDir, statuses)) {
    const full = joinPath(dir, entry.name);
    const row = document.createElement("div");
    row.className = "fs-row" + (entry.isDir ? " fs-dir" : " fs-file") + (entry.status ? ` status-${entry.status}` : "");
    row.style.paddingLeft = "8px";
    const guides = document.createElement("span");
    guides.className = "fs-tree-guides";
    for (let level = 0; level < depth; level++) {
      const guide = document.createElement("span");
      guide.style.setProperty("--guide-color", `hsl(${(level * 47 + 196) % 360} 55% 62%)`);
      guides.appendChild(guide);
    }
    row.appendChild(guides);
    const chev = document.createElement("span");
    chev.className = "tw-chevron" + (entry.isDir ? "" : " tw-spacer");
    if (entry.isDir) chev.textContent = fsExpandedDirs.has(full) ? "▾" : "▸";
    row.appendChild(chev);
    const ic = document.createElement("span");
    ic.className = "fs-ic";
    if (entry.isDir) ic.innerHTML = iconSvg("folder");
    else {
      const spec = fileIcon(entry.name);
      ic.innerHTML = spec.svg;
      ic.style.color = spec.color;
      ic.dataset.kind = spec.kind;
    }
    row.appendChild(ic);
    const label = document.createElement("span");
    label.className = "tw-label";
    label.textContent = entry.name;
    row.appendChild(label);
    if (entry.status) {
      const status = document.createElement("span");
      status.className = `fs-status fs-status-${entry.status}`;
      status.textContent = entry.status;
      row.appendChild(status);
    }
    row.addEventListener("click", () => {
      if (entry.isDir) {
        if (fsExpandedDirs.has(full)) fsExpandedDirs.delete(full);
        else fsExpandedDirs.add(full);
        localStorage.setItem("cove.files.expanded", JSON.stringify([...fsExpandedDirs]));
        renderSidebarContent("left");
      } else {
        void openFileInEditor(full);
      }
    });
    host.appendChild(row);
    if (entry.isDir && fsExpandedDirs.has(full) && depth < 12) renderFsLevel(host, rootDir, full, depth + 1, statuses);
  }
  if (cached.truncated) {
    const more = document.createElement("div");
    more.className = "fs-row fs-note";
    more.style.paddingLeft = `${10 + depth * 14}px`;
    more.textContent = "… more entries not shown";
    host.appendChild(more);
  }
}

function acknowledgeAgentAttention(nookId: string): void {
  if (mapAgentState(agentCards.find((card) => card.nookId === nookId)?.status ?? "idle") !== "done") return;
  if (acknowledgedDoneNooks.has(nookId)) return;
  acknowledgedDoneNooks.add(nookId);
  void invoke(FrontendCommand.ActivityAcknowledge, { nookId }).catch((err) => console.warn("activity.acknowledge failed", nookId, err));
  syncAgentNookStateClasses();
  if (sidebarModel.leftMode === "bays" && !collapsedOf(sidebarModel, "left")) renderSidebarContent("left");
}

function agentStateByNook(): Map<string, AgentState> {
  return new Map(buildAgentRows(agentCards, needsInputNooks, acknowledgedDoneNooks).map((r) => [r.nookId, r.state]));
}

function syncAgentNookStateClasses(): void {
  const states = agentStateByNook();
  for (const [nookId, nook] of nooks) {
    nook.el.classList.remove("agent-running", "agent-needs-input", "agent-done", "agent-idle");
    const state = states.get(nookId);
    if (!state) continue;
    nook.el.classList.add(`agent-${state}`);
    const agent = agentCards.find((card) => card.nookId === nookId);
    const accent = launcherFeature.adapters.find((adapter) => adapter.name === agent?.adapter)?.accent;
    nook.el.style.setProperty("--agent-accent", accent || AGENT_STATE_META[state].color);
  }
}

function adapterDisplayLabel(adapterName: string): string {
  return launcherFeature.adapters.find((a) => a.name === adapterName)?.displayName ?? adapterName.replace(/-/g, " ");
}

function buildNookCard(row: TreeRow, nookStates: Map<string, AgentState>, activate?: () => void, close?: () => void): HTMLElement {
  const nookId = row.nookId ?? "";
  const agent = agentCards.find((c) => c.nookId === nookId);
  const cardEl = document.createElement("div");
  cardEl.className = "nook-card";
  cardEl.style.marginLeft = `${6 + (row.depth - 1) * 14}px`;
  const titleRow = document.createElement("div");
  titleRow.className = "nook-card-title";
  const glyph = document.createElement("span");
  glyph.className = "pc-ic";
  glyph.innerHTML = agent ? adapterIconSvg(agent.adapter) : iconForNookType(row.nookType ?? "terminal");
  titleRow.appendChild(glyph);
  const titleText = document.createElement("span");
  titleText.className = "pc-title-text";
  titleText.textContent = row.label;
  titleRow.appendChild(titleText);
  cardEl.appendChild(titleRow);

  const metaRow = document.createElement("div");
  metaRow.className = "nook-card-meta";
  const st = nookStates.get(nookId);
  if (agent && st) {
    cardEl.classList.add(`state-${st}`);
    const dot = document.createElement("span");
    dot.className = "pc-dot";
    dot.style.background = AGENT_STATE_META[st].color;
    metaRow.appendChild(dot);
    const metaText = document.createElement("span");
    metaText.textContent = `${adapterDisplayLabel(agent.adapter)} · ${AGENT_STATE_META[st].label}`;
    metaRow.appendChild(metaText);
  } else {
    const metaText = document.createElement("span");
    metaText.textContent = NOOK_TYPE_LABELS[row.nookType ?? ""] ?? row.nookType ?? "nook";
    metaRow.appendChild(metaText);
  }
  cardEl.appendChild(metaRow);

  cardEl.addEventListener("click", () => {
    if (!nookId) return;
    if (activate) activate();
    else revealNook(nookId);
  });
  cardEl.addEventListener("contextmenu", (e) => {
    contextMenu.openAt(e, [
      { id: "focus", label: "Go to" },
      { id: "close", label: "Close", danger: true },
    ], (id) => {
      if (id === "focus") focusTreeRow("nook", row.shoreId, nookId);
      else if (id === "close") {
        if (close) close();
        else closeTreeRow("nook", row.shoreId, nookId);
      }
    });
  });
  return cardEl;
}

function renderBaysContent(container: HTMLElement): void {
  container.appendChild(sidebarHead("Bay", [{ icon: "+", title: "New bay", run: () => void newBay() }]));
  const emptyMessage = bayTreeEmptyMessage(bayBoxItems.length);
  if (emptyMessage) {
    const list = document.createElement("div");
    list.className = "sb-list";
    list.appendChild(buildEmptyState({ message: emptyMessage }));
    container.appendChild(list);
    return;
  }
  const entries = bayBoxItems.map((w) => ({ id: w.id, name: w.name, projectDir: w.projectDir ?? "", icon: w.icon }));
  const activeId = resolveActiveBayId(entries, workspace.snapshot?.id ?? null);
  const scroll = document.createElement("div");
  scroll.className = "sb-list ws-card-scroll";
  for (const w of entries) scroll.appendChild(renderBayCard(w, w.id === activeId));
  container.appendChild(scroll);
}

function wireBayCardDrag(el: HTMLElement, handle: HTMLElement, wid: string): void {
  handle.draggable = true;
  el.addEventListener("dragstart", (e) => {
    draggingBayId = wid;
    if (e.dataTransfer) e.dataTransfer.effectAllowed = "move";
  });
  el.addEventListener("dragend", () => { draggingBayId = null; });
  el.addEventListener("dragover", (e) => {
    if (!draggingBayId || draggingBayId === wid) return;
    e.preventDefault();
    el.classList.add("drag-over");
  });
  el.addEventListener("dragleave", () => el.classList.remove("drag-over"));
  el.addEventListener("drop", (e) => {
    e.preventDefault();
    el.classList.remove("drag-over");
    if (!draggingBayId || draggingBayId === wid) return;
    void reorderBays(draggingBayId, wid);
    draggingBayId = null;
  });
}

function buildBayIconGrid(selected: string | null, onSelect: (emoji: string | null) => void): HTMLElement {
  const grid = document.createElement("div");
  grid.className = "ws-icon-grid";
  const cells: HTMLElement[] = [];
  const addCell = (value: string | null) => {
    const cell = document.createElement("button");
    cell.type = "button";
    cell.className = "ws-icon-cell" + (selected === value ? " sel" : "");
    if (value === null) {
      const dot = document.createElement("span");
      dot.className = "ws-icon-none-dot";
      cell.appendChild(dot);
      cell.title = "No icon";
    } else {
      cell.textContent = value;
    }
    cell.addEventListener("click", () => {
      selected = value;
      for (const c of cells) c.classList.remove("sel");
      cell.classList.add("sel");
      onSelect(value);
    });
    cells.push(cell);
    grid.appendChild(cell);
  };
  addCell(null);
  for (const emoji of BAY_ICON_CHOICES) addCell(emoji);
  return grid;
}

let bayIconPopoverEl: HTMLElement | null = null;

let bayIconPopoverAway: ((e: MouseEvent) => void) | null = null;

let bayIconPopoverKey: ((e: KeyboardEvent) => void) | null = null;

function closeBayIconPopover(): void {
  if (bayIconPopoverAway) { document.removeEventListener("mousedown", bayIconPopoverAway, true); bayIconPopoverAway = null; }
  if (bayIconPopoverKey) { document.removeEventListener("keydown", bayIconPopoverKey, true); bayIconPopoverKey = null; }
  bayIconPopoverEl?.remove();
  bayIconPopoverEl = null;
}

function openBayIconPopover(anchor: HTMLElement, ws: BayCardEntry): void {
  closeBayIconPopover();
  const pop = document.createElement("div");
  pop.className = "ws-icon-popover";
  pop.appendChild(buildBayIconGrid(bayGlyph(ws.icon), (emoji) => {
    closeBayIconPopover();
    void changeBayIcon(ws.id, emoji);
  }));
  pop.style.cssText = "position:fixed;left:-9999px;top:-9999px;";
  document.body.appendChild(pop);
  const rect = anchor.getBoundingClientRect();
  const size = { width: pop.offsetWidth, height: pop.offsetHeight };
  const pos = clampMenuPosition({ x: rect.left, y: rect.bottom + 4 }, size, { width: window.innerWidth, height: window.innerHeight });
  pop.style.left = `${pos.x}px`;
  pop.style.top = `${pos.y}px`;
  bayIconPopoverEl = pop;
  bayIconPopoverKey = (e) => { if (e.key === "Escape") { e.preventDefault(); closeBayIconPopover(); } };
  document.addEventListener("keydown", bayIconPopoverKey, true);
  bayIconPopoverAway = (ev) => { if (bayIconPopoverEl && !bayIconPopoverEl.contains(ev.target as Node)) closeBayIconPopover(); };
  setTimeout(() => { if (bayIconPopoverAway) document.addEventListener("mousedown", bayIconPopoverAway, true); }, 0);
}

async function changeBayIcon(wsId: string, emoji: string | null): Promise<void> {
  try {
    if (emoji) await invoke(FrontendCommand.BaySetIcon, { id: wsId, kind: "emoji", value: emoji });
    else await invoke(FrontendCommand.BaySetIcon, { id: wsId, kind: "", value: "" });
    await loadBayBoxes();
  } catch (e) {
    console.warn("bay.set-icon failed", wsId, e);
    showInAppToast("Icon not changed", "Could not update the bay icon.", () => {});
  }
}

function bayCardHead(ws: BayCardEntry, mini: boolean): HTMLElement {
  const head = document.createElement("div");
  head.className = "ws-card-head";
  const swatch = document.createElement("span");
  swatch.className = "ws-card-swatch";
  const glyph = bayGlyph(ws.icon);
  if (glyph) {
    swatch.classList.add("has-glyph");
    swatch.textContent = glyph;
  }
  head.appendChild(swatch);
  const titles = document.createElement("div");
  titles.className = "ws-card-titles";
  const nameRow = document.createElement("div");
  nameRow.className = "ws-name-row";
  const name = document.createElement("span");
  name.className = "ws-card-name";
  name.textContent = ws.name;
  nameRow.appendChild(name);
  titles.appendChild(nameRow);
  const dir = document.createElement("span");
  dir.className = "ws-card-dir";
  dir.textContent = ws.projectDir || "no directory";
  dir.title = ws.projectDir;
  titles.appendChild(dir);
  head.appendChild(titles);
  head.addEventListener("contextmenu", (e) => {
    contextMenu.openAt(e, [
      { id: "new-shore", label: "New shore", disabled: mini },
      { id: "rename", label: "Rename" },
      { id: "change-icon", label: "Change icon" },
      { id: "sep", label: "", separator: true },
      { id: "close-ws", label: "Close bay", danger: true },
    ], (id) => {
      if (id === "new-shore") void newShore();
      else if (id === "rename") startBayRename(ws.id, name, ws.name);
      else if (id === "change-icon") openBayIconPopover(swatch, ws);
      else if (id === "close-ws") void deleteBay(ws.id);
    });
  });
  return head;
}

function renderBayCard(ws: BayCardEntry, isActive: boolean): HTMLElement {
  const cardCollapsed = collapsedBayCards.has(ws.id);
  const card = document.createElement("div");
  card.className = "ws-card" + (isActive ? " ws-card-active" : "") + (cardCollapsed ? " collapsed" : "");
  card.style.setProperty("--ws-accent", bayAccent(ws.id));
  const head = bayCardHead(ws, !isActive);
  if (ws.projectDir) {
    requestScmSummary(ws.projectDir);
    const summary = scmSummaryCache.get(ws.projectDir);
    const chipText = summary ? scmChipText(summary) : "";
    if (chipText) {
      const parts = chipText.split(" ");
      const branchEl = document.createElement("span");
      branchEl.className = "ws-branch";
      branchEl.textContent = parts[0];
      branchEl.title = `${ws.projectDir} — branch`;
      head.querySelector(".ws-name-row")?.appendChild(branchEl);
      const stats = parts.slice(1);
      if (stats.length > 0) {
        const chip = document.createElement("span");
        chip.className = "ws-scm-chip";
        for (const part of stats) {
          const seg = document.createElement("span");
          seg.textContent = part;
          if (part.startsWith("↑")) seg.className = "scm-ahead";
          else if (part.startsWith("↓")) seg.className = "scm-behind";
          else seg.className = "scm-dirty";
          chip.appendChild(seg);
        }
        chip.title = `${ws.projectDir} — ahead/behind upstream · modified files`;
        head.appendChild(chip);
      }
    }
  }
  const collapse = document.createElement("button");
  collapse.type = "button";
  collapse.className = "ws-card-collapse";
  collapse.title = cardCollapsed ? "Expand bay" : "Collapse bay";
  collapse.setAttribute("aria-label", collapse.title);
  collapse.setAttribute("aria-expanded", String(!cardCollapsed));
  collapse.innerHTML = "<span>▾</span>";
  collapse.addEventListener("click", (e) => {
    e.stopPropagation();
    collapsedBayCards = toggleCardCollapsed(collapsedBayCards, ws.id);
    localStorage.setItem("cove.bays.collapsedCards", serializeCollapsedCardIds(collapsedBayCards));
    renderSidebarContent("left");
  });
  head.appendChild(collapse);
  head.addEventListener("click", () => void openBayLauncher(ws.id));
  card.appendChild(head);
  wireBayCardDrag(card, head.querySelector<HTMLElement>(".ws-card-swatch")!, ws.id);
  if (cardCollapsed) return card;

  const body = document.createElement("div");
  body.className = "ws-card-body";
  const shoresHost = document.createElement("div");
  if (!isActive) {
    requestBaySnapshot(ws.id);
  }
  body.appendChild(shoresHost);
  const sourceShores = isActive ? (workspace.snapshot?.shores ?? []) : (wsSnapshotCache.get(ws.id)?.shores ?? []);
  const shores: TreeShoreInput[] = sourceShores.map((r) => ({ id: r.id, name: shoreTabName(r), leaves: shoreLeaves(r) }));
  const rows = buildBayTree({
    bayName: ws.name,
    activeShoreId: workspace.activeShoreId,
    focusedNookId: workspace.focusedNookId,
    shores,
    collapsedShoreIds: collapsedTreeShores,
    bayCollapsed: false,
    bays: [{ id: ws.id, name: ws.name }],
    activeBayId: ws.id,
  }).filter((r) => r.kind !== "bay");
  const nookStates = agentStateByNook();
  for (const row of rows) {
    if (row.kind === "nook" && row.nookId) {
      const activate = isActive ? undefined : () => void switchBay(ws.id, row.shoreId, row.nookId);
      const close = isActive ? undefined : () => void closeTreeRowInBay(ws.id, "nook", row.shoreId, row.nookId);
      shoresHost.appendChild(buildNookCard(row, nookStates, activate, close));
      continue;
    }
    const rowEl = document.createElement("div");
    rowEl.className = `tree-row kind-${row.kind}` + (row.active ? " active" : "") + (row.collapsed ? " collapsed" : "");
    rowEl.style.paddingLeft = `${6 + (row.depth - 1) * 14}px`;
    if (row.expandable) {
      const chev = document.createElement("span");
      chev.className = "tw-chevron";
      chev.textContent = "▾";
      chev.addEventListener("click", (e) => {
        e.stopPropagation();
        if (row.shoreId) {
          if (collapsedTreeShores.has(row.shoreId)) collapsedTreeShores.delete(row.shoreId);
          else collapsedTreeShores.add(row.shoreId);
          localStorage.setItem("cove.tree.collapsedShores", JSON.stringify([...collapsedTreeShores]));
        }
        renderSidebarContent("left");
      });
      rowEl.appendChild(chev);
    } else {
      const spacer = document.createElement("span");
      spacer.className = "tw-chevron tw-spacer";
      rowEl.appendChild(spacer);
    }
    const label = document.createElement("span");
    label.className = "tw-label";
    label.textContent = row.label;
    rowEl.appendChild(label);
    if (row.count > 1 && row.kind !== "nook") {
      const count = document.createElement("span");
      count.className = "tw-count";
      count.textContent = String(row.count);
      rowEl.appendChild(count);
    }
    rowEl.addEventListener("click", () => {
      if (isActive) onTreeRowClick(row.kind, row.shoreId, row.nookId);
      else void switchBay(ws.id, row.shoreId, row.nookId);
    });
    if (row.kind === "shore" && row.shoreId) {
      const rid = row.shoreId;
      rowEl.draggable = true;
      rowEl.addEventListener("dragstart", (e) => {
        treeDragShoreId = rid;
        if (e.dataTransfer) e.dataTransfer.effectAllowed = "move";
      });
      rowEl.addEventListener("dragend", () => { treeDragShoreId = null; });
      rowEl.addEventListener("dragover", (e) => {
        if (!treeDragShoreId || treeDragShoreId === rid) return;
        e.preventDefault();
        rowEl.classList.add("drag-over");
      });
      rowEl.addEventListener("dragleave", () => rowEl.classList.remove("drag-over"));
      rowEl.addEventListener("drop", (e) => {
        e.preventDefault();
        rowEl.classList.remove("drag-over");
        if (!treeDragShoreId || treeDragShoreId === rid) return;
        void reorderShores(treeDragShoreId, rid);
        treeDragShoreId = null;
      });
    }
    rowEl.addEventListener("contextmenu", (e) => {
      const renameable = row.kind === "shore" && !!row.shoreId;
      contextMenu.openAt(e, [
        { id: "focus", label: "Go to" },
        ...(renameable ? [{ id: "rename", label: "Rename" }] : []),
        { id: "close", label: "Close", danger: true },
      ], (id) => {
        if (id === "focus") focusTreeRow(row.kind, row.shoreId, row.nookId);
        else if (id === "rename" && row.shoreId) startShoreRename(row.shoreId, rowEl.querySelector(".tw-label") as HTMLElement, row.label);
        else if (id === "close") {
          if (isActive) closeTreeRow(row.kind, row.shoreId, row.nookId);
          else void closeTreeRowInBay(ws.id, row.kind, row.shoreId, row.nookId);
        }
      });
    });
    shoresHost.appendChild(rowEl);
  }

  const filesExpanded = filesExpandedWs.has(ws.id);
  const filesHead = document.createElement("div");
  filesHead.className = "ws-files-head" + (filesExpanded ? "" : " collapsed");
  const filesChev = document.createElement("span");
  filesChev.className = "tw-chevron";
  filesChev.textContent = filesExpanded ? "▾" : "▸";
  filesHead.appendChild(filesChev);
  const filesLabel = document.createElement("span");
  filesLabel.textContent = "Files";
  filesHead.appendChild(filesLabel);
  filesHead.addEventListener("click", (e) => {
    e.stopPropagation();
    filesExpandedWs = toggleCardCollapsed(filesExpandedWs, ws.id);
    localStorage.setItem("cove.files.expandedWs", serializeCollapsedCardIds(filesExpandedWs));
    renderSidebarContent("left");
  });
  body.appendChild(filesHead);
  if (filesExpanded) {
    const filesHost = document.createElement("div");
    filesHost.className = "ws-files";
    if (ws.projectDir) renderFsLevel(filesHost, ws.projectDir, ws.projectDir, 0, scmSummaryCache.get(ws.projectDir)?.files ?? []);
    else {
      const none = document.createElement("div");
      none.className = "fs-row fs-note";
      none.textContent = "no bay directory";
      filesHost.appendChild(none);
    }
    body.appendChild(filesHost);
  }
  card.appendChild(body);
  return card;
}

function onTreeRowClick(kind: string, shoreId: string | null, nookId: string | null): void {
  if (kind === "bay") {
    console.warn("tree click: bay rows are not rendered in card mode");
    return;
  }
  if (kind === "nook" && nookId) { revealNook(nookId); return; }
  if (kind === "shore" && shoreId) {
    const shore = workspace.snapshot?.shores.find((r) => r.id === shoreId);
    if (!shore) { console.warn("tree click: unknown shore", shoreId); return; }
    workspace.activeShoreId = shoreId;
    const f = firstLeafOf(shore);
    if (f) workspace.focusedNookId = f;
    renderShore();
    renderShoreTabs();
    renderSidebar();
    if (f) focusNook(f);
  }
}

function focusTreeRow(kind: string, shoreId: string | null, nookId: string | null): void {
  if (kind === "nook" && nookId) { revealNook(nookId); return; }
  if (kind === "shore" && shoreId) {
    const shore = workspace.snapshot?.shores.find((r) => r.id === shoreId);
    if (!shore) { console.warn("tree focus: unknown shore", shoreId); return; }
    workspace.activeShoreId = shoreId;
    const f = firstLeafOf(shore);
    if (f) workspace.focusedNookId = f;
    renderShore();
    renderShoreTabs();
    renderSidebar();
    if (f) focusNook(f);
  }
}

function closeTreeRow(kind: string, shoreId: string | null, nookId: string | null): void {
  if (kind === "nook" && nookId) { focusNook(nookId); void closeFocused(); return; }
  if (kind === "shore" && shoreId) { void closeShore(shoreId); }
}

async function closeTreeRowInBay(bayId: string, kind: string, shoreId: string | null, nookId: string | null): Promise<void> {
  if (workspace.snapshot?.id === bayId) {
    closeTreeRow(kind, shoreId, nookId);
    return;
  }
  if (!shoreId) {
    console.warn("close requested without a shore", bayId, kind, nookId);
    return;
  }
  const snapshot = wsSnapshotCache.get(bayId);
  const shore = snapshot?.shores.find((candidate) => candidate.id === shoreId);
  if (!shore) {
    console.warn("close requested for shore outside cached bay", bayId, shoreId);
    return;
  }
  const nookIds = kind === "shore" ? collectLeafIds(shore.layoutTree) : nookId ? [nookId] : [];
  if (nookIds.length === 0) {
    console.warn("close requested without a nook", bayId, shoreId);
    return;
  }
  for (const id of nookIds) {
    await closeBrowserWebview(id);
    try { await invoke(FrontendCommand.AppNookKill, { nookId: id }); }
    catch (err) { console.warn("inactive bay nook kill failed", bayId, id, err); }
    disposeNook(id);
  }
  try {
    await workspaceController.mutate(kind === "shore" ? "closeShore" : "close", { shoreId,
    nookId: kind === "nook" ? nookIds[0] : "",
    targetNookId: "",
    newNookId: "",
    orientation: "",
    name: "",
    dir: 0, });
  } catch (err) {
    console.warn("inactive bay layout close failed", bayId, shoreId, nookId, err);
    return;
  }
  wsSnapshotFetchedAt.delete(bayId);
  requestBaySnapshot(bayId);
}

let prevAgentStates = new Map<string, string>();

function agentChimesEnabled(): boolean {
  return chimesEnabledFrom(localStorage.getItem(AGENT_CHIMES_STORAGE_KEY));
}

function setAgentChimesEnabled(enabled: boolean): void {
  localStorage.setItem(AGENT_CHIMES_STORAGE_KEY, chimePrefValue(enabled));
}

async function refreshAgentsImpl(): Promise<void> {
  const previousCards = agentCards;
  let nextCards: AgentCard[];
  try {
    const res = await invoke<{ cards: AgentCard[] }>(FrontendCommand.ActivityList, {});
    nextCards = res.cards ?? [];
  } catch {
    return;
  }
  const cardsChanged = !agentCardsEqual(previousCards, nextCards);
  agentCards = nextCards;
  const nextStates = new Map(agentCards.map((c) => [c.nookId, mapAgentState(c.status)]));
  for (const nookId of acknowledgedDoneNooks) {
    if (nextStates.get(nookId) !== "done") acknowledgedDoneNooks.delete(nookId);
  }
  if (agentChimesEnabled()) {
    for (const kind of detectChimes(prevAgentStates, nextStates)) playChime(kind);
  }
  prevAgentStates = nextStates;
  syncAgentNookStateClasses();
  if (cardsChanged && agentsVisible()) renderSidebarContent("left");
}

const refreshAgents = createCoalescer(refreshAgentsImpl);

function agentsVisible(): boolean {
  return !collapsedOf(sidebarModel, "left") && sidebarModel.leftMode === "bays";
}

const SIDEBAR_PREF_KEYS = {
  leftMode: "sidebar.leftMode",
  leftCollapsed: "sidebar.leftCollapsed",
  rightCollapsed: "sidebar.rightCollapsed",
  leftWidth: "sidebar.leftWidth",
  rightWidth: "sidebar.rightWidth",
};

function persistSidebarModel(): void {
  const entries: [string, string][] = [
    [SIDEBAR_PREF_KEYS.leftMode, sidebarModel.leftMode],
    [SIDEBAR_PREF_KEYS.leftCollapsed, String(sidebarModel.leftCollapsed)],
    [SIDEBAR_PREF_KEYS.rightCollapsed, String(sidebarModel.rightCollapsed)],
    [SIDEBAR_PREF_KEYS.leftWidth, String(sidebarModel.leftWidth)],
    [SIDEBAR_PREF_KEYS.rightWidth, String(sidebarModel.rightWidth)],
  ];
  for (const [k, v] of entries) invoke(FrontendCommand.AppConfigSet, { key: k, value: v }).catch((e) => console.warn("sidebar configSet failed", k, e));
}

async function loadSidebarModel(): Promise<void> {
  const get = async (k: string): Promise<string | null> => {
    try { const r = await invoke<{ ok: boolean; value?: string }>(FrontendCommand.AppConfigGet, { key: k }); return r.ok ? r.value ?? null : null; } catch { return null; }
  };
  const validMode = (v: string | null): SidebarMode | null => (v && SIDEBAR_MODES.some((m) => m.mode === v)) ? v as SidebarMode : null;
  const lm = validMode(await get(SIDEBAR_PREF_KEYS.leftMode));
  if (lm) sidebarModel.leftMode = lm;
  sidebarModel.leftCollapsed = (await get(SIDEBAR_PREF_KEYS.leftCollapsed)) === "true";
  sidebarModel.rightCollapsed = (await get(SIDEBAR_PREF_KEYS.rightCollapsed)) === "true";
  sidebarModel = setWidth(sidebarModel, "left", Number(await get(SIDEBAR_PREF_KEYS.leftWidth)) || sidebarModel.leftWidth);
  sidebarModel = setWidth(sidebarModel, "right", Number(await get(SIDEBAR_PREF_KEYS.rightWidth)) || sidebarModel.rightWidth);
}

function wireSidebarResize(handle: HTMLElement, side: SidebarSide): void {
  lifecycle.listen(handle, "mousedown", (event) => {
    activeResizeCleanup?.();
    const e = event as MouseEvent;
    e.preventDefault();
    handle.classList.add("dragging");
    const startX = e.clientX;
    const startW = widthOf(sidebarModel, side);
    const onMove = (m: MouseEvent) => {
      const delta = side === "left" ? m.clientX - startX : startX - m.clientX;
      const { content } = sideEl(side);
      const next = startW + delta;
      sidebarModel = setWidth(sidebarModel, side, next);
      content.style.width = `${widthOf(sidebarModel, side)}px`;
      syncTitlebarWorkspaceOffset();
      fitAll();
    };
    let active = true;
    const cleanup = (persist: boolean): void => {
      if (!active) return;
      active = false;
      document.removeEventListener("mousemove", onMove);
      document.removeEventListener("mouseup", onUp);
      handle.classList.remove("dragging");
      activeResizeCleanup = null;
      if (persist) persistSidebarModel();
    };
    const onUp = () => cleanup(true);
    activeResizeCleanup = () => cleanup(false);
    document.addEventListener("mousemove", onMove);
    document.addEventListener("mouseup", onUp);
  });
}

function startAgentPolling(): void {
  void refreshAgents();
  if (agentPollTimer === null) {
    agentPollTimer = setInterval(() => {
    if (!agentsVisible()) return;
    void refreshAgents();
    for (const bay of bayBoxItems) {
      if (bay.projectDir) requestScmSummary(bay.projectDir);
    }
    }, 3000);
  }
}

  return {
    get model() { return sidebarModel; },
    get bayBoxes() { return bayBoxItems; },
    get defaultDirectory() { return baysDefaultDir; },
    get needsInputCount() { return needsInputNooks.size; },
    render: renderSidebar,
    renderContent: renderSidebarContent,
    applyModel: applySidebarModel,
    toggleLeft: toggleLeftSidebar,
    reveal: revealSidebarMode,
    rememberNookTitle,
    acknowledgeAgentAttention,
    syncAgentNookStateClasses,
    buildBayIconGrid,
    closeBayIconPopover,
    agentChimesEnabled,
    setAgentChimesEnabled,
    refreshAgents,
    agentsVisible,
    loadBayBoxes,
    setDefaultDirectory(value: string) { baysDefaultDir = value; },
    loadModel: loadSidebarModel,
    wireResize: wireSidebarResize,
    startAgentPolling,
    isModeVisible(mode: SidebarMode) { return sidebarModel.leftMode === mode && !collapsedOf(sidebarModel, "left"); },
    setChromeVisibility(leftHidden: boolean, rightHidden: boolean) {
      sidebarModel = setCollapsed(sidebarModel, "left", leftHidden);
      sidebarModel = setCollapsed(sidebarModel, "right", rightHidden);
      persistSidebarModel();
      applySidebarModel();
    },
    addNeedsInput(nookId: string) { needsInputNooks.add(nookId); },
    removeNeedsInput(nookId: string) { needsInputNooks.delete(nookId); },
    clearNeedsInput() { needsInputNooks.clear(); },
    shoreLeaves,
    async dispose() {
      closeBayIconPopover();
      if (agentPollTimer !== null) {
        clearInterval(agentPollTimer);
        agentPollTimer = null;
      }
      await notepadFeature.dispose();
      await lifecycle.dispose();
    },
  };
}
