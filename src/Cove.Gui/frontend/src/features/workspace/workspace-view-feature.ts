import { toBase64Utf8 } from "../../wsproto";
import { shouldResetReplay, streamReconciliationActions } from "../../stream-guard";
import { renderKanbanBoard } from "../../tasks-kanban";
import { renderTaskList } from "../../tasks-list";
import { renderTimelineFeed } from "../../timeline-feed";
import { renderMarkdownNote } from "../../markdown-note";
import { renderSketchNote } from "../../sketch-note";
import { renderCanvasNote } from "../../canvas-note";
import { renderHtmlNote } from "../../html-note";
import { renderNotepadNook } from "../../notepad-nook";
import { renderMermaidNote } from "../../mermaid-note";
import { renderSessionPicker } from "../../session-picker";
import { resumeSpawnPlan, type ResumeAction, type VaultResumeResult } from "../../session-resume";
import { renderLibraryPopover } from "../../library-popover";
import { renderSnapshotInspector } from "../../snapshot-inspector";
import { renderDiffReviewNook } from "../../diff-review-nook";
import { renderEditorNook } from "../../editor-nook";
import { renderSourceControlNook } from "../../source-control-nook";
import { renderSearchNook } from "../../search-nook";
import { renderBrowserNook, reconcileBrowserBounds } from "../../browser-nook";
import { renderDiffViewerNook } from "../../diff-viewer-nook";
import { renderMarkdownNook } from "../../markdown-nook";
import { renderPdfNook } from "../../pdf-nook";
import { renderVideoNook } from "../../video-nook";
import { mediaUrl } from "../../media-url";
import { resolveLauncherProjectDir } from "../../launcher-model";
import { shouldShowLauncher, isEmptyShoreTree, resolveLaunchCwd } from "../../box-launcher";
import { buildEmptyState, EmptyStateMessages } from "../../empty-states";
import { iconSvg } from "../../icons";
import { dropZoneFor, moveMutationFor, type DropZone, type MoveMutation } from "../../nook-dnd";
import { enqueueNookWrite } from "../../write-queue";
import { TerminalSession, type TerminalSettings } from "../../terminal-session";
import { createTerminalResources } from "../../terminal-resources";
import type { WorkspaceStore, MosaicNode, NookLeaf, ShoreSnapshot } from "../../workspace/workspace-store";
import type { WorkspaceController } from "../../workspace/workspace-controller";
import type { ContextMenuHost } from "../../shell/context-menu-host";
import type { FindBarFeature } from "../find/find-bar-feature";
import type { BayBoxInput } from "../../bay-boxes";
import type { NookDragState } from "../navigation/shore-tabs-feature";
import { LifecycleScope, type ComponentHandle, type NookContentHandle } from "../../app/lifecycle";
import { FrontendCommand } from "../../app/frontend-command";

export interface NookView {
  nookId: string;
  session: TerminalSession;
  el: HTMLElement;
  title: string;
  customTitle: string;
  headerTitleEl: HTMLElement;
  closeMenu: () => void;
}

export interface WorkspaceViewDependencies {
  document: Document;
  window: Window;
  grid: HTMLElement;
  shoreTabs: HTMLElement;
  leftSidebar: HTMLElement;
  workspace: WorkspaceStore;
  workspaceController: WorkspaceController;
  contextMenu: ContextMenuHost;
  findFeature: FindBarFeature;
  settings: TerminalSettings;
  nookDrag: NookDragState;
  invoke<T>(command: FrontendCommand, args: Record<string, unknown>): Promise<T>;
  currentTermTheme(): Record<string, string>;
  renderLauncher(shoreId: string | null, placeholderId: string | null): HTMLElement;
  invalidateLauncherRecents(): void;
  refreshLauncherRecents(): Promise<unknown>;
  launcherAdapters(): { name: string; displayName: string }[];
  launcherYolo(adapter: string): boolean;
  renderSidebar(): void;
  renderSidebarContent(): void;
  isSidebarModeVisible(mode: string): boolean;
  rememberNookTitle(nookId: string, title: string): void;
  acknowledgeAgentAttention(nookId: string): void;
  syncAgentNookStateClasses(): void;
  sidebarBayBoxes(): BayBoxInput[];
  sidebarDefaultDirectory(): string;
  renderShoreTabs(): void;
  shoreTabName(shore: ShoreSnapshot): string;
  getOverviewVisible(): boolean;
  setOverviewVisible(value: boolean): void;
  showInAppToast(title: string, body: string, action?: () => void): void;
  revealNook(nookId: string): void;
  runAction(action: string): void;
  openSplitChooser(event: MouseEvent, direction: "row" | "col"): void;
  closeNookById(nookId: string): Promise<void>;
  closeFocused(): Promise<void>;
  closeOthers(keepNookId: string): Promise<void>;
  paintDropOverlay(host: HTMLElement, zone: DropZone): void;
  clearDropOverlay(): void;
  applyNookMove(mutation: MoveMutation, sourceNookId: string): Promise<void>;
  newShore(): void | Promise<void>;
  openFileInEditor(path: string): Promise<void>;
}

export interface WorkspaceViewFeature extends ComponentHandle {
  readonly nooks: ReadonlyMap<string, NookView>;
  spawn(input: Record<string, unknown>): Promise<{ nookId: string }>;
  getNook(nookId: string): NookView;
  disposeNook(nookId: string): void;
  closeNookMenus(): void;
  setNookFilePath(nookId: string, path: string): void;
  collectLeafIds(node: MosaicNode | null): string[];
  findNookLocation(node: MosaicNode | null, nookId: string): { leaf: NookLeaf; subtabIndex: number } | null;
  activeShore(): ShoreSnapshot | null | undefined;
  activeLeafIds(): string[];
  firstLeafOf(shore: ShoreSnapshot): string | undefined;
  captureNookViewports(): void;
  render(): void;
  focus(nookId: string): void;
  refreshTitles(): void;
  fitAll(): void;
  applySettings(): void;
  syncTitlebarWorkspaceOffset(): void;
  resumeRecentSession(adapter: string, sessionId: string, cwd: string, displayName: string): Promise<void>;
  activeProjectDir(): string;
}

