import { restoredSummaryText } from "./restore-summary";
import { Terminal } from "@xterm/xterm";
import { FitAddon } from "@xterm/addon-fit";
import { WebglAddon } from "@xterm/addon-webgl";
import { SearchAddon } from "@xterm/addon-search";
import "@xterm/xterm/css/xterm.css";
import { toBase64Utf8, parseRelayText } from "./wsproto";
import { scrubTerminalReports } from "./terminal-scrub";
import { renderKanbanBoard } from "./tasks-kanban";
import { renderTaskList } from "./tasks-list";
import { renderTimelineFeed } from "./timeline-feed";
import { renderMarkdownNote } from "./markdown-note";
import { renderSketchNote } from "./sketch-note";
import { renderCanvasNote } from "./canvas-note";
import { renderHtmlNote } from "./html-note";
import { renderNotepadPane, openNote } from "./notepad-pane";
import { renderMermaidNote } from "./mermaid-note";
import { renderSessionPicker } from "./session-picker";
import { resumeSpawnPlan, type ResumeAction, type VaultResumeResult } from "./session-resume";
import { renderLibraryPopover } from "./library-popover";
import { renderSnapshotInspector } from "./snapshot-inspector";
import { renderDiffReviewPane } from "./diff-review-pane";
import { renderEditorPane } from "./editor-pane";
import { renderSourceControlPane } from "./source-control-pane";
import { renderSearchPane } from "./search-pane";
import { browserWebviewRegistry, closeBrowserWebview, reconcileBrowserBounds, renderBrowserPane } from "./browser-pane";
import { buildAutomationJs, type AutomationExecEvent } from "./automation-snapshot";
import { renderDiffViewerPane } from "./diff-viewer-pane";
import { renderMarkdownPane } from "./markdown-pane";
import { renderPdfPane } from "./pdf-pane";
import { renderVideoPane } from "./video-pane";
import { partitionPinned, reorderRoom, glyphForPaneType, visibleRoomIds, buildWingModel, filterRoomsByWing } from "./room-tabs";
import { groupByWorkspace, moveSelection, selectedNote, kindIcon, kindColor, type NoteListItem, type NavState } from "./notepad-sidebar";
import { initialSidebarModel, selectLeftMode, toggleSide, setCollapsed, setWidth, collapsedOf, widthOf, SIDEBAR_MODES, SIDEBAR_MODE_META, type SidebarModel, type SidebarSide, type SidebarMode } from "./sidebar-model";
import { nextWorkspaceName, type WorkspaceBoxInput } from "./workspace-boxes";
import { clampMenuPosition, normalizeItems, firstSelectableIndex, moveSelection as ctxMoveSelection, activeItem, type ContextMenuItem, type ContextMenuModel } from "./context-menu";
import { buildWorkspaceTree, workspaceTreeEmptyMessage, PANE_TYPE_LABELS, type TreeLeaf, type TreeRoomInput, type TreeRow } from "./workspace-tree";
import { buildAgentRows, AGENT_STATE_META, type AgentCard, type AgentState } from "./agents-model";
import { resolveActiveWorkspaceId, workspaceAccent, sortFsEntries, joinPath, scmChipText, parseCollapsedCardIds, serializeCollapsedCardIds, toggleCardCollapsed, type FsEntry, type WorkspaceCardEntry, type ScmSummary } from "./workspace-cards";
import { parseQuery, filterAndSort, MruTracker, cycleCategory, categoryLabel, type PaletteItem } from "./omni-palette";
import { buildEmptyState, EmptyStateMessages } from "./empty-states";
import { brandLogoAt, nextBrandIndex, parseBrandIndex } from "./brand";
import { adapterCardSubtitle, adapterStatusMeta } from "./tools-tab";
import { DEFAULT_DRAFT, draftFromTheme, themeFromDraft, cssVarsFromTheme, xtermThemeFromDto, isCustom, isBuiltin, canSaveDraft, canDelete, isValidHex, contrastRatio, contrastTier, THEME_COLOR_FIELDS, type ThemeDto, type ThemeDraft } from "./theme-editor";
import { categorizeBindings, isReservedChord, isValidChord, chordDisplay, canRecordChord, normalizeChord as normalizeChordStr, type KeybindDto } from "./keyboard-editor";
import { ONBOARDING_STEPS, INITIAL_ONBOARDING_STATE, nextStep, prevStep, dismiss as dismissOnboarding, currentStepData, isLastStep, isFirstStep, progressPercent, selectAdapter, setTelemetryOptIn, shouldShowOnboarding, onboardingSeenFromConfig, ONBOARDING_COMPLETED_KEY, type OnboardingState } from "./onboarding";
import { initBackdrop, setBackdropMaterial, nextToggleMaterial, coerceMaterial, BACKDROP_PREF_KEY, type BackdropDeps, type BackdropMaterial } from "./backdrop";
import { NotificationBridge, type NotificationBridgeDeps, type NotificationDeliverPayload } from "./notifications";
import { buildMenu, menuChordSet } from "./menu-model";
import { toolbarTiles } from "./toolbar-tiles";
import { shouldShowLauncher, buildAdapterTiles, buildBuiltinTiles, isEmptyRoomTree, placeablePaneForAction, type LauncherAdapter, type LauncherBuiltin, type LauncherTile } from "./box-launcher";
import { adapterAccent, toolAccent, assignHotkeys, detectedHarnessTiles, clampLauncherSelection, moveLauncherSelection, hotkeyTarget, shapeRecentSessions, tipAt, computeLauncherCols, type LauncherSelection, type LauncherGeometry, type LauncherArrowKey, type RecentSessionRow } from "./launcher-model";
import { iconSvg, iconForPaneType, monogram } from "./icons";
import { dropZoneFor, moveMutationFor, zoneOverlayRect } from "./pane-dnd";
import { cssPath, buildFeedbackReport, feedbackSlug, harnessPrompt } from "./inspect-mode";
import { clusterTools } from "./title-cluster";
import { nextUpdateState, updateButtonLabel, updateAffordanceVisible, type UpdateState, type UpdateEvent } from "./update-flow";
import { initialZenState, toggleZen, type ChromeVisibility, type ZenState } from "./zen-mode";
import { eventToChord, buildChordMap, resolveDispatch, defaultBindings, type ResolvedBinding } from "./keymap-dispatch";
import { enqueuePaneWrite } from "./write-queue";

const RYN_MENUBAR_EVENTS_BROKEN = false;
import { initHud, toggleHud, recordFrame, hudMetrics, readJsHeapBytes, hudLines, type HudState, type JsHeapProbe } from "./perf-hud";
import { parseSnapshotExport, snapshotRows, summarizeSnapshots, formatBytes as formatSnapshotBytes, type DiagnosticsSnapshot } from "./diagnostics-snapshot";
import { initialPerfBundlesState, applyBundleList, beginCreate, finishCreate, surfaceError, requestDelete, cancelDelete, bundleRows, PERF_BUNDLES_EMPTY_TEXT, type PerfBundlesState, type PerfBundleListResult, type PerfBundleDto } from "./perf-bundles";

let brandIndex = parseBrandIndex(localStorage.getItem("cove.brandLogo"));
localStorage.setItem("cove.brandLogo", String(nextBrandIndex(brandIndex)));
function applyBrandLogo(): void {
  const src = brandLogoAt(brandIndex);
  const wm = document.getElementById("wordmark-img") as HTMLImageElement | null;
  if (wm) wm.src = src;
  for (const img of document.querySelectorAll<HTMLImageElement>(".cl-brand-img")) img.src = src;
}

const CREDIT_THRESHOLD = 131072;

const THEME_BG = "#1e1e2e";
const THEME = {
  background: THEME_BG,
  foreground: "#cdd6f4",
  cursor: "#f5e0dc",
  cursorAccent: THEME_BG,
  selectionBackground: "#585b70",
  black: "#45475a",
  red: "#f38ba8",
  green: "#a6e3a1",
  yellow: "#f9e2af",
  blue: "#89b4fa",
  magenta: "#f5c2e7",
  cyan: "#94e2d5",
  white: "#bac2de",
  brightBlack: "#585b70",
  brightRed: "#f38ba8",
  brightGreen: "#a6e3a1",
  brightYellow: "#f9e2af",
  brightBlue: "#89b4fa",
  brightMagenta: "#f5c2e7",
  brightCyan: "#94e2d5",
  brightWhite: "#a6adc8",
};
function themeBackgroundWithOpacity(opacity: number): string {
  const n = opacity >= 0 && opacity <= 1 ? opacity : 1;
  const r = parseInt(THEME_BG.slice(1, 3), 16);
  const g = parseInt(THEME_BG.slice(3, 5), 16);
  const b = parseInt(THEME_BG.slice(5, 7), 16);
  return `rgba(${r}, ${g}, ${b}, ${n})`;
}
let activeThemeDto: ThemeDto | null = null;
function currentTermTheme(): Record<string, string> {
  if (activeThemeDto) return xtermThemeFromDto(activeThemeDto, settings.backgroundOpacity);
  return { ...THEME, background: themeBackgroundWithOpacity(settings.backgroundOpacity) };
}

async function invoke<T>(cmd: string, args: unknown): Promise<T> {
  let result: unknown;
  if (cmd.startsWith("cove://")) {
    result = await window.__ryn.invoke("app.callEngine", { uri: cmd, argsJson: JSON.stringify(args ?? {}) });
  } else {
    result = await window.__ryn.invoke(cmd, args as Record<string, unknown>);
  }
  return JSON.parse(result as string) as T;
}

interface Subtab {
  documentId: string;
  paneType: string;
  title: string | null;
}

interface PaneLeaf {
  kind: "leaf";
  paneId: string;
  subtabs: Subtab[];
  activeSubtab: number;
}

interface SplitNode {
  kind: "split";
  orientation: number | string;
  ratio: number;
  childA: MosaicNode;
  childB: MosaicNode;
}

type MosaicNode = SplitNode | PaneLeaf;

interface RoomSnapshot {
  id: string;
  name: string;
  layoutTree: MosaicNode;
  zoomedPaneId: string | null;
}

interface WorkspaceSnapshot {
  schemaVersion: number;
  id: string;
  name: string;
  projectDir: string;
  activeRoomId: string | null;
  rooms: RoomSnapshot[];
}

interface PaneView {
  paneId: string;
  term: Terminal;
  fit: FitAddon;
  ws: WebSocket;
  el: HTMLElement;
  consumed: number;
  lastAck: number;
  title: string;
  customTitle: string;
  headerTitleEl: HTMLElement;
  search: SearchAddon;
  replaying: boolean;
}

const panes = new Map<string, PaneView>();
let layout: WorkspaceSnapshot | null = null;
let activeRoomId: string | null = null;
let focusedPaneId: string | null = null;
interface TermSettings {
  fontFamily: string;
  fontSize: number;
  lineHeight: number;
  cursorStyle: "block" | "bar" | "underline";
  cursorBlink: boolean;
  ligatures: boolean;
  scrollback: number;
  padding: number;
  backgroundOpacity: number;
}
const defaultSettings: TermSettings = {
  fontFamily: "",
  fontSize: 13,
  lineHeight: 1.35,
  cursorStyle: "block",
  cursorBlink: false,
  ligatures: false,
  scrollback: 5000,
  padding: 8,
  backgroundOpacity: 1,
};
function clampInt(v: unknown, lo: number, hi: number, dflt: number): number {
  const n = Number(v); return Number.isFinite(n) && n >= lo && n <= hi ? Math.trunc(n) : dflt;
}
function clampFloat(v: unknown, lo: number, hi: number, dflt: number): number {
  const n = Number(v); return Number.isFinite(n) && n >= lo && n <= hi ? n : dflt;
}
async function loadSettings(): Promise<TermSettings> {
  const get = async (k: string): Promise<string | null> => {
    try { const res = await invoke<{ ok: boolean; value?: string }>("app.configGet", { key: k }); return res.ok ? res.value ?? null : null; } catch { return null; }
  };
  const fontFamily = (await get("terminal.fontFamily")) ?? defaultSettings.fontFamily;
  const fontSize = clampInt(await get("terminal.fontSize"), 9, 24, defaultSettings.fontSize);
  const lhRaw = Number(await get("terminal.lineHeight"));
  const lineHeight = clampFloat(lhRaw, 1, 2, defaultSettings.lineHeight);
  const scrollback = clampInt(await get("terminal.scrollbackLines"), 100, 100000, defaultSettings.scrollback);
  const padding = clampInt(await get("terminal.padding"), 0, 40, defaultSettings.padding);
  const backgroundOpacity = clampFloat(await get("terminal.backgroundOpacity"), 0.2, 1, defaultSettings.backgroundOpacity);
  const cs = await get("terminal.cursorStyle");
  const cursorStyle: TermSettings["cursorStyle"] = cs === "bar" || cs === "underline" ? cs : "block";
  const cursorBlink = (await get("terminal.cursorBlink")) === "true";
  const ligatures = (await get("terminal.ligatures")) === "true";
  return { fontFamily, fontSize, lineHeight, cursorStyle, cursorBlink, ligatures, scrollback, padding, backgroundOpacity };
}
let settings: TermSettings = { ...defaultSettings };
interface KeybindingOverride { chord: string; action: string; }
function loadKeybindings(): Record<string, string> {
  const out: Record<string, string> = {};
  try {
    const raw = localStorage.getItem("cove.keybindings");
    if (!raw) return out;
    const list = JSON.parse(raw) as KeybindingOverride[];
    for (const o of list) out[o.chord] = o.action;
  } catch { void 0; }
  return out;
}
function normalizeChord(e: KeyboardEvent): string {
  const parts: string[] = [];
  if (e.ctrlKey) parts.push("ctrl");
  if (e.altKey) parts.push("alt");
  if (e.shiftKey) parts.push("shift");
  if (e.metaKey) parts.push("cmd");
  parts.push(e.key.toLowerCase());
  return parts.join("+");
}

const gridEl = document.getElementById("grid")!;
const paletteEl = document.getElementById("palette")!;
const roomTabsEl = document.getElementById("room-tabs")!;
const leftSidebarEl = document.getElementById("left-sidebar")!;
const leftRailEl = document.getElementById("left-rail")!;
const leftContentEl = document.getElementById("left-content")!;
const leftResizeEl = document.getElementById("left-resize")!;
const palInput = document.getElementById("pal-input") as HTMLInputElement;
const palList = document.getElementById("pal-list")!;

gridEl.style.display = "flex";
gridEl.style.padding = "8px";

function fitAll() {
  requestAnimationFrame(() => {
    for (const pv of panes.values()) {
      try { pv.fit.fit(); } catch { void 0; }
    }
  });
}

function applySettings() {
  for (const pv of panes.values()) {
    if (settings.fontFamily) pv.term.options.fontFamily = settings.fontFamily;
    pv.term.options.fontSize = settings.fontSize;
    pv.term.options.lineHeight = settings.lineHeight;
    pv.term.options.cursorStyle = settings.cursorStyle;
    pv.term.options.cursorBlink = settings.cursorBlink;
    pv.term.options.scrollback = settings.scrollback;
    pv.term.options.theme = currentTermTheme();
  }
  gridEl.style.padding = `${settings.padding}px`;
  document.documentElement.style.setProperty("--cove-bg-opacity", String(settings.backgroundOpacity));
  fitAll();
  persistSettings();
}
function persistSettings() {
  const entries: [string, string][] = [
    ["terminal.fontFamily", settings.fontFamily],
    ["terminal.fontSize", String(settings.fontSize)],
    ["terminal.lineHeight", String(settings.lineHeight)],
    ["terminal.cursorStyle", settings.cursorStyle],
    ["terminal.cursorBlink", String(settings.cursorBlink)],
    ["terminal.ligatures", String(settings.ligatures)],
    ["terminal.scrollbackLines", String(settings.scrollback)],
    ["terminal.padding", String(settings.padding)],
    ["terminal.backgroundOpacity", String(settings.backgroundOpacity)],
  ];
  for (const [k, v] of entries)
    invoke("app.configSet", { key: k, value: v }).catch((e) => console.warn("configSet failed", k, e));
}
function attachWs(pane: PaneView) {
  const ws = pane.ws;
  ws.binaryType = "arraybuffer";
  const sendAck = () => {
    if (ws.readyState === 1 && pane.consumed > pane.lastAck) {
      ws.send(JSON.stringify({ t: "ack", off: pane.consumed }));
      pane.lastAck = pane.consumed;
    }
  };
  ws.onmessage = (ev) => {
    if (typeof ev.data === "string") {
      const m = parseRelayText(ev.data);
      if (!m) return;
      if (m.t === "base") { pane.consumed = m.off; pane.lastAck = m.off; }
      else if (m.t === "resync") { pane.term.reset(); pane.consumed = m.base; pane.lastAck = m.base; }
      else if (m.t === "end") {
        pane.term.write(`\r\n\x1b[38;5;244m[process exited: ${m.code}]\x1b[0m\r\n`);
        launcherRecentsAt = 0;
        void refreshLauncherRecents();
        window.setTimeout(() => { void closePaneById(pane.paneId); }, 400);
      }
      return;
    }
    const raw = new Uint8Array(ev.data as ArrayBuffer);
    const bytes = scrubTerminalReports(raw, { includeOscColorReports: pane.replaying });
    pane.term.write(bytes, () => {
      pane.consumed += raw.length;
      if (pane.consumed - pane.lastAck >= CREDIT_THRESHOLD) sendAck();
    });
  };
  setInterval(sendAck, 100);
  pane.term.onData((d) => { pane.replaying = false; void enqueuePaneWrite(pane.paneId, toBase64Utf8(d), (paneId, dataBase64) => invoke("app.paneWrite", { paneId, dataBase64 })); });
  pane.term.onResize(({ cols, rows }) => { void invoke("app.paneResize", { paneId: pane.paneId, cols, rows }); });
}

function makePane(paneId: string, since: number): PaneView {
  const term = new Terminal({ allowTransparency: true, scrollback: settings.scrollback, convertEol: false, fontFamily: settings.fontFamily || "ui-monospace, SFMono-Regular, Menlo, monospace", fontSize: settings.fontSize, lineHeight: settings.lineHeight, cursorStyle: settings.cursorStyle, cursorBlink: settings.cursorBlink, theme: currentTermTheme() });
  const fitAddon = new FitAddon();
  term.loadAddon(fitAddon);
  try { term.loadAddon(new WebglAddon()); } catch { void 0; }
  const searchAddon = new SearchAddon();
  term.loadAddon(searchAddon);

  const el = document.createElement("div");
  el.className = "pane";
  el.style.flexGrow = "1";
  const header = document.createElement("div");
  header.className = "pane-header";
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
    b.addEventListener("click", (e) => { e.stopPropagation(); focusPane(paneId); openSplitChooser(e, ctl.dir); });
    header.appendChild(b);
  }
  header.appendChild(moreBtn);
  const closeBtn = document.createElement("button");
  closeBtn.className = "pmore pclose";
  closeBtn.textContent = "✕";
  closeBtn.title = "Close pane";
  closeBtn.addEventListener("click", (e) => { e.stopPropagation(); focusPane(paneId); void closeFocused(); });
  header.appendChild(closeBtn);
  el.appendChild(header);
  const host = document.createElement("div");
  host.className = "term-host";
  el.appendChild(host);
  term.open(host);

  header.addEventListener("contextmenu", (e) => {
    focusPane(paneId);
    openContextMenuAt(e, [
      { id: "pane.split-right", label: "Split Right" },
      { id: "pane.split-down", label: "Split Down" },
      { id: "pane.maximize", label: "Maximize" },
      { id: "sep", label: "", separator: true },
      { id: "pane.close", label: "Close", danger: true },
    ], (id) => runAction(id));
  });
  host.addEventListener("contextmenu", (e) => {
    focusPane(paneId);
    const hasSel = term.hasSelection();
    openContextMenuAt(e, [
      { id: "copy", label: "Copy", disabled: !hasSel },
      { id: "paste", label: "Paste" },
      { id: "clear", label: "Clear" },
      { id: "sep", label: "", separator: true },
      { id: "find", label: "Find in Pane" },
    ], (id) => {
      if (id === "copy") { const s = term.getSelection(); if (s && navigator.clipboard) void navigator.clipboard.writeText(s); }
      else if (id === "paste") { if (navigator.clipboard && navigator.clipboard.readText) void navigator.clipboard.readText().then((t) => { if (t) void invoke("app.paneWrite", { paneId, dataBase64: toBase64Utf8(t) }); }); }
      else if (id === "clear") term.clear();
      else if (id === "find") openFind();
    });
  });

  const ws = new WebSocket(`ws://${location.host}/pty?pane=${encodeURIComponent(paneId)}&since=${since}`);
  const pv: PaneView = { paneId, term, fit: fitAddon, ws, el, consumed: 0, lastAck: 0, title: "", customTitle: "", headerTitleEl: titleSpan, search: searchAddon, replaying: true };

  el.addEventListener("mousedown", () => focusPane(paneId));
  attachWs(pv);
  const overrides = loadKeybindings();
  term.attachCustomKeyEventHandler((e) => {
    if (e.type !== "keydown") return true;
    if (e.key === "Tab") { e.preventDefault(); return true; }
    const chord = normalizeChord(e);
    const action = overrides[chord];
    if (action && action.startsWith("send-text:")) { void invoke("app.paneWrite", { paneId, dataBase64: toBase64Utf8(action.slice("send-text:".length)) }); return false; }
    if (e.shiftKey && e.key === "Enter") { void invoke("app.paneWrite", { paneId, dataBase64: toBase64Utf8("\x1b\r") }); return false; }
    if (!e.metaKey || e.altKey || e.ctrlKey) return true;
    const k = e.key.toLowerCase();
    if (k === "c") {
      if (term.hasSelection()) { const s = term.getSelection(); if (s && navigator.clipboard) void navigator.clipboard.writeText(s); }
      else { void invoke("app.paneWrite", { paneId, dataBase64: toBase64Utf8("\u0003") }); }
      return false;
    }
    if (k === "a") { term.selectAll(); return false; }
    if (k === "v") {
      if (navigator.clipboard && navigator.clipboard.readText) void navigator.clipboard.readText().then((t) => { if (t) void invoke("app.paneWrite", { paneId, dataBase64: toBase64Utf8(t) }); });
      return false;
    }
    if (e.key === "ArrowLeft") { void invoke("app.paneWrite", { paneId, dataBase64: toBase64Utf8("\u0001") }); return false; }
    if (e.key === "ArrowRight") { void invoke("app.paneWrite", { paneId, dataBase64: toBase64Utf8("\u0005") }); return false; }
    if (e.key === "Backspace") { void invoke("app.paneWrite", { paneId, dataBase64: toBase64Utf8("\u0015") }); return false; }
    return true;
  });
  const setTitle = () => { titleSpan.textContent = pv.customTitle || pv.title || "shell"; };
  header.addEventListener("mousedown", (e) => { if (e.target !== moreBtn) focusPane(paneId); });
  header.draggable = true;
  header.addEventListener("dragstart", (e) => {
    if (!e.dataTransfer) return;
    e.dataTransfer.setData("text/cove-pane", paneId);
    e.dataTransfer.effectAllowed = "move";
    draggingPaneId = paneId;
  });
  header.addEventListener("dragend", () => { draggingPaneId = null; clearDropOverlay(); });
  el.addEventListener("dragover", (e) => {
    if (!draggingPaneId || draggingPaneId === paneId) return;
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
    const src = e.dataTransfer?.getData("text/cove-pane") || draggingPaneId;
    clearDropOverlay();
    draggingPaneId = null;
    if (!src || !activeRoomId) { console.warn("pane drop without source or active room"); return; }
    const rect = el.getBoundingClientRect();
    const zone = dropZoneFor(e.clientX - rect.left, e.clientY - rect.top, rect.width, rect.height);
    const m = moveMutationFor(zone, src, paneId);
    if (!m) return;
    void applyPaneMove(m, src);
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
        rememberPaneTitle(paneId, newTitle || pv.title);
        void invoke("app.paneRename", { paneId, title: newTitle }).catch(() => void 0);
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
    closePaneMenus();
    const menu = document.createElement("div");
    menu.className = "pmenu";
    const mk = (label: string, fn: () => void) => { const r = document.createElement("div"); r.className = "pmi"; r.textContent = label; r.addEventListener("click", (ev) => { ev.stopPropagation(); closePaneMenus(); fn(); }); menu.appendChild(r); };
    mk("Copy Pane ID", () => { if (navigator.clipboard) void navigator.clipboard.writeText(paneId); });
    mk("Rename", startRename);
    mk("New subtab", () => void addSubtab(paneId));
    mk("Close", () => { focusPane(paneId); void closeFocused(); });
    mk("Close Others", () => { void closeOthers(paneId); });
    header.appendChild(menu);
  });
  term.onTitleChange((t) => { pv.title = t; rememberPaneTitle(paneId, pv.customTitle || t); setTitle(); refreshTitles(); });
  panes.set(paneId, pv);
  return pv;
}

function getPane(paneId: string): PaneView {
  const existing = panes.get(paneId);
  if (existing) return existing;
  return makePane(paneId, 0);
}

function closePaneMenus(): void {
  document.querySelectorAll(".pmenu").forEach((m) => m.remove());
}
document.addEventListener("click", closePaneMenus);

document.addEventListener("contextmenu", (e) => e.preventDefault());

