import { FrontendCommand } from "../../app/frontend-command";
import { LifecycleScope } from "../../app/lifecycle";
import type { EngineEventPayloads } from "../../app/engine-event-router";
import type { ComponentHandle } from "../../app/lifecycle";
import { createSurfaceMotion } from "../../app/surface-motion";
import { invoke } from "../../invoke";
import {
  buildAdapterTiles,
  buildBuiltinTiles,
  harnessInstallRows,
  type LauncherAdapter,
  type LauncherBuiltin,
  type LauncherTile,
} from "../../box-launcher";
import { brandLogoAt } from "../../brand";
import { adapterIconSvg, iconSvg } from "../../icons";
import {
  SESSION_FILTER_MIN_ROWS,
  adapterAccent,
  assignHotkeys,
  clampLauncherSelection,
  computeLauncherCols,
  detectedHarnessTiles,
  filterSessionRows,
  hotkeyTarget,
  moveLauncherSelection,
  resolveLauncherYolo,
  shapeRecentSessions,
  tipAt,
  toolAccent,
  type LauncherArrowKey,
  type LauncherGeometry,
  type LauncherSelection,
  type RecentSessionRow,
} from "../../launcher-model";
import {
  launcherProfileChoices,
  profileDisplayName,
  profilePickerLabel,
  selectedLauncherProfile,
  type LaunchProfileListItem,
} from "../../profiles";
import { toolbarTiles } from "../../toolbar-tiles";
import type { WorkspaceController } from "../../workspace/workspace-controller";
import type { WorkspaceStore } from "../../workspace/workspace-store";

export interface AdapterInfo {
  name: string;
  displayName: string;
  accent: string;
  binary: string;
  status?: string | null;
  iconSvg?: string | null;
  version?: string | null;
  binaryPath?: string | null;
  updateCommand?: string | null;
  installCommand?: string | null;
  uninstallCommand?: string | null;
  description?: string | null;
}

export interface LauncherContext {
  targetShoreId: string | null;
  targetPlaceholderId: string | null;
}

interface ProfileListResult {
  profiles: LaunchProfileListItem[];
}

interface ContextMenuItem {
  id: string;
  label: string;
  danger?: boolean;
}

export interface LauncherEventRegistrar {
  register<K extends keyof EngineEventPayloads>(
    channel: K,
    handler: (payload: EngineEventPayloads[K]) => void,
  ): ComponentHandle;
}

export interface LauncherFeatureDependencies {
  document: Document;
  engineEvents?: LauncherEventRegistrar;
  root: HTMLElement;
  agentsRoot: HTMLElement;
  workspace: WorkspaceStore;
  workspaceController: WorkspaceController;
  spawnNook: (params: Record<string, unknown>) => Promise<{ nookId: string }>;
  focusNook: (nookId: string) => void;
  focusActiveNook: () => void;
  safeReplaceTarget: (shoreId: string, placeholderId: string | null) => string | null;
  nextShoreName: () => string;
  activeProjectDir: () => string;
  renderShore: () => void;
  launchTileInto: (shoreId: string | null, placeholderId: string | null, action: string) => Promise<void>;
  resolveLauncherProfileSlug: (adapter: string) => Promise<string>;
  launcherProfileSlugKey: (adapter: string) => string;
  openProfileEditor: (adapter: { name: string; binary: string }, slug: string | null, onSaved: (savedSlug: string) => void | Promise<void>) => void;
  openContextMenuAt: (event: MouseEvent, items: ContextMenuItem[], onSelect: (id: string) => void) => void;
  showToast: (title: string, body: string, onClick: () => void) => void;
  resumeRecentSession: (adapter: string, sessionId: string, cwd: string, displayName: string) => Promise<void>;
  getBrandIndex: () => number;
  openAdapterSetup: () => void;
}

export interface LauncherFeature {
  readonly adapters: LauncherAdapter[];
  readonly profiles: ReadonlyMap<string, LaunchProfileListItem[]>;
  open(): void;
  close(): void;
  isOpen(): boolean;
  render(targetShoreId: string | null, targetPlaceholderId: string | null): HTMLElement;
  load(): Promise<void>;
  refreshRecents(): Promise<void>;
  invalidateRecents(): void;
  buildAdapterLaunch(adapter: AdapterInfo): Promise<{ command: string; args: string[]; yolo: boolean }>;
  launchHarnessShellTask(commandLine: string, shoreName: string): Promise<void>;
  yolo(adapter: string): boolean;
  yoloKey(adapter: string): string;
  dispose(): Promise<void>;
}

export function mapLauncherAdapters(adapters: AdapterInfo[] | null | undefined): LauncherAdapter[] {
  return (adapters ?? []).map((adapter) => ({
    name: adapter.name,
    displayName: adapter.displayName,
    accent: adapter.accent,
    binary: adapter.binary,
    iconSvg: adapter.iconSvg ?? "",
    version: adapter.version ?? "",
    status: adapter.status ?? "",
    updateCommand: adapter.updateCommand ?? "",
    installCommand: adapter.installCommand ?? "",
    uninstallCommand: adapter.uninstallCommand ?? "",
    description: adapter.description ?? "",
  }));
}