export function createWorkspaceViewFeature(dependencies: WorkspaceViewDependencies): WorkspaceViewFeature {
  const lifecycle = new LifecycleScope();
  const document = dependencies.document;
  const window = dependencies.window;
  const gridEl = dependencies.grid;
  const shoreTabsEl = dependencies.shoreTabs;
  const leftSidebarEl = dependencies.leftSidebar;
  const workspace = dependencies.workspace;
  const workspaceController = dependencies.workspaceController;
  const contextMenu = dependencies.contextMenu;
  const findFeature = dependencies.findFeature;
  const settings = dependencies.settings;
  const nookDrag = dependencies.nookDrag;
  const invoke = dependencies.invoke;
  const currentTermTheme = dependencies.currentTermTheme;
  const showInAppToast = dependencies.showInAppToast;
  const revealNook = dependencies.revealNook;
  const runAction = dependencies.runAction;
  const openSplitChooser = dependencies.openSplitChooser;
  const closeNookById = dependencies.closeNookById;
  const closeFocused = dependencies.closeFocused;
  const closeOthers = dependencies.closeOthers;
  const paintDropOverlay = dependencies.paintDropOverlay;
  const clearDropOverlay = dependencies.clearDropOverlay;
  const applyNookMove = dependencies.applyNookMove;
  const newShore = dependencies.newShore;
  const openFileInEditor = dependencies.openFileInEditor;
  const launcherFeature = {
    render: dependencies.renderLauncher,
    invalidateRecents: dependencies.invalidateLauncherRecents,
    refreshRecents: dependencies.refreshLauncherRecents,
    get adapters() { return dependencies.launcherAdapters(); },
    yolo: dependencies.launcherYolo,
  };
  const workspaceSidebar = {
    render: dependencies.renderSidebar,
    renderContent: (_side: string) => dependencies.renderSidebarContent(),
    get bayBoxes() { return dependencies.sidebarBayBoxes(); },
    get defaultDirectory() { return dependencies.sidebarDefaultDirectory(); },
    isModeVisible: dependencies.isSidebarModeVisible,
    rememberNookTitle: dependencies.rememberNookTitle,
    acknowledgeAgentAttention: dependencies.acknowledgeAgentAttention,
    syncAgentNookStateClasses: dependencies.syncAgentNookStateClasses,
  };
  const shoreTabsFeature = {
    render: dependencies.renderShoreTabs,
    tabName: dependencies.shoreTabName,
    get overviewVisible() { return dependencies.getOverviewVisible(); },
    set overviewVisible(value: boolean) { dependencies.setOverviewVisible(value); },
  };

function writeNook(nookId: string, dataBase64: string): Promise<void> {
  return enqueueNookWrite(nookId, dataBase64, (id, queuedDataBase64) => invoke(FrontendCommand.AppNookWrite, { nookId: id, dataBase64: queuedDataBase64 }));
}

const locallySpawnedNookIds = new Set<string>();

const renderedStreamNookIds = new Set<string>();

async function spawnNook(params: Record<string, unknown>): Promise<{ nookId: string }> {
  const cwd = resolveLaunchCwd(String(params.cwd ?? ""), String(params.inheritCwdFrom ?? ""), activeProjectDir());
  const r = await invoke<{ nookId?: string; error?: { code?: string; message?: string } }>(FrontendCommand.AppNookSpawn, { ...params, cwd });
  if (!r?.nookId) {
    const msg = r?.error?.message ?? "the engine could not start this terminal";
    console.warn("nook spawn failed", params, r);
    showInAppToast("Couldn't open terminal", msg, () => {});
    throw new Error(msg);
  }
  locallySpawnedNookIds.add(r.nookId);
  return { nookId: r.nookId };
}

interface NookView {
  nookId: string;
  session: TerminalSession;
  el: HTMLElement;
  title: string;
  customTitle: string;
  headerTitleEl: HTMLElement;
  closeMenu(): void;
}

const nooks = new Map<string, NookView>();

const nookFilePaths = new Map<string, string>();

interface OwnedContentRecord {
  readonly key: string;
  element: HTMLElement;
  handle: NookContentHandle | null;
  generation: number;
}

const ownedContent = new Map<string, OwnedContentRecord>();
const pendingContentDisposals = new Set<Promise<void>>();
let renderGeneration = 0;
let activeSplitDragCleanup: (() => void) | null = null;

function scheduleContentDisposal(handle: NookContentHandle): void {
  const disposal = Promise.resolve(handle.dispose()).catch((error: unknown) => {
    console.warn("nook content disposal failed", error);
  });
  pendingContentDisposals.add(disposal);
  void disposal.finally(() => pendingContentDisposals.delete(disposal));
}

function disposeOwnedContent(record: OwnedContentRecord): void {
  if (ownedContent.get(record.key) === record) ownedContent.delete(record.key);
  record.generation = -1;
  record.element.remove();
  if (record.handle) scheduleContentDisposal(record.handle);
}

function renderOwnedContent(
  key: string,
  placeholderClass: string,
  failureLabel: string,
  factory: () => Promise<NookContentHandle>,
): HTMLElement {
  const existing = ownedContent.get(key);
  if (existing) {
    existing.generation = renderGeneration;
    return existing.element;
  }
  const placeholder = document.createElement("div");
  placeholder.className = placeholderClass;
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  const record: OwnedContentRecord = { key, element: placeholder, handle: null, generation: renderGeneration };
  ownedContent.set(key, record);
  void factory().then((handle) => {
    if (
      lifecycle.isDisposed
      || ownedContent.get(key) !== record
      || record.generation !== renderGeneration
      || !placeholder.isConnected
    ) {
      scheduleContentDisposal(handle);
      return;
    }
    const element = handle.element;
    element.style.flex = "1 1 0";
    element.style.minWidth = "0";
    element.style.minHeight = "0";
    record.handle = handle;
    record.element = element;
    placeholder.replaceWith(element);
  }).catch((error: unknown) => {
    if (ownedContent.get(key) !== record || record.generation !== renderGeneration || lifecycle.isDisposed) return;
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load ${failureLabel}: ${(error as Error).message}</div>`;
  });
  return placeholder;
}

function renderOwnedContentSync(key: string, factory: () => NookContentHandle): HTMLElement {
  const existing = ownedContent.get(key);
  if (existing) {
    existing.generation = renderGeneration;
    return existing.element;
  }
  const handle = factory();
  const record: OwnedContentRecord = { key, element: handle.element, handle, generation: renderGeneration };
  ownedContent.set(key, record);
  return handle.element;
}

function syncTitlebarWorkspaceOffset(): void {
  const workspaceLeft = leftSidebarEl.offsetLeft + leftSidebarEl.offsetWidth + 6;
  document.documentElement.style.setProperty("--cove-workspace-left", `${workspaceLeft}px`);
}

function fitAll() {
  for (const pv of nooks.values()) pv.session.scheduleFit();
}

function applySettings() {
  for (const pv of nooks.values()) pv.session.applySettings();
  document.documentElement.style.setProperty("--workspace-padding", `${settings.padding}px`);
  document.documentElement.style.setProperty("--cove-bg-opacity", String(settings.backgroundOpacity));
  fitAll();
}

function armNookOpening(el: HTMLElement): void {
  const finish = () => el.classList.remove("nook-opening");
  el.classList.add("nook-opening");
  el.addEventListener("animationend", finish, { once: true });
  window.setTimeout(finish, 220);
}
function captureNookRects(): Map<string, { left: number; top: number }> {
  return new Map([...nooks].flatMap(([nookId, nook]) => {
    if (nook.el.parentElement === null) return [];
    const rect = nook.el.getBoundingClientRect();
    return [[nookId, { left: rect.left, top: rect.top }]];
  }));
}

function armNookReposition(el: HTMLElement, shiftX: number, shiftY: number): void {
  let timer: number | null = null;
  const finish = (): void => {
    if (timer !== null) window.clearTimeout(timer);
    timer = null;
    el.removeEventListener("animationend", onAnimationEnd);
    el.classList.remove("nook-repositioning");
    el.style.removeProperty("--nook-shift-x");
    el.style.removeProperty("--nook-shift-y");
  };
  const onAnimationEnd = (event: AnimationEvent): void => {
    if (event.target === el) finish();
  };
  el.classList.remove("nook-repositioning");
  el.style.setProperty("--nook-shift-x", `${shiftX}px`);
  el.style.setProperty("--nook-shift-y", `${shiftY}px`);
  void el.offsetWidth;
  el.classList.add("nook-repositioning");
  el.addEventListener("animationend", onAnimationEnd);
  timer = window.setTimeout(finish, 220);
}

function animateNookRepositions(previousRects: ReadonlyMap<string, { left: number; top: number }>): void {
  for (const [nookId, previous] of previousRects) {
    const nook = nooks.get(nookId)?.el;
    if (!nook || nook.parentElement === null) continue;
    const current = nook.getBoundingClientRect();
    const shiftX = Math.round(previous.left - current.left);
    const shiftY = Math.round(previous.top - current.top);
    if (Math.abs(shiftX) < 1 && Math.abs(shiftY) < 1) continue;
    armNookReposition(nook, shiftX, shiftY);
  }
}


function makeNook(nookId: string, since: number): NookView {
  const el = document.createElement("div");
  el.className = "nook";
  armNookOpening(el);
  el.style.flexGrow = "1";
  const header = document.createElement("div");
  header.className = "nook-header";
  const stateDot = document.createElement("span");
  stateDot.className = "nh-dot";
  header.appendChild(stateDot);
  const titleSpan = document.createElement("span");
  titleSpan.className = "pt";
  titleSpan.textContent = "shell";
  const moreBtn = document.createElement("button");
  moreBtn.className = "pmore";
  moreBtn.textContent = "\u22ef";
  header.appendChild(titleSpan);
  const splitCtls: { icon: string; title: string; dir: "row" | "col" }[] = [
    { icon: "split-right", title: "Split right (Cmd D)", dir: "row" },
    { icon: "split-down", title: "Split down (Cmd Shift D)", dir: "col" },
  ];
  for (const ctl of splitCtls) {
    const b = document.createElement("button");
    b.className = "pmore psplit";
    b.innerHTML = iconSvg(ctl.icon);
    b.title = ctl.title;
    b.addEventListener("click", (e) => { e.stopPropagation(); focusNook(nookId); openSplitChooser(e, ctl.dir); });
    header.appendChild(b);
  }
  header.appendChild(moreBtn);
  const closeBtn = document.createElement("button");
  closeBtn.className = "pmore pclose";
  closeBtn.textContent = "✕";
  closeBtn.title = "Close nook";
  closeBtn.addEventListener("click", (e) => { e.stopPropagation(); focusNook(nookId); void closeFocused(); });
  header.appendChild(closeBtn);
  el.appendChild(header);
  const host = document.createElement("div");
  host.className = "term-host";
  host.addEventListener("mousedown", (event) => {
    if ((event.target as Element | null)?.closest?.(".xterm-viewport")) event.stopPropagation();
  }, true);
  el.appendChild(host);
  const resetOnReplay = shouldResetReplay({ locallySpawned: locallySpawnedNookIds.has(nookId), renderedBefore: renderedStreamNookIds.has(nookId) });
  renderedStreamNookIds.add(nookId);
  let pv: NookView;
  const session = new TerminalSession(
    nookId,
    since,
    el,
    host,
    {
      createResources: () => createTerminalResources(settings, currentTermTheme()),
      settings: () => settings,
      theme: currentTermTheme,
      invoke,
      write: writeNook,
      onExit: (exitedNookId) => {
        launcherFeature.invalidateRecents();
        void launcherFeature.refreshRecents();
        void closeNookById(exitedNookId);
      },
      createSocket: (url) => new WebSocket(url),
      warn: (message, context) => console.warn(message, context),
      onTitleChange: (title) => {
        pv.title = title;
        workspaceSidebar.rememberNookTitle(nookId, pv.customTitle || title);
        titleSpan.textContent = pv.customTitle || title || "shell";
        refreshTitles();
      },
    },
    resetOnReplay,
  );
  const term = session.term;

  let closeOwnedMenu = (): void => {};
  const openNookMenu = (x: number, y: number): void => {
    contextMenu.close();
    closeNookMenus();
    document.getElementById("nook-menu")?.remove();
    focusNook(nookId);
    const pop = document.createElement("div");
    pop.id = "nook-menu";
    pop.className = "nook-menu";
    let closed = false;
    const close = (): void => {
      if (closed) return;
      closed = true;
      pop.remove();
      document.removeEventListener("mousedown", onAway, true);
      document.removeEventListener("keydown", onKey, true);
      if (closeOwnedMenu === close) closeOwnedMenu = () => {};
    };
    const onAway = (ev: MouseEvent): void => {
      if (!pop.contains(ev.target as Node)) close();
    };
    const onKey = (ev: KeyboardEvent): void => {
      if (ev.key === "Escape") {
        ev.stopPropagation();
        close();
      }
    };
    closeOwnedMenu = close;
    const addRow = (icon: { svg?: string; glyph?: string }, label: string, fn: () => void, opts?: { kbd?: string; danger?: boolean }): void => {
      const r = document.createElement("button");
      r.className = "nm-row" + (opts?.danger ? " danger" : "");
      const g = document.createElement("span");
      g.className = "nm-glyph";
      if (icon.svg) g.innerHTML = icon.svg;
      else g.textContent = icon.glyph ?? "";
      const l = document.createElement("span");
      l.className = "nm-label";
      l.textContent = label;
      r.appendChild(g);
      r.appendChild(l);
      if (opts?.kbd) {
        const k = document.createElement("span");
        k.className = "nm-kbd";
        k.textContent = opts.kbd;
        r.appendChild(k);
      }
      r.addEventListener("click", (ev) => {
        ev.stopPropagation();
        close();
        fn();
      });
      pop.appendChild(r);
    };
    const addSep = (): void => {
      const s = document.createElement("div");
      s.className = "nm-sep";
      pop.appendChild(s);
    };
    addRow({ glyph: "⤢" }, "Maximize", () => runAction("nook.maximize"));
    addSep();
    addRow({ glyph: "✎" }, "Rename", () => startRename());
    addSep();
    addRow({ glyph: "⊟" }, "Close Others", () => void closeOthers(nookId));
    addRow({ glyph: "✕" }, "Close", () => { focusNook(nookId); void closeFocused(); }, { danger: true });
    document.body.appendChild(pop);
    const rect = pop.getBoundingClientRect();
    pop.style.left = `${Math.max(8, Math.min(x, window.innerWidth - rect.width - 8))}px`;
    pop.style.top = `${Math.max(8, Math.min(y, window.innerHeight - rect.height - 8))}px`;
    setTimeout(() => {
      if (closed) return;
      document.addEventListener("mousedown", onAway, true);
      document.addEventListener("keydown", onKey, true);
    }, 0);
  };
  header.addEventListener("contextmenu", (e) => {
    e.preventDefault();
    openNookMenu(e.clientX, e.clientY);
  });
  host.addEventListener("contextmenu", (e) => {
    focusNook(nookId);
    const hasSel = term.hasSelection();
    contextMenu.openAt(e, [
      { id: "copy", label: "Copy", disabled: !hasSel },
      { id: "paste", label: "Paste" },
      { id: "clear", label: "Clear" },
      { id: "sep", label: "", separator: true },
      { id: "find", label: "Find in Nook" },
    ], (id) => {
      if (id === "copy") { const s = term.getSelection(); if (s && navigator.clipboard) void navigator.clipboard.writeText(s); }
      else if (id === "paste") { if (navigator.clipboard && navigator.clipboard.readText) void navigator.clipboard.readText().then((t) => { if (t) void writeNook(nookId, toBase64Utf8(t)); }); }
      else if (id === "clear") term.clear();
      else if (id === "find") findFeature.open();
    });
  });

  pv = { nookId, session, el, title: "", customTitle: "", headerTitleEl: titleSpan, closeMenu: () => closeOwnedMenu() };

  el.addEventListener("mousedown", (event) => {
    if ((event.target as Element | null)?.closest?.(".xterm-viewport")) return;
    workspaceSidebar.acknowledgeAgentAttention(nookId);
    focusNook(nookId);
  });
  const setTitle = () => { titleSpan.textContent = pv.customTitle || pv.title || "shell"; };
  const isHeaderControl = (target: EventTarget | null): boolean =>
    (target as Element | null)?.closest?.("button, input, textarea, select, a, [contenteditable='true']") !== null;
  header.addEventListener("mousedown", (e) => {
    if (isHeaderControl(e.target)) {
      e.stopPropagation();
      return;
    }
    focusNook(nookId);
  });
  titleSpan.draggable = true;
  header.addEventListener("dragstart", (e) => {
    if (isHeaderControl(e.target) || !e.target || !titleSpan.contains(e.target as Node)) {
      e.preventDefault();
      e.stopPropagation();
      return;
    }
    if (!e.dataTransfer) return;
    e.dataTransfer.setData("text/cove-nook", nookId);
    e.dataTransfer.effectAllowed = "move";
    nookDrag.nookId = nookId;
  });
  header.addEventListener("dragend", () => { nookDrag.nookId = null; clearDropOverlay(); });
  el.addEventListener("dragover", (e) => {
    if (!nookDrag.nookId || nookDrag.nookId === nookId) return;
    e.preventDefault();
    if (e.dataTransfer) e.dataTransfer.dropEffect = "move";
    const rect = el.getBoundingClientRect();
    const zone = dropZoneFor(e.clientX - rect.left, e.clientY - rect.top, rect.width, rect.height);
    if (zone.kind === "center") clearDropOverlay();
    else paintDropOverlay(el, zone);
  });
  el.addEventListener("dragleave", (e) => { if (e.target === el) clearDropOverlay(); });
  el.addEventListener("drop", (e) => {
    e.preventDefault();
    const src = e.dataTransfer?.getData("text/cove-nook") || nookDrag.nookId;
    clearDropOverlay();
    nookDrag.nookId = null;
    if (!src || !workspace.activeShoreId) { console.warn("nook drop without source or active shore"); return; }
    const rect = el.getBoundingClientRect();
    const zone = dropZoneFor(e.clientX - rect.left, e.clientY - rect.top, rect.width, rect.height);
    const m = moveMutationFor(zone, src, nookId);
    if (!m) return;
    void applyNookMove(m, src);
  });
  const startRename = () => {
    const input = document.createElement("input");
    input.className = "prename";
    input.value = pv.customTitle || pv.title || "";
    titleSpan.replaceWith(input);
    input.focus();
    input.select();
    const finish = (commit: boolean) => {
      const newTitle = commit ? input.value.trim() : pv.customTitle;
      if (commit && newTitle !== pv.customTitle) {
        pv.customTitle = newTitle;
        workspaceSidebar.rememberNookTitle(nookId, newTitle || pv.title);
        void invoke(FrontendCommand.AppNookRename, { nookId, title: newTitle }).catch(() => void 0);
      }
      input.replaceWith(titleSpan);
      setTitle();
    };
    input.addEventListener("keydown", (e) => { e.stopPropagation(); if (e.key === "Enter") finish(true); else if (e.key === "Escape") finish(false); });
    input.addEventListener("blur", () => finish(true));
  };
  titleSpan.addEventListener("dblclick", startRename);
  moreBtn.addEventListener("click", (e) => {
    e.stopPropagation();
    const rect = moreBtn.getBoundingClientRect();
    openNookMenu(rect.right - 190, rect.bottom + 5);
  });
  nooks.set(nookId, pv);
  return pv;
}

function getNook(nookId: string): NookView {
  const existing = nooks.get(nookId);
  if (existing) return existing;
  return makeNook(nookId, 0);
}

function disposeNook(nookId: string): void {
  const pv = nooks.get(nookId);
  if (!pv) return;
  pv.closeMenu();
  pv.session.dispose();
  nooks.delete(nookId);
}

function allLayoutNookIds(): Set<string> {
  const ids = new Set<string>();
  for (const shore of workspace.snapshot?.shores ?? []) for (const id of collectLeafIds(shore.layoutTree)) ids.add(id);
  return ids;
}

function reconcileNookStreams(): void {
  const layoutIds = allLayoutNookIds();
  for (const [id, pv] of nooks) {
    const actions = streamReconciliationActions({
      inLayout: layoutIds.has(id),
      visible: pv.el.isConnected,
      connected: pv.session.connected,
      socketClosed: pv.session.socketClosed,
    });
    for (const action of actions) {
      if (action === "connect") pv.session.connect();
      else if (action === "disconnect") pauseNookStream(pv);
      else disposeNook(id);
    }
  }
}

function pauseNookStream(pv: NookView): void {
  pv.session.pause();
}

function closeNookMenus(): void {
  document.querySelectorAll(".pmenu").forEach((m) => m.remove());
}

lifecycle.listen(document, "click", closeNookMenus);

lifecycle.listen(document, "contextmenu", (e) => e.preventDefault());

function isColumn(orientation: number | string): boolean {
  return orientation === 1 || orientation === "Column" || orientation === "column";
}

function collectLeafIds(node: MosaicNode): string[] {
  if (node.kind === "leaf") return node.subtabs.length > 0 ? node.subtabs.map((s) => s.documentId) : [node.nookId];
  return [...collectLeafIds(node.childA), ...collectLeafIds(node.childB)];
}

function findNookLocation(node: MosaicNode, nookId: string): { leaf: NookLeaf; subtabIndex: number } | null {
  if (node.kind === "leaf") {
    const subtabIndex = node.subtabs.findIndex((s) => s.documentId === nookId);
    if (node.nookId === nookId || subtabIndex >= 0) return { leaf: node, subtabIndex };
    return null;
  }
  return findNookLocation(node.childA, nookId) ?? findNookLocation(node.childB, nookId);
}

async function activateSubtab(leafId: string, index: number): Promise<void> {
  if (!workspace.activeShoreId) return;
  await workspaceController.mutate("activateSubtab", { shoreId: workspace.activeShoreId, nookId: leafId, targetNookId: "", newNookId: "", orientation: "", name: "", dir: index });
}

function emptyNookStrip(nookId: string): HTMLElement {
  const el = document.createElement("div");
  el.className = "nook empty-nook";
  el.style.flex = "1 1 0";
  el.style.minWidth = "0";
  el.style.minHeight = "0";
  el.style.display = "flex";
  el.style.flexDirection = "column";
  el.style.alignItems = "center";
  el.style.justifyContent = "flex-end";
  el.style.paddingBottom = "24px";
  const strip = document.createElement("div");
  strip.className = "nook-dock";
  strip.style.display = "flex";
  strip.style.gap = "8px";
  const tile = document.createElement("button");
  tile.className = "dock-tile";
  tile.textContent = "Terminal";
  tile.addEventListener("click", (e) => {
    e.stopPropagation();
    void spawnIntoNook(nookId);
  });
  strip.appendChild(tile);
  el.appendChild(strip);
  el.addEventListener("mousedown", () => focusNook(nookId));
  return el;
}

async function spawnIntoNook(nookId: string): Promise<void> {
  if (!workspace.activeShoreId) return;
  const sp = (await spawnNook({ command: "", cwd: "", inheritCwdFrom: nookId, cols: 80, rows: 24, adapter: "", agentName: "", bay: "", shore: "" })).nookId;
  await workspaceController.mutate("addSubtab", { shoreId: workspace.activeShoreId, nookId: nookId, newNookId: sp, targetNookId: "", orientation: "", name: "", dir: 0 });
}

function bayIdForNook(nookType: string): string | null {
  const bayId = workspace.snapshot?.id;
  if (bayId) return bayId;
  console.warn("bay-scoped nook rendered without an active bay", nookType);
  return null;
}

function renderKanbanNook(nookId: string): HTMLElement {
  const bayId = bayIdForNook("tasks-kanban");
  if (!bayId) return document.createElement("div");
  return renderOwnedContent(`tasks-kanban:${nookId}:${bayId}`, "kanban-nook-placeholder", "kanban", () => renderKanbanBoard(bayId));
}

function renderTaskListNook(_nookId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "task-list-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  const bayId = bayIdForNook("tasks-list");
  if (!bayId) return placeholder;
  renderTaskList(bayId).then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load task list: ${(e as Error).message}</div>`;
  });
  return placeholder;
}

function renderTaskDetailNook(_nookId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "task-detail-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;display:flex;align-items:center;justify-content:center;color:#6b7280;";
  placeholder.textContent = "Select a task to view details";
  return placeholder;
}

function renderTimelineNook(_nookId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "timeline-nook-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  const bayId = bayIdForNook("timeline");
  if (!bayId) return placeholder;
  renderTimelineFeed(bayId).then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load timeline: ${(e as Error).message}</div>`;
  });
  return placeholder;
}

function renderMarkdownNoteNook(nookId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "markdown-note-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  const bayId = bayIdForNook("markdown-note");
  if (!bayId) return placeholder;
  renderMarkdownNote(bayId, nookId).then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load note: ${(e as Error).message}</div>`;
  });
  return placeholder;
}

