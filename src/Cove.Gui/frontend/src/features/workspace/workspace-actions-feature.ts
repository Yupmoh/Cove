import { buildAdapterTiles, isPlaceholderLeaf, placeableNookForAction } from "../../box-launcher";
import { detectedHarnessTiles } from "../../launcher-model";
import { bayHeadNavigation } from "../../bay-cards";
import { dropZoneFor, zoneOverlayRect, type DropZone, type MoveMutation } from "../../nook-dnd";
import type { WorkspaceStore, BaySnapshot, MosaicNode } from "../../workspace/workspace-store";
import type { WorkspaceController } from "../../workspace/workspace-controller";
import type { WorkspaceViewFeature } from "./workspace-view-feature";
import type { ShoreTabsFeature } from "../navigation/shore-tabs-feature";
import type { WorkspaceSidebarFeature } from "../navigation/workspace-sidebar-feature";
import type { LauncherFeature } from "../launcher/launcher-feature";
import type { ComponentHandle } from "../../app/lifecycle";
import { FrontendCommand } from "../../app/frontend-command";

export interface WorkspaceActionsDependencies {
  document: Document;
  workspace: WorkspaceStore;
  workspaceController: WorkspaceController;
  workspaceView: WorkspaceViewFeature;
  shoreTabsFeature: ShoreTabsFeature;
  workspaceSidebar: WorkspaceSidebarFeature;
  launcherFeature: LauncherFeature;
  invoke<T>(command: FrontendCommand, args: Record<string, unknown>): Promise<T>;
  runAction(action: string): void;
  closeBrowserNook(nookId: string): Promise<void>;
  reconcileBrowserNooks(): void;
}

export interface WorkspaceActionsFeature extends ComponentHandle {
  reload(): Promise<BaySnapshot>;
  splitActive(direction: "row" | "col"): Promise<void>;
  moveNookToShore(nookId: string, shoreId: string): Promise<void>;
  paintDropOverlay(host: HTMLElement, zone: DropZone): void;
  clearDropOverlay(): void;
  applyNookMove(mutation: MoveMutation, sourceNookId: string): Promise<void>;
  closeNookById(nookId: string): Promise<void>;
  splitActiveWith(direction: "row" | "col", kind: string): Promise<void>;
  closeFocused(): Promise<void>;
  closeOthers(keepNookId: string): Promise<void>;
  toggleZoom(): Promise<void>;
  cycleFocus(direction: number): void;
  newShore(): Promise<void>;
  safeReplaceTarget(shoreId: string, placeholderId: string | null): string | null;
  launchTileInto(shoreId: string | null, placeholderId: string | null, action: string): Promise<void>;
  newBrowserShore(url: string): Promise<void>;
  closeShore(shoreId: string): Promise<void>;
  openBayLauncher(bayId: string): Promise<void>;
  switchBay(bayId: string, targetShoreId?: string | null, targetNookId?: string | null, showLauncher?: boolean): Promise<void>;
  openTaskInNook(taskId: string): Promise<void>;
  openFileInEditor(path: string): Promise<void>;
  openToolShore(nookType: string, name: string): Promise<void>;
  scrollActiveNook(toTop: boolean): void;
  nextShore(direction: number): void;
  switchBayByIndex(index: number): Promise<void>;
  revealNook(nookId: string): Promise<void>;
}