let ctxMenuEl: HTMLElement | null = null;
let ctxKeyHandler: ((e: KeyboardEvent) => void) | null = null;
let ctxAwayHandler: ((e: MouseEvent) => void) | null = null;

function closeContextMenu(): void {
  if (ctxMenuEl) { ctxMenuEl.remove(); ctxMenuEl = null; }
  if (ctxKeyHandler) { document.removeEventListener("keydown", ctxKeyHandler, true); ctxKeyHandler = null; }
  if (ctxAwayHandler) { document.removeEventListener("mousedown", ctxAwayHandler, true); ctxAwayHandler = null; }
}

function showContextMenu(model: ContextMenuModel, onSelect: (id: string) => void): void {
  closeContextMenu();
  const items = normalizeItems(model.items);
  if (items.length === 0) return;
  const menu = document.createElement("div");
  menu.className = "ctx-menu";
  let selected = firstSelectableIndex(items);
  const rowEls: HTMLElement[] = [];
  const paint = () => rowEls.forEach((el, i) => el.classList.toggle("sel", i === selected));
  const choose = (index: number) => {
    const item = activeItem(items, index);
    if (!item) return;
    closeContextMenu();
    onSelect(item.id);
  };
  items.forEach((item, i) => {
    if (item.separator) {
      const sep = document.createElement("div");
      sep.className = "ctx-sep";
      rowEls.push(sep);
      menu.appendChild(sep);
      return;
    }
    const rowEl = document.createElement("div");
    rowEl.className = "ctx-item" + (item.danger ? " danger" : "") + (item.disabled ? " disabled" : "");
    rowEl.textContent = item.label;
    rowEls.push(rowEl);
    if (!item.disabled) {
      rowEl.addEventListener("mouseenter", () => { selected = i; paint(); });
      rowEl.addEventListener("click", () => choose(i));
    }
    menu.appendChild(rowEl);
  });
  paint();
  menu.style.cssText = "position:fixed;left:-9999px;top:-9999px;";
  document.body.appendChild(menu);
  const size = { width: menu.offsetWidth, height: menu.offsetHeight };
  const pos = clampMenuPosition({ x: model.x, y: model.y }, size, { width: window.innerWidth, height: window.innerHeight });
  menu.style.left = `${pos.x}px`;
  menu.style.top = `${pos.y}px`;
  ctxMenuEl = menu;
  ctxKeyHandler = (e) => {
    if (e.key === "Escape") { e.preventDefault(); closeContextMenu(); }
    else if (e.key === "ArrowDown") { e.preventDefault(); selected = ctxMoveSelection(items, selected, 1); paint(); }
    else if (e.key === "ArrowUp") { e.preventDefault(); selected = ctxMoveSelection(items, selected, -1); paint(); }
    else if (e.key === "Enter") { e.preventDefault(); choose(selected); }
  };
  document.addEventListener("keydown", ctxKeyHandler, true);
  ctxAwayHandler = (ev) => { if (ctxMenuEl && !ctxMenuEl.contains(ev.target as Node)) closeContextMenu(); };
  setTimeout(() => { if (ctxAwayHandler) document.addEventListener("mousedown", ctxAwayHandler, true); }, 0);
}

function openContextMenuAt(e: MouseEvent, items: ContextMenuItem[], onSelect: (id: string) => void): void {
  e.preventDefault();
  e.stopPropagation();
  showContextMenu({ items, x: e.clientX, y: e.clientY }, onSelect);
}

function isColumn(orientation: number | string): boolean {
  return orientation === 1 || orientation === "Column" || orientation === "column";
}

function collectLeafIds(node: MosaicNode): string[] {
  if (node.kind === "leaf") return node.subtabs.length > 0 ? node.subtabs.map((s) => s.documentId) : [node.paneId];
  return [...collectLeafIds(node.childA), ...collectLeafIds(node.childB)];
}
function findLeafId(node: MosaicNode, termId: string): string | null {
  if (node.kind === "leaf") return (node.paneId === termId || node.subtabs.some((s) => s.documentId === termId)) ? node.paneId : null;
  return findLeafId(node.childA, termId) ?? findLeafId(node.childB, termId);
}
async function activateSubtab(leafId: string, index: number): Promise<void> {
  if (!activeRoomId) return;
  await invoke("app.layoutMutate", { op: "activateSubtab", roomId: activeRoomId, paneId: leafId, targetPaneId: "", newPaneId: "", orientation: "", name: "", dir: index });
  await reload();
}
async function addSubtab(termPaneId: string): Promise<void> {
  const room = activeRoom();
  if (!room || !activeRoomId) return;
  const leafId = findLeafId(room.layoutTree, termPaneId);
  if (!leafId) return;
  const sp = (await invoke<{ paneId: string }>("app.paneSpawn", { command: "", cwd: "", inheritCwdFrom: termPaneId, cols: 80, rows: 24, adapter: "", agentName: "", workspace: "", room: "" })).paneId;
  await invoke("app.layoutMutate", { op: "addSubtab", roomId: activeRoomId, paneId: leafId, newPaneId: sp, targetPaneId: "", orientation: "", name: "", dir: 0 });
  await reload();
  focusPane(sp);
}

function emptyPaneStrip(paneId: string): HTMLElement {
  const el = document.createElement("div");
  el.className = "pane empty-pane";
  el.style.flex = "1 1 0";
  el.style.minWidth = "0";
  el.style.minHeight = "0";
  el.style.display = "flex";
  el.style.flexDirection = "column";
  el.style.alignItems = "center";
  el.style.justifyContent = "flex-end";
  el.style.paddingBottom = "24px";
  const strip = document.createElement("div");
  strip.className = "pane-dock";
  strip.style.display = "flex";
  strip.style.gap = "8px";
  const tile = document.createElement("button");
  tile.className = "dock-tile";
  tile.textContent = "Terminal";
  tile.addEventListener("click", (e) => {
    e.stopPropagation();
    void spawnIntoPane(paneId);
  });
  strip.appendChild(tile);
  el.appendChild(strip);
  el.addEventListener("mousedown", () => focusPane(paneId));
  return el;
}

async function spawnIntoPane(paneId: string): Promise<void> {
  if (!activeRoomId) return;
  const sp = (await invoke<{ paneId: string }>("app.paneSpawn", { command: "", cwd: "", inheritCwdFrom: "", cols: 80, rows: 24, adapter: "", agentName: "", workspace: "", room: "" })).paneId;
  await invoke("app.layoutMutate", { op: "addSubtab", roomId: activeRoomId, paneId: paneId, newPaneId: sp, targetPaneId: "", orientation: "", name: "", dir: 0 });
  await reload();
}

function renderKanbanPane(paneId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "kanban-pane-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  const workspaceId = "default";
  renderKanbanBoard(workspaceId).then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load kanban: ${(e as Error).message}</div>`;
  });
  return placeholder;
}

function renderTaskListPane(paneId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "task-list-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  renderTaskList("default").then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load task list: ${(e as Error).message}</div>`;
  });
  return placeholder;
}

function renderTaskDetailPane(paneId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "task-detail-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;display:flex;align-items:center;justify-content:center;color:#6b7280;";
  placeholder.textContent = "Select a task to view details";
  return placeholder;
}
function renderTimelinePane(paneId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "timeline-pane-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  renderTimelineFeed("default").then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load timeline: ${(e as Error).message}</div>`;
  });
  return placeholder;
}
function renderMarkdownNotePane(paneId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "markdown-note-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  renderMarkdownNote("default", paneId).then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load note: ${(e as Error).message}</div>`;
  });
  return placeholder;
}
function renderSketchNotePane(paneId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "sketch-note-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  renderSketchNote("default", paneId).then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load sketch: ${(e as Error).message}</div>`;
  });
  return placeholder;
}
function renderCanvasNotePane(paneId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "canvas-note-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  renderCanvasNote("default", paneId).then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load canvas: ${(e as Error).message}</div>`;
  });
  return placeholder;
}
function renderHtmlNotePane(paneId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "html-note-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  renderHtmlNote("default", paneId).then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load HTML note: ${(e as Error).message}</div>`;
  });
  return placeholder;
}
function renderNotepadPaneWrapper(paneId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "notepad-pane-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  renderNotepadPane("default").then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load notepad: ${(e as Error).message}</div>`;
  });
  return placeholder;
}
function renderMermaidNotePane(paneId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "mermaid-note-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  renderMermaidNote("default", paneId).then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load mermaid note: ${(e as Error).message}</div>`;
  });
  return placeholder;
}
function renderSessionPickerPane(paneId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "session-picker-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  const workspaceId = layout?.id ?? "default";
  const projectDir = activeProjectDir();
  const adapters = launcherAdapters.map((a) => ({ name: a.name, displayName: a.displayName }));
  renderSessionPicker(workspaceId, projectDir, adapters, (adapter, sessionId, cwd, displayName) => {
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
    const result = await invoke<VaultResumeResult>("cove://commands/vault.resume", { adapter, sessionId, cwd, yolo: launcherYolo(adapter) });
    action = resumeSpawnPlan(result, cwd, displayName);
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
  const sp = (await invoke<{ paneId: string }>("app.paneSpawn", { command: action.command, args: action.args, cwd: action.cwd, inheritCwdFrom: "", cols: 80, rows: 24, adapter: action.adapter, agentName: action.roomName, workspace: "", room: "" })).paneId;
  const r = await invoke<{ roomId: string }>("app.layoutMutate", { op: "createRoom", newPaneId: sp, name: action.roomName, roomId: "", targetPaneId: "", orientation: "", paneId: "", dir: 0, paneType: "terminal" });
  activeRoomId = r.roomId;
  await reload();
  focusPane(sp);
  if (action.toast) showInAppToast(action.toast.title, action.toast.body, () => revealPane(sp));
}

function renderLibraryPane(paneId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "library-pane-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  renderLibraryPopover("default").then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load library: ${(e as Error).message}</div>`;
  });
  return placeholder;
}
function renderSnapshotInspectorPane(paneId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "snapshot-inspector-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  renderSnapshotInspector("default").then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load snapshots: ${(e as Error).message}</div>`;
  });
  return placeholder;
}
function renderDiffReviewPaneWrapper(paneId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "diff-review-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  renderDiffReviewPane("default").then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load diff review: ${(e as Error).message}</div>`;
  });
  return placeholder;
}
function renderEditorPaneWrapper(paneId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "editor-pane-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  renderEditorPane(paneId, paneFilePaths.get(paneId) ?? paneId).then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load editor: ${(e as Error).message}</div>`;
  });
  return placeholder;
}