function renderSketchNoteNook(nookId: string): HTMLElement {
  const bayId = bayIdForNook("sketch-note");
  if (!bayId) return document.createElement("div");
  return renderOwnedContent(`note-sketch:${nookId}:${bayId}`, "sketch-note-placeholder", "sketch", () => renderSketchNote(bayId, nookId));
}

function renderCanvasNoteNook(nookId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "canvas-note-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  const bayId = bayIdForNook("canvas-note");
  if (!bayId) return placeholder;
  renderCanvasNote(bayId, nookId).then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load canvas: ${(e as Error).message}</div>`;
  });
  return placeholder;
}

function renderHtmlNoteNook(nookId: string): HTMLElement {
  const bayId = bayIdForNook("html-note");
  if (!bayId) return document.createElement("div");
  return renderOwnedContent(`note-html:${nookId}:${bayId}`, "html-note-placeholder", "HTML note", () => renderHtmlNote(bayId, nookId));
}

function renderNotepadNookWrapper(_nookId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "notepad-nook-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  const bayId = bayIdForNook("notepad");
  if (!bayId) return placeholder;
  renderNotepadNook(bayId).then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load notepad: ${(e as Error).message}</div>`;
  });
  return placeholder;
}

function renderMermaidNoteNook(nookId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "mermaid-note-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  const bayId = bayIdForNook("mermaid-note");
  if (!bayId) return placeholder;
  renderMermaidNote(bayId, nookId).then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load mermaid note: ${(e as Error).message}</div>`;
  });
  return placeholder;
}

function renderSessionPickerNook(_nookId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "session-picker-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  const bayId = bayIdForNook("session-picker");
  if (!bayId) return placeholder;
  const projectDir = activeProjectDir();
  const adapters = launcherFeature.adapters.map((a) => ({ name: a.name, displayName: a.displayName }));
  renderSessionPicker(bayId, projectDir, adapters, (adapter, sessionId, cwd, displayName) => {
    void resumeRecentSession(adapter, sessionId, cwd, displayName);
  }).then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load session picker: ${(e as Error).message}</div>`;
  });
  return placeholder;
}