export function createWorkspaceActionsFeature(dependencies: WorkspaceActionsDependencies): WorkspaceActionsFeature {
  const document = dependencies.document;
  const workspace = dependencies.workspace;
  const workspaceController = dependencies.workspaceController;
  const workspaceView = dependencies.workspaceView;
  const shoreTabsFeature = dependencies.shoreTabsFeature;
  const workspaceSidebar = dependencies.workspaceSidebar;
  const launcherFeature = dependencies.launcherFeature;
  const invoke = dependencies.invoke;
  const runAction = dependencies.runAction;
  const closeBrowserNook = dependencies.closeBrowserNook;
  const reconcileBrowserNooks = dependencies.reconcileBrowserNooks;

let reloadGeneration = 0;

function applyLayoutSnapshot(snapshot: BaySnapshot): void {
  workspace.applySnapshot(snapshot);
  if (snapshot.activeWingId) shoreTabsFeature.setActiveWing(snapshot.activeWingId);
  workspaceView.render();
  shoreTabsFeature.render();
  workspaceSidebar.render();
  if (workspace.focusedNookId) {
    workspaceView.nooks.get(workspace.focusedNookId)?.session.term.focus();
  }
  workspaceView.refreshTitles();
}

async function hydrateNookTitles(generation: number): Promise<void> {
  try {
    const list = await invoke<{ nooks: { nookId: string; title: string | null }[] }>(FrontendCommand.AppNookList, {});
    if (generation !== reloadGeneration) return;
    for (const p of list.nooks) {
      const pv = workspaceView.nooks.get(p.nookId);
      if (pv && p.title) pv.customTitle = p.title;
    }
    workspaceView.refreshTitles();
  } catch { void 0; }
}

async function reload(): Promise<BaySnapshot> {
  const generation = ++reloadGeneration;
  const snapshot = await invoke<BaySnapshot>(FrontendCommand.AppLayoutGet, {});
  if (generation !== reloadGeneration) return snapshot;
  applyLayoutSnapshot(snapshot);
  void hydrateNookTitles(generation);
  return snapshot;
}

async function splitActive(dir: "row" | "col"): Promise<void> {
  if (!workspace.snapshot || workspace.snapshot.shores.length === 0 || !workspace.activeShoreId) {
    await newShore();
    return;
  }
  const src = workspace.focusedNookId;
  if (!src) return;
  const sp = (await workspaceView.spawn({ command: "", cwd: "", inheritCwdFrom: src, cols: 80, rows: 24, adapter: "", agentName: "", bay: "", shore: "" })).nookId;
  await workspaceController.mutate("split", { shoreId: workspace.activeShoreId, targetNookId: src, newNookId: sp, orientation: dir, name: "", nookId: "", dir: 0 });
  workspaceView.focus(sp);
}

let dropOverlayEl: HTMLElement | null = null;

async function moveNookToShore(nookId: string, targetShoreId: string): Promise<void> {
  try {
    await workspaceController.mutate("moveNookToShore", { shoreId: targetShoreId, nookId, targetNookId: "", newNookId: "", orientation: "", name: "", dir: 0 });
    workspace.activeShoreId = targetShoreId;
    workspaceView.focus(nookId);
  } catch (err) { console.warn("move nook to shore failed", nookId, targetShoreId, err); }
}

function paintDropOverlay(host: HTMLElement, zone: ReturnType<typeof dropZoneFor>): void {
  if (!dropOverlayEl) {
    dropOverlayEl = document.createElement("div");
    dropOverlayEl.className = "drop-overlay";
  }
  if (dropOverlayEl.parentElement !== host) host.appendChild(dropOverlayEl);
  const r = zoneOverlayRect(zone);
  dropOverlayEl.style.left = r.left;
  dropOverlayEl.style.top = r.top;
  dropOverlayEl.style.width = r.width;
  dropOverlayEl.style.height = r.height;
}

function clearDropOverlay(): void {
  dropOverlayEl?.remove();
}

async function applyNookMove(m: { op: string; nookId: string; targetNookId: string; orientation: string; dir: number }, sourceNookId: string): Promise<void> {
  if (!workspace.activeShoreId) { console.warn("nook move without active shore"); return; }
  try {
    const srcShore = workspace.snapshot?.shores.find((r) => workspaceView.collectLeafIds(r.layoutTree).includes(m.nookId));
    if (srcShore && srcShore.id !== workspace.activeShoreId) {
      await workspaceController.mutate("moveNookToShore", { shoreId: workspace.activeShoreId, nookId: m.nookId, targetNookId: "", newNookId: "", orientation: "", name: "", dir: 0 });
    }
    if (m.op === "centerDrop") {
      const shore = workspaceView.activeShore();
      const srcLeaf = shore ? findLeaf(shore.layoutTree, m.nookId) : null;
      const idx = srcLeaf ? Math.max(0, srcLeaf.activeSubtab) : 0;
      await workspaceController.mutate("centerDrop", { shoreId: workspace.activeShoreId, targetNookId: m.nookId, nookId: m.targetNookId, dir: idx, newNookId: "", orientation: "", name: "" });
    } else {
      await workspaceController.mutate("moveNook", { shoreId: workspace.activeShoreId, targetNookId: m.targetNookId, nookId: sourceNookId, orientation: m.orientation, dir: m.dir, newNookId: "", name: "" });
    }
    workspaceView.focus(sourceNookId);
  } catch (err) { console.warn("nook move failed", m.op, err); }
}

function findLeaf(node: MosaicNode, nookId: string): { nookId: string; activeSubtab: number } | null {
  if (node.kind === "leaf") return node.nookId === nookId ? { nookId: node.nookId, activeSubtab: node.activeSubtab } : null;
  return findLeaf(node.childA, nookId) ?? findLeaf(node.childB, nookId);
}

function nookTypeIn(node: MosaicNode, nookId: string): string | null {
  if (node.kind === "split") {
    return nookTypeIn(node.childA, nookId) ?? nookTypeIn(node.childB, nookId);
  }
  const subtab = node.subtabs.find((item) => item.documentId === nookId);
  if (subtab) return subtab.nookType;
  return node.nookId === nookId ? "terminal" : null;
}

function nookType(nookId: string): string | null {
  for (const shore of workspace.snapshot?.shores ?? []) {
    const type = nookTypeIn(shore.layoutTree, nookId);
    if (type) return type;
  }
  return null;
}

async function killNookProcess(nookId: string): Promise<void> {
  if (nookType(nookId) === "browser") return;
  try {
    await invoke(FrontendCommand.AppNookKill, { nookId });
  } catch (error) {
    console.warn("nook kill failed", nookId, error);
  }
}

async function closeNookById(nookId: string): Promise<void> {
  const shore = workspace.snapshot?.shores.find((r) => workspaceView.collectLeafIds(r.layoutTree).includes(nookId));
  if (!shore) { console.warn("close requested for nook not in layout", nookId); return; }
  await closeBrowserNook(nookId);
  await killNookProcess(nookId);
  try { await workspaceController.mutate("close", { shoreId: shore.id, nookId, targetNookId: "", newNookId: "", orientation: "", name: "", dir: 0 }); } catch (err) { console.warn("layout close on exit failed", nookId, err); }
  workspaceView.disposeNook(nookId);
}

async function splitActiveWith(dir: "row" | "col", kind: string): Promise<void> {
  if (!workspace.activeShoreId) { console.warn("split requested with no active shore"); return; }
  const target = workspace.focusedNookId ?? workspaceView.activeLeafIds()[0];
  if (!target) { console.warn("split requested with no target nook"); return; }
  let nookId: string;
  let nookType = "terminal";
  if (kind === "terminal") {
    nookId = (await workspaceView.spawn({ command: "", cwd: "", inheritCwdFrom: target, cols: 80, rows: 24, adapter: "", agentName: "", bay: "", shore: "" })).nookId;
  } else if (kind.startsWith("adapter:")) {
    const name = kind.slice("adapter:".length);
    const tile = detectedHarnessTiles(buildAdapterTiles(launcherFeature.adapters)).find((t) => t.adapterName === name);
    if (!tile) { console.warn("split chooser: unknown adapter", name); return; }
    const launch = await launcherFeature.buildAdapterLaunch({ name: tile.adapterName, displayName: tile.label, accent: tile.accent, binary: tile.binary });
    nookId = (await workspaceView.spawn({ command: launch.command, args: launch.args, cwd: "", inheritCwdFrom: target, cols: 80, rows: 24, adapter: tile.adapterName, agentName: tile.label, bay: "", shore: "", yolo: launch.yolo })).nookId;
  } else if (kind === "browser") {
    nookId = (await invoke<{ nookId: string; currentUrl: string }>(FrontendCommand.BrowserCreate, { url: "https://duckduckgo.com" })).nookId;
    nookType = "browser";
  } else {
    nookId = (await workspaceView.spawn({ command: "", cwd: "", inheritCwdFrom: "", cols: 80, rows: 24, adapter: "", agentName: "", bay: "", shore: "" })).nookId;
    nookType = kind;
  }
  await workspaceController.mutate("split", { shoreId: workspace.activeShoreId, targetNookId: target, newNookId: nookId, orientation: dir, name: "", nookId: "", dir: 0, nookType });
  workspaceView.focus(nookId);
}

async function closeFocused(): Promise<void> {
  if (!workspace.focusedNookId || !workspace.activeShoreId) return;
  const nookId = workspace.focusedNookId;
  await closeBrowserNook(nookId);
  await killNookProcess(nookId);
  await workspaceController.mutate("close", { shoreId: workspace.activeShoreId, nookId, targetNookId: "", newNookId: "", orientation: "", name: "", dir: 0 });
  workspaceView.disposeNook(nookId);
}

async function closeOthers(keepNookId: string): Promise<void> {
  if (!workspace.activeShoreId) return;
  const shore = workspaceView.activeShore();
  if (!shore) return;
  const others = workspaceView.collectLeafIds(shore.layoutTree).filter((id) => id !== keepNookId);
  for (const id of others) {
    await closeBrowserNook(id);
    await killNookProcess(id);
    try { await workspaceController.mutate("close", { shoreId: workspace.activeShoreId, nookId: id, targetNookId: "", newNookId: "", orientation: "", name: "", dir: 0 }); } catch { void 0; }
    workspaceView.disposeNook(id);
  }
  workspaceView.focus(keepNookId);
}

async function toggleZoom(): Promise<void> {
  if (!workspace.focusedNookId || !workspace.activeShoreId) return;
  const shore = workspaceView.activeShore();
  if (shore && shore.zoomedNookId === workspace.focusedNookId) {
    await workspaceController.mutate("unzoom", { shoreId: workspace.activeShoreId, nookId: "", targetNookId: "", newNookId: "", orientation: "", name: "", dir: 0 });
  } else {
    await workspaceController.mutate("zoom", { shoreId: workspace.activeShoreId, nookId: workspace.focusedNookId, targetNookId: "", newNookId: "", orientation: "", name: "", dir: 0 });
  }
}

function cycleFocus(d: number): void {
  const leaves = workspaceView.activeLeafIds();
  if (leaves.length === 0) return;
  const idx = workspace.focusedNookId ? leaves.indexOf(workspace.focusedNookId) : -1;
  const next = leaves[(idx + d + leaves.length) % leaves.length];
  workspaceView.focus(next);
}

function newPlaceholderId(): string {
  const rnd = (globalThis.crypto && "randomUUID" in globalThis.crypto) ? globalThis.crypto.randomUUID() : Math.random().toString(36).slice(2);
  return "empty-" + rnd;
}

async function newShore(): Promise<void> {
  const placeholder = newPlaceholderId();
  const r = await workspaceController.mutate<{ shoreId: string }>("createShore", { newNookId: placeholder, name: shoreTabsFeature.nextName(), shoreId: "", targetNookId: "", orientation: "", nookId: "", dir: 0, nookType: "empty" });
  workspace.activeShoreId = r.shoreId;
  workspace.focusedNookId = null;
}

function safeReplaceTarget(shoreId: string, placeholderId: string | null): string | null {
  if (!placeholderId) return null;
  const shore = workspace.snapshot?.shores.find((r) => r.id === shoreId);
  if (!shore) { console.warn("replace target shore missing", shoreId, placeholderId); return null; }
  if (!isPlaceholderLeaf(shore.layoutTree, placeholderId)) { console.warn("refusing to replace a live nook leaf", shoreId, placeholderId); return null; }
  return placeholderId;
}

async function placeNookIntoShore(shoreId: string, placeholderId: string | null, nookId: string, nookType: string, shoreName?: string): Promise<void> {
  const safePlaceholder = safeReplaceTarget(shoreId, placeholderId);
  if (safePlaceholder) {
    await workspaceController.mutate("replace", { shoreId, targetNookId: safePlaceholder, newNookId: nookId, orientation: "", name: "", nookId: "", dir: 0, nookType });
    if (shoreName) {
      try { await workspaceController.mutate("rename", { shoreId, name: shoreName, targetNookId: "", newNookId: "", orientation: "", nookId: "", dir: 0 }); } catch (err) { console.warn("shore rename after place failed", err); }
    }
  } else {
    await workspaceController.mutate("createShore", { newNookId: nookId, name: shoreName === "Shore" || !shoreName ? shoreTabsFeature.nextName() : shoreName, shoreId: "", targetNookId: "", orientation: "", nookId: "", dir: 0, nookType });
  }
  workspace.activeShoreId = shoreId;
  workspaceView.focus(nookId);
}

async function launchTileInto(shoreId: string | null, placeholderId: string | null, action: string): Promise<void> {
  const placeable = placeableNookForAction(action);
  if (!placeable) { runAction(action); return; }
  let nookId: string;
  if (placeable.kind === "browser") {
    const bp = await invoke<{ nookId: string; currentUrl: string }>(FrontendCommand.BrowserCreate, { url: "https://duckduckgo.com" });
    nookId = bp.nookId;
  } else {
    nookId = (await workspaceView.spawn({ command: "", cwd: "", inheritCwdFrom: "", cols: 80, rows: 24, adapter: "", agentName: "", bay: "", shore: "" })).nookId;
  }
  if (shoreId) {
    await placeNookIntoShore(shoreId, placeholderId, nookId, placeable.nookType, placeable.shoreName);
  } else {
    const r = await workspaceController.mutate<{ shoreId: string }>("createShore", { newNookId: nookId, name: placeable.shoreName === "Shore" ? shoreTabsFeature.nextName() : placeable.shoreName, shoreId: "", targetNookId: "", orientation: "", nookId: "", dir: 0, nookType: placeable.nookType });
    workspace.activeShoreId = r.shoreId;
    workspaceView.focus(nookId);
  }
}

async function newBrowserShore(url: string): Promise<void> {
  const bp = await invoke<{ nookId: string; currentUrl: string }>(FrontendCommand.BrowserCreate, { url });
  const r = await workspaceController.mutate<{ shoreId: string }>("createShore", { newNookId: bp.nookId, name: "Browser", shoreId: "", targetNookId: "", orientation: "", nookId: "", dir: 0, nookType: "browser" });
  workspace.activeShoreId = r.shoreId;
  workspaceView.focus(bp.nookId);
}

async function closeShore(shoreId: string): Promise<void> {
  const shore = workspace.snapshot?.shores.find((r) => r.id === shoreId);
  if (!shore) return;
  const leaves = workspaceView.collectLeafIds(shore.layoutTree);
  for (const id of leaves) {
    await closeBrowserNook(id);
    await killNookProcess(id);
    workspaceView.disposeNook(id);
  }
  await workspaceController.mutate("closeShore", { shoreId, nookId: "", targetNookId: "", newNookId: "", orientation: "", name: "", dir: 0 });
  if (workspace.activeShoreId === shoreId) workspace.activeShoreId = null;
}

async function openBayLauncher(wsId: string): Promise<void> {
  const navigation = bayHeadNavigation(workspace.snapshot?.id ?? null, wsId);
  if (navigation.switchRequired) {
    await switchBay(wsId, null, null, navigation.showLauncher);
    return;
  }
  shoreTabsFeature.overviewVisible = navigation.showLauncher;
  workspace.activeShoreId = null;
  workspace.focusedNookId = null;
  workspaceView.render();
  shoreTabsFeature.render();
  reconcileBrowserNooks();
}

async function switchBay(
  wsId: string,
  targetShoreId: string | null = null,
  targetNookId: string | null = null,
  showLauncher = false,
): Promise<void> {
  try {
    const generation = ++reloadGeneration;
    await invoke(FrontendCommand.BaySwitch, { id: wsId });
    shoreTabsFeature.overviewVisible = showLauncher;
    workspace.activeShoreId = targetShoreId;
    workspace.focusedNookId = null;
    const snapshot = await invoke<BaySnapshot>(FrontendCommand.LayoutGet, { bayId: wsId });
    if (generation !== reloadGeneration) return;
    applyLayoutSnapshot(snapshot);
    void hydrateNookTitles(generation);
    if (targetNookId) await revealNook(targetNookId);
    await shoreTabsFeature.loadWings();
    shoreTabsFeature.render();
  } catch { void 0; }
}

async function openTaskInNook(taskId: string): Promise<void> {
  try {
    const sp = (await workspaceView.spawn({ command: "", cwd: "", inheritCwdFrom: "", cols: 80, rows: 24, adapter: "", agentName: "", bay: "", shore: "" })).nookId;
    const r = await workspaceController.mutate<{ shoreId: string }>("createShore", { newNookId: sp, name: "Task", shoreId: "", targetNookId: "", orientation: "", nookId: "", dir: 0, nookType: "tasks-kanban" });
    workspace.activeShoreId = r.shoreId;
    workspaceView.setNookFilePath(sp, taskId);
    workspaceView.focus(sp);
  } catch { void 0; }
}

async function openFileInEditor(filePath: string): Promise<void> {
  try {
    const sp = (await workspaceView.spawn({ command: "", cwd: "", inheritCwdFrom: "", cols: 80, rows: 24, adapter: "", agentName: "", bay: "", shore: "" })).nookId;
    const r = await workspaceController.mutate<{ shoreId: string }>("createShore", { newNookId: sp, name: filePath.split("/").pop() || "Editor", shoreId: "", targetNookId: "", orientation: "", nookId: "", dir: 0, nookType: "editor" });
    workspace.activeShoreId = r.shoreId;
    workspaceView.setNookFilePath(sp, filePath);
    workspaceView.focus(sp);
  } catch { void 0; }
}

async function openToolShore(nookType: string, name: string): Promise<void> {
  try {
    const sp = (await workspaceView.spawn({ command: "", cwd: "", inheritCwdFrom: "", cols: 80, rows: 24, adapter: "", agentName: "", bay: "", shore: "" })).nookId;
    const r = await workspaceController.mutate<{ shoreId: string }>("createShore", { newNookId: sp, name, shoreId: "", targetNookId: "", orientation: "", nookId: "", dir: 0, nookType });
    workspace.activeShoreId = r.shoreId;
    workspaceView.focus(sp);
  } catch (e) { console.warn("openToolShore failed", nookType, e); }
}

function scrollActiveNook(toTop: boolean): void {
  if (!workspace.focusedNookId) { console.warn("scroll requested with no focused nook"); return; }
  const pv = workspaceView.nooks.get(workspace.focusedNookId);
  if (!pv) { console.warn("scroll requested for unknown nook", workspace.focusedNookId); return; }
  if (toTop) pv.session.term.scrollToTop();
  else pv.session.term.scrollToBottom();
}

function nextShore(dir: number): void {
  const shores = workspace.snapshot?.shores ?? [];
  if (shores.length === 0) { console.warn("shore cycle requested with no shores"); return; }
  const idx = shores.findIndex((r) => r.id === workspace.activeShoreId);
  const next = shores[((idx < 0 ? 0 : idx) + dir + shores.length) % shores.length];
  workspace.activeShoreId = next.id;
  const f = workspaceView.firstLeafOf(next);
  if (f) { workspace.focusedNookId = f; workspaceView.render(); workspaceSidebar.render(); shoreTabsFeature.render(); workspaceView.focus(f); }
}

async function switchBayByIndex(n: number): Promise<void> {
  try {
    const res = await invoke<{ bays: { id: string }[] }>(FrontendCommand.BayList, {});
    const ws = (res.bays ?? [])[n - 1];
    if (!ws) { console.warn("no bay at index", n); return; }
    await switchBay(ws.id);
  } catch (e) { console.warn("bay switch by index failed", e); }
}

async function revealNook(nookId: string): Promise<void> {
  if (!workspace.snapshot) {
    console.warn("nook reveal skipped without an active bay", nookId);
    return;
  }
  const match = workspace.snapshot.shores.map((shore) => ({ shore, location: workspaceView.findNookLocation(shore.layoutTree, nookId) })).find((item) => item.location !== null);
  if (!match?.location) { console.warn("nook reveal: no shore for nook", nookId); return; }
  const { shore, location } = match;
  const activatesSubtab = location.subtabIndex >= 0 && location.leaf.activeSubtab !== location.subtabIndex;
  if (activatesSubtab) workspace.activateSubtab(shore.id, location.leaf.nookId, location.subtabIndex);
  if (shoreTabsFeature.overviewVisible || workspace.activeShoreId !== shore.id || activatesSubtab) {
    shoreTabsFeature.overviewVisible = false;
    workspace.activeShoreId = shore.id;
    workspaceView.render();
    shoreTabsFeature.render();
    workspaceSidebar.render();
  }
  if (activatesSubtab) {
    void workspaceController.mutate("activateSubtab", { shoreId: shore.id, nookId: location.leaf.nookId, targetNookId: "", newNookId: "", orientation: "", name: "", dir: location.subtabIndex })
      .catch((error) => {
        console.warn("nook subtab activation failed", nookId, error);
      });
  }
  workspaceSidebar.acknowledgeAgentAttention(nookId);
  workspaceView.focus(nookId);
}

  const transact = <T>(work: () => Promise<T>): Promise<T> =>
    workspaceController.transaction(work);

  return {
    reload,
    splitActive: (direction) => transact(() => splitActive(direction)),
    moveNookToShore: (nookId, shoreId) =>
      transact(() => moveNookToShore(nookId, shoreId)),
    paintDropOverlay,
    clearDropOverlay,
    applyNookMove: (mutation, sourceNookId) =>
      transact(() => applyNookMove(mutation, sourceNookId)),
    closeNookById: (nookId) => transact(() => closeNookById(nookId)),
    splitActiveWith: (direction, kind) =>
      transact(() => splitActiveWith(direction, kind)),
    closeFocused: () => transact(closeFocused),
    closeOthers: (keepNookId) => transact(() => closeOthers(keepNookId)),
    toggleZoom: () => transact(toggleZoom),
    cycleFocus,
    newShore: () => transact(newShore),
    safeReplaceTarget,
    launchTileInto: (shoreId, placeholderId, action) =>
      transact(() => launchTileInto(shoreId, placeholderId, action)),
    newBrowserShore: (url) => transact(() => newBrowserShore(url)),
    closeShore: (shoreId) => transact(() => closeShore(shoreId)),
    openBayLauncher: (bayId) => transact(() => openBayLauncher(bayId)),
    switchBay: (bayId, targetShoreId, targetNookId, showLauncher) =>
      transact(() => switchBay(bayId, targetShoreId, targetNookId, showLauncher)),
    openTaskInNook: (taskId) => transact(() => openTaskInNook(taskId)),
    openFileInEditor: (path) => transact(() => openFileInEditor(path)),
    openToolShore: (nookType, name) =>
      transact(() => openToolShore(nookType, name)),
    scrollActiveNook,
    nextShore,
    switchBayByIndex: (index) => transact(() => switchBayByIndex(index)),
    revealNook: (nookId) => transact(() => revealNook(nookId)),
    dispose() { clearDropOverlay(); },
  };
}