export function createLauncherFeature(dependencies: LauncherFeatureDependencies): LauncherFeature {
  const lifecycle = new LifecycleScope();
  const document = dependencies.document;
  const launcherEl = dependencies.root;
  const surfaceMotion = createSurfaceMotion(launcherEl);
  const launchAgentsEl = dependencies.agentsRoot;
  const workspace = dependencies.workspace;
  const workspaceController = dependencies.workspaceController;
  const spawnNook = dependencies.spawnNook;
  const focusNook = dependencies.focusNook;
  const safeReplaceTarget = dependencies.safeReplaceTarget;
  const nextShoreName = dependencies.nextShoreName;
  const activeProjectDir = dependencies.activeProjectDir;
  const renderShore = dependencies.renderShore;
  const launchTileInto = dependencies.launchTileInto;
  const resolveLauncherProfileSlug = dependencies.resolveLauncherProfileSlug;
  const launcherProfileSlugKey = dependencies.launcherProfileSlugKey;
  const openProfileEditor = dependencies.openProfileEditor;
  const openContextMenuAt = dependencies.openContextMenuAt;
  const showInAppToast = dependencies.showToast;
  const resumeRecentSession = dependencies.resumeRecentSession;
  const launcherObservers = new Set<ResizeObserver>();
  const activeModalClosers = new Set<() => void>();

function openLauncher() { surfaceMotion.open(); void loadLauncherAgents(); }

function closeLauncher() { surfaceMotion.close(); dependencies.focusActiveNook(); }

interface AdapterListResult { adapters: AdapterInfo[]; }

async function launchHarnessUpdate(tile: LauncherTile): Promise<void> {
  if (!tile.updateCommand) return;
  try {
    const sp = (await spawnNook({ command: "", args: [], shellCommand: tile.updateCommand, cwd: "", inheritCwdFrom: "", cols: 80, rows: 24, adapter: "", agentName: `Update ${tile.label}`, bay: "", shore: "" })).nookId;
    const r = await workspaceController.mutate<{ shoreId: string }>("createShore", { newNookId: sp, name: `Update ${tile.label}`, shoreId: "", targetNookId: "", orientation: "", nookId: "", dir: 0, nookType: "terminal" });
    workspace.activeShoreId = r.shoreId;
    focusNook(sp);
  } catch (err) {
    console.warn("harness update launch failed", tile.adapterName, err);
    showInAppToast("Update not started", (err as Error).message, () => {});
  }
}

async function launchHarnessShellTask(commandLine: string, shoreName: string): Promise<void> {
  try {
    const sp = (await spawnNook({ command: "", args: [], shellCommand: commandLine, cwd: "", inheritCwdFrom: "", cols: 80, rows: 24, adapter: "", agentName: shoreName, bay: "", shore: "" })).nookId;
    const r = await workspaceController.mutate<{ shoreId: string }>("createShore", { newNookId: sp, name: shoreName, shoreId: "", targetNookId: "", orientation: "", nookId: "", dir: 0, nookType: "terminal" });
    workspace.activeShoreId = r.shoreId;
    focusNook(sp);
    scheduleAdapterRedetect();
  } catch (err) {
    console.warn("harness shell task failed", shoreName, err);
    showInAppToast(`${shoreName} not started`, (err as Error).message, () => {});
  }
}

let adapterRedetectTimer: number | null = null;

function scheduleAdapterRedetect(): void {
  if (adapterRedetectTimer !== null) return;
  let remaining = 30;
  adapterRedetectTimer = window.setInterval(async () => {
    remaining -= 1;
    if (remaining <= 0 && adapterRedetectTimer !== null) { window.clearInterval(adapterRedetectTimer); adapterRedetectTimer = null; }
    const before = JSON.stringify(launcherAdapters);
    try {
      await invoke(FrontendCommand.AdapterRescan, {});
      const result = await invoke<AdapterListResult>(FrontendCommand.AppAdapterList, {});
      launcherAdapters = mapLauncherAdapters(result.adapters);
    } catch { return; }
    if (JSON.stringify(launcherAdapters) !== before) repaintActiveLauncher();
  }, 10000);
}

function installableHarnesses(): LauncherAdapter[] {
  return launcherAdapters.filter((a) => a.status !== "detected" && (a.installCommand ?? "").trim().length > 0);
}

async function loadLauncherAgents(): Promise<void> {
  try {
    const result = await invoke<AdapterListResult>(FrontendCommand.AppAdapterList, {});
    launchAgentsEl.innerHTML = "";
    for (const a of result.adapters ?? []) {
      const tile = document.createElement("div");
      tile.className = "launch-tile";
      const mark = document.createElement("span");
      mark.className = "ic";
      mark.style.color = a.accent || "#cba6f7";
      mark.innerHTML = adapterIconSvg(a.name);
      const label = document.createElement("span");
      label.className = "lbl";
      label.textContent = a.displayName || a.name;
      tile.appendChild(mark);
      tile.appendChild(label);
      tile.addEventListener("click", () => { closeLauncher(); void spawnAgent(a); });
      launchAgentsEl.appendChild(tile);
    }
  } catch { void 0; }
}

async function spawnAgent(a: AdapterInfo): Promise<void> {
  await spawnAgentInto(null, null, a);
}

function launcherYoloKey(adapter: string): string {
  return "cove.launcher.yolo." + adapter;
}

function launcherYolo(adapter: string): boolean {
  return resolveLauncherYolo(localStorage.getItem(launcherYoloKey(adapter)), adapter);
}

async function buildAdapterLaunch(a: AdapterInfo): Promise<{ command: string; args: string[]; yolo: boolean }> {
  const yolo = launcherYolo(a.name);
  const profileSlug = await resolveLauncherProfileSlug(a.name);
  try {
    const built = await invoke<{ command: string; args: string[] }>(FrontendCommand.LaunchBuild, {
      adapter: a.name, profileSlug, yolo, workingDir: null, extraFlags: [], env: {},
    });
    if (built.command) return { command: built.command, args: built.args ?? [], yolo };
  } catch (err) { console.warn("launch.build failed, spawning raw binary", a.name, err); }
  return { command: a.binary, args: [], yolo };
}

async function spawnAgentInto(shoreId: string | null, placeholderId: string | null, a: AdapterInfo): Promise<void> {
  const launch = await buildAdapterLaunch(a);
  const sp = (await spawnNook({ command: launch.command, args: launch.args, cwd: "", inheritCwdFrom: "", cols: 80, rows: 24, adapter: a.name, agentName: a.displayName, bay: "", shore: "", yolo: launch.yolo })).nookId;
  if (shoreId) {
    const safePlaceholder = safeReplaceTarget(shoreId, placeholderId);
    if (safePlaceholder) {
      await workspaceController.mutate("replace", { shoreId, targetNookId: safePlaceholder, newNookId: sp, orientation: "", name: "", nookId: "", dir: 0, nookType: "terminal" });
    }
    workspace.activeShoreId = shoreId;
  } else {
    const r = await workspaceController.mutate<{ shoreId: string }>("createShore", { newNookId: sp, name: nextShoreName(), shoreId: "", targetNookId: "", orientation: "", nookId: "", dir: 0 });
    workspace.activeShoreId = r.shoreId;
  }
  focusNook(sp);
}

let launcherAdapters: LauncherAdapter[] = [];

let launcherRecents: RecentSessionRow[] = [];

interface SessionRecentResult { sessions: RecentSessionRow[]; }

const RECENTS_FALLBACK_MS = 30_000;
let launcherRecentsRevision = 0;
let recentsRefreshScheduled = false;
let recentsRefreshRunning = false;
let recentsRefreshTrailing = false;
let recentsRefreshPromise: Promise<void> | null = null;
let resolveRecentsRefresh: (() => void) | null = null;
let launcherDisposed = false;

async function loadLauncherAdapters(): Promise<void> {
  try {
    const result = await invoke<AdapterListResult>(FrontendCommand.AppAdapterList, {});
    launcherAdapters = mapLauncherAdapters(result.adapters);
  } catch { launcherAdapters = []; }
  await loadLauncherProfiles();
  if ((workspace.snapshot?.shores ?? []).length === 0) renderShore();
}

function repaintMountedLaunchers(): void {
  for (const wrap of document.querySelectorAll<HTMLElement>(".box-launcher")) {
    const ctx: LauncherContext = {
      targetShoreId: wrap.dataset.shoreId || null,
      targetPlaceholderId: wrap.dataset.placeholderId || null,
    };
    paintBoxLauncher(wrap, ctx);
  }
}

async function loadLauncherRecents(): Promise<void> {
  const cwd = activeProjectDir();
  try {
    const result = await invoke<SessionRecentResult>(FrontendCommand.SessionRecent, { cwd, limit: 0 });
    if (launcherDisposed || cwd !== activeProjectDir()) return;
    const next = result.sessions ?? [];
    const changed = JSON.stringify(next) !== JSON.stringify(launcherRecents);
    launcherRecents = next;
    if (changed) repaintMountedLaunchers();
  } catch (error) {
    console.warn("session.recent failed", cwd, error);
  }
}

async function runLauncherRecentsRefresh(): Promise<void> {
  recentsRefreshScheduled = false;
  recentsRefreshRunning = true;
  do {
    recentsRefreshTrailing = false;
    await loadLauncherRecents();
  } while (recentsRefreshTrailing && !launcherDisposed);
  recentsRefreshRunning = false;
  const resolve = resolveRecentsRefresh;
  resolveRecentsRefresh = null;
  recentsRefreshPromise = null;
  resolve?.();
}

function refreshLauncherRecents(): Promise<void> {
  if (launcherDisposed) return Promise.resolve();
  if (recentsRefreshRunning) {
    recentsRefreshTrailing = true;
    return recentsRefreshPromise ?? Promise.resolve();
  }
  if (recentsRefreshScheduled) return recentsRefreshPromise ?? Promise.resolve();
  recentsRefreshScheduled = true;
  recentsRefreshPromise = new Promise<void>((resolve) => {
    resolveRecentsRefresh = resolve;
  });
  queueMicrotask(() => {
    if (launcherDisposed) {
      recentsRefreshScheduled = false;
      const resolve = resolveRecentsRefresh;
      resolveRecentsRefresh = null;
      recentsRefreshPromise = null;
      resolve?.();
      return;
    }
    void runLauncherRecentsRefresh();
  });
  return recentsRefreshPromise;
}

function registerRecentsEvents(): void {
  if (!dependencies.engineEvents) return;
  const changed = dependencies.engineEvents.register("session.recents.changed", (event) => {
    if (event.revision <= launcherRecentsRevision) return;
    launcherRecentsRevision = event.revision;
    void refreshLauncherRecents();
  });
  const reconnected = dependencies.engineEvents.register("engine.reconnected", () => {
    launcherRecentsRevision = 0;
    void refreshLauncherRecents();
  });
  lifecycle.own(() => changed.dispose());
  lifecycle.own(() => reconnected.dispose());
}

registerRecentsEvents();
lifecycle.interval(() => {
  if (document.querySelector(".box-launcher")) void refreshLauncherRecents();
}, RECENTS_FALLBACK_MS);

const launcherProfiles = new Map<string, LaunchProfileListItem[]>();

let launcherProfilesAt = 0;

async function loadLauncherProfiles(): Promise<void> {
  const lists = await Promise.all(launcherAdapters.map(async (a) => {
    try {
      const result = await invoke<ProfileListResult>(FrontendCommand.LaunchProfileList, { adapter: a.name });
      return [a.name, result.profiles ?? []] as const;
    } catch (err) {
      console.warn("launch-profile.list failed", a.name, err);
      return [a.name, launcherProfiles.get(a.name) ?? []] as const;
    }
  }));
  launcherProfiles.clear();
  for (const [adapter, profiles] of lists) launcherProfiles.set(adapter, profiles);
  launcherProfilesAt = Date.now();
}

async function refreshLauncherProfiles(): Promise<void> {
  if (Date.now() - launcherProfilesAt < 4000) return;
  const before = JSON.stringify([...launcherProfiles.entries()]);
  await loadLauncherProfiles();
  if (JSON.stringify([...launcherProfiles.entries()]) !== before) repaintActiveLauncher();
}

function builtinLauncherDefs(): LauncherBuiltin[] {
  return toolbarTiles().map((t) => ({ id: t.id, label: t.label, icon: t.icon, action: t.action }));
}

const LAUNCHER_HARNESS_COLS = 3;

let launcherSelection: LauncherSelection = { section: "harness", index: 0 };

let launcherTipIndex = 0;

let launcherTipTimer: number | null = null;

let launcherCols = LAUNCHER_HARNESS_COLS;

function launcherGeometry(harnessCount: number, toolCount: number): LauncherGeometry {
  return { harnessCount, harnessCols: Math.min(launcherCols, Math.max(1, harnessCount)), toolCount };
}

function launchHarnessTile(ctx: LauncherContext, tile: LauncherTile): void {
  void spawnAgentInto(ctx.targetShoreId, ctx.targetPlaceholderId, { name: tile.adapterName, displayName: tile.label, accent: tile.accent, binary: tile.binary });
}

function launchToolTile(ctx: LauncherContext, tile: LauncherTile): void {
  void launchTileInto(ctx.targetShoreId, ctx.targetPlaceholderId, tile.action);
}

function renderToolTile(ctx: LauncherContext, tile: LauncherTile, letter: string, selected: boolean): HTMLElement {
  const id = tile.id.replace("builtin:", "");
  const accent = toolAccent(id);
  const element = document.createElement("div");
  element.className = "cl-tool" + (selected ? " selected" : "");
  element.style.setProperty("--tool-accent", accent);
  const icon = document.createElement("span");
  icon.className = "cl-tool-ic";
  icon.innerHTML = iconSvg(id);
  icon.style.color = accent;
  const label = document.createElement("span");
  label.className = "cl-tool-lbl";
  label.textContent = tile.label;
  const key = document.createElement("span");
  key.className = "cl-tool-key";
  key.textContent = letter;
  element.append(key, icon, label);
  element.addEventListener("click", () => launchToolTile(ctx, tile));
  return element;
}

function activateLauncherSelection(ctx: LauncherContext, harness: LauncherTile[], tools: LauncherTile[]): void {
  const sel = clampLauncherSelection(launcherSelection, launcherGeometry(harness.length, tools.length));
  if (sel.section === "harness") {
    const tile = harness[sel.index];
    if (tile && !tile.disabled) launchHarnessTile(ctx, tile);
  } else {
    const tile = tools[sel.index];
    if (tile) launchToolTile(ctx, tile);
  }
}

function renderBoxLauncher(targetShoreId: string | null, targetPlaceholderId: string | null): HTMLElement {
  const coldMount = document.querySelector(".box-launcher") === null;
  void refreshLauncherProfiles();
  const ctx: LauncherContext = { targetShoreId, targetPlaceholderId };
  const wrap = document.createElement("div");
  wrap.className = "box-launcher";
  wrap.tabIndex = 0;
  if (targetShoreId) wrap.dataset.shoreId = targetShoreId;
  if (targetPlaceholderId) wrap.dataset.placeholderId = targetPlaceholderId;
  paintBoxLauncher(wrap, ctx);
  const ro = new ResizeObserver(() => {
    if (!document.body.contains(wrap)) { ro.disconnect(); launcherObservers.delete(ro); return; }
    const count = Math.max(1, launcherTileSets().harness.length);
    const cols = computeLauncherCols(wrap.clientWidth || 680, count, LAUNCHER_HARNESS_COLS);
    if (cols !== launcherCols) paintBoxLauncher(wrap, ctx);
  });
  launcherObservers.add(ro);
  ro.observe(wrap);
  if (coldMount) void refreshLauncherRecents();
  wrap.addEventListener("keydown", (e) => {
    if (e.key === "Shift") wrap.classList.add("show-keys");
    handleLauncherKey(e, wrap, ctx);
  });
  wrap.addEventListener("keyup", (e) => {
    if (e.key === "Shift") wrap.classList.remove("show-keys");
  });
  wrap.addEventListener("blur", () => wrap.classList.remove("show-keys"));
  if (launcherTipTimer !== null) window.clearInterval(launcherTipTimer);
  launcherTipTimer = window.setInterval(() => {
    launcherTipIndex += 1;
    const tipEl = wrap.querySelector<HTMLElement>(".cl-tip");
    if (tipEl) setLauncherTip(tipEl, tipAt(launcherTipIndex));
    else if (launcherTipTimer !== null) { window.clearInterval(launcherTipTimer); launcherTipTimer = null; }
  }, 9000);
  queueMicrotask(() => { if (document.body.contains(wrap)) wrap.focus(); });
  return wrap;
}

function launcherTileSets(): { harness: LauncherTile[]; tools: LauncherTile[]; harnessKeys: string[]; toolKeys: string[] } {
  const harness = detectedHarnessTiles(buildAdapterTiles(launcherAdapters));
  const tools = buildBuiltinTiles(builtinLauncherDefs());
  const toolKeys = toolbarTiles().map((t) => t.letter);
  const harnessKeys = assignHotkeys(harness.map((t) => t.label), toolKeys);
  return { harness, tools, harnessKeys, toolKeys };
}

function handleLauncherKey(e: KeyboardEvent, wrap: HTMLElement, ctx: LauncherContext): void {
  const active = document.activeElement as HTMLElement | null;
  if (active && active !== wrap && (active.tagName === "INPUT" || active.tagName === "TEXTAREA" || active.isContentEditable)) return;
  const { harness, tools, harnessKeys, toolKeys } = launcherTileSets();
  const geo = launcherGeometry(harness.length, tools.length);
  if (e.key === "ArrowLeft" || e.key === "ArrowRight" || e.key === "ArrowUp" || e.key === "ArrowDown") {
    e.preventDefault();
    launcherSelection = moveLauncherSelection(launcherSelection, e.key as LauncherArrowKey, geo);
    paintBoxLauncher(wrap, ctx);
    return;
  }
  if (e.key === "Enter") {
    e.preventDefault();
    if (e.metaKey || e.ctrlKey) launcherSelection = clampLauncherSelection({ section: "harness", index: launcherSelection.section === "harness" ? launcherSelection.index : 0 }, geo);
    activateLauncherSelection(ctx, harness, tools);
    return;
  }
  if (/^[a-zA-Z]$/.test(e.key) && !e.metaKey && !e.ctrlKey && !e.altKey) {
    const target = hotkeyTarget(e.key, harnessKeys, toolKeys);
    if (target) {
      e.preventDefault();
      launcherSelection = target;
      activateLauncherSelection(ctx, harness, tools);
    }
  }
}

function firstSensibleSelection(harness: LauncherTile[], tools: LauncherTile[]): LauncherSelection {
  const firstEnabled = harness.findIndex((t) => !t.disabled);
  if (firstEnabled >= 0) return { section: "harness", index: firstEnabled };
  if (tools.length > 0) return { section: "tool", index: 0 };
  return { section: "harness", index: 0 };
}

function setLauncherTip(tipEl: HTMLElement, text: string): void {
  tipEl.classList.remove("driving");
  tipEl.style.removeProperty("--tip-shift");
  tipEl.style.removeProperty("--tip-dur");
  let inner = tipEl.querySelector<HTMLElement>(".cl-tip-text");
  if (!inner) {
    inner = document.createElement("span");
    inner.className = "cl-tip-text";
    tipEl.textContent = "";
    tipEl.appendChild(inner);
  }
  inner.textContent = text;
  requestAnimationFrame(() => {
    if (!tipEl.isConnected || !inner) return;
    const overflow = inner.scrollWidth - tipEl.clientWidth;
    if (overflow <= 0) return;
    const distance = overflow + 12;
    tipEl.style.setProperty("--tip-shift", `-${distance}px`);
    tipEl.style.setProperty("--tip-dur", `${Math.max(5, distance / 20)}s`);
    tipEl.classList.add("driving");
  });
}

function paintBoxLauncher(wrap: HTMLElement, ctx: LauncherContext): void {
  const { harness, tools, harnessKeys, toolKeys } = launcherTileSets();
  launcherCols = computeLauncherCols(wrap.clientWidth || 680, Math.max(1, harness.length), LAUNCHER_HARNESS_COLS);
  const geo = launcherGeometry(harness.length, tools.length);
  launcherSelection = clampLauncherSelection(launcherSelection, geo);
  if (launcherSelection.section === "harness" && harness[launcherSelection.index]?.disabled) {
    launcherSelection = firstSensibleSelection(harness, tools);
  }
  wrap.innerHTML = "";

  const header = document.createElement("div");
  header.className = "cl-header";
  const brand = document.createElement("img");
  brand.className = "cl-brand cl-brand-img";
  brand.alt = "cove";
  brand.src = brandLogoAt(dependencies.getBrandIndex());
  const tip = document.createElement("span");
  tip.className = "cl-tip";
  setLauncherTip(tip, tipAt(launcherTipIndex));
  const bayChip = document.createElement("span");
  bayChip.className = "cl-hint cl-bay-chip";
  bayChip.textContent = workspace.snapshot?.name?.trim() || "default";
  bayChip.title = activeProjectDir();
  const hint = document.createElement("span");
  hint.className = "cl-hint";
  hint.textContent = "hold ⇧ for shortcuts";
  header.appendChild(brand);
  header.appendChild(tip);
  header.appendChild(bayChip);
  header.appendChild(hint);
  wrap.appendChild(header);

  const cards = document.createElement("div");
  cards.className = "cl-harness-row";
  cards.style.gridTemplateColumns = `repeat(${geo.harnessCols}, minmax(0, 200px))`;
  harness.forEach((tile, i) => {
    const selected = launcherSelection.section === "harness" && launcherSelection.index === i;
    cards.appendChild(renderHarnessCard(ctx, tile, harnessKeys[i], selected));
  });
  if (harness.length > 0 && installableHarnesses().length > 0) {
    cards.appendChild(renderInstallHarnessCard());
  }
  if (harness.length === 0) {
    cards.style.gridTemplateColumns = "minmax(0, 280px)";
    cards.appendChild(renderConfigureAdapterCard());
  }
  wrap.appendChild(cards);

  if (launcherSelection.section === "harness") {
    const selTile = harness[launcherSelection.index];
    if (selTile && !selTile.disabled) wrap.appendChild(renderDetailDock(ctx, selTile));
  }

  if (tools.length > 0) {
    const toolLabel = document.createElement("div");
    toolLabel.className = "cl-section-label";
    toolLabel.textContent = "open a nook";
    wrap.appendChild(toolLabel);
  }

  const toolRow = document.createElement("div");
  toolRow.className = "cl-tool-row";
  tools.forEach((tile, i) => {
    const selected = launcherSelection.section === "tool" && launcherSelection.index === i;
    toolRow.appendChild(renderToolTile(ctx, tile, toolKeys[i], selected));
  });
  wrap.appendChild(toolRow);
}

function renderConfigureAdapterCard(): HTMLElement {
  const el = document.createElement("div");
  el.className = "cl-card cl-configure";
  el.style.setProperty("--card-accent", "#cba6f7");
  const badge = document.createElement("span");
  badge.className = "cl-card-badge";
  badge.innerHTML = iconSvg("gear");
  el.appendChild(badge);
  const name = document.createElement("div");
  name.className = "cl-card-name";
  name.textContent = "Configure an adapter";
  el.appendChild(name);
  const note = document.createElement("div");
  note.className = "cl-card-note";
  note.textContent = "no coding agents set up yet — connect one to launch sessions";
  el.appendChild(note);
  el.addEventListener("click", () => openAdapterSetup());
  return el;
}

function openAdapterSetup(): void {
  dependencies.openAdapterSetup();
}

function renderInstallHarnessCard(): HTMLElement {
  const el = document.createElement("div");
  el.className = "cl-card cl-install-card";
  const plus = document.createElement("span");
  plus.className = "cl-install-plus";
  plus.textContent = "+";
  const label = document.createElement("span");
  label.className = "cl-install-label";
  label.textContent = "Install harness";
  el.appendChild(plus);
  el.appendChild(label);
  el.addEventListener("click", (e) => {
    e.stopPropagation();
    openHarnessInstallModal();
  });
  return el;
}

function openHarnessInstallModal(): void {
  const rows = harnessInstallRows(launcherAdapters);
  if (rows.length === 0) return;
  const previousFocus = document.activeElement as HTMLElement | null;
  const overlay = document.createElement("div");
  overlay.className = "hi-overlay";
  const panel = document.createElement("div");
  panel.className = "hi-panel";
  panel.setAttribute("role", "dialog");
  panel.setAttribute("aria-modal", "true");
  panel.setAttribute("aria-label", "Install a harness");

  const close = (): void => {
    overlay.remove();
    activeModalClosers.delete(close);
    document.removeEventListener("keydown", onKey, true);
    previousFocus?.focus?.();
  };
  const onKey = (e: KeyboardEvent): void => {
    if (e.key === "Escape") {
      e.stopPropagation();
      e.preventDefault();
      close();
      return;
    }
    if (!panel.contains(e.target as Node)) {
      e.stopPropagation();
      e.preventDefault();
      return;
    }
    if (e.key === "Tab") {
      const focusables = Array.from(panel.querySelectorAll<HTMLElement>("button, [tabindex]"));
      if (focusables.length === 0) return;
      const index = focusables.indexOf(document.activeElement as HTMLElement);
      const next = e.shiftKey
        ? focusables[(index <= 0 ? focusables.length : index) - 1]
        : focusables[(index + 1) % focusables.length];
      e.preventDefault();
      e.stopPropagation();
      next.focus();
      return;
    }
    e.stopPropagation();
  };
  activeModalClosers.add(close);
  document.addEventListener("keydown", onKey, true);
  overlay.addEventListener("mousedown", (e) => {
    if (e.target === overlay) close();
  });

  const head = document.createElement("div");
  head.className = "hi-head";
  const heading = document.createElement("div");
  const title = document.createElement("div");
  title.className = "hi-title";
  title.textContent = "Install a harness";
  const sub = document.createElement("div");
  sub.className = "hi-sub";
  sub.textContent = "Add a coding agent to Cove. Uninstalled harnesses stay listed here, ready to reinstall.";
  heading.appendChild(title);
  heading.appendChild(sub);
  const x = document.createElement("button");
  x.className = "hi-x";
  x.textContent = "✕";
  x.addEventListener("click", close);
  head.appendChild(heading);
  head.appendChild(x);
  panel.appendChild(head);

  const list = document.createElement("div");
  list.className = "hi-list";
  rows.forEach((row, index) => {
    const card = document.createElement("div");
    card.className = "hi-card";
    card.style.setProperty("--card-accent", adapterAccent(row.name, row.accent));
    card.style.animationDelay = `${index * 45}ms`;

    const badge = document.createElement("span");
    badge.className = "hi-badge";
    badge.innerHTML = row.iconSvg || adapterIconSvg(row.name);
    badge.setAttribute("aria-hidden", "true");

    const body = document.createElement("div");
    body.className = "hi-body";
    const name = document.createElement("div");
    name.className = "hi-name";
    name.textContent = row.label;
    body.appendChild(name);
    if (row.description) {
      const desc = document.createElement("div");
      desc.className = "hi-desc";
      desc.textContent = row.description;
      body.appendChild(desc);
    }
    const cmd = document.createElement("div");
    cmd.className = "hi-cmd";
    cmd.textContent = row.command;
    cmd.title = row.command;
    body.appendChild(cmd);

    const btn = document.createElement("button");
    btn.className = "hi-install";
    btn.textContent = "Install";
    btn.addEventListener("click", () => {
      close();
      void launchHarnessShellTask(row.command, `Install ${row.label}`);
    });

    card.appendChild(badge);
    card.appendChild(body);
    card.appendChild(btn);
    list.appendChild(card);
  });
  panel.appendChild(list);
  overlay.appendChild(panel);
  document.body.appendChild(overlay);
  (panel.querySelector<HTMLElement>(".hi-install") ?? x).focus();
}

function renderHarnessCard(ctx: LauncherContext, tile: LauncherTile, letter: string, selected: boolean): HTMLElement {
  const accent = adapterAccent(tile.adapterName, tile.accent);
  const el = document.createElement("div");
  el.className = "cl-card" + (tile.disabled ? " disabled" : "") + (selected ? " selected" : "");
  el.style.setProperty("--card-accent", accent);

  const top = document.createElement("div");
  top.className = "cl-card-top";
  const badge = document.createElement("span");
  badge.className = "cl-card-badge";
  badge.innerHTML = adapterIconSvg(tile.adapterName);
  const key = document.createElement("span");
  key.className = "cl-card-key";
  key.textContent = letter;
  top.appendChild(badge);
  const topRight = document.createElement("span");
  topRight.className = "cl-card-top-right";
  if (tile.uninstallCommand) {
    const minus = document.createElement("button");
    minus.className = "cl-card-minus";
    minus.textContent = "−";
    minus.title = `Uninstall ${tile.label}`;
    minus.addEventListener("click", (e) => {
      e.stopPropagation();
      openContextMenuAt(e, [
        { id: "uninstall", label: `Uninstall ${tile.label}`, danger: true },
        { id: "cancel", label: "Cancel" },
      ], (id) => {
        if (id !== "uninstall" || !tile.uninstallCommand) return;
        void launchHarnessShellTask(tile.uninstallCommand, `Uninstall ${tile.label}`);
      });
    });
    topRight.appendChild(minus);
  }
  topRight.appendChild(key);
  top.appendChild(topRight);
  el.appendChild(top);

  const name = document.createElement("div");
  name.className = "cl-card-name";
  name.textContent = tile.label;
  el.appendChild(name);

  const status = document.createElement("div");
  status.className = "cl-card-status" + (tile.disabled ? " off" : "");
  const statusDot = document.createElement("span");
  statusDot.className = "cl-card-status-dot";
  const statusText = document.createElement("span");
  statusText.className = "cl-card-status-text";
  statusText.textContent = tile.disabled ? (tile.note || "not detected") : (tile.version ? tile.version : "ready");
  status.appendChild(statusDot);
  status.appendChild(statusText);
  el.appendChild(status);

  if (tile.disabled) return el;

  const cta = document.createElement("span");
  cta.className = "cl-card-cta";
  cta.textContent = "⌘↵";
  el.appendChild(cta);

  el.addEventListener("click", () => {
    if (selected) launchHarnessTile(ctx, tile);
    else { launcherSelection = { section: "harness", index: harnessIndexOf(tile) }; repaintActiveLauncher(); }
  });
  return el;
}

function harnessIndexOf(tile: LauncherTile): number {
  return detectedHarnessTiles(buildAdapterTiles(launcherAdapters)).findIndex((t) => t.id === tile.id);
}

function repaintActiveLauncher(): void {
  const wrap = document.querySelector(".box-launcher") as HTMLElement | null;
  if (!wrap) return;
  const targetShoreId = wrap.dataset.shoreId || null;
  const targetPlaceholderId = wrap.dataset.placeholderId || null;
  paintBoxLauncher(wrap, { targetShoreId, targetPlaceholderId });
  wrap.focus();
}

function closeLauncherDropdowns(): void {
  for (const el of document.querySelectorAll(".cl-resume-dd.open")) el.classList.remove("open");
}

function renderDetailDock(ctx: LauncherContext, tile: LauncherTile): HTMLElement {
  const accent = adapterAccent(tile.adapterName, tile.accent);
  const dock = document.createElement("div");
  dock.className = "cl-dock";
  dock.style.setProperty("--card-accent", accent);
  dock.addEventListener("click", (e) => { e.stopPropagation(); closeLauncherDropdowns(); });

  const identity = document.createElement("div");
  identity.className = "cl-dock-id";
  const dot = document.createElement("span");
  dot.className = "cl-dock-dot";
  const idText = document.createElement("div");
  idText.className = "cl-dock-id-text";
  const idName = document.createElement("div");
  idName.className = "cl-dock-id-name";
  idName.textContent = tile.label;
  const idSub = document.createElement("div");
  idSub.className = "cl-dock-id-sub";
  idSub.textContent = tile.version ? `${tile.version} · start a new session` : "start a new session";
  idText.appendChild(idName);
  idText.appendChild(idSub);
  identity.appendChild(dot);
  identity.appendChild(idText);
  if (tile.updateCommand) {
    const updateBtn = document.createElement("button");
    updateBtn.className = "cl-dock-update";
    updateBtn.textContent = "Update";
    updateBtn.title = tile.updateCommand;
    updateBtn.addEventListener("click", (e) => {
      e.stopPropagation();
      void launchHarnessUpdate(tile);
    });
    identity.appendChild(updateBtn);
  }
  dock.appendChild(identity);

  const choices = launcherProfileChoices(tile.adapterName, launcherProfiles.get(tile.adapterName) ?? []);
  const storedSlug = localStorage.getItem(launcherProfileSlugKey(tile.adapterName));
  const selectedProfile = selectedLauncherProfile(choices, storedSlug);

  const controls = document.createElement("div");
  controls.className = "cl-dock-controls";

  const profileDd = document.createElement("div");
  profileDd.className = "cl-resume-dd cl-profile-dd";
  const profileTrigger = document.createElement("button");
  profileTrigger.className = "cl-resume-trigger";
  const profileTag = document.createElement("span");
  profileTag.className = "cl-profile-tag";
  profileTag.textContent = "profile";
  const profileName = document.createElement("span");
  profileName.className = "cl-resume-label cl-profile-name";
  profileName.textContent = selectedProfile ? profileDisplayName(selectedProfile) : "Default";
  const profileChev = document.createElement("span");
  profileChev.className = "cl-resume-chev";
  profileChev.textContent = "▾";
  profileTrigger.appendChild(profileTag);
  profileTrigger.appendChild(profileName);
  profileTrigger.appendChild(profileChev);
  profileDd.appendChild(profileTrigger);
  const profileMenu = document.createElement("div");
  profileMenu.className = "cl-resume-menu";
  for (const p of choices) {
    const isSelected = p.slug === selectedProfile?.slug;
    const row = document.createElement("div");
    row.className = "cl-recent-row cl-profile-opt" + (isSelected ? " cl-profile-selected" : "");
    const col = document.createElement("div");
    col.className = "cl-profile-opt-col";
    const optName = document.createElement("span");
    optName.className = "cl-profile-opt-name";
    optName.textContent = profileDisplayName(p);
    optName.title = profilePickerLabel(p);
    col.appendChild(optName);
    const subParts: string[] = [];
    if (p.slug === "default" && p.argCount === 0 && p.envCount === 0 && !p.model) {
      subParts.push("stock settings");
    } else {
      if (p.model) subParts.push(p.model);
      if (p.effort) subParts.push(p.effort);
      if (p.argCount > 0) subParts.push(`${p.argCount} arg${p.argCount === 1 ? "" : "s"}`);
      if (p.envCount > 0) subParts.push(`${p.envCount} env`);
    }
    if (subParts.length > 0) {
      const optSub = document.createElement("span");
      optSub.className = "cl-profile-opt-sub";
      optSub.textContent = subParts.join(" · ");
      col.appendChild(optSub);
    }
    row.appendChild(col);
    if (isSelected) {
      const check = document.createElement("span");
      check.className = "cl-profile-check";
      check.textContent = "✓";
      row.appendChild(check);
    }
    row.addEventListener("click", (e) => {
      e.stopPropagation();
      localStorage.setItem(launcherProfileSlugKey(tile.adapterName), p.slug);
      repaintActiveLauncher();
    });
    profileMenu.appendChild(row);
  }
  const profileDivider = document.createElement("div");
  profileDivider.className = "cl-profile-divider";
  profileMenu.appendChild(profileDivider);
  const newProfileRow = document.createElement("div");
  newProfileRow.className = "cl-recent-row cl-profile-new";
  const newProfilePlus = document.createElement("span");
  newProfilePlus.className = "cl-profile-plus";
  newProfilePlus.textContent = "+";
  const newProfileLabel = document.createElement("span");
  newProfileLabel.className = "cl-recent-cwd";
  newProfileLabel.textContent = "New profile…";
  newProfileRow.appendChild(newProfilePlus);
  newProfileRow.appendChild(newProfileLabel);
  newProfileRow.addEventListener("click", (e) => {
    e.stopPropagation();
    profileDd.classList.remove("open");
    openProfileEditor({ name: tile.adapterName, binary: tile.binary }, null, async (savedSlug) => {
      localStorage.setItem(launcherProfileSlugKey(tile.adapterName), savedSlug);
      launcherProfilesAt = 0;
      await loadLauncherProfiles();
      repaintActiveLauncher();
    });
  });
  profileMenu.appendChild(newProfileRow);
  profileDd.appendChild(profileMenu);
  profileTrigger.addEventListener("click", (e) => {
    e.stopPropagation();
    const wasOpen = profileDd.classList.contains("open");
    closeLauncherDropdowns();
    if (!wasOpen) profileDd.classList.add("open");
  });

  const yoloRow = document.createElement("label");
  yoloRow.className = "cl-yolo-row";
  const yoloBox = document.createElement("input");
  yoloBox.type = "checkbox";
  yoloBox.checked = launcherYolo(tile.adapterName);
  yoloBox.addEventListener("change", () => localStorage.setItem(launcherYoloKey(tile.adapterName), String(yoloBox.checked)));
  const yoloLabel = document.createElement("span");
  yoloLabel.textContent = "skip permissions";
  yoloRow.appendChild(yoloBox);
  yoloRow.appendChild(yoloLabel);
  controls.appendChild(yoloRow);

  const recentRows = launcherRecents.filter((r) => r.adapter === tile.adapterName);
  const shaped = shapeRecentSessions(recentRows, Date.now(), recentRows.length);
  if (shaped.length > 0) {
    const dd = document.createElement("div");
    dd.className = "cl-resume-dd";
    const trigger = document.createElement("button");
    trigger.className = "cl-resume-trigger";
    const triggerLabel = document.createElement("span");
    triggerLabel.className = "cl-resume-label";
    triggerLabel.textContent = "Resume";
    const count = document.createElement("span");
    count.className = "cl-resume-count";
    count.textContent = String(shaped.length);
    const chev = document.createElement("span");
    chev.className = "cl-resume-chev";
    chev.textContent = "▾";
    trigger.appendChild(triggerLabel);
    trigger.appendChild(count);
    trigger.appendChild(chev);
    dd.appendChild(trigger);
    const menu = document.createElement("div");
    menu.className = "cl-resume-menu";
    let filter: HTMLInputElement | null = null;
    if (shaped.length >= SESSION_FILTER_MIN_ROWS) {
      filter = document.createElement("input");
      filter.className = "cl-resume-filter";
      filter.type = "text";
      filter.placeholder = "filter sessions…";
      filter.addEventListener("click", (e) => e.stopPropagation());
      filter.addEventListener("keydown", (e) => {
        e.stopPropagation();
        if (e.key === "Escape") {
          e.preventDefault();
          dd.classList.remove("open");
        }
      });
      filter.addEventListener("input", () => renderRows(filterSessionRows(shaped, filter!.value)));
      menu.appendChild(filter);
    }
    const rowsHost = document.createElement("div");
    rowsHost.className = "cl-resume-rows";
    menu.appendChild(rowsHost);
    const renderRows = (list: typeof shaped): void => {
      rowsHost.innerHTML = "";
      if (list.length === 0) {
        const empty = document.createElement("div");
        empty.className = "cl-resume-empty";
        empty.textContent = "no matching sessions";
        rowsHost.appendChild(empty);
        return;
      }
      for (const s of list) {
        const row = document.createElement("div");
        row.className = "cl-recent-row";
        const rowDot = document.createElement("span");
        rowDot.className = "cl-session-dot";
        rowDot.style.background = accent;
        const base = document.createElement("span");
        base.className = "cl-recent-cwd";
        base.textContent = s.label;
        base.title = s.label;
        const when = document.createElement("span");
        when.className = "cl-recent-when";
        when.textContent = s.relative;
        row.appendChild(rowDot);
        row.appendChild(base);
        row.appendChild(when);
        row.addEventListener("click", (e) => { e.stopPropagation(); void resumeRecentSession(s.adapter, s.sessionId, s.cwd, tile.label); });
        rowsHost.appendChild(row);
      }
    };
    renderRows(shaped);
    dd.appendChild(menu);
    trigger.addEventListener("click", (e) => {
      e.stopPropagation();
      const wasOpen = dd.classList.contains("open");
      closeLauncherDropdowns();
      if (!wasOpen) {
        dd.classList.add("open");
        filter?.focus();
      }
    });
    controls.appendChild(dd);
  }

  controls.appendChild(profileDd);
  const newSession = document.createElement("button");
  newSession.className = "cl-new-session";
  const nsLabel = document.createElement("span");
  nsLabel.textContent = "New session";
  const nsKbd = document.createElement("kbd");
  nsKbd.className = "cl-kbd";
  nsKbd.textContent = "⌘↵";
  newSession.appendChild(nsLabel);
  newSession.appendChild(nsKbd);
  newSession.addEventListener("click", (e) => { e.stopPropagation(); launchHarnessTile(ctx, tile); });
  controls.appendChild(newSession);

  dock.appendChild(controls);
  return dock;
}

  lifecycle.listen(launcherEl, "mousedown", (event) => {
    if (event.target === launcherEl) closeLauncher();
  });
  lifecycle.listen(launcherEl, "keydown", (event) => {
    if ((event as KeyboardEvent).key === "Escape") closeLauncher();
  });
  lifecycle.listen(document, "click", closeLauncherDropdowns);

  return {
    get adapters() { return launcherAdapters; },
    get profiles() { return launcherProfiles; },
    open: openLauncher,
    close: closeLauncher,
    isOpen: () => launcherEl.classList.contains("open"),
    render: renderBoxLauncher,
    load: loadLauncherAdapters,
    refreshRecents: refreshLauncherRecents,
    invalidateRecents: () => { void refreshLauncherRecents(); },
    buildAdapterLaunch,
    launchHarnessShellTask,
    yolo: launcherYolo,
    yoloKey: launcherYoloKey,
    async dispose() {
      launcherDisposed = true;
      surfaceMotion.dispose();
      if (adapterRedetectTimer !== null) window.clearInterval(adapterRedetectTimer);
      if (launcherTipTimer !== null) window.clearInterval(launcherTipTimer);
      for (const close of [...activeModalClosers]) close();
      for (const observer of launcherObservers) observer.disconnect();
      launcherObservers.clear();
      await lifecycle.dispose();
    },
  };
}