async function resumeRecentSession(adapter: string, sessionId: string, cwd: string, displayName: string): Promise<void> {
  let action: ResumeAction;
  try {
    const result = await invoke<VaultResumeResult>(FrontendCommand.VaultResume, { adapter, sessionId, cwd, yolo: launcherFeature.yolo(adapter) });
    action = resumeSpawnPlan(result, cwd, displayName, sessionId, launcherFeature.yolo(adapter));
  } catch (e) {
    console.warn("vault.resume failed", adapter, sessionId, e);
    action = { kind: "error", toast: { title: "Resume failed", body: (e as Error).message } };
  }
  await performResume(action);
}

async function performResume(action: ResumeAction): Promise<void> {
  if (action.kind === "error") {
    showInAppToast(action.toast.title, action.toast.body, () => {});
    return;
  }
  const sp = (await spawnNook({ command: action.command, args: action.args, cwd: action.cwd, inheritCwdFrom: "", cols: 80, rows: 24, adapter: action.adapter, agentName: action.shoreName, bay: "", shore: "", sessionId: action.sessionId ?? undefined, yolo: action.yolo })).nookId;
  const r = await workspaceController.mutate<{ shoreId: string }>("createShore", { newNookId: sp, name: action.shoreName, shoreId: "", targetNookId: "", orientation: "", nookId: "", dir: 0, nookType: "terminal" });
  workspace.activeShoreId = r.shoreId;
  focusNook(sp);
  if (action.toast) showInAppToast(action.toast.title, action.toast.body, () => revealNook(sp));
}