function renderImagePane(paneId: string): HTMLElement {
  const el = document.createElement("div");
  el.className = "image-pane";
  el.style.cssText = "display:flex;align-items:center;justify-content:center;height:100%;background:#0d1117;overflow:hidden;position:relative;";
  const img = document.createElement("img");
  img.style.cssText = "max-width:100%;max-height:100%;object-fit:contain;transition:transform 0.1s;";
  img.alt = paneId;
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
  fitBtn.addEventListener("click", () => { img.style.transform = "scale(1)"; zoom = 1; });
  zoomInBtn.addEventListener("click", () => { zoom = Math.min(zoom * 1.25, 10); img.style.transform = `scale(${zoom})`; });
  zoomOutBtn.addEventListener("click", () => { zoom = Math.max(zoom / 1.25, 0.1); img.style.transform = `scale(${zoom})`; });
  controls.appendChild(fitBtn);
  controls.appendChild(zoomOutBtn);
  controls.appendChild(zoomInBtn);
  el.appendChild(img);
  el.appendChild(controls);
  return el;
}
function activeProjectDir(): string {
  return workspaceBoxItems.find((w) => w.id === (layout?.id ?? null))?.projectDir ?? "";
}
function renderGitPaneWrapper(paneId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "git-pane-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  renderSourceControlPane(activeProjectDir(), (path) => { void openFileInEditor(path); }).then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load source control: ${(e as Error).message}</div>`;
  });
  return placeholder;
}
function renderSearchPaneWrapper(paneId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "search-pane-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  renderSearchPane("default").then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load search: ${(e as Error).message}</div>`;
  });
  return placeholder;
}
function renderBrowserPaneWrapper(paneId: string, url: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "browser-pane-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  renderBrowserPane(paneId, url).then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load browser: ${(e as Error).message}</div>`;
  });
  return placeholder;
}
function renderDiffViewerPaneWrapper(paneId: string, refInput: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "diff-viewer-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  renderDiffViewerPane(paneId, paneId, refInput).then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load diff: ${(e as Error).message}</div>`;
  });
  return placeholder;
}
function renderMarkdownPaneWrapper(paneId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "markdown-pane-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  renderMarkdownPane(paneId, paneId).then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load markdown: ${(e as Error).message}</div>`;
  });
  return placeholder;
}
function renderNode(node: MosaicNode): HTMLElement {
  if (node.kind === "leaf") {
    const subs = node.subtabs.length > 0 ? node.subtabs : [{ documentId: node.paneId, paneType: "terminal", title: null }];
    const activeIdx = Math.min(Math.max(0, node.activeSubtab), subs.length - 1);
    const active = subs[activeIdx];
    const isEmpty = active.paneType === "empty" || node.subtabs.length === 0;
    if (active.paneType === "tasks-kanban") return renderKanbanPane(active.documentId);
    if (active.paneType === "tasks-list") return renderTaskListPane(active.documentId);
    if (active.paneType === "tasks-detail") return renderTaskDetailPane(active.documentId);
    if (active.paneType === "timeline-feed") return renderTimelinePane(active.documentId);
    if (active.paneType === "note-markdown") return renderMarkdownNotePane(active.documentId);
    if (active.paneType === "note-sketch") return renderSketchNotePane(active.documentId);
    if (active.paneType === "note-canvas") return renderCanvasNotePane(active.documentId);
    if (active.paneType === "note-html") return renderHtmlNotePane(active.documentId);
    if (active.paneType === "markdown") return renderMarkdownPaneWrapper(active.documentId);
    if (active.paneType === "notepad") return renderNotepadPaneWrapper(active.documentId);
    if (active.paneType === "note-mermaid") return renderMermaidNotePane(active.documentId);
    if (active.paneType === "session-picker") return renderSessionPickerPane(active.documentId);
    if (active.paneType === "library") return renderLibraryPane(active.documentId);
    if (active.paneType === "snapshot-inspector") return renderSnapshotInspectorPane(active.documentId);
    if (active.paneType === "diff-review") return renderDiffReviewPaneWrapper(active.documentId);
    if (active.paneType === "editor") return renderEditorPaneWrapper(active.documentId);
    if (active.paneType === "image") return renderImagePane(active.documentId);
    if (active.paneType === "git" || active.paneType === "sourceControl") return renderGitPaneWrapper(active.documentId);
    if (active.paneType === "search") return renderSearchPaneWrapper(active.documentId);
    if (active.paneType === "browser") return renderBrowserPaneWrapper(active.documentId, active.title ?? "about:blank");
    if (active.paneType === "diff") return renderDiffViewerPaneWrapper(active.documentId, active.title ?? "");
    if (active.paneType === "pdf") return renderPdfPane(paneFilePaths.get(active.documentId) ?? active.title ?? active.documentId);
    if (active.paneType === "video") return renderVideoPane(paneFilePaths.get(active.documentId) ?? active.title ?? active.documentId);
    if (isEmpty) return emptyPaneStrip(node.paneId);
    const activeEl = getPane(subs[activeIdx].documentId).el;
    activeEl.style.flexGrow = "1";
    if (subs.length <= 1) return activeEl;
    const wrap = document.createElement("div");
    wrap.className = "leaf-wrap";
    const strip = document.createElement("div");
    strip.className = "subtab-strip";
    subs.forEach((s, i) => {
      const tab = document.createElement("div");
      tab.className = "subtab" + (i === activeIdx ? " active" : "");
      const pvv = panes.get(s.documentId);
      tab.textContent = (pvv && (pvv.customTitle || pvv.title)) || s.title || "shell";
      tab.addEventListener("click", () => { void activateSubtab(node.paneId, i); });
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
    const onUp = () => {
      document.removeEventListener("mousemove", onMove);
      document.removeEventListener("mouseup", onUp);
      fitAll();
    };
    document.addEventListener("mousemove", onMove);
    document.addEventListener("mouseup", onUp);
  });
}

function activeRoom(): RoomSnapshot | undefined {
  if (!layout) return undefined;
  return layout.rooms.find((r) => r.id === activeRoomId) ?? layout.rooms[0];
}

function activeLeafIds(): string[] {
  const room = activeRoom();
  if (!room) return [];
  return collectLeafIds(room.layoutTree);
}

function firstLeafOf(room: RoomSnapshot): string | undefined {
  return collectLeafIds(room.layoutTree)[0];
}

function renderRoom(): void {
  const room = activeRoom();
  gridEl.innerHTML = "";
  const roomEmpty = room ? isEmptyRoomTree(room.layoutTree) : false;
  let zoomed = false;
  if (room && room.layoutTree && !roomEmpty) {
    const treeIds = collectLeafIds(room.layoutTree);
    const zid = room.zoomedPaneId;
    if (zid && treeIds.includes(zid)) {
      const zoomEl = getPane(zid).el;
      zoomEl.style.flexGrow = "1";
      gridEl.appendChild(zoomEl);
      focusedPaneId = zid;
      zoomed = true;
    } else {
      gridEl.appendChild(renderNode(room.layoutTree));
    }
    if (!zoomed) {
      const keep = new Set<string>(treeIds);
      for (const [id, pv] of panes) {
        if (!keep.has(id)) {
          try { pv.ws.close(); } catch { void 0; }
          pv.term.dispose();
          panes.delete(id);
        }
      }
    }
  } else {
    for (const [id, pv] of panes) {
      try { pv.ws.close(); } catch { void 0; }
      pv.term.dispose();
      panes.delete(id);
    }
  }
  if (room && roomEmpty) {
    const placeholder = collectLeafIds(room.layoutTree)[0] ?? null;
    focusedPaneId = null;
    gridEl.appendChild(renderBoxLauncher(room.id, placeholder));
  } else if (!room || !room.layoutTree) {
    if (shouldShowLauncher((layout?.rooms ?? []).length)) {
      gridEl.appendChild(renderBoxLauncher(null, null));
    } else {
      const empty = buildEmptyState({ message: EmptyStateMessages.noRooms, actionLabel: "New terminal", actionIcon: "+" });
      const action = empty.querySelector(".cove-empty-action");
      if (action) action.addEventListener("click", () => void newRoom());
      gridEl.appendChild(empty);
    }
  }
  for (const [id, pv] of panes) {
    pv.el.classList.toggle("focused", id === focusedPaneId);
  }
  fitAll();
  requestAnimationFrame(() => { fitAll(); reconcileBrowserBounds(); });
}

function focusPane(paneId: string): void {
  focusedPaneId = paneId;
  for (const [id, pv] of panes) {
    pv.el.classList.toggle("focused", id === paneId);
  }
  panes.get(paneId)?.term.focus();
  refreshTitles();
  if (sidebarModel.leftMode === "workspaces" && !collapsedOf(sidebarModel, "left")) renderSidebarContent("left");
  if (activeRoomId) {
    void invoke("app.layoutMutate", { op: "focus", roomId: activeRoomId, paneId, targetPaneId: "", newPaneId: "", orientation: "", name: "", dir: 0 });
  }
}

function refreshTitles(): void {
  const tabEls = roomTabsEl.querySelectorAll<HTMLElement>(".rtab");
  (layout?.rooms ?? []).forEach((r) => {
    const tab = Array.from(tabEls).find((el) => el.title === roomTabName(r) || el.querySelector(".rtab-name")?.textContent === roomTabName(r));
    if (tab) {
      const nameEl = tab.querySelector<HTMLElement>(".rtab-name");
      if (nameEl) nameEl.textContent = roomTabName(r);
      tab.title = roomTabName(r);
    }
  });
}

async function reload(): Promise<WorkspaceSnapshot> {
  layout = await invoke<WorkspaceSnapshot>("app.layoutGet", {});
  try {
    const list = await invoke<{ panes: { paneId: string; title: string | null }[] }>("app.paneList", {});
    for (const p of list.panes) {
      const pv = panes.get(p.paneId);
      if (pv && p.title) pv.customTitle = p.title;
    }
  } catch { void 0; }
  if (!activeRoomId) {
    activeRoomId = layout.activeRoomId ?? layout.rooms[0]?.id ?? null;
  }
  const leaves = activeLeafIds();
  if (!focusedPaneId || !leaves.includes(focusedPaneId)) {
    focusedPaneId = leaves[0] ?? null;
  }
  if (activeProjectDir() !== launcherRecentsCwd) await loadLauncherRecents();
  renderRoom();
  renderRoomTabs();
  renderSidebar();
  if (focusedPaneId) {
    panes.get(focusedPaneId)?.term.focus();
  }
  refreshTitles();
  return layout;
}

async function splitActive(dir: "row" | "col"): Promise<void> {
  if (!layout || layout.rooms.length === 0 || !activeRoomId) {
    await newRoom();
    return;
  }
  const src = focusedPaneId;
  if (!src) return;
  const sp = (await invoke<{ paneId: string }>("app.paneSpawn", { command: "", cwd: "", inheritCwdFrom: src, cols: 80, rows: 24, adapter: "", agentName: "", workspace: "", room: "" })).paneId;
  await invoke("app.layoutMutate", { op: "split", roomId: activeRoomId, targetPaneId: src, newPaneId: sp, orientation: dir, name: "", paneId: "", dir: 0 });
  await reload();
  focusPane(sp);
}

let draggingPaneId: string | null = null;
let dropOverlayEl: HTMLElement | null = null;
let tabSpringTimer: number | null = null;
let tabSpringRoomId: string | null = null;
document.addEventListener("dragend", () => { draggingPaneId = null; clearDropOverlay(); });

async function movePaneToRoom(paneId: string, targetRoomId: string): Promise<void> {
  try {
    await invoke("app.layoutMutate", { op: "movePaneToRoom", roomId: targetRoomId, paneId, targetPaneId: "", newPaneId: "", orientation: "", name: "", dir: 0 });
    activeRoomId = targetRoomId;
    await reload();
    focusPane(paneId);
  } catch (err) { console.warn("move pane to room failed", paneId, targetRoomId, err); }
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

async function applyPaneMove(m: { op: string; paneId: string; targetPaneId: string; orientation: string; dir: number }, focusId: string): Promise<void> {
  if (!activeRoomId) { console.warn("pane move without active room"); return; }
  try {
    const srcRoom = layout?.rooms.find((r) => collectLeafIds(r.layoutTree).includes(m.paneId));
    if (srcRoom && srcRoom.id !== activeRoomId) {
      await invoke("app.layoutMutate", { op: "movePaneToRoom", roomId: activeRoomId, paneId: m.paneId, targetPaneId: "", newPaneId: "", orientation: "", name: "", dir: 0 });
    }
    if (m.op === "centerDrop") {
      const room = activeRoom();
      const srcLeaf = room ? findLeaf(room.layoutTree, m.paneId) : null;
      const idx = srcLeaf ? Math.max(0, srcLeaf.activeSubtab) : 0;
      await invoke("app.layoutMutate", { op: "centerDrop", roomId: activeRoomId, targetPaneId: m.paneId, paneId: m.targetPaneId, dir: idx, newPaneId: "", orientation: "", name: "" });
    } else {
      await invoke("app.layoutMutate", { op: "movePane", roomId: activeRoomId, paneId: m.paneId, targetPaneId: m.targetPaneId, orientation: m.orientation, dir: m.dir, newPaneId: "", name: "" });
    }
    await reload();
    focusPane(focusId);
  } catch (err) { console.warn("pane move failed", m.op, err); }
}

function findLeaf(node: MosaicNode, paneId: string): { paneId: string; activeSubtab: number } | null {
  if (node.kind === "leaf") return node.paneId === paneId ? { paneId: node.paneId, activeSubtab: node.activeSubtab } : null;
  return findLeaf(node.childA, paneId) ?? findLeaf(node.childB, paneId);
}

async function closePaneById(paneId: string): Promise<void> {
  const room = layout?.rooms.find((r) => collectLeafIds(r.layoutTree).includes(paneId));
  if (!room) { console.warn("close requested for pane not in layout", paneId); return; }
  await closeBrowserWebview(paneId);
  try { await invoke("app.paneKill", { paneId }); } catch (err) { console.warn("pane kill on exit failed", paneId, err); }
  try { await invoke("app.layoutMutate", { op: "close", roomId: room.id, paneId, targetPaneId: "", newPaneId: "", orientation: "", name: "", dir: 0 }); } catch (err) { console.warn("layout close on exit failed", paneId, err); }
  await reload();
}

function openSplitChooser(e: MouseEvent, dir: "row" | "col"): void {
  const harnesses = detectedHarnessTiles(buildAdapterTiles(launcherAdapters));
  const items: ContextMenuItem[] = [
    { id: "terminal", label: "Terminal" },
    ...harnesses.map((h) => ({ id: `adapter:${h.adapterName}`, label: h.label })),
    { id: "sep1", label: "", separator: true },
    { id: "browser", label: "Browser" },
    { id: "search", label: "Search" },
    { id: "git", label: "Source Control" },
    { id: "tasks-list", label: "Tasks" },
  ];
  openContextMenuAt(e, items, (id) => { void splitActiveWith(dir, id); });
}

async function splitActiveWith(dir: "row" | "col", kind: string): Promise<void> {
  if (!activeRoomId) { console.warn("split requested with no active room"); return; }
  const target = focusedPaneId ?? activeLeafIds()[0];
  if (!target) { console.warn("split requested with no target pane"); return; }
  let paneId: string;
  let paneType = "terminal";
  if (kind === "terminal") {
    paneId = (await invoke<{ paneId: string }>("app.paneSpawn", { command: "", cwd: "", inheritCwdFrom: target, cols: 80, rows: 24, adapter: "", agentName: "", workspace: "", room: "" })).paneId;
  } else if (kind.startsWith("adapter:")) {
    const name = kind.slice("adapter:".length);
    const tile = detectedHarnessTiles(buildAdapterTiles(launcherAdapters)).find((t) => t.adapterName === name);
    if (!tile) { console.warn("split chooser: unknown adapter", name); return; }
    const launch = await buildAdapterLaunch({ name: tile.adapterName, displayName: tile.label, accent: tile.accent, binary: tile.binary });
    paneId = (await invoke<{ paneId: string }>("app.paneSpawn", { command: launch.command, args: launch.args, cwd: "", inheritCwdFrom: target, cols: 80, rows: 24, adapter: tile.adapterName, agentName: tile.label, workspace: "", room: "" })).paneId;
  } else if (kind === "browser") {
    paneId = (await invoke<{ paneId: string; currentUrl: string }>("cove://commands/browser.create", { url: "https://duckduckgo.com" })).paneId;
    paneType = "browser";
  } else {
    paneId = (await invoke<{ paneId: string }>("app.paneSpawn", { command: "", cwd: "", inheritCwdFrom: "", cols: 80, rows: 24, adapter: "", agentName: "", workspace: "", room: "" })).paneId;
    paneType = kind;
  }
  await invoke("app.layoutMutate", { op: "split", roomId: activeRoomId, targetPaneId: target, newPaneId: paneId, orientation: dir, name: "", paneId: "", dir: 0, paneType });
  await reload();
  focusPane(paneId);
}

async function closeFocused(): Promise<void> {
  if (!focusedPaneId || !activeRoomId) return;
  await invoke("app.paneKill", { paneId: focusedPaneId });
  await invoke("app.layoutMutate", { op: "close", roomId: activeRoomId, paneId: focusedPaneId, targetPaneId: "", newPaneId: "", orientation: "", name: "", dir: 0 });
  await reload();
}

async function closeOthers(keepPaneId: string): Promise<void> {
  if (!activeRoomId) return;
  const room = activeRoom();
  if (!room) return;
  const others = collectLeafIds(room.layoutTree).filter((id) => id !== keepPaneId);
  for (const id of others) {
    try { await invoke("app.paneKill", { paneId: id }); } catch { void 0; }
    try { await invoke("app.layoutMutate", { op: "close", roomId: activeRoomId, paneId: id, targetPaneId: "", newPaneId: "", orientation: "", name: "", dir: 0 }); } catch { void 0; }
  }
  focusPane(keepPaneId);
  await reload();
}

async function toggleZoom(): Promise<void> {
  if (!focusedPaneId || !activeRoomId) return;
  const room = activeRoom();
  if (room && room.zoomedPaneId === focusedPaneId) {
    await invoke("app.layoutMutate", { op: "unzoom", roomId: activeRoomId, paneId: "", targetPaneId: "", newPaneId: "", orientation: "", name: "", dir: 0 });
  } else {
    await invoke("app.layoutMutate", { op: "zoom", roomId: activeRoomId, paneId: focusedPaneId, targetPaneId: "", newPaneId: "", orientation: "", name: "", dir: 0 });
  }
  await reload();
}

function cycleFocus(d: number): void {
  const leaves = activeLeafIds();
  if (leaves.length === 0) return;
  const idx = focusedPaneId ? leaves.indexOf(focusedPaneId) : -1;
  const next = leaves[(idx + d + leaves.length) % leaves.length];
  focusPane(next);
}

function newPlaceholderId(): string {
  const rnd = (globalThis.crypto && "randomUUID" in globalThis.crypto) ? globalThis.crypto.randomUUID() : Math.random().toString(36).slice(2);
  return "empty-" + rnd;
}

async function newRoom(): Promise<void> {
  const placeholder = newPlaceholderId();
  const r = await invoke<{ roomId: string }>("app.layoutMutate", { op: "createRoom", newPaneId: placeholder, name: nextRoomName(), roomId: "", targetPaneId: "", orientation: "", paneId: "", dir: 0, paneType: "empty" });
  activeRoomId = r.roomId;
  focusedPaneId = null;
  await reload();
}

async function placePaneIntoRoom(roomId: string, placeholderId: string | null, paneId: string, paneType: string, roomName?: string): Promise<void> {
  if (placeholderId) {
    await invoke("app.layoutMutate", { op: "replace", roomId, targetPaneId: placeholderId, newPaneId: paneId, orientation: "", name: "", paneId: "", dir: 0, paneType });
    if (roomName) {
      try { await invoke("app.layoutMutate", { op: "rename", roomId, name: roomName, targetPaneId: "", newPaneId: "", orientation: "", paneId: "", dir: 0 }); } catch (err) { console.warn("room rename after place failed", err); }
    }
  } else {
    await invoke("app.layoutMutate", { op: "createRoom", newPaneId: paneId, name: roomName === "Room" || !roomName ? nextRoomName() : roomName, roomId: "", targetPaneId: "", orientation: "", paneId: "", dir: 0, paneType });
  }
  activeRoomId = roomId;
  await reload();
  focusPane(paneId);
}

async function launchTileInto(roomId: string | null, placeholderId: string | null, action: string): Promise<void> {
  const placeable = placeablePaneForAction(action);
  if (!placeable) { runAction(action); return; }
  let paneId: string;
  if (placeable.kind === "browser") {
    const bp = await invoke<{ paneId: string; currentUrl: string }>("cove://commands/browser.create", { url: "https://duckduckgo.com" });
    paneId = bp.paneId;
  } else {
    paneId = (await invoke<{ paneId: string }>("app.paneSpawn", { command: "", cwd: "", inheritCwdFrom: "", cols: 80, rows: 24, adapter: "", agentName: "", workspace: "", room: "" })).paneId;
  }
  if (roomId) {
    await placePaneIntoRoom(roomId, placeholderId, paneId, placeable.paneType, placeable.roomName);
  } else {
    const r = await invoke<{ roomId: string }>("app.layoutMutate", { op: "createRoom", newPaneId: paneId, name: placeable.roomName === "Room" ? nextRoomName() : placeable.roomName, roomId: "", targetPaneId: "", orientation: "", paneId: "", dir: 0, paneType: placeable.paneType });
    activeRoomId = r.roomId;
    await reload();
    focusPane(paneId);
  }
}

async function newBrowserRoom(url: string): Promise<void> {
  const bp = await invoke<{ paneId: string; currentUrl: string }>("cove://commands/browser.create", { url });
  const r = await invoke<{ roomId: string }>("app.layoutMutate", { op: "createRoom", newPaneId: bp.paneId, name: "Browser", roomId: "", targetPaneId: "", orientation: "", paneId: "", dir: 0, paneType: "browser" });
  activeRoomId = r.roomId;
  await reload();
  focusPane(bp.paneId);
}

async function closeRoom(roomId: string): Promise<void> {
  const room = layout?.rooms.find((r) => r.id === roomId);
  if (!room) return;
  const leaves = collectLeafIds(room.layoutTree);
  for (const id of leaves) {
    await closeBrowserWebview(id);
    try { await invoke("app.paneKill", { paneId: id }); } catch { void 0; }
  }
  try { await invoke("app.layoutMutate", { op: "closeRoom", roomId, paneId: "", targetPaneId: "", newPaneId: "", orientation: "", name: "", dir: 0 }); } catch { void 0; }
  if (activeRoomId === roomId) activeRoomId = null;
  await reload();
}

let sidebarModel: SidebarModel = initialSidebarModel();
const collapsedTreeRooms = new Set<string>(JSON.parse(localStorage.getItem("cove.tree.collapsedRooms") ?? "[]"));
let agentCards: AgentCard[] = [];
const needsInputPanes = new Set<string>();
let agentPollTimer: ReturnType<typeof setInterval> | null = null;
let workspaceBoxItems: WorkspaceBoxInput[] = [];

async function loadWorkspaceBoxes(): Promise<void> {
  try {
    const res = await invoke<{ workspaces: { id: string; name: string; projectDir?: string }[] }>("cove://commands/workspace.list", {});
    workspaceBoxItems = (res.workspaces ?? []).map((w) => ({ id: w.id, name: w.name, projectDir: w.projectDir }));
  } catch { workspaceBoxItems = []; }
  renderSidebarContent("left");
}

let draggingWorkspaceId: string | null = null;

async function reorderWorkspaces(fromId: string, toId: string): Promise<void> {
  const ids = workspaceBoxItems.map((w) => w.id);
  const fromIdx = ids.indexOf(fromId);
  const toIdx = ids.indexOf(toId);
  if (fromIdx < 0 || toIdx < 0) { console.warn("workspace reorder with unknown ids", fromId, toId); return; }
  ids.splice(toIdx, 0, ids.splice(fromIdx, 1)[0]);
  try { await invoke("cove://commands/workspace.reorder", { orderedIds: ids }); } catch (err) { console.warn("workspace reorder failed", err); }
  await loadWorkspaceBoxes();
}

let treeDragRoomId: string | null = null;

function startWorkspaceRename(wsId: string, boxEl: HTMLElement, currentName: string): void {
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
    const newName = nextWorkspaceName(input.value, currentName);
    if (save && newName !== currentName) {
      try { await invoke("cove://commands/workspace.rename", { id: wsId, name: newName }); }
      catch (e) { console.warn("workspace.rename failed", wsId, e); }
      await loadWorkspaceBoxes();
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

async function deleteWorkspace(wsId: string): Promise<void> {
  try {
    await invoke("cove://commands/workspace.delete", { id: wsId });
    await loadWorkspaceBoxes();
    await reload();
  } catch (e) { console.warn("workspace.delete failed", wsId, e); }
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
  renderSidebarContent("left");
  renderLeftRail();
  fitAll();
}

function renderLeftRail(): void {
  leftRailEl.innerHTML = "";
  const activeMode = sidebarModel.leftMode;
  for (const meta of SIDEBAR_MODES) {
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
  if (collapsedOf(sidebarModel, side)) { content.innerHTML = ""; return; }
  content.innerHTML = "";
  const mode = sidebarModel.leftMode;
  if (mode === "workspaces") renderWorkspacesContent(content);
  else if (mode === "notepad") renderNotepadContent(content);
  else renderStubContent(content, mode);
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

function roomLeaves(room: RoomSnapshot): TreeLeaf[] {
  const collect = (node: MosaicNode): TreeLeaf[] => {
    if (node.kind === "leaf") {
      const subs = node.subtabs.length > 0 ? node.subtabs : [{ documentId: node.paneId, paneType: "terminal", title: null }];
      return subs.map((s) => {
        const pv = panes.get(s.documentId);
        return { paneId: s.documentId, paneType: s.paneType, title: (pv && (pv.customTitle || pv.title)) || paneTitleCache.get(s.documentId) || s.title || "" };
      });
    }
    return [...collect(node.childA), ...collect(node.childB)];
  };
  return collect(room.layoutTree);
}

const fsExpandedDirs = new Set<string>(JSON.parse(localStorage.getItem("cove.files.expanded") ?? "[]"));
let filesExpandedWs = parseCollapsedCardIds(localStorage.getItem("cove.files.expandedWs"));
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
  void invoke<ScmSummary>("app.gitSummary", { path: dir })
    .then((r) => {
      const prev = scmSummaryCache.get(dir);
      scmSummaryCache.set(dir, r);
      scmSummaryFetchedAt.set(dir, Date.now());
      scmSummaryFetching.delete(dir);
      if (JSON.stringify(prev ?? null) !== JSON.stringify(r)) renderSidebarContent("left");
    })
    .catch((err) => {
      console.warn("git summary failed", dir, err);
      scmSummaryFetchedAt.set(dir, Date.now());
      scmSummaryFetching.delete(dir);
    });
}

function loadPaneTitleCache(): Map<string, string> {
  try {
    return new Map(Object.entries(JSON.parse(localStorage.getItem("cove.paneTitles") ?? "{}") as Record<string, string>));
  } catch (err) {
    console.warn("pane title cache unreadable, starting empty", err);
    return new Map();
  }
}
const paneTitleCache = loadPaneTitleCache();

function rememberPaneTitle(paneId: string, title: string): void {
  if (!title) return;
  if (paneTitleCache.get(paneId) === title) return;
  paneTitleCache.set(paneId, title);
  while (paneTitleCache.size > 300) paneTitleCache.delete(paneTitleCache.keys().next().value!);
  localStorage.setItem("cove.paneTitles", JSON.stringify(Object.fromEntries(paneTitleCache)));
}

const wsSnapshotCache = new Map<string, WorkspaceSnapshot>();
const wsSnapshotFetchedAt = new Map<string, number>();
const wsSnapshotFetching = new Set<string>();
const WS_SNAPSHOT_TTL_MS = 10000;

function requestWorkspaceSnapshot(wsId: string): void {
  if (!wsId || wsSnapshotFetching.has(wsId)) return;
  if (Date.now() - (wsSnapshotFetchedAt.get(wsId) ?? 0) < WS_SNAPSHOT_TTL_MS) return;
  wsSnapshotFetching.add(wsId);
  void invoke<WorkspaceSnapshot>("cove://commands/layout.get", { workspaceId: wsId })
    .then((snap) => {
      if (snap.id !== wsId) {
        console.warn("workspace snapshot id mismatch (daemon predates layout.get workspaceId)", wsId, snap.id);
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
      console.warn("workspace snapshot fetch failed", wsId, err);
      wsSnapshotFetchedAt.set(wsId, Date.now());
      wsSnapshotFetching.delete(wsId);
    });
}

function requestFsDir(path: string): void {
  if (fsDirCache.has(path) || fsDirLoading.has(path)) return;
  fsDirLoading.add(path);
  void invoke<{ entries: FsEntry[]; truncated: boolean; error: string | null }>("app.fsList", { path })
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

function renderFsLevel(host: HTMLElement, dir: string, depth: number): void {
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
  for (const entry of cached.entries) {
    const full = joinPath(dir, entry.name);
    const row = document.createElement("div");
    row.className = "fs-row" + (entry.isDir ? " fs-dir" : " fs-file");
    row.style.paddingLeft = `${10 + depth * 14}px`;
    const chev = document.createElement("span");
    chev.className = "tw-chevron" + (entry.isDir ? "" : " tw-spacer");
    if (entry.isDir) chev.textContent = fsExpandedDirs.has(full) ? "▾" : "▸";
    row.appendChild(chev);
    const ic = document.createElement("span");
    ic.className = "fs-ic";
    ic.innerHTML = iconSvg(entry.isDir ? "folder" : "file");
    row.appendChild(ic);
    const label = document.createElement("span");
    label.className = "tw-label";
    label.textContent = entry.name;
    row.appendChild(label);
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
    if (entry.isDir && fsExpandedDirs.has(full) && depth < 12) renderFsLevel(host, full, depth + 1);
  }
  if (cached.truncated) {
    const more = document.createElement("div");
    more.className = "fs-row fs-note";
    more.style.paddingLeft = `${10 + depth * 14}px`;
    more.textContent = "… more entries not shown";
    host.appendChild(more);
  }
}

function agentStateByPane(): Map<string, AgentState> {
  return new Map(buildAgentRows(agentCards, needsInputPanes).map((r) => [r.paneId, r.state]));
}

function adapterDisplayLabel(adapterName: string): string {
  return launcherAdapters.find((a) => a.name === adapterName)?.displayName ?? adapterName.replace(/-/g, " ");
}

function buildPaneCard(row: TreeRow, paneStates: Map<string, AgentState>): HTMLElement {
  const paneId = row.paneId ?? "";
  const cardEl = document.createElement("div");
  cardEl.className = "pane-card";
  cardEl.style.marginLeft = `${6 + (row.depth - 1) * 14}px`;
  const titleRow = document.createElement("div");
  titleRow.className = "pane-card-title";
  const glyph = document.createElement("span");
  glyph.className = "pc-ic";
  glyph.innerHTML = iconSvg(iconForPaneType(row.paneType ?? "terminal"));
  titleRow.appendChild(glyph);
  const titleText = document.createElement("span");
  titleText.className = "pc-title-text";
  titleText.textContent = row.label;
  titleRow.appendChild(titleText);
  cardEl.appendChild(titleRow);

  const metaRow = document.createElement("div");
  metaRow.className = "pane-card-meta";
  const agent = agentCards.find((c) => c.paneId === paneId);
  const st = paneStates.get(paneId);
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
    metaText.textContent = PANE_TYPE_LABELS[row.paneType ?? ""] ?? row.paneType ?? "pane";
    metaRow.appendChild(metaText);
  }
  cardEl.appendChild(metaRow);

  cardEl.addEventListener("click", () => { if (paneId) revealPane(paneId); });
  cardEl.addEventListener("contextmenu", (e) => {
    openContextMenuAt(e, [
      { id: "focus", label: "Go to" },
      { id: "copy-id", label: "Copy pane id" },
      { id: "close", label: "Close", danger: true },
    ], (id) => {
      if (id === "focus") focusTreeRow("pane", row.roomId, paneId);
      else if (id === "copy-id") { if (navigator.clipboard) void navigator.clipboard.writeText(paneId); }
      else if (id === "close") closeTreeRow("pane", row.roomId, paneId);
    });
  });
  return cardEl;
}

function renderWorkspacesContent(container: HTMLElement): void {
  container.appendChild(sidebarHead("Workspace", [{ icon: "+", title: "New workspace", run: () => void newWorkspace() }]));
  const emptyMessage = workspaceTreeEmptyMessage(workspaceBoxItems.length);
  if (emptyMessage) {
    const list = document.createElement("div");
    list.className = "sb-list";
    list.appendChild(buildEmptyState({ message: emptyMessage }));
    container.appendChild(list);
    return;
  }
  const entries = workspaceBoxItems.map((w) => ({ id: w.id, name: w.name, projectDir: w.projectDir ?? "" }));
  const activeId = resolveActiveWorkspaceId(entries, layout?.id ?? null);
  const scroll = document.createElement("div");
  scroll.className = "sb-list ws-card-scroll";
  for (const w of entries) scroll.appendChild(renderWorkspaceCard(w, w.id === activeId));
  container.appendChild(scroll);
}

function wireWorkspaceCardDrag(el: HTMLElement, wid: string): void {
  el.draggable = true;
  el.addEventListener("dragstart", (e) => {
    draggingWorkspaceId = wid;
    if (e.dataTransfer) e.dataTransfer.effectAllowed = "move";
  });
  el.addEventListener("dragend", () => { draggingWorkspaceId = null; });
  el.addEventListener("dragover", (e) => {
    if (!draggingWorkspaceId || draggingWorkspaceId === wid) return;
    e.preventDefault();
    el.classList.add("drag-over");
  });
  el.addEventListener("dragleave", () => el.classList.remove("drag-over"));
  el.addEventListener("drop", (e) => {
    e.preventDefault();
    el.classList.remove("drag-over");
    if (!draggingWorkspaceId || draggingWorkspaceId === wid) return;
    void reorderWorkspaces(draggingWorkspaceId, wid);
    draggingWorkspaceId = null;
  });
}

function workspaceCardHead(ws: WorkspaceCardEntry, mini: boolean): HTMLElement {
  const head = document.createElement("div");
  head.className = "ws-card-head";
  const swatch = document.createElement("span");
  swatch.className = "ws-card-swatch";
  head.appendChild(swatch);
  const titles = document.createElement("div");
  titles.className = "ws-card-titles";
  const name = document.createElement("span");
  name.className = "ws-card-name";
  name.textContent = ws.name;
  titles.appendChild(name);
  const dir = document.createElement("span");
  dir.className = "ws-card-dir";
  dir.textContent = ws.projectDir || "no directory";
  dir.title = ws.projectDir;
  titles.appendChild(dir);
  head.appendChild(titles);
  head.addEventListener("contextmenu", (e) => {
    openContextMenuAt(e, [
      { id: "new-room", label: "New room", disabled: mini },
      { id: "rename", label: "Rename" },
      { id: "sep", label: "", separator: true },
      { id: "close-ws", label: "Close workspace", danger: true },
    ], (id) => {
      if (id === "new-room") void newRoom();
      else if (id === "rename") startWorkspaceRename(ws.id, name, ws.name);
      else if (id === "close-ws") void deleteWorkspace(ws.id);
    });
  });
  return head;
}

function renderWorkspaceCard(ws: WorkspaceCardEntry, isActive: boolean): HTMLElement {
  const card = document.createElement("div");
  card.className = "ws-card" + (isActive ? " ws-card-active" : "");
  card.style.setProperty("--ws-accent", workspaceAccent(ws.id));
  const head = workspaceCardHead(ws, !isActive);
  if (ws.projectDir) {
    requestScmSummary(ws.projectDir);
    const summary = scmSummaryCache.get(ws.projectDir);
    const chipText = summary ? scmChipText(summary) : "";
    if (chipText) {
      const chip = document.createElement("span");
      chip.className = "ws-scm-chip";
      for (const part of chipText.split(" ")) {
        const seg = document.createElement("span");
        seg.textContent = part;
        if (part.startsWith("↑")) seg.className = "scm-ahead";
        else if (part.startsWith("↓")) seg.className = "scm-behind";
        else if (part.startsWith("●")) seg.className = "scm-dirty";
        else seg.className = "scm-branch";
        chip.appendChild(seg);
      }
      chip.title = `${ws.projectDir} — ahead/behind upstream · modified files`;
      head.appendChild(chip);
    }
  }
  if (!isActive) head.addEventListener("click", () => void switchWorkspace(ws.id));
  card.appendChild(head);
  wireWorkspaceCardDrag(card, ws.id);

  const body = document.createElement("div");
  body.className = "ws-card-body";
  const roomsHost = document.createElement("div");
  if (!isActive) {
    requestWorkspaceSnapshot(ws.id);
    roomsHost.addEventListener("click", (e) => {
      e.stopPropagation();
      void switchWorkspace(ws.id);
    }, true);
  }
  body.appendChild(roomsHost);
  const sourceRooms = isActive ? (layout?.rooms ?? []) : (wsSnapshotCache.get(ws.id)?.rooms ?? []);
  const rooms: TreeRoomInput[] = sourceRooms.map((r) => ({ id: r.id, name: roomTabName(r), leaves: roomLeaves(r) }));
  const rows = buildWorkspaceTree({
    workspaceName: ws.name,
    activeRoomId,
    focusedPaneId,
    rooms,
    collapsedRoomIds: collapsedTreeRooms,
    workspaceCollapsed: false,
    workspaces: [{ id: ws.id, name: ws.name }],
    activeWorkspaceId: ws.id,
  }).filter((r) => r.kind !== "workspace");
  const paneStates = agentStateByPane();
  for (const row of rows) {
    if (row.kind === "pane" && row.paneId) {
      roomsHost.appendChild(buildPaneCard(row, paneStates));
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
        if (row.roomId) {
          if (collapsedTreeRooms.has(row.roomId)) collapsedTreeRooms.delete(row.roomId);
          else collapsedTreeRooms.add(row.roomId);
          localStorage.setItem("cove.tree.collapsedRooms", JSON.stringify([...collapsedTreeRooms]));
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
    if (row.count > 1 && row.kind !== "pane") {
      const count = document.createElement("span");
      count.className = "tw-count";
      count.textContent = String(row.count);
      rowEl.appendChild(count);
    }
    rowEl.addEventListener("click", () => onTreeRowClick(row.kind, row.roomId, row.paneId, row.expandable));
    if (row.kind === "room" && row.roomId) {
      const rid = row.roomId;
      rowEl.draggable = true;
      rowEl.addEventListener("dragstart", (e) => {
        treeDragRoomId = rid;
        if (e.dataTransfer) e.dataTransfer.effectAllowed = "move";
      });
      rowEl.addEventListener("dragend", () => { treeDragRoomId = null; });
      rowEl.addEventListener("dragover", (e) => {
        if (!treeDragRoomId || treeDragRoomId === rid) return;
        e.preventDefault();
        rowEl.classList.add("drag-over");
      });
      rowEl.addEventListener("dragleave", () => rowEl.classList.remove("drag-over"));
      rowEl.addEventListener("drop", (e) => {
        e.preventDefault();
        rowEl.classList.remove("drag-over");
        if (!treeDragRoomId || treeDragRoomId === rid) return;
        void reorderRooms(treeDragRoomId, rid);
        treeDragRoomId = null;
      });
    }
    rowEl.addEventListener("contextmenu", (e) => {
      openContextMenuAt(e, [
        { id: "focus", label: "Go to" },
        { id: "close", label: "Close", danger: true },
      ], (id) => {
        if (id === "focus") focusTreeRow(row.kind, row.roomId, row.paneId);
        else if (id === "close") closeTreeRow(row.kind, row.roomId, row.paneId);
      });
    });
    roomsHost.appendChild(rowEl);
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
    if (ws.projectDir) renderFsLevel(filesHost, ws.projectDir, 0);
    else {
      const none = document.createElement("div");
      none.className = "fs-row fs-note";
      none.textContent = "no workspace directory";
      filesHost.appendChild(none);
    }
    body.appendChild(filesHost);
  }
  card.appendChild(body);
  return card;
}

function onTreeRowClick(kind: string, roomId: string | null, paneId: string | null, expandable: boolean): void {
  if (kind === "workspace") {
    console.warn("tree click: workspace rows are not rendered in card mode");
    return;
  }
  if (kind === "pane" && paneId) { revealPane(paneId); return; }
  if (kind === "room" && roomId) {
    const room = layout?.rooms.find((r) => r.id === roomId);
    if (!room) { console.warn("tree click: unknown room", roomId); return; }
    activeRoomId = roomId;
    const f = firstLeafOf(room);
    if (f) focusedPaneId = f;
    renderRoom();
    renderRoomTabs();
    renderSidebar();
    if (f) focusPane(f);
  }
}

function focusTreeRow(kind: string, roomId: string | null, paneId: string | null): void {
  if (kind === "pane" && paneId) { revealPane(paneId); return; }
  if (kind === "room" && roomId) {
    const room = layout?.rooms.find((r) => r.id === roomId);
    if (!room) { console.warn("tree focus: unknown room", roomId); return; }
    activeRoomId = roomId;
    const f = firstLeafOf(room);
    if (f) focusedPaneId = f;
    renderRoom();
    renderRoomTabs();
    renderSidebar();
    if (f) focusPane(f);
  }
}

function closeTreeRow(kind: string, roomId: string | null, paneId: string | null): void {
  if (kind === "pane" && paneId) { focusPane(paneId); void closeFocused(); return; }
  if (kind === "room" && roomId) { void closeRoom(roomId); }
}

async function refreshAgents(): Promise<void> {
  try {
    const res = await invoke<{ cards: AgentCard[] }>("cove://commands/activity.list", {});
    agentCards = res.cards ?? [];
  } catch { agentCards = []; }
  if (agentsVisible()) renderSidebarContent("left");
}

function agentsVisible(): boolean {
  return !collapsedOf(sidebarModel, "left") && sidebarModel.leftMode === "workspaces";
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
  for (const [k, v] of entries) invoke("app.configSet", { key: k, value: v }).catch((e) => console.warn("sidebar configSet failed", k, e));
}

async function loadSidebarModel(): Promise<void> {
  const get = async (k: string): Promise<string | null> => {
    try { const r = await invoke<{ ok: boolean; value?: string }>("app.configGet", { key: k }); return r.ok ? r.value ?? null : null; } catch { return null; }
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
  handle.addEventListener("mousedown", (e) => {
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
      fitAll();
    };
    const onUp = () => {
      document.removeEventListener("mousemove", onMove);
      document.removeEventListener("mouseup", onUp);
      handle.classList.remove("dragging");
      persistSidebarModel();
    };
    document.addEventListener("mousemove", onMove);
    document.addEventListener("mouseup", onUp);
  });
}

function startAgentPolling(): void {
  void refreshAgents();
  if (agentPollTimer === null) agentPollTimer = setInterval(() => { if (agentsVisible()) void refreshAgents(); }, 3000);
}

const pinnedRoomIds = new Set<string>(JSON.parse(localStorage.getItem("cove.pinnedRooms") ?? "[]"));
function savePinnedRooms(): void { localStorage.setItem("cove.pinnedRooms", JSON.stringify([...pinnedRoomIds])); }

interface WingInfo { id: string; name: string; }
let wings: WingInfo[] = [];
let activeWingId: string | null = "main";
let wingSwitcherExpanded = false;
let roomWingSummaries: { id: string; wingId: string; pinned: boolean }[] = [];
async function loadWings(): Promise<void> {
  const wsId = layout?.id ?? "default";
  try {
    const res = await invoke<{ wings: { id: string; name: string }[] }>("cove://commands/wing.list", { workspaceId: wsId });
    wings = res.wings ?? [{ id: "main", name: "main" }];
  } catch { wings = [{ id: "main", name: "main" }]; }
  try {
    const list = await invoke<{ rooms: { id: string; wingId: string; pinned: boolean }[] }>("cove://commands/room.list", { workspaceId: wsId });
    roomWingSummaries = list.rooms ?? [];
  } catch { roomWingSummaries = []; }
}
async function switchWingActive(wingId: string): Promise<void> {
  activeWingId = wingId;
  try { await invoke("cove://commands/wing.switch", { workspaceId: "default", wingId }); } catch { void 0; }
  await loadWings();
  await reload();
  renderRoomTabs();
}

function roomTabName(room: RoomSnapshot): string {
  if (room.name.trim().length > 0) return room.name;
  const leaves = collectLeafIds(room.layoutTree);
  const first = leaves[0] ? panes.get(leaves[0]) : undefined;
  return (first && first.title) || "Room";
}

function nextRoomName(): string {
  return "Room " + ((layout?.rooms.length ?? 0) + 1);
}

function renderRoomTabs(): void {
  const activeRename = roomTabsEl.querySelector(".rtab-rename-input");
  if (activeRename && activeRename === document.activeElement) return;
  roomTabsEl.innerHTML = "";
  const allRooms = layout?.rooms ?? [];
  const wingModel = buildWingModel(wings, roomWingSummaries, activeWingId);
  const visibleIds = visibleRoomIds(wingModel);
  const rooms = visibleIds.length > 0 || wings.length > 1 ? filterRoomsByWing(allRooms, visibleIds) : allRooms;
  if (rooms.length === 0) { roomTabsEl.style.display = "none"; return; }
  roomTabsEl.style.display = "flex";

  const { pinned, unpinned } = partitionPinned(rooms.map((r) => ({ id: r.id, name: r.name, pinned: pinnedRoomIds.has(r.id) })));
  const roomMap = new Map(rooms.map((r) => [r.id, r]));

  let dragSrcId: string | null = null;

  const makeTab = (roomId: string): HTMLElement => {
    const room = roomMap.get(roomId);
    if (!room) return document.createElement("div");
    const isPinned = pinnedRoomIds.has(roomId);
    const tab = document.createElement("div");
    tab.className = "rtab" + (roomId === activeRoomId ? " active" : "") + (isPinned ? " pinned" : "");
    tab.draggable = true;
    tab.title = roomTabName(room);

    const glyph = document.createElement("span");
    glyph.className = "rtab-glyph";
    glyph.innerHTML = iconForPaneType(roomLeaves(room)[0]?.paneType ?? "terminal");
    tab.appendChild(glyph);

    const nameEl = document.createElement("span");
    nameEl.className = "rtab-name";
    nameEl.textContent = roomTabName(room);
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
        void closeRoom(roomId);
        return;
      }
      if (roomId === activeRoomId) {
        clickCount++;
        if (clickCount >= 2) {
          startRename(roomId, tab, nameEl);
          clickCount = 0;
        } else {
          setTimeout(() => { clickCount = 0; }, 400);
        }
      } else {
        activeRoomId = roomId;
        const f = firstLeafOf(room);
        if (f) focusedPaneId = f;
        renderRoom();
        renderRoomTabs();
        renderSidebar();
        if (f) focusPane(f);
      }
    });
    tab.addEventListener("contextmenu", (e) => {
      const pinned = pinnedRoomIds.has(roomId);
      openContextMenuAt(e, [
        { id: "rename", label: "Rename" },
        { id: "pin", label: pinned ? "Unpin" : "Pin" },
        { id: "sep", label: "", separator: true },
        { id: "close", label: "Close", danger: true, disabled: pinned },
        { id: "close-others", label: "Close Others" },
      ], (id) => {
        if (id === "rename") startRename(roomId, tab, tab.querySelector(".rtab-name") as HTMLElement);
        else if (id === "pin") { if (pinned) pinnedRoomIds.delete(roomId); else pinnedRoomIds.add(roomId); savePinnedRooms(); renderRoomTabs(); }
        else if (id === "close") void closeRoom(roomId);
        else if (id === "close-others") void closeOtherRooms(roomId);
      });
    });
    tab.addEventListener("dragstart", () => { dragSrcId = roomId; tab.classList.add("dragging"); });
    tab.addEventListener("dragend", () => { tab.classList.remove("dragging"); dragSrcId = null; });
    tab.addEventListener("dragover", (e) => {
      e.preventDefault();
      if (draggingPaneId) {
        tab.classList.add("pane-drop-target");
        if (roomId !== activeRoomId && tabSpringRoomId !== roomId) {
          if (tabSpringTimer !== null) window.clearTimeout(tabSpringTimer);
          tabSpringRoomId = roomId;
          tabSpringTimer = window.setTimeout(() => {
            tabSpringTimer = null;
            tabSpringRoomId = null;
            if (!draggingPaneId) return;
            activeRoomId = roomId;
            renderRoom();
            renderRoomTabs();
            renderSidebar();
          }, 550);
        }
        return;
      }
      tab.classList.add("drag-over");
    });
    tab.addEventListener("dragleave", () => {
      tab.classList.remove("drag-over");
      tab.classList.remove("pane-drop-target");
      if (tabSpringRoomId === roomId && tabSpringTimer !== null) {
        window.clearTimeout(tabSpringTimer);
        tabSpringTimer = null;
        tabSpringRoomId = null;
      }
    });
    tab.addEventListener("drop", (e) => {
      e.preventDefault();
      tab.classList.remove("drag-over");
      tab.classList.remove("pane-drop-target");
      if (tabSpringTimer !== null) { window.clearTimeout(tabSpringTimer); tabSpringTimer = null; tabSpringRoomId = null; }
      const paneSrc = e.dataTransfer?.getData("text/cove-pane") || draggingPaneId;
      if (paneSrc) {
        draggingPaneId = null;
        clearDropOverlay();
        void movePaneToRoom(paneSrc, roomId);
        return;
      }
      if (dragSrcId && dragSrcId !== roomId) {
        void reorderRooms(dragSrcId, roomId);
      }
    });
    return tab;
  };

  const homeBtn = document.createElement("div");
  homeBtn.className = "rbox-ctl rbox-home";
  homeBtn.innerHTML = iconSvg("home");
  homeBtn.title = "Workspace overview";
  homeBtn.addEventListener("click", () => revealSidebarMode("workspaces"));
  roomTabsEl.appendChild(homeBtn);

  for (const id of pinned) roomTabsEl.appendChild(makeTab(id));
  if (pinned.length > 0 && unpinned.length > 0) {
    const divider = document.createElement("div");
    divider.className = "rtab-divider";
    roomTabsEl.appendChild(divider);
  }
  for (const id of unpinned) roomTabsEl.appendChild(makeTab(id));

  if (wings.length > 1 || wingSwitcherExpanded) {
    const switcher = document.createElement("div");
    switcher.id = "wing-switcher";
    if (!wingSwitcherExpanded) {
      const toggle = document.createElement("div");
      toggle.className = "wing-btn";
      toggle.textContent = "\u27e8";
      toggle.title = "Wings";
      toggle.addEventListener("click", () => { wingSwitcherExpanded = true; renderRoomTabs(); });
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
      collapse.addEventListener("click", () => { wingSwitcherExpanded = false; renderRoomTabs(); });
      switcher.appendChild(collapse);
    }
    roomTabsEl.appendChild(switcher);
  }

  const addBtn = document.createElement("div");
  addBtn.className = "rbox-ctl rbox-add";
  addBtn.style.cssText = "margin-left:auto;";
  addBtn.innerHTML = iconSvg("plus");
  addBtn.title = "New room (Cmd T)";
  addBtn.addEventListener("click", () => void newRoom());
  roomTabsEl.appendChild(addBtn);

  updateEdgeFade();
}

function updateEdgeFade(): void {
  roomTabsEl.classList.remove("edge-fade-left", "edge-fade-right");
  if (roomTabsEl.scrollWidth > roomTabsEl.clientWidth) {
    if (roomTabsEl.scrollLeft > 2) roomTabsEl.classList.add("edge-fade-left");
    if (roomTabsEl.scrollLeft + roomTabsEl.clientWidth < roomTabsEl.scrollWidth - 2) roomTabsEl.classList.add("edge-fade-right");
  }
}
roomTabsEl.addEventListener("scroll", updateEdgeFade);

async function reorderRooms(fromId: string, toId: string): Promise<void> {
  if (!layout) return;
  const ids = layout.rooms.map((r) => r.id);
  const fromIdx = ids.indexOf(fromId);
  const toIdx = ids.indexOf(toId);
  if (fromIdx < 0 || toIdx < 0) return;
  const reordered = reorderRoom(layout.rooms, fromIdx, toIdx);
  layout.rooms = reordered;
  renderRoomTabs();
  renderSidebarContent("left");
  try {
    const newOrder = reordered.map((r) => r.id);
    await invoke("app.layoutMutate", { op: "reorder", roomIds: newOrder, roomId: "", targetPaneId: "", newPaneId: "", orientation: "", name: "", paneId: "", dir: 0 });
  } catch (err) { console.warn("room reorder failed", err); }
  await reload();
}

function startRename(roomId: string, tab: HTMLElement, nameEl: HTMLElement): void {
  const room = layout?.rooms.find((r) => r.id === roomId);
  if (!room) return;
  const input = document.createElement("input");
  input.className = "rtab-rename-input";
  input.value = roomTabName(room);
  input.spellcheck = false;
  nameEl.replaceWith(input);
  input.focus();
  input.select();
  const commit = async () => {
    const newName = input.value.trim() || room.name;
    if (newName !== room.name) {
      room.name = newName;
      try {
        await invoke("app.layoutMutate", { op: "rename", roomId, name: newName, paneId: "", targetPaneId: "", newPaneId: "", orientation: "", dir: 0 });
        await reload();
        return;
      } catch (err) { console.warn("room rename failed", roomId, err); }
    }
    renderRoomTabs();
    renderSidebar();
  };
  input.addEventListener("blur", commit);
  input.addEventListener("keydown", (e) => {
    e.stopPropagation();
    if (e.key === "Enter") input.blur();
    if (e.key === "Escape") { input.value = room.name; input.blur(); }
  });
}

async function closeOtherRooms(keepRoomId: string): Promise<void> {
  if (!layout) return;
  const toClose = layout.rooms.filter((r) => r.id !== keepRoomId);
  for (const room of toClose) {
    await closeRoom(room.id);
  }
}

interface Action { label: string; icon: string; key?: string; run: () => void; }

function baseActions(): Action[] {
  return [
    { label: "New terminal", icon: "+", key: "Cmd T", run: () => void newRoom() },
    { label: "New browser", icon: "\uD83C\uDF10", run: () => void newBrowserRoom("https://duckduckgo.com") },
    { label: "Split right", icon: "\u2502", key: "Cmd D", run: () => void splitActive("row") },
    { label: "Split down", icon: "\u2500", key: "Cmd Shift D", run: () => void splitActive("col") },
    { label: "Close pane", icon: "\u00d7", key: "Cmd W", run: () => void closeFocused() },
    { label: "Toggle left sidebar", icon: "\u25e7", key: "Cmd B", run: toggleLeftSidebar },
    { label: "Show notepad", icon: "\u270e", run: () => revealSidebarMode("notepad") },
    { label: "Show workspaces", icon: "\u25c9", key: "Cmd Shift A", run: () => revealSidebarMode("workspaces") },
    { label: "Toggle window backdrop", icon: "\u25d0", run: () => void toggleBackdrop() },
    { label: "Toggle performance HUD", icon: "\ud83d\udcc8", run: doTogglePerfHud },
    { label: "Increase font size", icon: "+", key: "Cmd =", run: () => { settings.fontSize = Math.min(24, settings.fontSize + 1); applySettings(); } },
    { label: "Decrease font size", icon: "-", key: "Cmd -", run: () => { settings.fontSize = Math.max(9, settings.fontSize - 1); applySettings(); } },
    { label: "Reset font size", icon: "\u21ba", key: "Cmd 0", run: () => { settings.fontSize = 13; applySettings(); } },
    { label: "Settings", icon: "\u2699", key: "Cmd ,", run: openSettings },
    { label: "Inspect UI (report a bug)", icon: "\u2316", run: startInspectMode },
  ];
}

function jumpActions(): Action[] {
  return (layout?.rooms ?? []).map((r, i) => ({
    label: `Go to ${r.name}`,
    icon: "\u203a",
    key: i < 9 ? `Cmd ${i + 1}` : undefined,
    run: () => {
      activeRoomId = r.id;
      const f = firstLeafOf(r);
      if (f) { focusedPaneId = f; renderRoom(); renderSidebar(); focusPane(f); }
    },
  }));
}

let palSel = 0;
let palActions: PaletteItem[] = [];
const palMru = new MruTracker(JSON.parse(localStorage.getItem("cove.palette.mru") ?? "[]"));
let palCachedItems: PaletteItem[] | null = null;
const paneFilePaths = new Map<string, string>();
let palFileSearchTimer: ReturnType<typeof setTimeout> | null = null;
let palFileResults: PaletteItem[] = [];
let palFileQuery = "";
let palFileSearchTag = 0;

function openPalette() {
  paletteEl.classList.add("open");
  palInput.value = "";
  palSel = 0;
  palCachedItems = null;
  palFileResults = [];
  palFileQuery = "";
  palFileSearchTag++;
  void loadPaletteCache();
  renderPalette();
  palInput.focus();
}

async function loadPaletteCache(): Promise<void> {
  palCachedItems = await paletteItems();
  renderPalette();
}

function closePalette() {
  paletteEl.classList.remove("open");
  if (focusedPaneId) {
    const pv = panes.get(focusedPaneId);
    if (pv) pv.term.focus();
  }
}

async function paletteItems(): Promise<PaletteItem[]> {
  const items: PaletteItem[] = [];
  for (const a of baseActions()) {
    items.push({ id: `cmd:${a.label}`, label: a.label, category: "commands", icon: a.icon, key: a.key, run: () => { a.run(); } });
  }
  for (const a of jumpActions()) {
    items.push({ id: `room:${a.label}`, label: a.label, category: "rooms", icon: a.icon, key: a.key, run: () => { a.run(); } });
  }
  for (const [id, pv] of panes) {
    items.push({ id: `pane:${id}`, label: pv.title || id, category: "panes", icon: "\u25a0", run: () => focusPane(id) });
  }
  try {
    const wsResult = await invoke<{ workspaces: { id: string; name: string }[] }>("cove://commands/workspace.list", {});
    for (const ws of wsResult.workspaces ?? []) {
      items.push({ id: `ws:${ws.id}`, label: ws.name, category: "workspaces", icon: "\u25c8", run: () => void switchWorkspace(ws.id) });
    }
  } catch { void 0; }
  try {
    const taskResult = await invoke<{ cards: { id: string; title: string; humanId: string }[] }>("cove://commands/task.list", { workspaceId: "default" });
    for (const t of taskResult.cards ?? []) {
      items.push({ id: `task:${t.id}`, label: `${t.humanId}: ${t.title}`, category: "tasks", icon: "#", run: () => void openTaskInPane(t.id) });
    }
  } catch { void 0; }
  return items;
}

function renderPalette() {
  const parsed = parseQuery(palInput.value);
  const all = palCachedItems ?? [];
  palActions = filterAndSort(all, parsed);
  if (parsed.category === "files" && parsed.text.length > 0 && parsed.text !== palFileQuery) {
    if (palFileSearchTimer) clearTimeout(palFileSearchTimer);
    palFileSearchTimer = setTimeout(() => void searchFiles(parsed.text), 200);
  }
  if (parsed.category === "files") {
    palActions = [...palActions, ...palFileResults.filter((f) => !palActions.some((e) => e.id === f.id))];
  }
  if (parsed.text.length === 0 && parsed.category === "all") {
    const mruIds = palMru.toList().map((e) => e.id).reverse();
    const mruItems = mruIds.map((id) => palActions.find((i) => i.id === id)).filter((x): x is PaletteItem => x !== undefined);
    const rest = palActions.filter((i) => !mruIds.includes(i.id));
    palActions = [...mruItems, ...rest];
  }
  if (palSel >= palActions.length) palSel = Math.max(0, palActions.length - 1);
  palList.innerHTML = "";

  if (parsed.category !== "all" || parsed.text.length > 0) {
    const catBar = document.createElement("div");
    catBar.className = "pal-cat-bar";
    catBar.style.cssText = "display:flex;gap:4px;padding:4px 8px;border-bottom:1px solid var(--border);font-size:11px;color:var(--muted);";
    catBar.textContent = parsed.category === "all" ? `Results for "${parsed.text}"` : `${categoryLabel(parsed.category)}: "${parsed.text}"`;
    palList.appendChild(catBar);
  }

  if (palActions.length === 0) {
    const empty = document.createElement("div");
    empty.className = "pal-empty";
    empty.style.cssText = "padding:16px;text-align:center;color:var(--muted);font-size:12px;";
    empty.textContent = palCachedItems === null ? "Loading..." : "No results";
    palList.appendChild(empty);
    return;
  }

  palActions.forEach((a, i) => {
    const el = document.createElement("div");
    el.className = "pal-item" + (i === palSel ? " sel" : "");
    el.innerHTML = `<span class="ic"></span><span class="lbl"></span>${a.key ? `<span class="key">${a.key}</span>` : ""}`;
    (el.querySelector(".ic") as HTMLElement).textContent = a.icon;
    (el.querySelector(".lbl") as HTMLElement).textContent = a.label;
    el.addEventListener("click", (e) => {
      const split = e.metaKey || e.ctrlKey;
      closePalette();
      palMru.record(a.id);
      localStorage.setItem("cove.palette.mru", JSON.stringify(palMru.toList()));
      a.run();
      if (split) void splitActive("row");
    });
    palList.appendChild(el);
  });
}

async function searchFiles(query: string): Promise<void> {
  const tag = ++palFileSearchTag;
  palFileQuery = query;
  try {
    const result = await invoke<{ matches: { file: string; line: number; text: string }[] }>("cove://commands/search.query", { query, workspaceId: "default" });
    if (tag !== palFileSearchTag) return;
    const seen = new Set<string>();
    palFileResults = (result.matches ?? []).filter((m) => {
      if (seen.has(m.file)) return false;
      seen.add(m.file);
      return true;
    }).slice(0, 20).map((m) => ({
      id: `file:${m.file}`,
      label: m.file,
      category: "files" as const,
      icon: "/",
      run: () => void openFileInEditor(m.file),
    }));
    renderPalette();
  } catch {
    if (tag === palFileSearchTag) palFileResults = [];
  }
}

async function switchWorkspace(wsId: string): Promise<void> {
  try {
    await invoke("cove://commands/workspace.switch", { id: wsId });
    activeRoomId = null;
    focusedPaneId = null;
    await reload();
    await loadWings();
    renderRoomTabs();
  } catch { void 0; }
}

async function openTaskInPane(taskId: string): Promise<void> {
  try {
    const sp = (await invoke<{ paneId: string }>("app.paneSpawn", { command: "", cwd: "", inheritCwdFrom: "", cols: 80, rows: 24, adapter: "", agentName: "", workspace: "", room: "" })).paneId;
    const r = await invoke<{ roomId: string }>("app.layoutMutate", { op: "createRoom", newPaneId: sp, name: "Task", roomId: "", targetPaneId: "", orientation: "", paneId: "", dir: 0, paneType: "tasks-kanban" });
    activeRoomId = r.roomId;
    paneFilePaths.set(sp, taskId);
    await reload();
    focusPane(sp);
  } catch { void 0; }
}

async function openFileInEditor(filePath: string): Promise<void> {
  try {
    const sp = (await invoke<{ paneId: string }>("app.paneSpawn", { command: "", cwd: "", inheritCwdFrom: "", cols: 80, rows: 24, adapter: "", agentName: "", workspace: "", room: "" })).paneId;
    const r = await invoke<{ roomId: string }>("app.layoutMutate", { op: "createRoom", newPaneId: sp, name: filePath.split("/").pop() || "Editor", roomId: "", targetPaneId: "", orientation: "", paneId: "", dir: 0, paneType: "editor" });
    activeRoomId = r.roomId;
    paneFilePaths.set(sp, filePath);
    await reload();
    focusPane(sp);
  } catch { void 0; }
}


wireSidebarResize(leftResizeEl, "left");
document.body.classList.add(navigator.platform.toUpperCase().includes("MAC") ? "platform-mac" : "platform-other");
window.__ryn.on("window.focused", () => document.body.classList.remove("window-inactive"));
window.__ryn.on("window.blurred", () => document.body.classList.add("window-inactive"));
void invoke<{ version?: string }>("cove://sys/daemon.status", {}).then((s) => {
  if (s?.version) document.getElementById("wordmark-ver")!.textContent = "v" + s.version;
}).catch(() => void 0);

void invoke<{ theme: ThemeDto | null }>("cove://commands/theme.get-active", {}).then((r) => {
  if (r.theme) { themeActiveName = r.theme.name; themeDraft = draftFromTheme(r.theme); applyThemeVars(r.theme); }
}).catch((err) => console.warn("active theme load failed, using built-in defaults", err));

const wordmarkImg = document.getElementById("wordmark-img") as HTMLImageElement;
applyBrandLogo();
wordmarkImg.addEventListener("click", () => {
  brandIndex = nextBrandIndex(brandIndex);
  localStorage.setItem("cove.brandLogo", String(nextBrandIndex(brandIndex)));
  applyBrandLogo();
});

palInput.addEventListener("input", () => { palSel = 0; renderPalette(); });
palInput.addEventListener("keydown", (e) => {
  if (e.key === "Escape") { e.preventDefault(); closePalette(); }
  else if (e.key === "Enter") {
    e.preventDefault();
    const a = palActions[palSel];
    const split = e.metaKey || e.ctrlKey;
    if (a) { palMru.record(a.id); localStorage.setItem("cove.palette.mru", JSON.stringify(palMru.toList())); }
    closePalette();
    if (a) a.run();
    if (split && a) void splitActive("row");
  }
  else if (e.key === "ArrowDown") { e.preventDefault(); palSel = Math.min(palActions.length - 1, palSel + 1); renderPalette(); }
  else if (e.key === "ArrowUp") { e.preventDefault(); palSel = Math.max(0, palSel - 1); renderPalette(); }
});
paletteEl.addEventListener("mousedown", (e) => { if (e.target === paletteEl) closePalette(); });

const settingsEl = document.getElementById("settings")!;
const setTabsEl = document.getElementById("set-tabs")!;
const setBodyEl = document.getElementById("set-body")!;

interface ConfigSchemaEntry { key: string; label: string; tab: string; control: string; description: string | null; type: string; options: string[] | null; }
let configSchema: ConfigSchemaEntry[] = [];
let activeSettingsTab: string | null = null;

async function loadConfigSchema(): Promise<void> {
  try {
    const res = await invoke<{ entries: ConfigSchemaEntry[] }>("cove://commands/config.schema", {});
    configSchema = res.entries ?? [];
  } catch {
    configSchema = [];
  }
}

function openSettings(): void {
  if (configSchema.length === 0) {
    void loadConfigSchema().then(() => renderSettings());
  } else {
    renderSettings();
  }
  settingsEl.classList.add("open");
}

function closeSettings(): void {
  settingsEl.classList.remove("open");
  if (focusedPaneId) panes.get(focusedPaneId)?.term.focus();
}
function isRealSetting(e: ConfigSchemaEntry): boolean {
  return e.control !== "section" && e.type !== "object";
}

function renderSettings(): void {
  const schemaTabs = [...new Set(configSchema.filter(isRealSetting).map((e) => e.tab))].sort();
  const tabs = schemaTabs.includes("theme") ? (schemaTabs.includes("keyboard") ? schemaTabs : ["theme", "keyboard", ...schemaTabs]) : (schemaTabs.includes("keyboard") ? ["theme", ...schemaTabs] : ["theme", "keyboard", ...schemaTabs]);
  if (tabs.length === 0) {
    setTabsEl.innerHTML = "";
    setBodyEl.innerHTML = `<div style="padding:20px;color:var(--muted);text-align:center;">No settings available</div>`;
    return;
  }
  if (!tabs.includes("tools")) tabs.push("tools");
  if (!activeSettingsTab || !tabs.includes(activeSettingsTab)) activeSettingsTab = tabs[0];

  setTabsEl.innerHTML = "";
  for (const tab of tabs) {
    const el = document.createElement("div");
    el.className = "set-tab" + (tab === activeSettingsTab ? " active" : "");
    el.textContent = tab.charAt(0).toUpperCase() + tab.slice(1);
    el.addEventListener("click", () => { activeSettingsTab = tab; renderSettings(); });
    setTabsEl.appendChild(el);
  }

  setBodyEl.innerHTML = "";
  if (activeSettingsTab === "theme") {
    renderThemeEditor(setBodyEl);
    return;
  }
  if (activeSettingsTab === "keyboard") {
    renderKeyboardEditor(setBodyEl);
    return;
  }
  if (activeSettingsTab === "tools") {
    void renderToolsTab(setBodyEl);
    return;
  }
  const entries = configSchema.filter((e) => e.tab === activeSettingsTab && (e.control === "section" || isRealSetting(e)));
  for (const entry of entries) {
    if (entry.control === "section") {
      const header = document.createElement("div");
      header.className = "set-section-header";
      header.style.cssText = "padding:12px 0 4px;font-size:11px;font-weight:600;color:var(--muted);text-transform:uppercase;letter-spacing:0.5px;border-bottom:1px solid var(--border);";
      header.textContent = entry.label;
      setBodyEl.appendChild(header);
      continue;
    }
    const row = document.createElement("div");
    row.className = "set-row";
    const label = document.createElement("label");
    const labelText = document.createElement("span");
    labelText.textContent = entry.label;
    label.appendChild(labelText);
    if (entry.description) {
      const desc = document.createElement("span");
      desc.className = "set-desc";
      desc.textContent = entry.description;
      label.appendChild(desc);
    }
    row.appendChild(label);

    void loadSettingValue(entry, row);
    setBodyEl.appendChild(row);
  }
  if (activeSettingsTab === "diagnostics") renderDiagnosticsExtras(setBodyEl);
  if (activeSettingsTab === "updates") renderUpdatesExtras(setBodyEl);
}

async function renderToolsTab(container: HTMLElement): Promise<void> {
  container.innerHTML = "";
  let adapters: AdapterInfo[];
  try {
    const result = await invoke<AdapterListResult>("app.adapterList", {});
    adapters = result.adapters ?? [];
  } catch (err) {
    console.warn("adapter.list failed for tools tab", err);
    const failed = document.createElement("div");
    failed.className = "tools-empty";
    failed.textContent = "adapter list unavailable";
    container.appendChild(failed);
    return;
  }
  if (adapters.length === 0) {
    const empty = document.createElement("div");
    empty.className = "tools-empty";
    empty.textContent = "no adapters installed";
    container.appendChild(empty);
    return;
  }
  const list = document.createElement("div");
  list.className = "tools-list";
  for (const a of adapters) {
    const card = document.createElement("div");
    card.className = "tools-card";

    const swatch = document.createElement("span");
    swatch.className = "tools-accent";
    swatch.style.background = a.accent || "var(--accent)";
    card.appendChild(swatch);

    const body = document.createElement("div");
    body.className = "tools-body";

    const titleRow = document.createElement("div");
    titleRow.className = "tools-title-row";
    const name = document.createElement("span");
    name.className = "tools-name";
    name.textContent = a.displayName || a.name;
    titleRow.appendChild(name);

    const meta = adapterStatusMeta(a.status);
    const status = document.createElement("span");
    status.className = "tools-status";
    const dot = document.createElement("span");
    dot.className = "tools-dot";
    dot.style.background = meta.cssColor;
    const statusLabel = document.createElement("span");
    statusLabel.textContent = meta.label;
    statusLabel.style.color = meta.cssColor;
    status.appendChild(dot);
    status.appendChild(statusLabel);
    titleRow.appendChild(status);
    body.appendChild(titleRow);

    const subtitle = document.createElement("div");
    subtitle.className = "tools-subtitle";
    subtitle.textContent = adapterCardSubtitle(a.version, a.binaryPath);
    body.appendChild(subtitle);

    const manifestName = document.createElement("div");
    manifestName.className = "tools-manifest";
    manifestName.textContent = a.name;
    body.appendChild(manifestName);

    card.appendChild(body);
    list.appendChild(card);
  }
  container.appendChild(list);
}

function diagnosticsSectionHeader(text: string): HTMLElement {
  const header = document.createElement("div");
  header.className = "set-section-header";
  header.style.cssText = "padding:12px 0 4px;font-size:11px;font-weight:600;color:var(--muted);text-transform:uppercase;letter-spacing:0.5px;border-bottom:1px solid var(--border);";
  header.textContent = text;
  return header;
}

function renderDiagnosticsExtras(container: HTMLElement): void {
  container.appendChild(diagnosticsSectionHeader("Performance overlay"));

  const hudRow = document.createElement("div");
  hudRow.className = "set-row";
  const hudLabel = document.createElement("label");
  const hudLabelText = document.createElement("span");
  hudLabelText.textContent = "Live HUD";
  const hudDesc = document.createElement("span");
  hudDesc.className = "set-desc";
  hudDesc.textContent = "In-page GUI frame rate, frame time, and webview JS heap. Off by default; also toggleable from the command palette.";
  hudLabel.appendChild(hudLabelText);
  hudLabel.appendChild(hudDesc);
  const hudToggle = document.createElement("button");
  hudToggle.className = "diag-toggle" + (perfHudState.enabled ? " on" : "");
  hudToggle.textContent = perfHudState.enabled ? "On" : "Off";
  hudToggle.addEventListener("click", () => doTogglePerfHud());
  hudRow.appendChild(hudLabel);
  hudRow.appendChild(hudToggle);
  container.appendChild(hudRow);

  container.appendChild(diagnosticsSectionHeader("Snapshot inspector"));
  const snapCaption = document.createElement("div");
  snapCaption.className = "diag-caption";
  snapCaption.textContent = "Capture a live diagnostics snapshot from the engine, or paste an exported one (a single object or an array — the same JSON the engine writes to diagnostics-snapshots.json inside a performance bundle).";
  container.appendChild(snapCaption);

  const textarea = document.createElement("textarea");
  textarea.className = "diag-input";
  textarea.placeholder = '{ "takenAt": "…", "managedMemoryBytes": … }';
  container.appendChild(textarea);

  const snapActions = document.createElement("div");
  snapActions.style.cssText = "display:flex;gap:8px;flex-wrap:wrap;";
  container.appendChild(snapActions);

  const renderBtn = document.createElement("button");
  renderBtn.className = "diag-btn";
  renderBtn.textContent = "Inspect snapshot";
  snapActions.appendChild(renderBtn);

  const takeBtn = document.createElement("button");
  takeBtn.className = "diag-btn";
  takeBtn.textContent = "Take snapshot";
  snapActions.appendChild(takeBtn);

  const loadBtn = document.createElement("button");
  loadBtn.className = "diag-btn";
  loadBtn.textContent = "Load snapshots";
  snapActions.appendChild(loadBtn);

  const output = document.createElement("div");
  output.className = "diag-snap";
  container.appendChild(output);

  renderBtn.addEventListener("click", () => renderSnapshotInspection(textarea.value, output));
  takeBtn.addEventListener("click", () => void doTakeSnapshot(textarea, output));
  loadBtn.addEventListener("click", () => void doLoadSnapshots(textarea, output));

  container.appendChild(diagnosticsSectionHeader("Performance bundles"));
  renderPerfBundles(container);

  container.appendChild(diagnosticsSectionHeader("Not yet available"));
  const note = document.createElement("div");
  note.className = "diag-note";
  note.textContent = "In-page flame graphs are not available yet: a bundle's optional trace is a binary .nettrace with no in-webview parser or viewer — open it in an external profiler such as PerfView or dotnet-trace. Per-pane element inspection is available now from any browser pane menu (DevTools).";
  container.appendChild(note);
}

async function doTakeSnapshot(textarea: HTMLTextAreaElement, output: HTMLElement): Promise<void> {
  try {
    const snapshot = await invoke<DiagnosticsSnapshot>("cove://commands/diagnostics.snapshot.take", {});
    textarea.value = JSON.stringify(snapshot, null, 2);
    renderSnapshotInspection(textarea.value, output);
  } catch (e) {
    showSnapshotError(output, `Take snapshot failed: ${(e as Error).message}`);
  }
}

async function doLoadSnapshots(textarea: HTMLTextAreaElement, output: HTMLElement): Promise<void> {
  try {
    const snapshots = await invoke<DiagnosticsSnapshot[]>("cove://commands/diagnostics.snapshot.list", {});
    textarea.value = JSON.stringify(snapshots, null, 2);
    renderSnapshotInspection(textarea.value, output);
  } catch (e) {
    showSnapshotError(output, `Load snapshots failed: ${(e as Error).message}`);
  }
}

function showSnapshotError(output: HTMLElement, message: string): void {
  output.innerHTML = "";
  const err = document.createElement("div");
  err.className = "diag-error";
  err.textContent = message;
  output.appendChild(err);
}

function renderPerfBundles(container: HTMLElement): void {
  let state: PerfBundlesState = initialPerfBundlesState();

  const caption = document.createElement("div");
  caption.className = "diag-caption";
  caption.textContent = "Create a performance bundle to package the engine's diagnostics snapshots into a shareable .zip, then manage the saved bundles below.";
  container.appendChild(caption);

  const createBtn = document.createElement("button");
  createBtn.className = "diag-btn";
  container.appendChild(createBtn);

  const errorEl = document.createElement("div");
  errorEl.className = "diag-error";
  container.appendChild(errorEl);

  const listEl = document.createElement("div");
  listEl.className = "diag-snap";
  container.appendChild(listEl);

  const paint = (): void => {
    createBtn.textContent = state.creating ? "Creating…" : "Create bundle";
    createBtn.disabled = state.creating;
    errorEl.textContent = state.error ?? "";
    errorEl.style.display = state.error ? "block" : "none";
    renderPerfBundleList(state, listEl, run);
  };

  const run = (next: PerfBundlesState): void => {
    state = next;
    paint();
  };

  const refresh = async (): Promise<void> => {
    try {
      const result = await invoke<PerfBundleListResult>("cove://commands/perf.bundle.list", {});
      run(applyBundleList(state, result));
    } catch (e) {
      run(surfaceError(state, `List bundles failed: ${(e as Error).message}`));
    }
  };

  createBtn.addEventListener("click", () => {
    if (state.creating) return;
    run(beginCreate(state));
    void (async () => {
      try {
        await invoke<PerfBundleDto>("cove://commands/perf.bundle.create", {});
        run(finishCreate(state));
        await refresh();
      } catch (e) {
        run(surfaceError(state, `Create bundle failed: ${(e as Error).message}`));
      }
    })();
  });

  paint();
  void refresh();
}

function renderPerfBundleList(state: PerfBundlesState, listEl: HTMLElement, run: (next: PerfBundlesState) => void): void {
  listEl.innerHTML = "";
  const rows = bundleRows(state);
  if (rows.length === 0) {
    const empty = document.createElement("div");
    empty.className = "diag-caption";
    empty.textContent = PERF_BUNDLES_EMPTY_TEXT;
    listEl.appendChild(empty);
    return;
  }

  for (const row of rows) {
    const card = document.createElement("div");
    card.className = "diag-snap-card";
    card.style.cssText = "display:flex;gap:12px;align-items:center;justify-content:space-between;";

    const info = document.createElement("div");
    info.style.cssText = "min-width:0;flex:1;";
    const name = document.createElement("div");
    name.style.cssText = "font-size:12px;color:var(--fg);overflow:hidden;text-overflow:ellipsis;white-space:nowrap;";
    name.textContent = row.name;
    name.title = row.bundlePath;
    const meta = document.createElement("div");
    meta.style.cssText = "font-size:11px;color:var(--muted);";
    meta.textContent = `${row.createdAtLabel} · ${row.sizeLabel} · ${row.detail}`;
    info.appendChild(name);
    info.appendChild(meta);
    card.appendChild(info);

    const actions = document.createElement("div");
    actions.style.cssText = "display:flex;gap:6px;flex-shrink:0;";
    if (row.confirmingDelete) {
      const confirm = document.createElement("button");
      confirm.className = "diag-btn";
      confirm.textContent = "Confirm";
      confirm.addEventListener("click", () => void doDeleteBundle(state, row.bundlePath, run));
      const cancel = document.createElement("button");
      cancel.className = "diag-btn";
      cancel.textContent = "Cancel";
      cancel.addEventListener("click", () => run(cancelDelete(state)));
      actions.appendChild(confirm);
      actions.appendChild(cancel);
    } else {
      const del = document.createElement("button");
      del.className = "diag-btn";
      del.textContent = "Delete";
      del.addEventListener("click", () => run(requestDelete(state, row.bundlePath)));
      actions.appendChild(del);
    }
    card.appendChild(actions);
    listEl.appendChild(card);
  }
}

async function doDeleteBundle(state: PerfBundlesState, bundlePath: string, run: (next: PerfBundlesState) => void): Promise<void> {
  try {
    await invoke("cove://commands/perf.bundle.delete", { bundlePath });
    const result = await invoke<PerfBundleListResult>("cove://commands/perf.bundle.list", {});
    run(applyBundleList(cancelDelete(state), result));
  } catch (e) {
    run(surfaceError(cancelDelete(state), `Delete bundle failed: ${(e as Error).message}`));
  }
}

function renderSnapshotInspection(text: string, output: HTMLElement): void {
  output.innerHTML = "";
  const result = parseSnapshotExport(text);
  if (!result.ok) {
    const err = document.createElement("div");
    err.className = "diag-error";
    err.textContent = result.error ?? "Could not read snapshot.";
    output.appendChild(err);
    return;
  }

  const summary = summarizeSnapshots(result.snapshots);
  const summaryEl = document.createElement("div");
  summaryEl.className = "diag-caption";
  summaryEl.textContent = `${summary.count} snapshot${summary.count === 1 ? "" : "s"} · peak managed memory ${formatSnapshotBytes(summary.peakManagedMemoryBytes)}`;
  output.appendChild(summaryEl);

  for (const snapshot of result.snapshots) appendSnapshotCard(snapshot, output);
}

function appendSnapshotCard(snapshot: DiagnosticsSnapshot, output: HTMLElement): void {
  const card = document.createElement("div");
  card.className = "diag-snap-card";
  for (const row of snapshotRows(snapshot)) {
    const rowEl = document.createElement("div");
    rowEl.className = "diag-snap-row";
    const key = document.createElement("span");
    key.className = "k";
    key.textContent = row.label;
    const value = document.createElement("span");
    value.className = "v";
    value.textContent = row.value;
    rowEl.appendChild(key);
    rowEl.appendChild(value);
    card.appendChild(rowEl);
  }
  output.appendChild(card);
}

async function loadSettingValue(entry: ConfigSchemaEntry, row: HTMLElement): Promise<void> {
  let currentValue = "";
  try {
    const res = await invoke<{ value: string } | null>("cove://commands/config.get", { key: entry.key });
    currentValue = res?.value ?? "";
  } catch { void 0; }

  const input = createSettingControl(entry, currentValue);
  input.addEventListener("change", () => void saveSetting(entry.key, input));
  row.appendChild(input);
}

function createSettingControl(entry: ConfigSchemaEntry, value: string): HTMLInputElement | HTMLSelectElement {
  if (entry.control === "select" && entry.options && entry.options.length > 0) {
    const select = document.createElement("select");
    for (const opt of entry.options) {
      const o = document.createElement("option");
      o.value = opt;
      o.textContent = opt;
      select.appendChild(o);
    }
    select.value = value;
    select.style.cssText = "width:140px;";
    return select;
  }
  if (entry.type === "bool" || entry.control === "toggle") {
    const select = document.createElement("select");
    const t = document.createElement("option"); t.value = "true"; t.textContent = "On"; select.appendChild(t);
    const f = document.createElement("option"); f.value = "false"; f.textContent = "Off"; select.appendChild(f);
    select.value = value === "true" ? "true" : "false";
    select.style.cssText = "width:120px;";
    return select;
  }
  if (entry.type === "int" || entry.type === "double") {
    const input = document.createElement("input");
    input.type = "number";
    input.value = value;
    input.style.cssText = "width:120px;";
    return input;
  }
  const input = document.createElement("input");
  input.type = "text";
  input.value = value;
  return input;
}

async function saveSetting(key: string, input: HTMLInputElement | HTMLSelectElement): Promise<void> {
  const value = input.type === "checkbox" ? String((input as HTMLInputElement).checked) : input.value;
  try {
    await invoke("cove://commands/config.set", { key, value });
    if (key.startsWith("terminal.")) { settings = await loadSettings(); applySettings(); }
    if (key.startsWith("appearance.")) { await applyAppearance(key); }
  } catch { void 0; }
}

async function applyAppearance(changedKey: string | null): Promise<void> {
  const get = async (k: string): Promise<string> => { try { const r = await invoke<{ ok: boolean; value?: string }>("app.configGet", { key: k }); return r.ok ? r.value ?? "" : ""; } catch { return ""; } };
  const root = document.documentElement;
  if (changedKey === null || changedKey === "appearance.uiScale") { const scale = parseFloat(await get("appearance.uiScale")) || 1; root.style.setProperty("--ui-scale", String(scale)); document.body.style.fontSize = `${13 * scale}px`; }
  if (changedKey === null || changedKey === "appearance.layoutGap") { const gap = parseInt(await get("appearance.layoutGap")) || 4; root.style.setProperty("--layout-gap", `${gap}px`); gridEl.style.gap = `${gap}px`; }
  if (changedKey === null || changedKey === "appearance.accent") { const accent = await get("appearance.accent"); if (accent) { root.style.setProperty("--accent", accent); root.style.setProperty("--accent-dim", accent); } }
  if (changedKey === null || changedKey === "appearance.wallpaper") { const wp = await get("appearance.wallpaper"); if (wp) { document.body.style.backgroundImage = `url("${wp}")`; document.body.style.backgroundSize = "cover"; } else { document.body.style.backgroundImage = ""; } }
  if (changedKey === null || changedKey === "appearance.paneLight") { const pl = await get("appearance.paneLight") === "true"; root.style.setProperty("--pane-light", pl ? "1" : "0"); }
  if (changedKey === null || changedKey === "appearance.iconSet") { const ic = (await get("appearance.iconSet")) || "default"; document.body.classList.remove("icon-set-outline", "icon-set-filled"); if (ic === "outline") document.body.classList.add("icon-set-outline"); else if (ic === "filled") document.body.classList.add("icon-set-filled"); }
}
let themeList: ThemeDto[] = [];
let themeActiveName: string | null = null;
let themeCustomNames: string[] = [];
let themeDraft: ThemeDraft = { ...DEFAULT_DRAFT };
let themeBuiltinNames: string[] = [];
let themeAppliedVars: Record<string, string> | null = null;
let themeAppliedTermTheme: typeof THEME | null = null;

async function loadThemeData(): Promise<void> {
  try {
    const list = await invoke<{ themes: ThemeDto[] }>("cove://commands/theme.list", {});
    themeList = list.themes ?? [];
    themeBuiltinNames = themeList.filter((t) => (t.name.startsWith("cove-") || t.name === "catppuccin-mocha") && !themeCustomNames.includes(t.name)).map((t) => t.name);
  } catch { themeList = []; }
  try {
    const active = await invoke<{ theme: ThemeDto | null }>("cove://commands/theme.get-active", {});
    themeActiveName = active.theme?.name ?? null;
    if (active.theme) { themeDraft = draftFromTheme(active.theme); }
  } catch { themeActiveName = null; }
  themeCustomNames = themeList.filter((t) => !themeBuiltinNames.includes(t.name)).map((t) => t.name);
}

function applyThemeVars(theme: ThemeDto): void {
  const vars = cssVarsFromTheme(theme);
  const root = document.documentElement;
  for (const [k, v] of Object.entries(vars)) { root.style.setProperty(k, v); }
  themeAppliedVars = vars;
  activeThemeDto = theme;
  const termTheme = xtermThemeFromDto(theme, settings.backgroundOpacity);
  themeAppliedTermTheme = termTheme as typeof THEME;
  for (const pv of panes.values()) { pv.term.options.theme = termTheme; }
}

function revertThemeVars(): void {
  if (!themeAppliedVars) return;
  const root = document.documentElement;
  for (const k of Object.keys(themeAppliedVars)) { root.style.removeProperty(k); }
  themeAppliedVars = null;
  activeThemeDto = null;
  if (themeAppliedTermTheme) {
    const restored = { ...THEME, background: themeBackgroundWithOpacity(settings.backgroundOpacity) };
    for (const pv of panes.values()) { pv.term.options.theme = restored; }
    themeAppliedTermTheme = null;
  }
}

function renderThemeEditor(container: HTMLElement): void {
  void loadThemeData().then(() => renderThemeEditorBody(container));
  container.innerHTML = `<div style="padding:20px;color:var(--muted);text-align:center;">Loading themes…</div>`;
}

function renderThemeEditorBody(container: HTMLElement): void {
  container.innerHTML = "";

  const dropdownRow = document.createElement("div");
  dropdownRow.style.cssText = "padding:12px 0;display:flex;align-items:center;gap:10px;border-bottom:1px solid var(--border);";
  const dropdownLabel = document.createElement("span");
  dropdownLabel.textContent = "Active theme";
  dropdownLabel.style.cssText = "font-size:12px;color:var(--muted);";
  const dropdown = document.createElement("select");
  dropdown.style.cssText = "background:var(--panel-2);border:1px solid var(--border);color:var(--fg);border-radius:6px;padding:4px 8px;min-width:160px;";
  const noneOpt = document.createElement("option");
  noneOpt.value = ""; noneOpt.textContent = "— none —"; dropdown.appendChild(noneOpt);
  for (const t of themeList) {
    const o = document.createElement("option");
    o.value = t.name; o.textContent = t.name + (themeBuiltinNames.includes(t.name) ? "" : " (custom)");
    dropdown.appendChild(o);
  }
  dropdown.value = themeActiveName ?? "";
  dropdown.addEventListener("change", () => void onThemeSelect(dropdown.value));
  dropdownRow.appendChild(dropdownLabel);
  dropdownRow.appendChild(dropdown);

  const deleteBtn = document.createElement("button");
  deleteBtn.textContent = "Delete";
  deleteBtn.style.cssText = "margin-left:auto;background:transparent;border:1px solid var(--border);color:var(--muted);border-radius:6px;padding:4px 10px;font-size:11px;cursor:pointer;";
  deleteBtn.disabled = !canDelete(themeActiveName ?? "", themeCustomNames);
  deleteBtn.addEventListener("click", () => void onThemeDelete(themeActiveName ?? ""));
  dropdownRow.appendChild(deleteBtn);
  container.appendChild(dropdownRow);

  const editorHeader = document.createElement("div");
  editorHeader.style.cssText = "padding:12px 0 4px;font-size:11px;font-weight:600;color:var(--muted);text-transform:uppercase;letter-spacing:0.5px;";
  editorHeader.textContent = "Edit & preview";
  container.appendChild(editorHeader);

  const nameRow = document.createElement("div");
  nameRow.className = "set-row";
  const nameLabel = document.createElement("label");
  nameLabel.textContent = "Theme name";
  const nameInput = document.createElement("input");
  nameInput.type = "text";
  nameInput.value = themeDraft.name;
  nameInput.style.cssText = "background:var(--panel-2);border:1px solid var(--border);color:var(--fg);border-radius:6px;padding:4px 8px;width:180px;";
  nameInput.addEventListener("input", () => { themeDraft.name = nameInput.value; updateThemePreview(); });
  nameLabel.appendChild(nameInput);
  nameRow.appendChild(nameLabel);
  container.appendChild(nameRow);

  const typeRow = document.createElement("div");
  typeRow.className = "set-row";
  const typeLabel = document.createElement("label");
  typeLabel.textContent = "Type";
  const typeSelect = document.createElement("select");
  for (const tp of ["dark", "light"]) { const o = document.createElement("option"); o.value = tp; o.textContent = tp; typeSelect.appendChild(o); }
  typeSelect.value = themeDraft.type;
  typeSelect.style.cssText = "background:var(--panel-2);border:1px solid var(--border);color:var(--fg);border-radius:6px;padding:4px 8px;width:120px;";
  typeSelect.addEventListener("change", () => { themeDraft.type = typeSelect.value; updateThemePreview(); });
  typeLabel.appendChild(typeSelect);
  typeRow.appendChild(typeLabel);
  container.appendChild(typeRow);

  for (const field of THEME_COLOR_FIELDS) {
    const row = document.createElement("div");
    row.className = "set-row";
    row.style.cssText = "flex-direction:row;align-items:center;gap:10px;";
    const label = document.createElement("label");
    label.style.cssText = "flex-direction:column;gap:2px;flex:1;";
    const labelText = document.createElement("span");
    labelText.textContent = field.label;
    label.appendChild(labelText);
    if (field.desc) { const d = document.createElement("span"); d.className = "set-desc"; d.textContent = field.desc; label.appendChild(d); }
    const colorInput = document.createElement("input");
    colorInput.type = "color";
    colorInput.value = (themeDraft as unknown as Record<string, string>)[field.key];
    colorInput.style.cssText = "width:40px;height:28px;border:1px solid var(--border);border-radius:6px;background:transparent;cursor:pointer;";
    const hexInput = document.createElement("input");
    hexInput.type = "text";
    hexInput.value = (themeDraft as unknown as Record<string, string>)[field.key];
    hexInput.style.cssText = "background:var(--panel-2);border:1px solid var(--border);color:var(--fg);border-radius:6px;padding:4px 8px;width:100px;font-family:monospace;";
    colorInput.addEventListener("input", () => {
      (themeDraft as unknown as Record<string, string>)[field.key] = colorInput.value;
      hexInput.value = colorInput.value;
      updateThemePreview();
    });
    hexInput.addEventListener("input", () => {
      if (isValidHex(hexInput.value)) { colorInput.value = hexInput.value; (themeDraft as unknown as Record<string, string>)[field.key] = hexInput.value; updateThemePreview(); }
    });
    label.appendChild(hexInput);
    row.appendChild(label);
    row.appendChild(colorInput);
    container.appendChild(row);
  }

  const contrastInfo = document.createElement("div");
  contrastInfo.id = "theme-contrast";
  contrastInfo.style.cssText = "padding:8px 0;font-size:11px;color:var(--muted);";
  container.appendChild(contrastInfo);

  const actions = document.createElement("div");
  actions.style.cssText = "padding:12px 0;display:flex;gap:10px;";
  const saveBtn = document.createElement("button");
  saveBtn.textContent = "Save as custom";
  saveBtn.style.cssText = "background:var(--accent);border:none;color:#000;border-radius:6px;padding:6px 14px;font-size:12px;cursor:pointer;font-weight:600;";
  saveBtn.addEventListener("click", () => void onThemeSave());
  const resetBtn = document.createElement("button");
  resetBtn.textContent = "Reset preview";
  resetBtn.style.cssText = "background:transparent;border:1px solid var(--border);color:var(--muted);border-radius:6px;padding:6px 14px;font-size:12px;cursor:pointer;";
  resetBtn.addEventListener("click", () => { revertThemeVars(); if (themeActiveName) { const t = themeList.find((x) => x.name === themeActiveName); if (t) { themeDraft = draftFromTheme(t); } } else { themeDraft = { ...DEFAULT_DRAFT }; } renderThemeEditorBody(container); });
  actions.appendChild(saveBtn);
  actions.appendChild(resetBtn);
  container.appendChild(actions);
}

function updateThemePreview(): void {
  const theme = themeFromDraft(themeDraft);
  applyThemeVars(theme);
  const contrastEl = document.getElementById("theme-contrast");
  if (contrastEl) {
    const fgBg = contrastRatio(themeDraft.terminalForeground, themeDraft.terminalBackground);
    const tier = contrastTier(fgBg);
    contrastEl.textContent = `Terminal contrast: ${fgBg.toFixed(2)}:1 (${tier === "fail" ? "below AA" : tier})`;
    contrastEl.style.color = tier === "fail" ? "#e06c75" : "var(--muted)";
  }
  const saveBtn = document.querySelector("#set-body button");
  if (saveBtn && saveBtn.textContent === "Save as custom") {
    saveBtn.setAttribute("data-valid", canSaveDraft(themeDraft) ? "1" : "0");
  }
}

async function onThemeSelect(name: string): Promise<void> {
  if (!name) { themeActiveName = null; revertThemeVars(); renderThemeEditor(setBodyEl); return; }
  try {
    const res = await invoke<{ theme: ThemeDto }>("cove://commands/theme.set-active", { name });
    themeActiveName = name;
    if (res.theme) { themeDraft = draftFromTheme(res.theme); applyThemeVars(res.theme); }
    await loadThemeData();
    renderThemeEditorBody(setBodyEl);
  } catch { void 0; }
}

async function onThemeSave(): Promise<void> {
  if (!canSaveDraft(themeDraft)) return;
  try {
    await invoke("cove://commands/theme.save-custom", themeDraft);
    await invoke("cove://commands/theme.set-active", { name: themeDraft.name });
    themeActiveName = themeDraft.name;
    await loadThemeData();
    renderThemeEditorBody(setBodyEl);
  } catch { void 0; }
}

async function onThemeDelete(name: string): Promise<void> {
  if (!canDelete(name, themeCustomNames)) return;
  try {
    await invoke("cove://commands/theme.delete-custom", { name });
    if (themeActiveName === name) { themeActiveName = null; revertThemeVars(); }
    await loadThemeData();
    renderThemeEditorBody(setBodyEl);
  } catch { void 0; }
}
let keybindList: KeybindDto[] = [];
let keybindConflicts: string[] = [];
let keybindRecordingAction: string | null = null;

async function loadKeybindData(): Promise<void> {
  try {
    const res = await invoke<{ bindings: KeybindDto[]; conflicts: string[] }>("cove://commands/keybind.list", {});
    keybindList = res.bindings ?? [];
    keybindConflicts = res.conflicts ?? [];
  } catch { keybindList = []; keybindConflicts = []; }
}

function renderKeyboardEditor(container: HTMLElement): void {
  void loadKeybindData().then(() => renderKeyboardEditorBody(container));
  container.innerHTML = `<div style="padding:20px;color:var(--muted);text-align:center;">Loading keybindings…</div>`;
}

function renderKeyboardEditorBody(container: HTMLElement): void {
  container.innerHTML = "";
  const categories = categorizeBindings(keybindList, keybindConflicts, []);

  if (keybindConflicts.length > 0) {
    const warn = document.createElement("div");
    warn.style.cssText = "padding:8px 12px;margin-bottom:8px;background:color-mix(in srgb, #e06c75 15%, transparent);border:1px solid #e06c75;border-radius:6px;font-size:11px;color:#e5a0a8;";
    warn.textContent = `Conflicts: ${keybindConflicts.join(", ")} — two actions share the same chord`;
    container.appendChild(warn);
  }

  for (const cat of categories) {
    const header = document.createElement("div");
    header.className = "set-section-header";
    header.style.cssText = "padding:12px 0 4px;font-size:11px;font-weight:600;color:var(--muted);text-transform:uppercase;letter-spacing:0.5px;border-bottom:1px solid var(--border);";
    header.textContent = cat.name;
    container.appendChild(header);

    for (const row of cat.rows) {
      const rowEl = document.createElement("div");
      rowEl.className = "set-row";
      rowEl.style.cssText = "flex-direction:row;align-items:center;gap:10px;";
      const label = document.createElement("label");
      label.style.cssText = "flex-direction:column;gap:2px;flex:1;";
      const labelText = document.createElement("span");
      labelText.textContent = row.description ?? row.action;
      label.appendChild(labelText);
      const actionLabel = document.createElement("span");
      actionLabel.className = "set-desc";
      actionLabel.textContent = row.action;
      label.appendChild(actionLabel);
      rowEl.appendChild(label);

      const chordBtn = document.createElement("button");
      chordBtn.textContent = chordDisplay(row.chord);
      chordBtn.style.cssText = `background:var(--panel-2);border:1px solid ${row.hasConflict ? "#e06c75" : "var(--border)"};color:var(--fg);border-radius:6px;padding:4px 10px;font-size:11px;font-family:monospace;min-width:80px;cursor:pointer;${keybindRecordingAction === row.action ? "outline:2px solid var(--accent);" : ""}`;
      if (keybindRecordingAction === row.action) { chordBtn.textContent = "Press keys…"; }
      chordBtn.addEventListener("click", () => { keybindRecordingAction = keybindRecordingAction === row.action ? null : row.action; renderKeyboardEditorBody(container); });
      rowEl.appendChild(chordBtn);

      const clearBtn = document.createElement("button");
      clearBtn.textContent = "×";
      clearBtn.style.cssText = "background:transparent;border:1px solid var(--border);color:var(--muted);border-radius:6px;padding:4px 8px;font-size:13px;cursor:pointer;";
      clearBtn.addEventListener("click", () => void onKeybindClear(row.chord, container));
      rowEl.appendChild(clearBtn);

      container.appendChild(rowEl);
    }
  }

  if (keybindRecordingAction) {
    const hint = document.createElement("div");
    hint.style.cssText = "padding:8px 0;font-size:11px;color:var(--muted);";
    hint.textContent = `Recording for "${keybindRecordingAction}" — press a key combination, Esc to cancel.`;
    container.appendChild(hint);
    const escHandler = (e: KeyboardEvent): void => {
      e.preventDefault();
      e.stopPropagation();
      if (e.key === "Escape") { keybindRecordingAction = null; renderKeyboardEditorBody(container); settingsEl.removeEventListener("keydown", escHandler, true); return; }
      const chord = captureChord(e);
      if (chord) {
        settingsEl.removeEventListener("keydown", escHandler, true);
        const act = keybindRecordingAction;
        if (act) { void onKeybindSet(act, chord, container); }
        keybindRecordingAction = null;
      }
    };
    settingsEl.addEventListener("keydown", escHandler, true);
  }
}


function captureChord(e: KeyboardEvent): string {
  const parts: string[] = [];
  if (e.metaKey) parts.push("cmd");
  if (e.ctrlKey) parts.push("ctrl");
  if (e.altKey) parts.push("alt");
  if (e.shiftKey) parts.push("shift");
  const key = e.key.toLowerCase();
  if (!["meta", "control", "alt", "shift"].includes(key)) {
    parts.push(key === " " ? "space" : key);
  }
  if (parts.length === 0) return "";
  const chord = parts.join("+");
  return isValidChord(chord) ? chord : "";
}

async function onKeybindSet(action: string, chord: string, container: HTMLElement): Promise<void> {
  const normalized = normalizeChordStr(chord);
  const check = canRecordChord(normalized, action, keybindList);
  if (!check.valid) {
    if (isReservedChord(normalized)) { return; }
    if (check.conflictAction) { const proceed = confirm(`"${chordDisplay(normalized)}" is bound to "${check.conflictAction}". Rebind?`); if (!proceed) return; }
  }
  try {
    const res = await invoke<{ success: boolean; warning?: { warning: string } | null }>("cove://commands/keybind.set", { chord: normalized, actionType: "app-command", action });
    if (res.success) { await loadKeybindData(); await reloadKeymap(); renderKeyboardEditorBody(container); }
  } catch { void 0; }
}

async function onKeybindClear(chord: string, container: HTMLElement): Promise<void> {
  try {
    await invoke("cove://commands/keybind.clear", { chord });
    await loadKeybindData();
    await reloadKeymap();
    renderKeyboardEditorBody(container);
  } catch { void 0; }
}
const onboardingEl = document.getElementById("onboarding")!;
let onboardingState: OnboardingState = { ...INITIAL_ONBOARDING_STATE };

async function maybeShowOnboarding(): Promise<void> {
  try {
    const seen = await invoke<{ value?: string }>("app.configGet", { key: ONBOARDING_COMPLETED_KEY });
    const hasSeen = onboardingSeenFromConfig(seen.value);
    if (!shouldShowOnboarding(hasSeen)) return;
    onboardingEl.classList.add("open");
    renderOnboarding();
  } catch { void 0; }
}

function renderOnboarding(): void {
  const step = currentStepData(onboardingState);
  (onboardingEl.querySelector(".ob-title") as HTMLElement).textContent = step.title;
  (onboardingEl.querySelector(".ob-progress-bar") as HTMLElement).style.width = `${progressPercent(onboardingState)}%`;
  const body = onboardingEl.querySelector(".ob-body") as HTMLElement;
  body.innerHTML = "";
  const p = document.createElement("p");
  p.textContent = step.body;
  body.appendChild(p);

  if (step.id === "adapters") { renderAdapterChoice(body); }
  if (step.id === "telemetry") { renderTelemetryChoice(body); }

  const prevBtn = onboardingEl.querySelector(".ob-prev") as HTMLButtonElement;
  const nextBtn = onboardingEl.querySelector(".ob-next") as HTMLButtonElement;
  prevBtn.disabled = isFirstStep(onboardingState);
  nextBtn.textContent = isLastStep(onboardingState) ? "Finish" : "Next";
}

function renderAdapterChoice(body: HTMLElement): void {
  const list = document.createElement("div");
  list.className = "ob-adapter-list";
  const adapters = [
    { id: "claude", name: "Claude Code" },
    { id: "codex", name: "Codex" },
    { id: "gemini", name: "Gemini CLI" },
    { id: null, name: "Skip — configure later" },
  ];
  for (const a of adapters) {
    const el = document.createElement("div");
    el.className = "ob-adapter" + (onboardingState.selectedAdapter === a.id ? " selected" : "");
    const name = document.createElement("span");
    name.className = "ob-adapter-name";
    name.textContent = a.name;
    el.appendChild(name);
    el.addEventListener("click", () => { onboardingState = selectAdapter(onboardingState, a.id); renderOnboarding(); });
    list.appendChild(el);
  }
  body.appendChild(list);
}

function renderTelemetryChoice(body: HTMLElement): void {
  const toggle = document.createElement("div");
  toggle.className = "ob-telemetry-toggle";
  const cb = document.createElement("input");
  cb.type = "checkbox";
  cb.id = "ob-telemetry-cb";
  cb.checked = onboardingState.telemetryOptIn;
  const label = document.createElement("label");
  label.htmlFor = "ob-telemetry-cb";
  label.textContent = "Enable anonymous telemetry (optional)";
  label.style.fontSize = "12px";
  label.style.color = "var(--fg)";
  cb.addEventListener("change", () => { onboardingState = setTelemetryOptIn(onboardingState, cb.checked); });
  toggle.appendChild(cb);
  toggle.appendChild(label);
  body.appendChild(toggle);
}

async function onOnboardingNext(): Promise<void> {
  if (isLastStep(onboardingState)) {
    await completeOnboarding();
    return;
  }
  onboardingState = nextStep(onboardingState);
  renderOnboarding();
}

function onOnboardingPrev(): void {
  onboardingState = prevStep(onboardingState);
  renderOnboarding();
}

async function onOnboardingSkip(): Promise<void> {
  onboardingState = dismissOnboarding(onboardingState);
  await completeOnboarding();
}

async function completeOnboarding(): Promise<void> {
  onboardingEl.classList.remove("open");
  try {
    await invoke("app.configSet", { key: ONBOARDING_COMPLETED_KEY, value: "true" });
    if (onboardingState.telemetryOptIn) { await invoke("app.configSet", { key: "telemetry.enabled", value: "true" }); }
    if (onboardingState.selectedAdapter) { await invoke("app.configSet", { key: "adapterCommands.default", value: onboardingState.selectedAdapter }); }
  } catch (e) { console.warn("onboarding persist failed", e); }
}

(onboardingEl.querySelector(".ob-next") as HTMLButtonElement).addEventListener("click", () => void onOnboardingNext());
(onboardingEl.querySelector(".ob-prev") as HTMLButtonElement).addEventListener("click", onOnboardingPrev);
(onboardingEl.querySelector(".ob-skip") as HTMLElement).addEventListener("click", () => void onOnboardingSkip());

settingsEl.addEventListener("mousedown", (e) => { if (e.target === settingsEl) closeSettings(); });
document.getElementById("set-close")!.addEventListener("click", closeSettings);
settingsEl.addEventListener("keydown", (e) => { if (e.key === "Escape") closeSettings(); });

const findEl = document.getElementById("findbar")!;
const findInput = document.getElementById("find-input") as HTMLInputElement;
const findDecor = { matchBackground: "#6c5b8e", activeMatchBackground: "#cba6f7", matchOverviewRuler: "#cba6f7", activeMatchColorOverviewRuler: "#cba6f7" };
function activeSearch(): SearchAddon | null { return focusedPaneId ? (panes.get(focusedPaneId)?.search ?? null) : null; }
function openFind() { findEl.classList.add("open"); findInput.focus(); findInput.select(); }
function closeFind() { findEl.classList.remove("open"); activeSearch()?.clearDecorations(); if (focusedPaneId) panes.get(focusedPaneId)?.term.focus(); }
async function doFind(dir: number) {
  const s = activeSearch();
  const q = findInput.value;
  if (!s || !q) return;
  const paneId = focusedPaneId!;
  try {
    const res = await invoke<{ matches: { line: number; text: string }[] }>("app.paneSearch", { paneId, query: q, caseSensitive: false });
    if (res.matches.length === 0) { s.clearDecorations(); return; }
  } catch { void 0; }
  if (dir >= 0) s.findNext(q, { caseSensitive: false, decorations: findDecor });
  else s.findPrevious(q, { caseSensitive: false, decorations: findDecor });
}
findInput.addEventListener("input", () => doFind(1));
findInput.addEventListener("keydown", (e) => {
  if (e.key === "Escape") { e.preventDefault(); closeFind(); }
  else if (e.key === "Enter") { e.preventDefault(); doFind(e.shiftKey ? -1 : 1); }
});
document.getElementById("find-next")!.addEventListener("click", () => doFind(1));
document.getElementById("find-prev")!.addEventListener("click", () => doFind(-1));
document.getElementById("find-close")!.addEventListener("click", closeFind);

const launcherEl = document.getElementById("launcher")!;
function openLauncher() { launcherEl.classList.add("open"); void loadLauncherAgents(); }
function closeLauncher() { launcherEl.classList.remove("open"); if (focusedPaneId) panes.get(focusedPaneId)?.term.focus(); }
launcherEl.addEventListener("mousedown", (e) => { if (e.target === launcherEl) closeLauncher(); });
launcherEl.addEventListener("keydown", (e) => { if (e.key === "Escape") closeLauncher(); });

const launchAgentsEl = document.getElementById("launch-agents")!;
interface AdapterInfo { name: string; displayName: string; accent: string; binary: string; status?: string | null; version?: string | null; binaryPath?: string | null; }
interface AdapterListResult { adapters: AdapterInfo[]; }
async function loadLauncherAgents(): Promise<void> {
  try {
    const result = await invoke<AdapterListResult>("app.adapterList", {});
    launchAgentsEl.innerHTML = "";
    for (const a of result.adapters ?? []) {
      const tile = document.createElement("div");
      tile.className = "launch-tile";
      tile.innerHTML = `<span class="ic" style="color:${a.accent || "#cba6f7"}">&#9881;</span><span class="lbl">${a.displayName || a.name}</span>`;
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
  const stored = localStorage.getItem(launcherYoloKey(adapter));
  if (stored !== null) return stored === "true";
  return adapter === "claude-code";
}

async function buildAdapterLaunch(a: AdapterInfo): Promise<{ command: string; args: string[] }> {
  try {
    const built = await invoke<{ command: string; args: string[] }>("cove://commands/launch.build", {
      adapter: a.name, profileSlug: "default", yolo: launcherYolo(a.name), workingDir: null, extraFlags: [], env: {},
    });
    if (built.command) return { command: built.command, args: built.args ?? [] };
  } catch (err) { console.warn("launch.build failed, spawning raw binary", a.name, err); }
  return { command: a.binary, args: [] };
}

async function spawnAgentInto(roomId: string | null, placeholderId: string | null, a: AdapterInfo): Promise<void> {
  const launch = await buildAdapterLaunch(a);
  const sp = (await invoke<{ paneId: string }>("app.paneSpawn", { command: launch.command, args: launch.args, cwd: "", inheritCwdFrom: "", cols: 80, rows: 24, adapter: a.name, agentName: a.displayName, workspace: "", room: "" })).paneId;
  if (roomId) {
    if (placeholderId) {
      await invoke("app.layoutMutate", { op: "replace", roomId, targetPaneId: placeholderId, newPaneId: sp, orientation: "", name: "", paneId: "", dir: 0, paneType: "terminal" });
    }
    activeRoomId = roomId;
  } else {
    const r = await invoke<{ roomId: string }>("app.layoutMutate", { op: "createRoom", newPaneId: sp, name: nextRoomName(), roomId: "", targetPaneId: "", orientation: "", paneId: "", dir: 0 });
    activeRoomId = r.roomId;
  }
  await reload();
  focusPane(sp);
}

let launcherAdapters: LauncherAdapter[] = [];
let launcherRecents: RecentSessionRow[] = [];
interface SessionRecentResult { sessions: RecentSessionRow[]; }
async function loadLauncherAdapters(): Promise<void> {
  try {
    const result = await invoke<AdapterListResult>("app.adapterList", {});
    launcherAdapters = (result.adapters ?? []).map((a) => ({ name: a.name, displayName: a.displayName, accent: a.accent, binary: a.binary }));
  } catch { launcherAdapters = []; }
  await loadLauncherRecents();
  if ((layout?.rooms ?? []).length === 0) renderRoom();
}

let launcherRecentsCwd: string | null = null;
let launcherRecentsAt = 0;
async function loadLauncherRecents(): Promise<void> {
  const cwd = activeProjectDir();
  try {
    const res = await invoke<SessionRecentResult>("cove://commands/session.recent", { cwd, limit: 50 });
    launcherRecents = res.sessions ?? [];
  } catch (err) {
    console.warn("session.recent failed", cwd, err);
    launcherRecents = [];
  }
  launcherRecentsCwd = cwd;
  launcherRecentsAt = Date.now();
}

async function refreshLauncherRecents(): Promise<void> {
  if (activeProjectDir() === launcherRecentsCwd && Date.now() - launcherRecentsAt < 4000) return;
  const before = JSON.stringify(launcherRecents);
  await loadLauncherRecents();
  if (JSON.stringify(launcherRecents) !== before) renderRoom();
}

function builtinLauncherDefs(): LauncherBuiltin[] {
  return toolbarTiles().map((t) => ({ id: t.id, label: t.label, icon: t.icon, action: t.action }));
}

interface LauncherContext {
  targetRoomId: string | null;
  targetPlaceholderId: string | null;
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
  void spawnAgentInto(ctx.targetRoomId, ctx.targetPlaceholderId, { name: tile.adapterName, displayName: tile.label, accent: tile.accent, binary: tile.binary });
}

function launchToolTile(ctx: LauncherContext, tile: LauncherTile): void {
  void launchTileInto(ctx.targetRoomId, ctx.targetPlaceholderId, tile.action);
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

function renderBoxLauncher(targetRoomId: string | null, targetPlaceholderId: string | null): HTMLElement {
  void refreshLauncherRecents();
  const ctx: LauncherContext = { targetRoomId, targetPlaceholderId };
  const wrap = document.createElement("div");
  wrap.className = "box-launcher";
  wrap.tabIndex = 0;
  if (targetRoomId) wrap.dataset.roomId = targetRoomId;
  if (targetPlaceholderId) wrap.dataset.placeholderId = targetPlaceholderId;
  paintBoxLauncher(wrap, ctx);
  const ro = new ResizeObserver(() => {
    if (!document.body.contains(wrap)) { ro.disconnect(); return; }
    const count = Math.max(1, launcherTileSets().harness.length);
    const cols = computeLauncherCols(wrap.clientWidth || 680, count, LAUNCHER_HARNESS_COLS);
    if (cols !== launcherCols) paintBoxLauncher(wrap, ctx);
  });
  ro.observe(wrap);
  wrap.addEventListener("keydown", (e) => handleLauncherKey(e, wrap, ctx));
  if (launcherTipTimer !== null) window.clearInterval(launcherTipTimer);
  launcherTipTimer = window.setInterval(() => {
    launcherTipIndex += 1;
    const tipEl = wrap.querySelector(".cl-tip");
    if (tipEl) tipEl.textContent = tipAt(launcherTipIndex);
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
  brand.src = brandLogoAt(brandIndex);
  const tip = document.createElement("span");
  tip.className = "cl-tip";
  tip.textContent = tipAt(launcherTipIndex);
  const hint = document.createElement("span");
  hint.className = "cl-hint";
  hint.textContent = "hold ⇧ for shortcuts";
  header.appendChild(brand);
  header.appendChild(tip);
  header.appendChild(hint);
  wrap.appendChild(header);

  const cards = document.createElement("div");
  cards.className = "cl-harness-row";
  cards.style.gridTemplateColumns = `repeat(${geo.harnessCols}, minmax(0, 200px))`;
  harness.forEach((tile, i) => {
    const selected = launcherSelection.section === "harness" && launcherSelection.index === i;
    cards.appendChild(renderHarnessCard(ctx, tile, harnessKeys[i], selected));
  });
  if (harness.length === 0) {
    cards.style.gridTemplateColumns = "minmax(0, 280px)";
    cards.appendChild(renderConfigureAdapterCard());
  }
  wrap.appendChild(cards);

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
  onboardingState = { ...INITIAL_ONBOARDING_STATE, currentStep: 1 };
  onboardingEl.classList.add("open");
  renderOnboarding();
}

function renderHarnessCard(ctx: LauncherContext, tile: LauncherTile, letter: string, selected: boolean): HTMLElement {
  const accent = adapterAccent(tile.adapterName, tile.accent);
  const el = document.createElement("div");
  el.className = "cl-card" + (tile.disabled ? " disabled" : "") + (selected ? " selected" : "");
  if (selected && !tile.disabled) { el.classList.add("expanded"); el.style.setProperty("--card-accent", accent); }
  el.style.setProperty("--card-accent", accent);

  const top = document.createElement("div");
  top.className = "cl-card-top";
  const key = document.createElement("span");
  key.className = "cl-card-key";
  key.textContent = letter;
  const cta = document.createElement("span");
  cta.className = "cl-card-cta";
  cta.textContent = "⌘↵";
  top.appendChild(key);
  top.appendChild(cta);
  el.appendChild(top);

  const badge = document.createElement("span");
  badge.className = "cl-card-badge";
  badge.textContent = monogram(tile.label);
  el.appendChild(badge);

  const name = document.createElement("div");
  name.className = "cl-card-name";
  name.textContent = tile.label;
  el.appendChild(name);

  if (tile.disabled) {
    const note = document.createElement("div");
    note.className = "cl-card-note";
    note.textContent = tile.note || "not detected";
    el.appendChild(note);
    return el;
  }

  el.addEventListener("click", () => {
    if (selected) launchHarnessTile(ctx, tile);
    else { launcherSelection = { section: "harness", index: harnessIndexOf(tile) }; repaintActiveLauncher(); }
  });

  if (selected) el.appendChild(renderCardExpansion(ctx, tile));
  return el;
}

function harnessIndexOf(tile: LauncherTile): number {
  return detectedHarnessTiles(buildAdapterTiles(launcherAdapters)).findIndex((t) => t.id === tile.id);
}

function repaintActiveLauncher(): void {
  const wrap = document.querySelector(".box-launcher") as HTMLElement | null;
  if (!wrap) return;
  const targetRoomId = wrap.dataset.roomId || null;
  const targetPlaceholderId = wrap.dataset.placeholderId || null;
  paintBoxLauncher(wrap, { targetRoomId, targetPlaceholderId });
  wrap.focus();
}

function renderCardExpansion(ctx: LauncherContext, tile: LauncherTile): HTMLElement {
  const body = document.createElement("div");
  body.className = "cl-card-expand";

  const chipRow = document.createElement("div");
  chipRow.className = "cl-key-chip-row";
  const chip = document.createElement("span");
  chip.className = "cl-key-chip";
  chip.textContent = "⌘↵ new session";
  chipRow.appendChild(chip);
  body.appendChild(chipRow);

  const newSession = document.createElement("button");
  newSession.className = "cl-new-session";
  newSession.textContent = "New session";
  newSession.addEventListener("click", (e) => { e.stopPropagation(); launchHarnessTile(ctx, tile); });
  body.appendChild(newSession);

  const yoloRow = document.createElement("label");
  yoloRow.className = "cl-yolo-row";
  yoloRow.addEventListener("click", (e) => e.stopPropagation());
  const yoloBox = document.createElement("input");
  yoloBox.type = "checkbox";
  yoloBox.checked = launcherYolo(tile.adapterName);
  yoloBox.addEventListener("change", () => localStorage.setItem(launcherYoloKey(tile.adapterName), String(yoloBox.checked)));
  const yoloLabel = document.createElement("span");
  yoloLabel.textContent = "skip permissions";
  yoloRow.appendChild(yoloBox);
  yoloRow.appendChild(yoloLabel);
  body.appendChild(yoloRow);

  const recentRows = launcherRecents.filter((r) => r.adapter === tile.adapterName);
  const shaped = shapeRecentSessions(recentRows, Date.now(), 8);
  if (shaped.length > 0) {
    const dd = document.createElement("div");
    dd.className = "cl-resume-dd";
    const trigger = document.createElement("button");
    trigger.className = "cl-resume-trigger";
    const dot = document.createElement("span");
    dot.className = "cl-session-dot";
    dot.style.background = adapterAccent(tile.adapterName, tile.accent);
    const triggerLabel = document.createElement("span");
    triggerLabel.className = "cl-resume-label";
    triggerLabel.textContent = "Resume session";
    const count = document.createElement("span");
    count.className = "cl-resume-count";
    count.textContent = String(shaped.length);
    const chev = document.createElement("span");
    chev.className = "cl-resume-chev";
    chev.textContent = "▾";
    trigger.appendChild(dot);
    trigger.appendChild(triggerLabel);
    trigger.appendChild(count);
    trigger.appendChild(chev);
    dd.appendChild(trigger);
    const menu = document.createElement("div");
    menu.className = "cl-resume-menu";
    for (const s of shaped) {
      const row = document.createElement("div");
      row.className = "cl-recent-row";
      const base = document.createElement("span");
      base.className = "cl-recent-cwd";
      base.textContent = s.label;
      base.title = s.label;
      const when = document.createElement("span");
      when.className = "cl-recent-when";
      when.textContent = s.relative;
      row.appendChild(base);
      row.appendChild(when);
      row.addEventListener("click", (e) => { e.stopPropagation(); void resumeRecentSession(s.adapter, s.sessionId, s.cwd, tile.label); });
      menu.appendChild(row);
    }
    dd.appendChild(menu);
    trigger.addEventListener("click", (e) => {
      e.stopPropagation();
      dd.classList.toggle("open");
    });
    body.appendChild(dd);
  }
  return body;
}

let inspectActive = false;

function startInspectMode(): void {
  if (inspectActive) return;
  inspectActive = true;
  const overlay = document.createElement("div");
  overlay.id = "inspect-overlay";
  const hi = document.createElement("div");
  hi.className = "inspect-highlight";
  const tagEl = document.createElement("div");
  tagEl.className = "inspect-tag";
  tagEl.textContent = "inspect mode — click an element, drag a region, esc to exit";
  overlay.appendChild(hi);
  overlay.appendChild(tagEl);
  document.body.appendChild(overlay);
  tagEl.style.left = "50%";
  tagEl.style.top = "10px";
  tagEl.style.transform = "translateX(-50%)";

  let dragStart: { x: number; y: number } | null = null;
  let marquee = false;
  let marqueeRect = { x: 0, y: 0, width: 0, height: 0 };

  const pick = (x: number, y: number): Element | null => {
    const els = document.elementsFromPoint(x, y);
    return els.find((el) => el !== overlay && !overlay.contains(el)) ?? null;
  };
  const placeHighlight = (r: { left: number; top: number; width: number; height: number }) => {
    hi.style.left = `${r.left}px`;
    hi.style.top = `${r.top}px`;
    hi.style.width = `${r.width}px`;
    hi.style.height = `${r.height}px`;
  };
  const placeTag = (text: string, x: number, y: number) => {
    tagEl.style.transform = "none";
    tagEl.textContent = text;
    tagEl.style.left = `${Math.max(4, x)}px`;
    tagEl.style.top = `${Math.max(4, y - 22)}px`;
  };
  const onMove = (e: MouseEvent) => {
    if (dragStart && (Math.abs(e.clientX - dragStart.x) > 8 || Math.abs(e.clientY - dragStart.y) > 8)) marquee = true;
    if (marquee && dragStart) {
      const left = Math.min(dragStart.x, e.clientX);
      const top = Math.min(dragStart.y, e.clientY);
      const width = Math.abs(e.clientX - dragStart.x);
      const height = Math.abs(e.clientY - dragStart.y);
      marqueeRect = { x: left, y: top, width, height };
      placeHighlight({ left, top, width, height });
      placeTag("region", left, top);
      return;
    }
    const el = pick(e.clientX, e.clientY);
    if (!el) return;
    const r = el.getBoundingClientRect();
    placeHighlight(r);
    placeTag(cssPath(el as unknown as import("./inspect-mode").InspectElementLike, 3), r.left, r.top);
  };
  const teardown = () => {
    inspectActive = false;
    overlay.remove();
    document.removeEventListener("keydown", onKey, true);
  };
  const onKey = (e: KeyboardEvent) => {
    if (e.key === "Escape") { e.preventDefault(); e.stopPropagation(); teardown(); }
  };
  overlay.addEventListener("mousemove", onMove);
  overlay.addEventListener("mousedown", (e) => { dragStart = { x: e.clientX, y: e.clientY }; });
  overlay.addEventListener("mouseup", (e) => {
    const wasMarquee = marquee;
    const start = dragStart;
    dragStart = null;
    marquee = false;
    if (wasMarquee && start) {
      teardown();
      openInspectNote(null, { ...marqueeRect });
      return;
    }
    const el = pick(e.clientX, e.clientY);
    teardown();
    openInspectNote(el, null);
  });
  document.addEventListener("keydown", onKey, true);
}

function inspectTargetOf(el: Element): import("./inspect-mode").InspectTarget {
  const r = el.getBoundingClientRect();
  return {
    selector: cssPath(el as unknown as import("./inspect-mode").InspectElementLike),
    tag: el.tagName.toLowerCase(),
    classes: [...el.classList],
    rect: { x: Math.round(r.left), y: Math.round(r.top), width: Math.round(r.width), height: Math.round(r.height) },
    textExcerpt: (el.textContent ?? "").trim().slice(0, 120),
  };
}

function openInspectNote(el: Element | null, regionRect: { x: number; y: number; width: number; height: number } | null): void {
  const panel = document.createElement("div");
  panel.className = "inspect-note";
  const target = el ? inspectTargetOf(el) : null;
  const summary = document.createElement("div");
  summary.className = "inspect-note-summary";
  summary.textContent = target ? target.selector : `region ${regionRect?.width}×${regionRect?.height}`;
  panel.appendChild(summary);
  const ta = document.createElement("textarea");
  ta.className = "inspect-note-input";
  ta.placeholder = "what's wrong here?";
  panel.appendChild(ta);
  const row = document.createElement("div");
  row.className = "inspect-send-row";
  const harnesses = detectedHarnessTiles(buildAdapterTiles(launcherAdapters));
  for (const tile of harnesses) {
    const accent = adapterAccent(tile.adapterName, tile.accent);
    const btn = document.createElement("button");
    btn.className = "inspect-btn inspect-send";
    btn.style.setProperty("--card-accent", accent);
    btn.textContent = `Send to ${tile.label}`;
    btn.addEventListener("click", () => {
      void submitInspectFeedback(ta.value, el, target, regionRect, tile);
      panel.remove();
    });
    row.appendChild(btn);
  }
  const save = document.createElement("button");
  save.className = "inspect-btn";
  save.textContent = "Save report";
  save.addEventListener("click", () => {
    void submitInspectFeedback(ta.value, el, target, regionRect, null);
    panel.remove();
  });
  row.appendChild(save);
  const cancel = document.createElement("button");
  cancel.className = "inspect-btn inspect-cancel";
  cancel.textContent = "Cancel";
  cancel.addEventListener("click", () => panel.remove());
  row.appendChild(cancel);
  panel.appendChild(row);
  document.body.appendChild(panel);
  const anchor = target?.rect ?? regionRect;
  const px = Math.min(window.innerWidth - 360, Math.max(8, (anchor?.x ?? 100)));
  const py = Math.min(window.innerHeight - 220, Math.max(8, (anchor?.y ?? 100) + (anchor?.height ?? 0) + 8));
  panel.style.left = `${px}px`;
  panel.style.top = `${py}px`;
  ta.focus();
}

async function submitInspectFeedback(
  note: string,
  el: Element | null,
  target: import("./inspect-mode").InspectTarget | null,
  regionRect: { x: number; y: number; width: number; height: number } | null,
  harness: LauncherTile | null,
): Promise<void> {
  const trimmed = note.trim() || "(no note)";
  const report = buildFeedbackReport({
    note: trimmed,
    target,
    regionRect,
    workspace: layout?.name ?? "",
    room: activeRoom()?.name ?? "",
    appVersion: document.getElementById("wordmark-ver")?.textContent ?? "dev",
    htmlExcerpt: el instanceof HTMLElement ? el.outerHTML : "",
    nowIso: new Date().toISOString(),
  });
  try {
    const res = await invoke<{ path: string }>("app.feedbackSave", { json: JSON.stringify(report, null, 2), slug: feedbackSlug(trimmed) });
    if (harness) {
      await spawnFeedbackAgent(harness, harnessPrompt(report, res.path), `Fix: ${feedbackSlug(trimmed)}`);
    } else {
      console.warn("feedback report saved", res.path);
    }
  } catch (err) { console.warn("feedback save failed", err); }
}

async function spawnFeedbackAgent(tile: LauncherTile, prompt: string, roomName: string): Promise<void> {
  const launch = await buildAdapterLaunch({ name: tile.adapterName, displayName: tile.label, accent: tile.accent, binary: tile.binary });
  const sp = (await invoke<{ paneId: string }>("app.paneSpawn", { command: launch.command, args: [...launch.args, prompt], cwd: "", inheritCwdFrom: "", cols: 80, rows: 24, adapter: tile.adapterName, agentName: tile.label, workspace: "", room: "" })).paneId;
  const r = await invoke<{ roomId: string }>("app.layoutMutate", { op: "createRoom", newPaneId: sp, name: roomName, roomId: "", targetPaneId: "", orientation: "", paneId: "", dir: 0, paneType: "terminal" });
  activeRoomId = r.roomId;
  await reload();
  focusPane(sp);
}

function renderToolTile(ctx: LauncherContext, tile: LauncherTile, letter: string, selected: boolean): HTMLElement {
  const id = tile.id.replace("builtin:", "");
  const accent = toolAccent(id);
  const el = document.createElement("div");
  el.className = "cl-tool" + (selected ? " selected" : "");
  el.style.setProperty("--tool-accent", accent);
  const ic = document.createElement("span");
  ic.className = "cl-tool-ic";
  ic.innerHTML = iconSvg(id);
  ic.style.color = accent;
  const lbl = document.createElement("span");
  lbl.className = "cl-tool-lbl";
  lbl.textContent = tile.label;
  const key = document.createElement("span");
  key.className = "cl-tool-key";
  key.textContent = letter;
  el.appendChild(key);
  el.appendChild(ic);
  el.appendChild(lbl);
  el.addEventListener("click", () => launchToolTile(ctx, tile));
  return el;
}

let resolvedBindings: ResolvedBinding[] = defaultBindings();
let chordMap = buildChordMap(resolvedBindings);
let menuChords = menuChordSet(bindingsAsActionChords());
const menuIdToAction = new Map<string, string>();

function bindingsAsActionChords(): { action: string; chord: string }[] {
  return resolvedBindings.map((b) => ({ action: b.action, chord: b.chord }));
}

async function reloadKeymap(): Promise<void> {
  const merged = new Map<string, ResolvedBinding>();
  for (const b of defaultBindings()) merged.set(normalizeChordStr(b.chord), b);
  try {
    const res = await invoke<{ bindings: { chord: string; actionType: string; action: string }[] }>("cove://commands/keybind.list", {});
    for (const b of res.bindings ?? []) merged.set(normalizeChordStr(b.chord), { chord: b.chord, actionType: b.actionType, action: b.action });
  } catch (e) {
    console.warn("keybind.list unavailable, using default keymap", e);
  }
  resolvedBindings = [...merged.values()];
  chordMap = buildChordMap(resolvedBindings);
  menuChords = menuChordSet(bindingsAsActionChords());
  refreshMenu();
}

window.addEventListener("keydown", (e) => {
  const chord = eventToChord({ metaKey: e.metaKey, ctrlKey: e.ctrlKey, altKey: e.altKey, shiftKey: e.shiftKey, key: e.key });
  if (!chord) return;
  const decision = resolveDispatch(chord, chordMap, menuChords);
  const dispatchable = decision.kind === "dispatch" || (RYN_MENUBAR_EVENTS_BROKEN && decision.kind === "menu-owned");
  if (!dispatchable) return;
  if (paletteEl.classList.contains("open") && decision.action !== "tool.palette") return;
  e.preventDefault();
  runAction(decision.action);
}, true);

window.addEventListener("resize", () => fitAll());

async function openToolRoom(paneType: string, name: string): Promise<void> {
  try {
    const sp = (await invoke<{ paneId: string }>("app.paneSpawn", { command: "", cwd: "", inheritCwdFrom: "", cols: 80, rows: 24, adapter: "", agentName: "", workspace: "", room: "" })).paneId;
    const r = await invoke<{ roomId: string }>("app.layoutMutate", { op: "createRoom", newPaneId: sp, name, roomId: "", targetPaneId: "", orientation: "", paneId: "", dir: 0, paneType });
    activeRoomId = r.roomId;
    await reload();
    focusPane(sp);
  } catch (e) { console.warn("openToolRoom failed", paneType, e); }
}

function scrollActivePane(toTop: boolean): void {
  if (!focusedPaneId) { console.warn("scroll requested with no focused pane"); return; }
  const pv = panes.get(focusedPaneId);
  if (!pv) { console.warn("scroll requested for unknown pane", focusedPaneId); return; }
  if (toTop) pv.term.scrollToTop();
  else pv.term.scrollToBottom();
}

function nextRoom(dir: number): void {
  const rooms = layout?.rooms ?? [];
  if (rooms.length === 0) { console.warn("room cycle requested with no rooms"); return; }
  const idx = rooms.findIndex((r) => r.id === activeRoomId);
  const next = rooms[((idx < 0 ? 0 : idx) + dir + rooms.length) % rooms.length];
  activeRoomId = next.id;
  const f = firstLeafOf(next);
  if (f) { focusedPaneId = f; renderRoom(); renderSidebar(); renderRoomTabs(); focusPane(f); }
}

function pinActiveRoom(): void {
  if (!activeRoomId) { console.warn("pin requested with no active room"); return; }
  if (pinnedRoomIds.has(activeRoomId)) pinnedRoomIds.delete(activeRoomId);
  else pinnedRoomIds.add(activeRoomId);
  savePinnedRooms();
  renderRoomTabs();
}

const wsCreateEl = document.getElementById("ws-create")!;
const wscNameEl = document.getElementById("wsc-name") as HTMLInputElement;
const wscPathEl = document.getElementById("wsc-path") as HTMLInputElement;
const wscErrorEl = document.getElementById("wsc-error")!;

function closeWorkspaceDialog(): void {
  wsCreateEl.classList.remove("open");
}

function newWorkspace(): void {
  wscNameEl.value = "";
  wscPathEl.value = "";
  wscErrorEl.textContent = "";
  wsCreateEl.classList.add("open");
  wscNameEl.focus();
}

async function browseWorkspaceDir(): Promise<void> {
  try {
    const typed = wscPathEl.value.trim();
    const initial = typed.startsWith("/") ? typed : (activeProjectDir() || "/");
    const picked = await window.__ryn.invoke("dialog.openFolder", { initialPath: initial });
    const path = typeof picked === "string" ? picked.trim() : "";
    if (path) wscPathEl.value = path;
    else if (picked === null) console.info("folder picker cancelled");
    else console.warn("folder picker returned nothing", initial, picked);
  } catch (e) { console.warn("folder picker failed", e); }
}

async function submitWorkspaceDialog(): Promise<void> {
  const name = wscNameEl.value.trim();
  const path = wscPathEl.value.trim();
  if (!name) { wscErrorEl.textContent = "Name is required."; wscNameEl.focus(); return; }
  if (!path) { wscErrorEl.textContent = "Directory is required."; wscPathEl.focus(); return; }
  try {
    await invoke("cove://commands/workspace.create", { name, projectDir: path, collectionId: "" });
    closeWorkspaceDialog();
    await loadWorkspaceBoxes();
    await reload();
  } catch (e) {
    console.warn("workspace.create failed", e);
    wscErrorEl.textContent = "Could not create workspace at that directory.";
  }
}

document.getElementById("wsc-close")!.addEventListener("click", closeWorkspaceDialog);
document.getElementById("wsc-browse")!.addEventListener("click", () => void browseWorkspaceDir());
document.getElementById("wsc-create")!.addEventListener("click", () => void submitWorkspaceDialog());
wsCreateEl.addEventListener("mousedown", (e) => { if (e.target === wsCreateEl) closeWorkspaceDialog(); });
wsCreateEl.addEventListener("keydown", (e) => {
  if (e.key === "Escape") { e.stopPropagation(); closeWorkspaceDialog(); }
  else if (e.key === "Enter") { e.stopPropagation(); void submitWorkspaceDialog(); }
});

async function switchWorkspaceByIndex(n: number): Promise<void> {
  try {
    const res = await invoke<{ workspaces: { id: string }[] }>("cove://commands/workspace.list", {});
    const ws = (res.workspaces ?? [])[n - 1];
    if (!ws) { console.warn("no workspace at index", n); return; }
    await switchWorkspace(ws.id);
  } catch (e) { console.warn("workspace switch by index failed", e); }
}

let zenState: ZenState = initialZenState();
function currentChrome(): ChromeVisibility {
  return {
    leftSidebarHidden: collapsedOf(sidebarModel, "left"),
    rightSidebarHidden: collapsedOf(sidebarModel, "right"),
  };
}
function applyChrome(v: ChromeVisibility): void {
  sidebarModel = setCollapsed(sidebarModel, "left", v.leftSidebarHidden);
  sidebarModel = setCollapsed(sidebarModel, "right", v.rightSidebarHidden);
  persistSidebarModel();
  applySidebarModel();
}
function doToggleZen(): void {
  const t = toggleZen(zenState, currentChrome());
  zenState = t.state;
  document.body.classList.toggle("zen-mode", zenState.active);
  applyChrome(t.visibility);
  fitAll();
}

const perfHudEl = document.getElementById("perf-hud")!;
let perfHudState: HudState = initHud();
let perfHudRaf: number | null = null;

function readJsHeapProbe(): JsHeapProbe | null {
  const probe = (performance as unknown as { memory?: JsHeapProbe }).memory;
  return probe ?? null;
}

function renderPerfHud(): void {
  const lines = hudLines(hudMetrics(perfHudState), readJsHeapBytes(readJsHeapProbe()));
  perfHudEl.innerHTML = "";
  for (const line of lines) {
    const row = document.createElement("div");
    row.className = "hud-row";
    const label = document.createElement("span");
    label.className = "hud-label";
    label.textContent = line.label;
    const value = document.createElement("span");
    value.className = "hud-value";
    value.textContent = line.value;
    row.appendChild(label);
    row.appendChild(value);
    perfHudEl.appendChild(row);
  }
  const caption = document.createElement("div");
  caption.className = "hud-caption";
  caption.textContent = "GUI render loop (requestAnimationFrame); JS heap from the webview.";
  perfHudEl.appendChild(caption);
}

function perfHudFrame(ts: number): void {
  perfHudState = recordFrame(perfHudState, ts);
  renderPerfHud();
  perfHudRaf = perfHudState.enabled ? requestAnimationFrame(perfHudFrame) : null;
}

function doTogglePerfHud(): void {
  perfHudState = toggleHud(perfHudState);
  perfHudEl.classList.toggle("open", perfHudState.enabled);
  if (perfHudState.enabled) {
    renderPerfHud();
    if (perfHudRaf === null) perfHudRaf = requestAnimationFrame(perfHudFrame);
  }
  if (settingsEl.classList.contains("open") && activeSettingsTab === "diagnostics") renderSettings();
}

function runAction(action: string): void {
  if (action.startsWith("workspace.switch-")) {
    const n = Number(action.slice("workspace.switch-".length));
    if (Number.isFinite(n)) void switchWorkspaceByIndex(n);
    return;
  }
  switch (action) {
    case "room.new": void newRoom(); break;
    case "room.close": if (activeRoomId) void closeRoom(activeRoomId); break;
    case "room.next": nextRoom(1); break;
    case "room.prev": nextRoom(-1); break;
    case "room.pin": pinActiveRoom(); break;
    case "room.omni-jump": openPalette(); break;
    case "pane.close": void closeFocused(); break;
    case "pane.split-right": void splitActive("row"); break;
    case "pane.split-down": void splitActive("col"); break;
    case "pane.focus-next": cycleFocus(1); break;
    case "pane.focus-prev": cycleFocus(-1); break;
    case "pane.find": openFind(); break;
    case "pane.scroll-top": scrollActivePane(true); break;
    case "pane.scroll-bottom": scrollActivePane(false); break;
    case "pane.maximize": void toggleZoom(); break;
    case "workspace.create": void newWorkspace(); break;
    case "view.toggle-sidebar": toggleLeftSidebar(); break;
    case "view.toggle-notepad": revealSidebarMode("notepad"); break;
    case "view.zen-mode": doToggleZen(); break;
    case "view.zoom-in": settings.fontSize = Math.min(24, settings.fontSize + 1); applySettings(); break;
    case "view.zoom-out": settings.fontSize = Math.max(9, settings.fontSize - 1); applySettings(); break;
    case "view.zoom-reset": settings.fontSize = 13; applySettings(); break;
    case "view.toggle-backdrop": void toggleBackdrop(); break;
    case "tool.inspect": startInspectMode(); break;
    case "tool.git": void openToolRoom("git", "Source Control"); break;
    case "tool.search": void openToolRoom("search", "Search"); break;
    case "tool.tasks": void openToolRoom("tasks-list", "Tasks"); break;
    case "tool.library": void openToolRoom("library", "Library"); break;
    case "tool.browser": void newBrowserRoom("https://duckduckgo.com"); break;
    case "tool.notepad": revealSidebarMode("notepad"); break;
    case "tool.palette": paletteEl.classList.contains("open") ? closePalette() : openPalette(); break;
    case "tool.launcher": launcherEl.classList.contains("open") ? closeLauncher() : openLauncher(); break;
    case "app.settings": openSettings(); break;
    case "app.zoom-in": appZoom += 0.1; applyAppZoom(); break;
    case "app.zoom-out": appZoom -= 0.1; applyAppZoom(); break;
    case "app.update": activeSettingsTab = "updates"; openSettings(); break;
    default: console.warn("unhandled keymap action", action); break;
  }
}

function refreshMenu(): void {
  const menu = buildMenu(bindingsAsActionChords(), RYN_MENUBAR_EVENTS_BROKEN);
  menuIdToAction.clear();
  for (const section of menu) {
    for (const item of section.items ?? []) {
      if (item.id && item.action) menuIdToAction.set(item.id, item.action);
    }
  }
  invoke("menubar.setMenu", { items: menu }).catch(() => void 0);
}

function setupMenuBar(): void {
  window.__ryn.on("menubar.itemClicked", (data: unknown) => {
    const id = data as string;
    if (!id) return;
    const action = menuIdToAction.get(id);
    if (!action) { console.warn("menu item without an action", id); return; }
    runAction(action);
  });
  refreshMenu();
}

let clusterUpdateStaged = false;
function renderTitleCluster(): void {
  const cluster = document.getElementById("tb-cluster");
  const right = document.getElementById("tb-right");
  if (!cluster || !right) { console.warn("title cluster containers missing"); return; }
  cluster.innerHTML = "";
  right.innerHTML = "";
  for (const tool of clusterTools({ updateStaged: clusterUpdateStaged })) {
    if (tool.id === "find-anything") {
      const find = document.createElement("div");
      find.className = "tb-find-anything";
      find.title = tool.title;
      const ic = document.createElement("span");
      ic.className = "tb-find-ic";
      ic.innerHTML = iconSvg("search");
      ic.style.display = "inline-flex";
      const ph = document.createElement("span");
      ph.className = "tb-find-ph";
      ph.textContent = "find anything…";
      find.setAttribute("data-webview-ignore", "");
      find.appendChild(ic);
      find.appendChild(ph);
      find.addEventListener("click", (e) => { e.stopPropagation(); runAction(tool.action); });
      cluster.appendChild(find);
    } else {
      if (tool.id === "zoom-in") {
        const pct = document.createElement("div");
        pct.id = "tb-zoom-label";
        pct.title = "Current app zoom — click to reset";
        pct.setAttribute("data-webview-ignore", "");
        pct.textContent = `${Math.round(appZoom * 100)}%`;
        pct.addEventListener("click", (e) => { e.stopPropagation(); appZoom = 1; applyAppZoom(); });
        right.appendChild(pct);
      }
      const btn = document.createElement("div");
      btn.className = "tbtn tb-cluster-btn" + (tool.id === "update" ? " tb-update" : "");
      btn.title = tool.title;
      btn.setAttribute("data-webview-ignore", "");
      const btnIcon: Record<string, string> = { settings: "gear", inspect: "inspect", "zoom-in": "plus", "zoom-out": "minus", update: "refresh" };
      btn.innerHTML = iconSvg(btnIcon[tool.id] ?? "gear");
      btn.addEventListener("click", (e) => { e.stopPropagation(); runAction(tool.action); });
      right.appendChild(btn);
    }
  }
}

let appZoom = (() => {
  const stored = parseFloat(localStorage.getItem("cove.appZoom") ?? "1");
  return Number.isFinite(stored) && stored > 0 ? stored : 1;
})();

function applyAppZoom(): void {
  appZoom = Math.min(1.5, Math.max(0.7, Math.round(appZoom * 10) / 10));
  (document.body.style as CSSStyleDeclaration & { zoom: string }).zoom = String(appZoom);
  localStorage.setItem("cove.appZoom", String(appZoom));
  const label = document.getElementById("tb-zoom-label");
  if (label) label.textContent = `${Math.round(appZoom * 100)}%`;
  fitAll();
}

function setupTitleCluster(): void {
  renderTitleCluster();
}

interface UpdateInfoDto { version: string; releaseUrl?: string; assetUrl?: string; signatureUrl?: string; releaseNotes?: string; }

let updateState: UpdateState = { kind: "idle" };

function dispatchUpdate(event: UpdateEvent): UpdateState {
  updateState = nextUpdateState(updateState, event);
  clusterUpdateStaged = updateAffordanceVisible(updateState);
  renderTitleCluster();
  if (settingsEl.classList.contains("open") && activeSettingsTab === "updates") renderUpdatesButton();
  return updateState;
}

async function runUpdateCheck(): Promise<void> {
  dispatchUpdate({ type: "check" });
  try {
    const raw = (await window.__ryn.invoke("updater.check", {})) as string;
    const info = raw && raw !== "null" ? (JSON.parse(raw) as UpdateInfoDto) : null;
    if (!info || !info.version) { dispatchUpdate({ type: "checkedUpToDate" }); return; }
    updateLatest = info;
    dispatchUpdate({ type: "checkedAvailable", version: info.version, notes: info.releaseUrl ?? null });
  } catch (err) {
    console.warn("updater.check failed", err);
    dispatchUpdate({ type: "error", message: String(err) });
  }
}

async function runUpdateDownload(): Promise<void> {
  dispatchUpdate({ type: "download" });
  try {
    const handle = (await window.__ryn.invoke("updater.download", {})) as string;
    if (!handle) { console.warn("updater.download returned no handle"); dispatchUpdate({ type: "error", message: "no download handle" }); return; }
    const version = updateLatest?.version ?? "";
    dispatchUpdate({ type: "downloaded", handle, version });
  } catch (err) {
    console.warn("updater.download failed", err);
    dispatchUpdate({ type: "error", message: String(err) });
  }
}

async function runUpdateApply(handle: string): Promise<void> {
  dispatchUpdate({ type: "apply" });
  try {
    void window.__ryn.invoke("updater.apply", { downloadHandle: handle });
  } catch (err) {
    console.warn("updater.apply failed", err);
    dispatchUpdate({ type: "error", message: String(err) });
  }
}

let updateLatest: UpdateInfoDto | null = null;

function onUpdateButton(): void {
  const s = updateState;
  if (s.kind === "idle" || s.kind === "upToDate") { void runUpdateCheck(); return; }
  if (s.kind === "failed") { dispatchUpdate({ type: "retry" }); void runUpdateCheck(); return; }
  if (s.kind === "available") { void runUpdateDownload(); return; }
  if (s.kind === "readyToApply") { void runUpdateApply(s.handle); return; }
}

function currentAppVersion(): string {
  const raw = document.getElementById("wordmark-ver")?.textContent ?? "";
  return raw.replace(/^v/, "").trim() || "dev";
}

function updateStatusText(): string {
  const cur = `Current version ${currentAppVersion()}`;
  const s = updateState;
  if (s.kind === "checking") return `${cur} · checking…`;
  if (s.kind === "upToDate") return `${cur} · you are on the latest release`;
  if (s.kind === "available") return `${cur} · ${s.version} available`;
  if (s.kind === "downloading") return `${cur} · downloading ${updateLatest?.version ?? "update"}…`;
  if (s.kind === "readyToApply") return `${cur} · ${s.version} downloaded — restart to apply`;
  if (s.kind === "applying") return `${cur} · applying update — the app will restart`;
  if (s.kind === "failed") return `${cur} · update failed: ${s.message}`;
  return cur;
}

function updateButtonBusy(state: UpdateState): boolean {
  return state.kind === "checking" || state.kind === "downloading" || state.kind === "applying";
}

function renderUpdatesButton(): void {
  const btn = document.getElementById("cove-update-btn") as HTMLButtonElement | null;
  const status = document.getElementById("cove-update-status");
  const notes = document.getElementById("cove-update-notes") as HTMLAnchorElement | null;
  if (btn) { btn.textContent = updateButtonLabel(updateState); btn.disabled = updateButtonBusy(updateState); }
  if (status) status.textContent = updateStatusText();
  if (notes) {
    const href = updateLatest?.releaseUrl ?? "";
    const show = (updateState.kind === "available" || updateState.kind === "readyToApply") && href.length > 0;
    notes.style.display = show ? "inline" : "none";
    if (show) notes.href = href;
  }
}

function renderUpdatesExtras(container: HTMLElement): void {
  container.appendChild(diagnosticsSectionHeader("Software updates"));
  const row = document.createElement("div");
  row.className = "set-row";
  row.style.cssText = "display:flex;flex-direction:column;align-items:flex-start;gap:8px;";

  const btn = document.createElement("button");
  btn.id = "cove-update-btn";
  btn.className = "set-btn";
  btn.style.cssText = "padding:6px 14px;border:1px solid var(--border);border-radius:6px;background:var(--accent);color:#fff;cursor:pointer;font-size:12px;";
  btn.addEventListener("click", (e) => { e.stopPropagation(); onUpdateButton(); });
  row.appendChild(btn);

  const status = document.createElement("span");
  status.id = "cove-update-status";
  status.className = "set-desc";
  status.style.cssText = "color:var(--muted);font-size:11px;";
  row.appendChild(status);

  const notes = document.createElement("a");
  notes.id = "cove-update-notes";
  notes.className = "set-desc";
  notes.textContent = "View release notes";
  notes.target = "_blank";
  notes.rel = "noreferrer";
  notes.style.cssText = "color:var(--accent);font-size:11px;text-decoration:underline;display:none;";
  row.appendChild(notes);

  container.appendChild(row);
  renderUpdatesButton();
}

const engineEventHandlers = new Map<string, (payload: unknown) => void>();

function onNeedsInputChanged(): void {
  const count = needsInputPanes.size;
  if (count === 0) invoke("badge.clear", {}).catch(() => void 0);
  else invoke("badge.setCount", count).catch(() => void 0);
  if (agentsVisible()) renderSidebarContent("left");
}

function setupBadge(): void {
  engineEventHandlers.set("dock.badge", (payload) => {
    const evt = payload as { paneId?: string };
    if (evt?.paneId) { needsInputPanes.add(evt.paneId); onNeedsInputChanged(); }
  });
  engineEventHandlers.set("needs-input.clear", (payload) => {
    const evt = payload as { paneId?: string };
    if (evt?.paneId) { needsInputPanes.delete(evt.paneId); onNeedsInputChanged(); }
  });
  engineEventHandlers.set("dock.badge.clear", () => { needsInputPanes.clear(); onNeedsInputChanged(); });
  engineEventHandlers.set("state.changed", () => { if (agentsVisible()) void refreshAgents(); });
  engineEventHandlers.set("restore.summary", (payload) => {
    const p = payload as { restored?: number; fresh?: number; skipped?: number };
    const text = restoredSummaryText(p.restored ?? 0, p.fresh ?? 0, p.skipped ?? 0);
    if (text) showInAppToast("Sessions restored", text, () => {});
  });
}

let backdropMaterial: BackdropMaterial = "none";
const backdropDeps: BackdropDeps = {
  getBackdrop: () => window.__ryn.invoke("window.getBackdrop", {}),
  setBackdrop: async (material) => { await window.__ryn.invoke("window.setBackdrop", { material }); },
  loadPref: async () => {
    try { const res = await invoke<{ ok: boolean; value?: string }>("app.configGet", { key: BACKDROP_PREF_KEY }); return res.ok ? res.value ?? null : null; }
    catch { return null; }
  },
  savePref: async (material) => { await invoke("app.configSet", { key: BACKDROP_PREF_KEY, value: material }).catch((e) => console.warn("backdrop configSet failed", e)); },
  applyClass: (translucent) => { document.body.classList.toggle("backdrop-translucent", translucent); },
  warn: (message) => console.warn(message),
};
async function setupBackdrop(): Promise<void> {
  try { backdropMaterial = coerceMaterial(await initBackdrop(backdropDeps)); }
  catch (e) { console.warn("backdrop init failed", e); }
}
async function toggleBackdrop(): Promise<void> {
  const next = nextToggleMaterial(backdropMaterial);
  backdropMaterial = coerceMaterial(await setBackdropMaterial(next, backdropDeps));
}

function revealPane(paneId: string): void {
  if (!layout) return;
  const room = layout.rooms.find((r) => findLeafId(r.layoutTree, paneId) !== null);
  if (!room) { console.warn("notification reveal: no room for pane", paneId); return; }
  if (activeRoomId !== room.id) {
    activeRoomId = room.id;
    renderRoom();
    renderRoomTabs();
    renderSidebar();
  }
  const leaf = findLeafId(room.layoutTree, paneId) ?? paneId;
  focusPane(leaf);
}

function toastHost(): HTMLElement {
  let host = document.getElementById("toast-host");
  if (!host) {
    host = document.createElement("div");
    host.id = "toast-host";
    document.body.appendChild(host);
  }
  return host;
}

function showInAppToast(title: string, body: string, onClick: () => void): void {
  const host = toastHost();
  const toast = document.createElement("div");
  toast.className = "toast";
  toast.setAttribute("role", "status");
  const t = document.createElement("div");
  t.className = "toast-title";
  t.textContent = title;
  toast.appendChild(t);
  if (body) {
    const b = document.createElement("div");
    b.className = "toast-body";
    b.textContent = body;
    toast.appendChild(b);
  }
  let dismissed = false;
  const dismiss = () => {
    if (dismissed) return;
    dismissed = true;
    toast.classList.add("leaving");
    setTimeout(() => toast.remove(), 200);
  };
  toast.addEventListener("click", () => { onClick(); dismiss(); });
  host.appendChild(toast);
  requestAnimationFrame(() => toast.classList.add("in"));
  setTimeout(dismiss, 6000);
}

function setupNotifications(): void {
  const deps: NotificationBridgeDeps = {
    isPermissionGranted: () => invoke<boolean>("notification.isPermissionGranted", {}).catch(() => false),
    requestPermission: () => invoke<boolean>("notification.requestPermission", {}).catch(() => false),
    send: async (payload) => { await window.__ryn.invoke("notification.sendWithId", { id: payload.id, title: payload.title, body: payload.body }); },
    reveal: (paneId) => revealPane(paneId),
    toast: (payload) => showInAppToast(payload.title, payload.body, () => revealPane(payload.paneId)),
    warn: (message) => console.warn(message),
  };
  const bridge = new NotificationBridge(deps);
  engineEventHandlers.set("notification.deliver", (payload) => {
    const evt = payload as NotificationDeliverPayload | undefined;
    if (!evt?.id) { console.warn("notification.deliver: malformed payload"); return; }
    void bridge.deliver(evt);
  });
  window.__ryn.on("notification.activated", (data: unknown) => {
    const id = typeof data === "string" ? data : (data as { id?: string })?.id;
    if (id) bridge.onActivated(id);
  });
  window.__ryn.on("notification.dismissed", (data: unknown) => {
    const id = typeof data === "string" ? data : (data as { id?: string })?.id;
    if (id) bridge.onDismissed(id);
  });
}

window.__ryn.on("engine.event", (data: unknown) => {
  const evt = data as { channel?: string; payload?: unknown };
  if (evt?.channel === "config.changed") {
    const key = (evt.payload as { key?: string } | undefined)?.key;
    if (key) {
      if (key.startsWith("appearance.")) { void applyAppearance(key); }
      if (key.startsWith("terminal.")) { void loadSettings().then((s) => { settings = s; applySettings(); }); }
      if (settingsEl.classList.contains("open")) { renderSettings(); }
    }
  }
  if (evt?.channel === "browser.automation.exec") {
    void handleAutomationExec(evt.payload as AutomationExecEvent);
  }
  if (evt?.channel) {
    engineEventHandlers.get(evt.channel)?.(evt.payload);
  }
});

async function handleAutomationExec(ev: AutomationExecEvent): Promise<void> {
  if (!ev?.requestId) return;
  let resultJson: string;
  try {
    const webviewId = browserWebviewRegistry.get(ev.paneId);
    if (!webviewId) {
      resultJson = JSON.stringify({ ok: false, error: `no live webview for pane ${ev.paneId}` });
    } else if (ev.kind === "screenshot") {
      const png = await invoke<string>("webviewPane.screenshot", { id: webviewId });
      resultJson = JSON.stringify({ ok: true, png });
    } else if (ev.kind === "setUserAgent") {
      await invoke("webviewPane.setUserAgent", { id: webviewId, userAgent: ev.value ?? "" });
      resultJson = JSON.stringify({ ok: true });
    } else {
      const js = buildAutomationJs(ev);
      const raw = await invoke<string>("webviewPane.eval", { id: webviewId, code: js });
      resultJson = typeof raw === "string" && raw.length > 0 ? raw : JSON.stringify({ ok: true });
    }
  } catch (e) {
    resultJson = JSON.stringify({ ok: false, error: (e as Error).message });
  }
  try {
    await invoke("cove://commands/browser.automation.result", { requestId: ev.requestId, resultJson });
  } catch (e) {
    console.warn("automation result post failed", e);
  }
}

let notepadGroups: { workspaceId: string; workspaceName: string; notes: NoteListItem[] }[] = [];
let notepadNav: NavState = { groupIdx: -1, noteIdx: -1 };
let notepadLoaded = false;
const collapsedGroups = new Set<string>(JSON.parse(localStorage.getItem("cove.notepad.collapsedGroups") ?? "[]"));

function notepadVisible(): boolean {
  return sidebarModel.leftMode === "notepad" && !collapsedOf(sidebarModel, "left");
}
function rerenderNotepad(): void {
  if (notepadVisible()) renderSidebarContent("left");
}

async function loadNotepadNotes(): Promise<void> {
  try {
    const res = await invoke<{ notes: NoteListItem[] }>("cove://commands/note.list", { workspaceId: "default" });
    notepadGroups = groupByWorkspace(res.notes ?? [], { default: "Default" });
  } catch {
    notepadGroups = [];
  }
  notepadLoaded = true;
  rerenderNotepad();
}

function renderNotepadContent(container: HTMLElement): void {
  container.appendChild(sidebarHead("Notes", [{ icon: "+", title: "New note", run: () => void createNote() }]));
  const body = document.createElement("div");
  body.className = "sb-list ns-body";
  container.appendChild(body);
  container.tabIndex = 0;
  container.addEventListener("keydown", onNotepadKey);
  if (!notepadLoaded) { void loadNotepadNotes(); }

  if (notepadGroups.length === 0) {
    const empty = document.createElement("div");
    empty.className = "ns-empty";
    empty.innerHTML = `No notes yet<div class="ns-empty-action" id="ns-empty-create">Create a note</div>`;
    body.appendChild(empty);
    const createAction = empty.querySelector("#ns-empty-create");
    if (createAction) createAction.addEventListener("click", () => void createNote());
    return;
  }

  for (let gi = 0; gi < notepadGroups.length; gi++) {
    const group = notepadGroups[gi];
    const groupEl = document.createElement("div");
    groupEl.className = "ns-group" + (collapsedGroups.has(group.workspaceId) ? " collapsed" : "");

    const head = document.createElement("div");
    head.className = "ns-group-head";
    head.innerHTML = `<span class="chevron">\u25bc</span><span class="ns-group-name"></span><span class="ns-group-count"></span>`;
    head.querySelector(".ns-group-name")!.textContent = group.workspaceName;
    head.querySelector(".ns-group-count")!.textContent = String(group.notes.length);
    head.addEventListener("click", () => {
      if (collapsedGroups.has(group.workspaceId)) collapsedGroups.delete(group.workspaceId);
      else collapsedGroups.add(group.workspaceId);
      localStorage.setItem("cove.notepad.collapsedGroups", JSON.stringify([...collapsedGroups]));
      rerenderNotepad();
    });
    groupEl.appendChild(head);

    const notesEl = document.createElement("div");
    notesEl.className = "ns-group-notes";
    for (let ni = 0; ni < group.notes.length; ni++) {
      const note = group.notes[ni];
      const noteEl = document.createElement("div");
      const isSelected = gi === notepadNav.groupIdx && ni === notepadNav.noteIdx;
      noteEl.className = "ns-note" + (isSelected ? " selected" : "");
      noteEl.innerHTML = `<span class="ns-note-icon"></span><span class="ns-note-title"></span>`;
      const iconEl = noteEl.querySelector(".ns-note-icon") as HTMLElement;
      iconEl.textContent = kindIcon(note.kind);
      iconEl.style.color = kindColor(note.kind);
      noteEl.querySelector(".ns-note-title")!.textContent = note.title || "Untitled";
      noteEl.addEventListener("click", () => {
        notepadNav = { groupIdx: gi, noteIdx: ni };
        void openNoteInPane(note.id, note.workspaceId);
        rerenderNotepad();
      });
      notesEl.appendChild(noteEl);
    }
    groupEl.appendChild(notesEl);
    body.appendChild(groupEl);
  }
}

function onNotepadKey(e: KeyboardEvent): void {
  if (!notepadVisible()) return;
  if (e.key === "ArrowDown") { e.preventDefault(); notepadNav = moveSelection(notepadGroups, notepadNav, "down"); rerenderNotepad(); }
  else if (e.key === "ArrowUp") { e.preventDefault(); notepadNav = moveSelection(notepadGroups, notepadNav, "up"); rerenderNotepad(); }
  else if (e.key === "Enter") {
    e.preventDefault();
    const note = selectedNote(notepadGroups, notepadNav);
    if (note) void openNoteInPane(note.id, note.workspaceId);
  }
}

async function openNoteInPane(noteId: string, workspaceId: string): Promise<void> {
  try {
    const sp = (await invoke<{ paneId: string }>("app.paneSpawn", { command: "", cwd: "", inheritCwdFrom: "", cols: 80, rows: 24, adapter: "", agentName: "", workspace: "", room: "" })).paneId;
    const r = await invoke<{ roomId: string }>("app.layoutMutate", { op: "createRoom", newPaneId: sp, name: "Note", roomId: "", targetPaneId: "", orientation: "", paneId: "", dir: 0, paneType: "notepad" });
    activeRoomId = r.roomId;
    await reload();
    focusPane(sp);
    await waitForElement(".notepad-editor", 3000);
    await openNote(workspaceId, noteId);
  } catch { void 0; }
}

function waitForElement(selector: string, timeoutMs: number): Promise<HTMLElement | null> {
  return new Promise((resolve) => {
    const existing = document.querySelector<HTMLElement>(selector);
    if (existing) { resolve(existing); return; }
    const start = Date.now();
    const interval = setInterval(() => {
      const el = document.querySelector<HTMLElement>(selector);
      if (el) { clearInterval(interval); resolve(el); }
      else if (Date.now() - start > timeoutMs) { clearInterval(interval); resolve(null); }
    }, 50);
  });
}

async function createNote(): Promise<void> {
  try {
    await invoke("cove://commands/note.create", { title: "Untitled", workspaceId: "default", source: "user", content: "", kind: "markdown" });
    await loadNotepadNotes();
  } catch { void 0; }
}

(async () => {
  settings = await loadSettings();
  applySettings();
  void applyAppearance(null);
  await loadSidebarModel();
  applySidebarModel();
  setupMenuBar();
  void reloadKeymap();
  setupTitleCluster();
  applyAppZoom();
  try { await window.__ryn.invoke("window.center", {}); } catch (err) { console.warn("window center failed", err); }
  setupBadge();
  setupNotifications();
  void setupBackdrop();
  void loadWings();
  void loadWorkspaceBoxes();
  void loadLauncherAdapters();
  await reload();
  startAgentPolling();
  void maybeShowOnboarding();
})();