function renderLibraryNook(_nookId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "library-nook-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  const bayId = bayIdForNook("library");
  if (!bayId) return placeholder;
  renderLibraryPopover(bayId).then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load library: ${(e as Error).message}</div>`;
  });
  return placeholder;
}

function renderSnapshotInspectorNook(_nookId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "snapshot-inspector-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  const bayId = bayIdForNook("snapshot-inspector");
  if (!bayId) return placeholder;
  renderSnapshotInspector(bayId).then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load snapshots: ${(e as Error).message}</div>`;
  });
  return placeholder;
}

function renderDiffReviewNookWrapper(nookId: string): HTMLElement {
  const bayId = bayIdForNook("diff-review");
  if (!bayId) return document.createElement("div");
  return renderOwnedContent(`diff-review:${nookId}:${bayId}`, "diff-review-placeholder", "diff review", () => renderDiffReviewNook(bayId));
}

function renderEditorNookWrapper(nookId: string): HTMLElement {
  const filePath = nookFilePaths.get(nookId) ?? nookId;
  return renderOwnedContent(`editor:${nookId}:${filePath}`, "editor-nook-placeholder", "editor", () => renderEditorNook(nookId, filePath));
}

function renderImageNook(nookId: string): HTMLElement {
  const imagePath = nookFilePaths.get(nookId) ?? nookId;
  return renderOwnedContentSync(`image:${nookId}:${imagePath}`, () => {
  const scope = new LifecycleScope();
  const el = document.createElement("div");
  el.className = "image-nook";
  el.style.cssText = "display:flex;align-items:center;justify-content:center;height:100%;background:#0d1117;overflow:hidden;position:relative;";
  const img = document.createElement("img");
  img.style.cssText = "max-width:100%;max-height:100%;object-fit:contain;transition:transform 0.1s;";
  img.alt = imagePath.split("/").pop() || imagePath;
  mediaUrl(imagePath).then((url) => {
    if (scope.isDisposed) return;
    img.src = url;
  }).catch((err) => {
    if (scope.isDisposed) return;
    console.warn("image media lease failed", imagePath, err);
  });
  const controls = document.createElement("div");
  controls.style.cssText = "position:absolute;bottom:8px;right:8px;display:flex;gap:4px;background:#21262d;padding:4px;border-radius:4px;";
  const fitBtn = document.createElement("button");
  fitBtn.textContent = "Fit";
  fitBtn.style.cssText = "padding:2px 8px;background:#30363d;border:none;color:#e6edf3;border-radius:3px;cursor:pointer;font-size:11px;";
  const zoomInBtn = document.createElement("button");
  zoomInBtn.textContent = "+";
  zoomInBtn.style.cssText = "padding:2px 8px;background:#30363d;border:none;color:#e6edf3;border-radius:3px;cursor:pointer;font-size:11px;";
  const zoomOutBtn = document.createElement("button");
  zoomOutBtn.textContent = "-";
  zoomOutBtn.style.cssText = "padding:2px 8px;background:#30363d;border:none;color:#e6edf3;border-radius:3px;cursor:pointer;font-size:11px;";
  let zoom = 1;
  scope.listen(fitBtn, "click", () => { img.style.transform = "scale(1)"; zoom = 1; });
  scope.listen(zoomInBtn, "click", () => { zoom = Math.min(zoom * 1.25, 10); img.style.transform = `scale(${zoom})`; });
  scope.listen(zoomOutBtn, "click", () => { zoom = Math.max(zoom / 1.25, 0.1); img.style.transform = `scale(${zoom})`; });
  controls.appendChild(fitBtn);
  controls.appendChild(zoomOutBtn);
  controls.appendChild(zoomInBtn);
  el.appendChild(img);
  el.appendChild(controls);
  scope.own(() => {
    img.removeAttribute("src");
    el.remove();
  });
  return { element: el, dispose: () => scope.dispose() };
  });
}

function activeProjectDir(): string {
  return resolveLauncherProjectDir(workspace.snapshot, workspaceSidebar.bayBoxes, workspaceSidebar.defaultDirectory);
}

function renderGitNookWrapper(_nookId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "git-nook-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  renderSourceControlNook(activeProjectDir(), (path) => { void openFileInEditor(path); }).then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load source control: ${(e as Error).message}</div>`;
  });
  return placeholder;
}

function renderSearchNookWrapper(_nookId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "search-nook-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  const bayId = bayIdForNook("search");
  if (!bayId) return placeholder;
  renderSearchNook(bayId).then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load search: ${(e as Error).message}</div>`;
  });
  return placeholder;
}

function wrapToolNookChrome(nookId: string, label: string, content: HTMLElement): HTMLElement {
  const el = document.createElement("div");
  el.className = "nook tool-nook";
  el.style.flexGrow = "1";
  const header = document.createElement("div");
  header.className = "nook-header";
  const title = document.createElement("span");
  title.className = "pt";
  title.textContent = label;
  header.appendChild(title);
  const closeBtn = document.createElement("button");
  closeBtn.className = "pmore pclose";
  closeBtn.textContent = "✕";
  closeBtn.title = "Close nook";
  closeBtn.addEventListener("click", (e) => { e.stopPropagation(); void closeNookById(nookId); });
  header.appendChild(closeBtn);
  el.appendChild(header);
  content.style.flex = "1 1 0";
  content.style.minWidth = "0";
  content.style.minHeight = "0";
  el.appendChild(content);
  return el;
}

function renderBrowserNookWrapper(nookId: string, url: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "browser-nook-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  renderBrowserNook(nookId, url).then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    console.warn("browser nook load failed", nookId, e);
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load browser: ${(e as Error).message}</div>`;
  });
  return wrapToolNookChrome(nookId, "Browser", placeholder);
}

function renderDiffViewerNookWrapper(nookId: string, refInput: string): HTMLElement {
  return renderOwnedContent(`diff:${nookId}:${refInput}`, "diff-viewer-placeholder", "diff", () => renderDiffViewerNook(nookId, nookId, refInput));
}

function renderMarkdownNookWrapper(nookId: string): HTMLElement {
  return renderOwnedContent(`markdown:${nookId}`, "markdown-nook-placeholder", "markdown", () => renderMarkdownNook(nookId, nookId));
}

function renderNode(node: MosaicNode): HTMLElement {
  if (node.kind === "leaf") {
    const subs = node.subtabs.length > 0 ? node.subtabs : [{ documentId: node.nookId, nookType: "terminal", title: null }];
    const activeIdx = Math.min(Math.max(0, node.activeSubtab), subs.length - 1);
    const active = subs[activeIdx];
    const isEmpty = active.nookType === "empty" || node.subtabs.length === 0;
    if (active.nookType === "tasks-kanban") return renderKanbanNook(active.documentId);
    if (active.nookType === "tasks-list") return renderTaskListNook(active.documentId);
    if (active.nookType === "tasks-detail") return renderTaskDetailNook(active.documentId);
    if (active.nookType === "timeline-feed") return renderTimelineNook(active.documentId);
    if (active.nookType === "note-markdown") return renderMarkdownNoteNook(active.documentId);
    if (active.nookType === "note-sketch") return renderSketchNoteNook(active.documentId);
    if (active.nookType === "note-canvas") return renderCanvasNoteNook(active.documentId);
    if (active.nookType === "note-html") return renderHtmlNoteNook(active.documentId);
    if (active.nookType === "markdown") return renderMarkdownNookWrapper(active.documentId);
    if (active.nookType === "notepad") return renderNotepadNookWrapper(active.documentId);
    if (active.nookType === "note-mermaid") return renderMermaidNoteNook(active.documentId);
    if (active.nookType === "session-picker") return renderSessionPickerNook(active.documentId);
    if (active.nookType === "library") return renderLibraryNook(active.documentId);
    if (active.nookType === "snapshot-inspector") return renderSnapshotInspectorNook(active.documentId);
    if (active.nookType === "diff-review") return renderDiffReviewNookWrapper(active.documentId);
    if (active.nookType === "editor") return renderEditorNookWrapper(active.documentId);
    if (active.nookType === "image") return renderImageNook(active.documentId);
    if (active.nookType === "git" || active.nookType === "sourceControl") return renderGitNookWrapper(active.documentId);
    if (active.nookType === "search") return renderSearchNookWrapper(active.documentId);
    if (active.nookType === "browser") return renderBrowserNookWrapper(active.documentId, active.title ?? "about:blank");
    if (active.nookType === "diff") return renderDiffViewerNookWrapper(active.documentId, active.title ?? "");
    if (active.nookType === "pdf") {
      const filePath = nookFilePaths.get(active.documentId) ?? active.title ?? active.documentId;
      return renderOwnedContentSync(`pdf:${active.documentId}:${filePath}`, () => renderPdfNook(filePath));
    }
    if (active.nookType === "video") {
      const filePath = nookFilePaths.get(active.documentId) ?? active.title ?? active.documentId;
      return renderOwnedContentSync(`video:${active.documentId}:${filePath}`, () => renderVideoNook(filePath));
    }
    if (isEmpty) return emptyNookStrip(node.nookId);
    const activeEl = getNook(subs[activeIdx].documentId).el;
    activeEl.style.flexGrow = "1";
    if (subs.length <= 1) return activeEl;
    const wrap = document.createElement("div");
    wrap.className = "leaf-wrap";
    const strip = document.createElement("div");
    strip.className = "subtab-strip";
    subs.forEach((s, i) => {
      const tab = document.createElement("div");
      tab.className = "subtab" + (i === activeIdx ? " active" : "");
      const pvv = nooks.get(s.documentId);
      tab.textContent = (pvv && (pvv.customTitle || pvv.title)) || s.title || "shell";
      tab.addEventListener("click", () => { void activateSubtab(node.nookId, i); });
      strip.appendChild(tab);
    });
    wrap.appendChild(strip);
    wrap.appendChild(activeEl);
    return wrap;
  }
  const col = isColumn(node.orientation);
  const container = document.createElement("div");
  container.className = "split" + (col ? " col" : "");
  container.style.display = "flex";
  container.style.flex = "1 1 0";
  container.style.minWidth = "0";
  container.style.minHeight = "0";
  if (col) container.style.flexDirection = "column";

  const a = renderNode(node.childA);
  const b = renderNode(node.childB);
  const div = document.createElement("div");
  div.className = "divider";
  container.appendChild(a);
  container.appendChild(div);
  container.appendChild(b);

  const r = node.ratio > 0 && node.ratio < 1 ? node.ratio : 0.5;
  a.style.flexGrow = String(r);
  b.style.flexGrow = String(1 - r);

  wireSplitDivider(div, col, a, b);
  return container;
}

function wireSplitDivider(div: HTMLElement, col: boolean, a: HTMLElement, b: HTMLElement) {
  div.addEventListener("mousedown", (e) => {
    e.preventDefault();
    activeSplitDragCleanup?.();
    const parent = div.parentElement;
    if (!parent) return;
    const rect = parent.getBoundingClientRect();
    const total = col ? rect.height : rect.width;
    const start = col ? e.clientY : e.clientX;
    const ga = parseFloat(a.style.flexGrow || "1");
    const gb = parseFloat(b.style.flexGrow || "1");
    const sum = ga + gb;
    const onMove = (m: MouseEvent) => {
      const frac = ((col ? m.clientY : m.clientX) - start) / total;
      const na = Math.max(sum * 0.12, Math.min(sum * 0.88, ga + frac * sum));
      a.style.flexGrow = String(na);
      b.style.flexGrow = String(sum - na);
      fitAll();
    };
    const cleanup = () => {
      document.removeEventListener("mousemove", onMove);
      document.removeEventListener("mouseup", onUp);
      if (activeSplitDragCleanup === cleanup) activeSplitDragCleanup = null;
    };
    const onUp = () => {
      cleanup();
      fitAll();
    };
    document.addEventListener("mousemove", onMove);
    document.addEventListener("mouseup", onUp);
    activeSplitDragCleanup = cleanup;
  });
}

function activeShore(): ShoreSnapshot | undefined {
  if (!workspace.snapshot) return undefined;
  return workspace.snapshot.shores.find((r) => r.id === workspace.activeShoreId) ?? workspace.snapshot.shores[0];
}

function activeLeafIds(): string[] {
  const shore = activeShore();
  if (!shore) return [];
  return collectLeafIds(shore.layoutTree);
}

function firstLeafOf(shore: ShoreSnapshot): string | undefined {
  return collectLeafIds(shore.layoutTree)[0];
}

function captureNookViewports(): void {
  for (const pv of nooks.values()) pv.session.captureViewport();
}

function collectOwnedContentKeys(node: MosaicNode | null, keys: Set<string>): void {
  if (!node) return;
  if (node.kind !== "leaf") {
    collectOwnedContentKeys(node.childA, keys);
    collectOwnedContentKeys(node.childB, keys);
    return;
  }
  const subtabs = node.subtabs.length > 0 ? node.subtabs : [{ documentId: node.nookId, nookType: "terminal", title: null }];
  const active = subtabs[Math.min(Math.max(0, node.activeSubtab), subtabs.length - 1)];
  const bayId = workspace.snapshot?.id;
  if (active.nookType === "tasks-kanban" && bayId) keys.add(`tasks-kanban:${active.documentId}:${bayId}`);
  else if (active.nookType === "note-sketch" && bayId) keys.add(`note-sketch:${active.documentId}:${bayId}`);
  else if (active.nookType === "note-html" && bayId) keys.add(`note-html:${active.documentId}:${bayId}`);
  else if (active.nookType === "diff-review" && bayId) keys.add(`diff-review:${active.documentId}:${bayId}`);
  else if (active.nookType === "editor") {
    const filePath = nookFilePaths.get(active.documentId) ?? active.documentId;
    keys.add(`editor:${active.documentId}:${filePath}`);
  } else if (active.nookType === "diff") keys.add(`diff:${active.documentId}:${active.title ?? ""}`);
  else if (active.nookType === "markdown") keys.add(`markdown:${active.documentId}`);
  else if (active.nookType === "pdf") {
    const filePath = nookFilePaths.get(active.documentId) ?? active.title ?? active.documentId;
    keys.add(`pdf:${active.documentId}:${filePath}`);
  } else if (active.nookType === "video") {
    const filePath = nookFilePaths.get(active.documentId) ?? active.title ?? active.documentId;
    keys.add(`video:${active.documentId}:${filePath}`);
  } else if (active.nookType === "image") {
    const filePath = nookFilePaths.get(active.documentId) ?? active.documentId;
    keys.add(`image:${active.documentId}:${filePath}`);
  }
}

function renderShore(): void {
  const shore = activeShore();
  const previousNookRects = captureNookRects();
  captureNookViewports();
  renderGeneration += 1;
  activeSplitDragCleanup?.();
  const desiredContent = new Set<string>();
  if (!shoreTabsFeature.overviewVisible && shore?.layoutTree && !isEmptyShoreTree(shore.layoutTree)) {
    collectOwnedContentKeys(shore.layoutTree, desiredContent);
  }
  for (const record of [...ownedContent.values()]) {
    if (!desiredContent.has(record.key)) disposeOwnedContent(record);
  }
  gridEl.innerHTML = "";
  const shoreEmpty = shore ? isEmptyShoreTree(shore.layoutTree) : false;
  if (shoreTabsFeature.overviewVisible) {
    workspace.focusedNookId = null;
    gridEl.appendChild(launcherFeature.render(null, null));
  } else if (shore && shore.layoutTree && !shoreEmpty) {
    const treeIds = collectLeafIds(shore.layoutTree);
    const zid = shore.zoomedNookId;
    if (zid && treeIds.includes(zid)) {
      const zoomEl = getNook(zid).el;
      zoomEl.style.flexGrow = "1";
      gridEl.appendChild(zoomEl);
      workspace.focusedNookId = zid;
    } else {
      gridEl.appendChild(renderNode(shore.layoutTree));
    }
  }
  if (!shoreTabsFeature.overviewVisible && shore && shoreEmpty) {
    const placeholder = collectLeafIds(shore.layoutTree)[0] ?? null;
    workspace.focusedNookId = null;
    gridEl.appendChild(launcherFeature.render(shore.id, placeholder));
  } else if (!shoreTabsFeature.overviewVisible && (!shore || !shore.layoutTree)) {
    if (shouldShowLauncher((workspace.snapshot?.shores ?? []).length)) {
      gridEl.appendChild(launcherFeature.render(null, null));
    } else {
      const empty = buildEmptyState({ message: EmptyStateMessages.noShores, actionLabel: "New terminal", actionIcon: "+" });
      const action = empty.querySelector(".cove-empty-action");
      if (action) action.addEventListener("click", () => void newShore());
      gridEl.appendChild(empty);
    }
  }
  animateNookRepositions(previousNookRects);
  reconcileNookStreams();
  for (const [id, pv] of nooks) {
    pv.el.classList.toggle("focused", id === workspace.focusedNookId);
  }
  workspaceSidebar.syncAgentNookStateClasses();
  fitAll();
  window.requestAnimationFrame(() => { fitAll(); reconcileBrowserBounds(); });
}

function focusNook(nookId: string): void {
  if (shoreTabsFeature.overviewVisible) {
    shoreTabsFeature.overviewVisible = false;
    renderShore();
  }
  workspace.focusedNookId = nookId;
  for (const [id, pv] of nooks) {
    pv.el.classList.toggle("focused", id === nookId);
  }
  nooks.get(nookId)?.session.term.focus();
  refreshTitles();
  if (workspaceSidebar.isModeVisible("bays")) workspaceSidebar.renderContent("left");
  if (workspace.activeShoreId) {
    void workspaceController.mutate("focus", { shoreId: workspace.activeShoreId, nookId, targetNookId: "", newNookId: "", orientation: "", name: "", dir: 0 });
  }
}

function refreshTitles(): void {
  const tabEls = shoreTabsEl.querySelectorAll<HTMLElement>(".rtab");
  (workspace.snapshot?.shores ?? []).forEach((r) => {
    const tab = Array.from(tabEls).find((el) => el.title === shoreTabsFeature.tabName(r) || el.querySelector(".rtab-name")?.textContent === shoreTabsFeature.tabName(r));
    if (tab) {
      const nameEl = tab.querySelector<HTMLElement>(".rtab-name");
      if (nameEl) nameEl.textContent = shoreTabsFeature.tabName(r);
      tab.title = shoreTabsFeature.tabName(r);
    }
  });
}

  return {
    get nooks() { return nooks; },
    spawn: spawnNook,
    getNook,
    disposeNook,
    closeNookMenus,
    setNookFilePath(nookId: string, path: string) { nookFilePaths.set(nookId, path); },
    collectLeafIds,
    findNookLocation,
    activeShore,
    activeLeafIds,
    firstLeafOf,
    captureNookViewports,
    render: renderShore,
    focus: focusNook,
    refreshTitles,
    fitAll,
    applySettings,
    syncTitlebarWorkspaceOffset,
    resumeRecentSession,
    activeProjectDir,
    async dispose() {
      activeSplitDragCleanup?.();
      for (const record of [...ownedContent.values()]) disposeOwnedContent(record);
      await Promise.all([...pendingContentDisposals]);
      gridEl.innerHTML = "";
      for (const nookId of [...nooks.keys()]) disposeNook(nookId);
      await lifecycle.dispose();
    },
  };
}
