import { restoredSummaryText, shouldShowRestoreToast } from "./restore-summary";
import { Terminal } from "@xterm/xterm";
import { FitAddon } from "@xterm/addon-fit";
import { CanvasAddon } from "@xterm/addon-canvas";
import { SearchAddon } from "@xterm/addon-search";
import { SerializeAddon } from "@xterm/addon-serialize";
import "@xterm/xterm/css/xterm.css";
import { decodeBase64Bytes, decodeRelayData, decodeTerminalRestoreBytes, toBase64Utf8, parseRelayText } from "./wsproto";
import { createKeyboardProtocolTracker, shiftEnterSequence, type KeyboardProtocolTracker } from "./terminal-keyboard";
import { isPaneFittable, scrollLineAfterFit, shouldResize, viewportScrollTopFor, type TermDims } from "./terminal-fit";
import { createStreamGenerations, processExitAction, replayViewportAction, shouldDisposeNook, shouldResetReplay, streamVisibilityAction } from "./stream-guard";
import { renderKanbanBoard } from "./tasks-kanban";
import { renderTaskList } from "./tasks-list";
import { renderTimelineFeed } from "./timeline-feed";
import { renderMarkdownNote } from "./markdown-note";
import { renderSketchNote } from "./sketch-note";
import { renderCanvasNote } from "./canvas-note";
import { renderHtmlNote } from "./html-note";
import { renderNotepadNook, openNote } from "./notepad-nook";
import { renderMermaidNote } from "./mermaid-note";
import { renderSessionPicker } from "./session-picker";
import { resumeSpawnPlan, type ResumeAction, type VaultResumeResult } from "./session-resume";
import { renderLibraryPopover } from "./library-popover";
import { renderSnapshotInspector } from "./snapshot-inspector";
import { renderDiffReviewNook } from "./diff-review-nook";
import { renderEditorNook } from "./editor-nook";
import { renderSourceControlNook } from "./source-control-nook";
import { renderSearchNook } from "./search-nook";
import { browserWebviewRegistry, closeBrowserWebview, reconcileBrowserBounds, renderBrowserNook } from "./browser-nook";
import { buildAutomationJs, type AutomationExecEvent } from "./automation-snapshot";
import { renderDiffViewerNook } from "./diff-viewer-nook";
import { renderMarkdownNook } from "./markdown-nook";
import { renderPdfNook } from "./pdf-nook";
import { renderVideoNook } from "./video-nook";
import { partitionPinned, reorderShore, glyphForNookType, visibleShoreIds, buildWingModel, filterShoresByWing } from "./shore-tabs";
import { groupByBay, moveSelection, selectedNote, kindIcon, kindColor, type NoteListItem, type NavState } from "./notepad-sidebar";
import { initialSidebarModel, selectLeftMode, toggleSide, setCollapsed, setWidth, collapsedOf, widthOf, SIDEBAR_MODES, SIDEBAR_RAIL_MODES, SIDEBAR_MODE_META, type SidebarModel, type SidebarSide, type SidebarMode } from "./sidebar-model";
import { nextBayName, type BayBoxInput } from "./bay-boxes";
import { clampMenuPosition, normalizeItems, firstSelectableIndex, moveSelection as ctxMoveSelection, activeItem, type ContextMenuItem, type ContextMenuModel } from "./context-menu";
import { buildBayTree, bayTreeEmptyMessage, NOOK_TYPE_LABELS, type TreeLeaf, type TreeShoreInput, type TreeRow } from "./bay-tree";
import { buildAgentRows, mapAgentState, agentCardsEqual, AGENT_STATE_META, type AgentCard, type AgentState } from "./agents-model";
import { resolveActiveBayId, bayAccent, bayHeadNavigation, sortFsEntries, joinPath, mergeFsStatus, scmChipText, parseCollapsedCardIds, serializeCollapsedCardIds, toggleCardCollapsed, type FsEntry, type FsStatusEntry, type BayCardEntry, type ScmSummary } from "./bay-cards";
import { BAY_ICON_CHOICES, bayGlyph } from "./bay-icons";
import { orderSettingsTabs, settingsTabLabel, resolveActiveSettingsTab } from "./settings-tabs";
import { parseQuery, filterAndSort, MruTracker, cycleCategory, categoryLabel, type PaletteItem } from "./omni-palette";
import { buildEmptyState, EmptyStateMessages } from "./empty-states";
import { brandLogoAt, nextBrandIndex, parseBrandIndex } from "./brand";
import { adapterStatusMeta, toolsSubtitle, retentionChipVisible, retentionChipLabel, type ToolsAdapter } from "./tools-tab";
import {
  deriveProfileSlug, isValidProfileSlug, profilePickerLabel, profileDisplayName, selectedLauncherProfile, launcherProfileChoices, envMapFromRows,
  type LaunchProfileListItem, type LaunchProfileDetail,
  type CreateProfileInput, type UpdateProfileInput,
} from "./profiles";
import { DEFAULT_DRAFT, draftFromTheme, themeFromDraft, cssVarsFromTheme, xtermThemeFromDto, isCustom, isBuiltin, canSaveDraft, canDelete, isValidHex, contrastRatio, contrastTier, THEME_COLOR_FIELDS, type ThemeDto, type ThemeDraft } from "./theme-editor";
import { categorizeBindings, isReservedChord, isValidChord, chordDisplay, canRecordChord, normalizeChord as normalizeChordStr, type KeybindDto } from "./keyboard-editor";
import { ONBOARDING_STEPS, INITIAL_ONBOARDING_STATE, nextStep, prevStep, dismiss as dismissOnboarding, currentStepData, isLastStep, isFirstStep, progressPercent, setDefaultBayDir, setAdapterYolo, setBackdrop as setOnboardingBackdrop, setTheme as setOnboardingTheme, setAgentChimes as setOnboardingAgentChimes, shouldShowOnboarding, onboardingSeenFromConfig, ONBOARDING_COMPLETED_KEY, type OnboardingState } from "./onboarding";
import { initBackdrop, setBackdropMaterial, nextToggleMaterial, coerceMaterial, BACKDROP_PREF_KEY, type BackdropDeps, type BackdropMaterial } from "./backdrop";
import { detectChimes, playChime, chimesEnabledFrom, chimePrefValue, AGENT_CHIMES_STORAGE_KEY } from "./chime";
import { NotificationBridge, type NotificationBridgeDeps, type NotificationDeliverPayload } from "./notifications";
import { buildMenu, menuChordSet } from "./menu-model";
import { toolbarTiles } from "./toolbar-tiles";
import { shouldShowLauncher, buildAdapterTiles, buildBuiltinTiles, isEmptyShoreTree, isPlaceholderLeaf, placeableNookForAction, resolveLaunchCwd, type LauncherAdapter, type LauncherBuiltin, type LauncherTile } from "./box-launcher";
import { adapterAccent, toolAccent, assignHotkeys, detectedHarnessTiles, clampLauncherSelection, moveLauncherSelection, hotkeyTarget, shapeRecentSessions, tipAt, computeLauncherCols, resolveLauncherYolo, resolveLauncherProjectDir, type LauncherSelection, type LauncherGeometry, type LauncherArrowKey, type RecentSessionRow } from "./launcher-model";
import { adapterIconSvg, fileIcon, iconSvg, iconForNookType } from "./icons";
import { dropZoneFor, moveMutationFor, zoneOverlayRect } from "./nook-dnd";
import { cssPath, buildFeedbackReport, feedbackSlug, harnessPrompt } from "./inspect-mode";
import { clusterTools } from "./title-cluster";
import { nextUpdateState, updateButtonLabel, updateAffordanceVisible, type UpdateState, type UpdateEvent } from "./update-flow";
import { initialZenState, toggleZen, type ChromeVisibility, type ZenState } from "./zen-mode";
import { eventToChord, buildChordMap, resolveDispatch, defaultBindings, type ResolvedBinding } from "./keymap-dispatch";
import { enqueueNookWrite } from "./write-queue";
import { installConsoleCapture } from "./console-capture";

installConsoleCapture((level, message) => {
  window.__ryn.invoke("app.frontendLog", { level, message }).catch(() => {});
});


const RYN_MENUBAR_EVENTS_BROKEN = false;
import { initHud, toggleHud, recordFrame, hudMetrics, readJsHeapBytes, hudLines, type HudState, type JsHeapProbe } from "./perf-hud";
import { parseSnapshotExport, snapshotRows, summarizeSnapshots, formatBytes as formatSnapshotBytes, type DiagnosticsSnapshot } from "./diagnostics-snapshot";
import { initialPerfBundlesState, applyBundleList, beginCreate, finishCreate, surfaceError, requestDelete, cancelDelete, bundleRows, PERF_BUNDLES_EMPTY_TEXT, type PerfBundlesState, type PerfBundleListResult, type PerfBundleDto } from "./perf-bundles";
import { setupDictation, dictationToggleEnabled, modelPollOutcome, DICTATION_SPACE_KEY, DICTATION_LIVE_TYPING_KEY } from "./dictation";

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

const locallySpawnedNookIds = new Set<string>();
const renderedStreamNookIds = new Set<string>();

async function spawnNook(params: Record<string, unknown>): Promise<{ nookId: string }> {
  const cwd = resolveLaunchCwd(String(params.cwd ?? ""), String(params.inheritCwdFrom ?? ""), activeProjectDir());
  const r = await invoke<{ nookId?: string; error?: { code?: string; message?: string } }>("app.nookSpawn", { ...params, cwd });
  if (!r?.nookId) {
    const msg = r?.error?.message ?? "the engine could not start this terminal";
    console.warn("nook spawn failed", params, r);
    showInAppToast("Couldn't open terminal", msg, () => {});
    throw new Error(msg);
  }
  locallySpawnedNookIds.add(r.nookId);
  return { nookId: r.nookId };
}

interface Subtab {
  documentId: string;
  nookType: string;
  title: string | null;
}

interface NookLeaf {
  kind: "leaf";
  nookId: string;
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

type MosaicNode = SplitNode | NookLeaf;

interface ShoreSnapshot {
  id: string;
  name: string;
  layoutTree: MosaicNode;
  zoomedNookId: string | null;
}

interface BaySnapshot {
  schemaVersion: number;
  id: string;
  name: string;
  projectDir: string;
  activeShoreId: string | null;
  shores: ShoreSnapshot[];
}

interface NookView {
  nookId: string;
  term: Terminal;
  fit: FitAddon;
  serialize: SerializeAddon;
  ws: WebSocket | null;
  el: HTMLElement;
  consumed: number;
  lastAck: number;
  expectedOffset: number;
  replayUntilOffset: number;
  title: string;
  customTitle: string;
  headerTitleEl: HTMLElement;
  search: SearchAddon;
  replaying: boolean;
  resetOnReplay: boolean;
  restoringCheckpoint: boolean;
  keyboard: KeyboardProtocolTracker;
  lastSent: TermDims | null;
  handlersBound: boolean;
  resizeObserver: ResizeObserver | null;
  fitFrame: number | null;
  reconnectTimer: number | null;
  checkpointTimer: number | null;
  exited: boolean;
}

const nooks = new Map<string, NookView>();
const streamGens = createStreamGenerations();
let layout: BaySnapshot | null = null;
let activeShoreId: string | null = null;
let focusedNookId: string | null = null;
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
const shoresRowEl = document.getElementById("shores-row")!;
const shoreTabsEl = document.getElementById("shore-tabs")!;
const leftSidebarEl = document.getElementById("left-sidebar")!;
const leftRailEl = document.getElementById("left-rail")!;
const leftContentEl = document.getElementById("left-content")!;
const leftResizeEl = document.getElementById("left-resize")!;
const palInput = document.getElementById("pal-input") as HTMLInputElement;
const palList = document.getElementById("pal-list")!;
let bayOverviewVisible = false;

gridEl.style.display = "flex";

function syncTitlebarWorkspaceOffset(): void {
  const workspaceLeft = leftSidebarEl.offsetLeft + leftSidebarEl.offsetWidth + 6;
  document.documentElement.style.setProperty("--cove-workspace-left", `${workspaceLeft}px`);
}

function paneFittable(pv: NookView): boolean {
  const host = pv.term.element?.parentElement as HTMLElement | null;
  if (!host) return false;
  return isPaneFittable(host.clientWidth, host.clientHeight, host.isConnected, host.offsetParent !== null);
}
const savedNookViewports = new Map<string, { baseY: number; viewportY: number }>();
function fitNook(pv: NookView): void {
  if (!paneFittable(pv)) return;
  try {
    const live = { baseY: pv.term.buffer.active.baseY, viewportY: pv.term.buffer.active.viewportY };
    let before = live;
    if (!pv.replaying && !pv.restoringCheckpoint) {
      const saved = savedNookViewports.get(pv.nookId);
      if (saved) {
        savedNookViewports.delete(pv.nookId);
        before = saved;
      }
    }
    pv.fit.fit();
    const targetLine = scrollLineAfterFit(before, pv.term.buffer.active.baseY);
    pv.term.scrollToLine(targetLine);
    const viewport = pv.el.querySelector<HTMLElement>(".xterm-viewport");
    if (viewport) {
      const scrollTop = viewportScrollTopFor(targetLine, pv.term.buffer.active.baseY, viewport.scrollHeight, viewport.clientHeight);
      if (scrollTop !== null) viewport.scrollTop = scrollTop;
    }
    pv.term.refresh(0, Math.max(0, pv.term.rows - 1));
  } catch { void 0; }
}
function scheduleFit(pv: NookView): void {
  if (pv.fitFrame !== null) return;
  pv.fitFrame = requestAnimationFrame(() => {
    pv.fitFrame = null;
    fitNook(pv);
  });
}
function fitAll() {
  for (const pv of nooks.values()) scheduleFit(pv);
}

function applySettings() {
  for (const pv of nooks.values()) {
    if (settings.fontFamily) pv.term.options.fontFamily = settings.fontFamily;
    pv.term.options.fontSize = settings.fontSize;
    pv.term.options.lineHeight = settings.lineHeight;
    pv.term.options.cursorStyle = settings.cursorStyle;
    pv.term.options.cursorBlink = settings.cursorBlink;
    pv.term.options.scrollback = settings.scrollback;
    pv.term.options.theme = currentTermTheme();
  }
  document.documentElement.style.setProperty("--workspace-padding", `${settings.padding}px`);
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
function scheduleTerminalCheckpoint(nook: NookView): void {
  if (nook.checkpointTimer !== null) return;
  nook.checkpointTimer = window.setTimeout(() => {
    nook.checkpointTimer = null;
    if (nook.replaying || nook.restoringCheckpoint || nook.expectedOffset !== nook.consumed || nook.exited || nooks.get(nook.nookId) !== nook) return;
    const dataBase64 = toBase64Utf8(nook.serialize.serialize());
    void invoke("app.nookCheckpoint", {
      nookId: nook.nookId,
      dataBase64,
      offset: nook.consumed,
      cols: nook.term.cols,
      rows: nook.term.rows,
      scrollbackLines: settings.scrollback,
    }).catch((error) => console.warn("terminal checkpoint failed", { nookId: nook.nookId, error: String(error) }));
  }, 1000);
}

function finishReplay(nook: NookView, resynced = false): void {
  const viewportAction = replayViewportAction({ resetOnReplay: nook.resetOnReplay, resynced });
  nook.replaying = false;
  nook.resetOnReplay = false;
  if (viewportAction === "bottom") nook.term.scrollToBottom();
  scheduleTerminalCheckpoint(nook);
}

function attachWs(nook: NookView): void {
  if (nook.exited) return;
  if (nook.ws && nook.ws.readyState < WebSocket.CLOSING) return;
  if (nook.reconnectTimer !== null) {
    window.clearTimeout(nook.reconnectTimer);
    nook.reconnectTimer = null;
  }
  const ws = new WebSocket(`ws://${location.host}/pty?nook=${encodeURIComponent(nook.nookId)}&since=${nook.consumed}`);
  nook.ws = ws;
  nook.replaying = true;
  nook.replayUntilOffset = nook.consumed;
  const generation = streamGens.claim(nook.nookId);
  const current = () => streamGens.isCurrent(nook.nookId, generation);
  ws.binaryType = "arraybuffer";
  const sendAck = () => {
    if (ws.readyState === WebSocket.OPEN && nook.consumed > nook.lastAck) {
      ws.send(JSON.stringify({ t: "ack", off: nook.consumed }));
      nook.lastAck = nook.consumed;
    }
  };
  ws.onmessage = (ev) => {
    if (!current()) return;
    if (typeof ev.data === "string") {
      const m = parseRelayText(ev.data);
      if (!m) return;
      if (m.t === "base") {
        nook.consumed = m.off;
        nook.expectedOffset = m.off;
        nook.replayUntilOffset = m.head;
        nook.lastAck = m.off;
        if (m.checkpoint && m.checkpointCols && m.checkpointRows) {
          nook.term.reset();
          nook.restoringCheckpoint = true;
          nook.term.resize(m.checkpointCols, m.checkpointRows);
          nook.term.write(decodeTerminalRestoreBytes(m.checkpoint, m.modes), () => {
            nook.restoringCheckpoint = false;
            if (!current()) return;
            scheduleFit(nook);
            if (m.head <= m.off) finishReplay(nook);
          });
        } else {
          if (nook.resetOnReplay && nook.replaying) {
            nook.term.reset();
            if (m.modes) nook.term.write(decodeBase64Bytes(m.modes));
          }
          if (m.head <= m.off) finishReplay(nook);
        }
      } else if (m.t === "resync") {
        nook.term.reset();
        nook.consumed = m.base;
        nook.expectedOffset = m.base;
        nook.replayUntilOffset = m.base;
        nook.lastAck = m.base;
        if (m.checkpoint && m.checkpointCols && m.checkpointRows) {
          nook.restoringCheckpoint = true;
          nook.term.resize(m.checkpointCols, m.checkpointRows);
          nook.term.write(decodeTerminalRestoreBytes(m.checkpoint, m.modes), () => {
            nook.restoringCheckpoint = false;
            if (!current()) return;
            scheduleFit(nook);
            finishReplay(nook, true);
          });
        } else {
          if (m.modes) nook.term.write(decodeBase64Bytes(m.modes));
          finishReplay(nook, true);
        }
      } else if (m.t === "end") {
        if (processExitAction(nook.exited) === "ignore") return;
        nook.exited = true;
        streamGens.invalidate(nook.nookId);
        try { ws.close(1000, "process exited"); } catch { void 0; }
        launcherRecentsAt = 0;
        void refreshLauncherRecents();
        void closeNookById(nook.nookId);
      }
      return;
    }
    let frame;
    try {
      frame = decodeRelayData(ev.data as ArrayBuffer);
    } catch (error) {
      console.warn("invalid terminal stream frame", { nookId: nook.nookId, error: String(error) });
      ws.close(1008, "invalid terminal stream frame");
      return;
    }
    const { offset, raw } = frame;
    const nextOffset = offset + raw.length;
    if (offset < nook.expectedOffset && nextOffset <= nook.expectedOffset) return;
    if (offset !== nook.expectedOffset) {
      console.warn("terminal stream offset mismatch", { nookId: nook.nookId, expected: nook.expectedOffset, received: offset });
      ws.close(1008, "terminal stream offset mismatch");
      return;
    }
    nook.expectedOffset = nextOffset;
    nook.keyboard.push(raw);
    const bytes = raw;
    const commit = () => {
      if (!current()) return;
      nook.consumed = nextOffset;
      if (nook.consumed - nook.lastAck >= CREDIT_THRESHOLD) sendAck();
      scheduleTerminalCheckpoint(nook);
    };
    nook.term.write(bytes, () => {
      if (!current()) return;
      if (nook.replaying && nextOffset >= nook.replayUntilOffset) finishReplay(nook);
      commit();
    });
  };
  const ackTimer = window.setInterval(() => {
    if (!current()) { window.clearInterval(ackTimer); return; }
    sendAck();
  }, 100);
  ws.onclose = () => {
    window.clearInterval(ackTimer);
    nook.consumed = Math.max(nook.consumed, nook.expectedOffset);
    if (nook.ws === ws) nook.ws = null;
    if (!current() || nook.exited || nooks.get(nook.nookId) !== nook || !nook.el.isConnected) return;
    nook.reconnectTimer = window.setTimeout(() => {
      nook.reconnectTimer = null;
      if (nook.el.isConnected) attachWs(nook);
    }, 250);
  };
  if (nook.handlersBound) return;
  nook.handlersBound = true;
  nook.term.onData((d) => {
    if (nook.replaying) return;
    void enqueueNookWrite(nook.nookId, toBase64Utf8(d), (nookId, dataBase64) => invoke("app.nookWrite", { nookId, dataBase64 }));
  });
  nook.term.onResize(({ cols, rows }) => {
    if (nook.restoringCheckpoint) return;
    const dims: TermDims = { cols, rows };
    if (!shouldResize(dims, nook.lastSent, paneFittable(nook))) return;
    nook.lastSent = dims;
    void invoke("app.nookResize", { nookId: nook.nookId, cols, rows });
  });
}

function makeNook(nookId: string, since: number): NookView {
  const term = new Terminal({ allowTransparency: true, scrollback: settings.scrollback, convertEol: false, fontFamily: settings.fontFamily || "ui-monospace, SFMono-Regular, Menlo, monospace", fontSize: settings.fontSize, lineHeight: settings.lineHeight, cursorStyle: settings.cursorStyle, cursorBlink: settings.cursorBlink, theme: currentTermTheme() });
  const fitAddon = new FitAddon();
  term.loadAddon(fitAddon);
  const searchAddon = new SearchAddon();
  term.loadAddon(searchAddon);
  const serializeAddon = new SerializeAddon();
  term.loadAddon(serializeAddon);

  const el = document.createElement("div");
  el.className = "nook";
  el.style.flexGrow = "1";
  const header = document.createElement("div");
  header.className = "nook-header";
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
  el.appendChild(host);
  term.open(host);
  try { term.loadAddon(new CanvasAddon()); } catch (error) { console.warn("terminal canvas renderer unavailable", { nookId, error: String(error) }); }

  header.addEventListener("contextmenu", (e) => {
    focusNook(nookId);
    openContextMenuAt(e, [
      { id: "nook.split-right", label: "Split Right" },
      { id: "nook.split-down", label: "Split Down" },
      { id: "nook.maximize", label: "Maximize" },
      { id: "sep", label: "", separator: true },
      { id: "nook.close", label: "Close", danger: true },
    ], (id) => runAction(id));
  });
  host.addEventListener("contextmenu", (e) => {
    focusNook(nookId);
    const hasSel = term.hasSelection();
    openContextMenuAt(e, [
      { id: "copy", label: "Copy", disabled: !hasSel },
      { id: "paste", label: "Paste" },
      { id: "clear", label: "Clear" },
      { id: "sep", label: "", separator: true },
      { id: "find", label: "Find in Nook" },
    ], (id) => {
      if (id === "copy") { const s = term.getSelection(); if (s && navigator.clipboard) void navigator.clipboard.writeText(s); }
      else if (id === "paste") { if (navigator.clipboard && navigator.clipboard.readText) void navigator.clipboard.readText().then((t) => { if (t) void invoke("app.nookWrite", { nookId, dataBase64: toBase64Utf8(t) }); }); }
      else if (id === "clear") term.clear();
      else if (id === "find") openFind();
    });
  });

  const resetOnReplay = shouldResetReplay({ locallySpawned: locallySpawnedNookIds.has(nookId), renderedBefore: renderedStreamNookIds.has(nookId) });
  renderedStreamNookIds.add(nookId);
  const pv: NookView = { nookId, term, fit: fitAddon, serialize: serializeAddon, ws: null, el, consumed: since, expectedOffset: since, replayUntilOffset: since, lastAck: since, title: "", customTitle: "", headerTitleEl: titleSpan, search: searchAddon, replaying: true, resetOnReplay, restoringCheckpoint: false, keyboard: createKeyboardProtocolTracker(), lastSent: null, handlersBound: false, resizeObserver: null, fitFrame: null, reconnectTimer: null, checkpointTimer: null, exited: false };

  el.addEventListener("mousedown", () => { acknowledgeAgentAttention(nookId); focusNook(nookId); });
  const overrides = loadKeybindings();
  term.attachCustomKeyEventHandler((e) => {
    if (e.shiftKey && e.key === "Enter" && e.type !== "keydown") return false;
    if (e.type !== "keydown") return true;
    if (e.key === "Tab") { e.preventDefault(); return true; }
    const chord = normalizeChord(e);
    const action = overrides[chord];
    if (action && action.startsWith("send-text:")) { void enqueueNookWrite(nookId, toBase64Utf8(action.slice("send-text:".length)), (id, dataBase64) => invoke("app.nookWrite", { nookId: id, dataBase64 })); return false; }
    if (e.shiftKey && e.key === "Enter") { void enqueueNookWrite(nookId, toBase64Utf8(shiftEnterSequence(pv.keyboard.encoding())), (id, dataBase64) => invoke("app.nookWrite", { nookId: id, dataBase64 })); return false; }
    if (!e.metaKey || e.altKey || e.ctrlKey) return true;
    const k = e.key.toLowerCase();
    if (k === "c") {
      if (term.hasSelection()) { const s = term.getSelection(); if (s && navigator.clipboard) void navigator.clipboard.writeText(s); }
      else { void invoke("app.nookWrite", { nookId, dataBase64: toBase64Utf8("\u0003") }); }
      return false;
    }
    if (k === "a") { term.selectAll(); return false; }
    if (k === "v") {
      if (navigator.clipboard && navigator.clipboard.readText) void navigator.clipboard.readText().then((t) => { if (t) void invoke("app.nookWrite", { nookId, dataBase64: toBase64Utf8(t) }); });
      return false;
    }
    if (e.key === "ArrowLeft") { void invoke("app.nookWrite", { nookId, dataBase64: toBase64Utf8("\u0001") }); return false; }
    if (e.key === "ArrowRight") { void invoke("app.nookWrite", { nookId, dataBase64: toBase64Utf8("\u0005") }); return false; }
    if (e.key === "Backspace") { void invoke("app.nookWrite", { nookId, dataBase64: toBase64Utf8("\u0015") }); return false; }
    return true;
  });
  const setTitle = () => { titleSpan.textContent = pv.customTitle || pv.title || "shell"; };
  header.addEventListener("mousedown", (e) => { if (e.target !== moreBtn) focusNook(nookId); });
  header.draggable = true;
  header.addEventListener("dragstart", (e) => {
    if (!e.dataTransfer) return;
    e.dataTransfer.setData("text/cove-nook", nookId);
    e.dataTransfer.effectAllowed = "move";
    draggingNookId = nookId;
  });
  header.addEventListener("dragend", () => { draggingNookId = null; clearDropOverlay(); });
  el.addEventListener("dragover", (e) => {
    if (!draggingNookId || draggingNookId === nookId) return;
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
    const src = e.dataTransfer?.getData("text/cove-nook") || draggingNookId;
    clearDropOverlay();
    draggingNookId = null;
    if (!src || !activeShoreId) { console.warn("nook drop without source or active shore"); return; }
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
        rememberNookTitle(nookId, newTitle || pv.title);
        void invoke("app.nookRename", { nookId, title: newTitle }).catch(() => void 0);
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
    closeNookMenus();
    const menu = document.createElement("div");
    menu.className = "pmenu";
    const mk = (label: string, fn: () => void) => { const r = document.createElement("div"); r.className = "pmi"; r.textContent = label; r.addEventListener("click", (ev) => { ev.stopPropagation(); closeNookMenus(); fn(); }); menu.appendChild(r); };
    mk("Copy Nook ID", () => { if (navigator.clipboard) void navigator.clipboard.writeText(nookId); });
    mk("Rename", startRename);
    mk("New subtab", () => void addSubtab(nookId));
    mk("Close", () => { focusNook(nookId); void closeFocused(); });
    mk("Close Others", () => { void closeOthers(nookId); });
    header.appendChild(menu);
  });
  term.onTitleChange((t) => { pv.title = t; rememberNookTitle(nookId, pv.customTitle || t); setTitle(); refreshTitles(); });
  nooks.set(nookId, pv);
  pv.resizeObserver = new ResizeObserver(() => scheduleFit(pv));
  pv.resizeObserver.observe(host);
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
  streamGens.invalidate(nookId);
  savedNookViewports.delete(nookId);
  if (pv.reconnectTimer !== null) window.clearTimeout(pv.reconnectTimer);
  if (pv.checkpointTimer !== null) window.clearTimeout(pv.checkpointTimer);
  if (pv.fitFrame !== null) cancelAnimationFrame(pv.fitFrame);
  pv.resizeObserver?.disconnect();
  try { pv.ws?.close(); } catch { void 0; }
  pv.ws = null;
  pv.term.dispose();
  nooks.delete(nookId);
}

function allLayoutNookIds(): Set<string> {
  const ids = new Set<string>();
  for (const shore of layout?.shores ?? []) for (const id of collectLeafIds(shore.layoutTree)) ids.add(id);
  return ids;
}

function sweepDetachedNooks(): void {
  const layoutIds = allLayoutNookIds();
  for (const [id, pv] of nooks) {
    const wsClosed = pv.ws === null || pv.ws.readyState === WebSocket.CLOSED;
    if (shouldDisposeNook({ inLayout: layoutIds.has(id), wsClosed })) disposeNook(id);
  }
}
function pauseNookStream(pv: NookView): void {
  if (!pv.ws) return;
  streamGens.invalidate(pv.nookId);
  if (pv.reconnectTimer !== null) {
    window.clearTimeout(pv.reconnectTimer);
    pv.reconnectTimer = null;
  }
  pv.consumed = Math.max(pv.consumed, pv.expectedOffset);
  const ws = pv.ws;
  pv.ws = null;
  try { ws.close(1000, "terminal hidden"); } catch { void 0; }
}
function syncNookStreams(): void {
  for (const pv of nooks.values()) {
    const connected = pv.ws !== null && pv.ws.readyState < WebSocket.CLOSING;
    const action = streamVisibilityAction({ visible: pv.el.isConnected, connected });
    if (action === "connect") attachWs(pv);
    else if (action === "disconnect") pauseNookStream(pv);
  }
}

function closeNookMenus(): void {
  document.querySelectorAll(".pmenu").forEach((m) => m.remove());
}
document.addEventListener("click", closeNookMenus);

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
  if (node.kind === "leaf") return node.subtabs.length > 0 ? node.subtabs.map((s) => s.documentId) : [node.nookId];
  return [...collectLeafIds(node.childA), ...collectLeafIds(node.childB)];
}
function findLeafId(node: MosaicNode, termId: string): string | null {
  if (node.kind === "leaf") return (node.nookId === termId || node.subtabs.some((s) => s.documentId === termId)) ? node.nookId : null;
  return findLeafId(node.childA, termId) ?? findLeafId(node.childB, termId);
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
  if (!activeShoreId) return;
  await invoke("app.layoutMutate", { op: "activateSubtab", shoreId: activeShoreId, nookId: leafId, targetNookId: "", newNookId: "", orientation: "", name: "", dir: index });
  await reload();
}
async function addSubtab(termNookId: string): Promise<void> {
  const shore = activeShore();
  if (!shore || !activeShoreId) return;
  const leafId = findLeafId(shore.layoutTree, termNookId);
  if (!leafId) return;
  const sp = (await spawnNook({ command: "", cwd: "", inheritCwdFrom: termNookId, cols: 80, rows: 24, adapter: "", agentName: "", bay: "", shore: "" })).nookId;
  await invoke("app.layoutMutate", { op: "addSubtab", shoreId: activeShoreId, nookId: leafId, newNookId: sp, targetNookId: "", orientation: "", name: "", dir: 0 });
  await reload();
  focusNook(sp);
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
  if (!activeShoreId) return;
  const sp = (await spawnNook({ command: "", cwd: "", inheritCwdFrom: "", cols: 80, rows: 24, adapter: "", agentName: "", bay: "", shore: "" })).nookId;
  await invoke("app.layoutMutate", { op: "addSubtab", shoreId: activeShoreId, nookId: nookId, newNookId: sp, targetNookId: "", orientation: "", name: "", dir: 0 });
  await reload();
}

function renderKanbanNook(nookId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "kanban-nook-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  const bayId = "default";
  renderKanbanBoard(bayId).then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load kanban: ${(e as Error).message}</div>`;
  });
  return placeholder;
}

function renderTaskListNook(nookId: string): HTMLElement {
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

function renderTaskDetailNook(nookId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "task-detail-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;display:flex;align-items:center;justify-content:center;color:#6b7280;";
  placeholder.textContent = "Select a task to view details";
  return placeholder;
}
function renderTimelineNook(nookId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "timeline-nook-placeholder";
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
function renderMarkdownNoteNook(nookId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "markdown-note-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  renderMarkdownNote("default", nookId).then(el => {
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
  const placeholder = document.createElement("div");
  placeholder.className = "sketch-note-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  renderSketchNote("default", nookId).then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load sketch: ${(e as Error).message}</div>`;
  });
  return placeholder;
}
function renderCanvasNoteNook(nookId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "canvas-note-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  renderCanvasNote("default", nookId).then(el => {
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
  const placeholder = document.createElement("div");
  placeholder.className = "html-note-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  renderHtmlNote("default", nookId).then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load HTML note: ${(e as Error).message}</div>`;
  });
  return placeholder;
}
function renderNotepadNookWrapper(nookId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "notepad-nook-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  renderNotepadNook("default").then(el => {
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
  renderMermaidNote("default", nookId).then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load mermaid note: ${(e as Error).message}</div>`;
  });
  return placeholder;
}
function renderSessionPickerNook(nookId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "session-picker-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  const bayId = layout?.id ?? "default";
  const projectDir = activeProjectDir();
  const adapters = launcherAdapters.map((a) => ({ name: a.name, displayName: a.displayName }));
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
    const result = await invoke<VaultResumeResult>("cove://commands/vault.resume", { adapter, sessionId, cwd, yolo: launcherYolo(adapter) });
    action = resumeSpawnPlan(result, cwd, displayName, sessionId, launcherYolo(adapter));
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
  const r = await invoke<{ shoreId: string }>("app.layoutMutate", { op: "createShore", newNookId: sp, name: action.shoreName, shoreId: "", targetNookId: "", orientation: "", nookId: "", dir: 0, nookType: "terminal" });
  activeShoreId = r.shoreId;
  await reload();
  focusNook(sp);
  if (action.toast) showInAppToast(action.toast.title, action.toast.body, () => revealNook(sp));
}

function renderLibraryNook(nookId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "library-nook-placeholder";
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
function renderSnapshotInspectorNook(nookId: string): HTMLElement {
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
function renderDiffReviewNookWrapper(nookId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "diff-review-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  renderDiffReviewNook("default").then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load diff review: ${(e as Error).message}</div>`;
  });
  return placeholder;
}
function renderEditorNookWrapper(nookId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "editor-nook-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  renderEditorNook(nookId, nookFilePaths.get(nookId) ?? nookId).then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load editor: ${(e as Error).message}</div>`;
  });
  return placeholder;
}

function renderImageNook(nookId: string): HTMLElement {
  const el = document.createElement("div");
  el.className = "image-nook";
  el.style.cssText = "display:flex;align-items:center;justify-content:center;height:100%;background:#0d1117;overflow:hidden;position:relative;";
  const img = document.createElement("img");
  img.style.cssText = "max-width:100%;max-height:100%;object-fit:contain;transition:transform 0.1s;";
  img.alt = nookId;
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
  return resolveLauncherProjectDir(layout, bayBoxItems);
}
function renderGitNookWrapper(nookId: string): HTMLElement {
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
function renderSearchNookWrapper(nookId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "search-nook-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  renderSearchNook("default").then(el => {
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
  const placeholder = document.createElement("div");
  placeholder.className = "diff-viewer-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  renderDiffViewerNook(nookId, nookId, refInput).then(el => {
    el.style.flex = "1 1 0";
    el.style.minWidth = "0";
    el.style.minHeight = "0";
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load diff: ${(e as Error).message}</div>`;
  });
  return placeholder;
}
function renderMarkdownNookWrapper(nookId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "markdown-nook-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  renderMarkdownNook(nookId, nookId).then(el => {
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
    if (active.nookType === "pdf") return renderPdfNook(nookFilePaths.get(active.documentId) ?? active.title ?? active.documentId);
    if (active.nookType === "video") return renderVideoNook(nookFilePaths.get(active.documentId) ?? active.title ?? active.documentId);
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

function activeShore(): ShoreSnapshot | undefined {
  if (!layout) return undefined;
  return layout.shores.find((r) => r.id === activeShoreId) ?? layout.shores[0];
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
  for (const [id, pv] of nooks) {
    if (!pv.el.isConnected || pv.replaying || pv.restoringCheckpoint) continue;
    const buf = pv.term.buffer.active;
    savedNookViewports.set(id, { baseY: buf.baseY, viewportY: buf.viewportY });
  }
}

function renderShore(): void {
  const shore = activeShore();
  captureNookViewports();
  gridEl.innerHTML = "";
  const shoreEmpty = shore ? isEmptyShoreTree(shore.layoutTree) : false;
  if (bayOverviewVisible) {
    focusedNookId = null;
    gridEl.appendChild(renderBoxLauncher(null, null));
  } else if (shore && shore.layoutTree && !shoreEmpty) {
    const treeIds = collectLeafIds(shore.layoutTree);
    const zid = shore.zoomedNookId;
    if (zid && treeIds.includes(zid)) {
      const zoomEl = getNook(zid).el;
      zoomEl.style.flexGrow = "1";
      gridEl.appendChild(zoomEl);
      focusedNookId = zid;
    } else {
      gridEl.appendChild(renderNode(shore.layoutTree));
    }
  }
  sweepDetachedNooks();
  if (!bayOverviewVisible && shore && shoreEmpty) {
    const placeholder = collectLeafIds(shore.layoutTree)[0] ?? null;
    focusedNookId = null;
    gridEl.appendChild(renderBoxLauncher(shore.id, placeholder));
  } else if (!bayOverviewVisible && (!shore || !shore.layoutTree)) {
    if (shouldShowLauncher((layout?.shores ?? []).length)) {
      gridEl.appendChild(renderBoxLauncher(null, null));
    } else {
      const empty = buildEmptyState({ message: EmptyStateMessages.noShores, actionLabel: "New terminal", actionIcon: "+" });
      const action = empty.querySelector(".cove-empty-action");
      if (action) action.addEventListener("click", () => void newShore());
      gridEl.appendChild(empty);
    }
  }
  syncNookStreams();
  for (const [id, pv] of nooks) {
    pv.el.classList.toggle("focused", id === focusedNookId);
  }
  syncAgentNookStateClasses();
  fitAll();
  requestAnimationFrame(() => { fitAll(); reconcileBrowserBounds(); });
}

function focusNook(nookId: string): void {
  if (bayOverviewVisible) {
    bayOverviewVisible = false;
    renderShore();
  }
  focusedNookId = nookId;
  for (const [id, pv] of nooks) {
    pv.el.classList.toggle("focused", id === nookId);
  }
  nooks.get(nookId)?.term.focus();
  refreshTitles();
  if (sidebarModel.leftMode === "bays" && !collapsedOf(sidebarModel, "left")) renderSidebarContent("left");
  if (activeShoreId) {
    void invoke("app.layoutMutate", { op: "focus", shoreId: activeShoreId, nookId, targetNookId: "", newNookId: "", orientation: "", name: "", dir: 0 });
  }
}

function refreshTitles(): void {
  const tabEls = shoreTabsEl.querySelectorAll<HTMLElement>(".rtab");
  (layout?.shores ?? []).forEach((r) => {
    const tab = Array.from(tabEls).find((el) => el.title === shoreTabName(r) || el.querySelector(".rtab-name")?.textContent === shoreTabName(r));
    if (tab) {
      const nameEl = tab.querySelector<HTMLElement>(".rtab-name");
      if (nameEl) nameEl.textContent = shoreTabName(r);
      tab.title = shoreTabName(r);
    }
  });
}

let reloadGeneration = 0;

function applyLayoutSnapshot(snapshot: BaySnapshot): void {
  layout = snapshot;
  if (!activeShoreId || !layout.shores.some((shore) => shore.id === activeShoreId)) {
    activeShoreId = layout.activeShoreId ?? layout.shores[0]?.id ?? null;
  }
  const leaves = activeLeafIds();
  if (!focusedNookId || !leaves.includes(focusedNookId)) {
    focusedNookId = leaves[0] ?? null;
  }
  renderShore();
  renderShoreTabs();
  renderSidebar();
  if (focusedNookId) {
    nooks.get(focusedNookId)?.term.focus();
  }
  refreshTitles();
}

async function hydrateNookTitles(generation: number): Promise<void> {
  try {
    const list = await invoke<{ nooks: { nookId: string; title: string | null }[] }>("app.nookList", {});
    if (generation !== reloadGeneration) return;
    for (const p of list.nooks) {
      const pv = nooks.get(p.nookId);
      if (pv && p.title) pv.customTitle = p.title;
    }
    refreshTitles();
  } catch { void 0; }
}

async function reload(): Promise<BaySnapshot> {
  const generation = ++reloadGeneration;
  const snapshot = await invoke<BaySnapshot>("app.layoutGet", {});
  if (generation !== reloadGeneration) return snapshot;
  applyLayoutSnapshot(snapshot);
  void hydrateNookTitles(generation);
  if (activeProjectDir() !== launcherRecentsCwd) {
    void loadLauncherRecents().then(() => {
      if (generation === reloadGeneration && bayOverviewVisible) renderShore();
    });
  }
  return snapshot;
}

async function splitActive(dir: "row" | "col"): Promise<void> {
  if (!layout || layout.shores.length === 0 || !activeShoreId) {
    await newShore();
    return;
  }
  const src = focusedNookId;
  if (!src) return;
  const sp = (await spawnNook({ command: "", cwd: "", inheritCwdFrom: src, cols: 80, rows: 24, adapter: "", agentName: "", bay: "", shore: "" })).nookId;
  await invoke("app.layoutMutate", { op: "split", shoreId: activeShoreId, targetNookId: src, newNookId: sp, orientation: dir, name: "", nookId: "", dir: 0 });
  await reload();
  focusNook(sp);
}

let draggingNookId: string | null = null;
let dropOverlayEl: HTMLElement | null = null;
let tabSpringTimer: number | null = null;
let tabSpringShoreId: string | null = null;
document.addEventListener("dragend", () => { draggingNookId = null; clearDropOverlay(); });

async function moveNookToShore(nookId: string, targetShoreId: string): Promise<void> {
  try {
    await invoke("app.layoutMutate", { op: "moveNookToShore", shoreId: targetShoreId, nookId, targetNookId: "", newNookId: "", orientation: "", name: "", dir: 0 });
    activeShoreId = targetShoreId;
    await reload();
    focusNook(nookId);
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

async function applyNookMove(m: { op: string; nookId: string; targetNookId: string; orientation: string; dir: number }, focusId: string): Promise<void> {
  if (!activeShoreId) { console.warn("nook move without active shore"); return; }
  try {
    const srcShore = layout?.shores.find((r) => collectLeafIds(r.layoutTree).includes(m.nookId));
    if (srcShore && srcShore.id !== activeShoreId) {
      await invoke("app.layoutMutate", { op: "moveNookToShore", shoreId: activeShoreId, nookId: m.nookId, targetNookId: "", newNookId: "", orientation: "", name: "", dir: 0 });
    }
    if (m.op === "centerDrop") {
      const shore = activeShore();
      const srcLeaf = shore ? findLeaf(shore.layoutTree, m.nookId) : null;
      const idx = srcLeaf ? Math.max(0, srcLeaf.activeSubtab) : 0;
      await invoke("app.layoutMutate", { op: "centerDrop", shoreId: activeShoreId, targetNookId: m.nookId, nookId: m.targetNookId, dir: idx, newNookId: "", orientation: "", name: "" });
    } else {
      await invoke("app.layoutMutate", { op: "moveNook", shoreId: activeShoreId, nookId: m.nookId, targetNookId: m.targetNookId, orientation: m.orientation, dir: m.dir, newNookId: "", name: "" });
    }
    await reload();
    focusNook(focusId);
  } catch (err) { console.warn("nook move failed", m.op, err); }
}

function findLeaf(node: MosaicNode, nookId: string): { nookId: string; activeSubtab: number } | null {
  if (node.kind === "leaf") return node.nookId === nookId ? { nookId: node.nookId, activeSubtab: node.activeSubtab } : null;
  return findLeaf(node.childA, nookId) ?? findLeaf(node.childB, nookId);
}

async function closeNookById(nookId: string): Promise<void> {
  const shore = layout?.shores.find((r) => collectLeafIds(r.layoutTree).includes(nookId));
  if (!shore) { console.warn("close requested for nook not in layout", nookId); return; }
  await closeBrowserWebview(nookId);
  try { await invoke("app.nookKill", { nookId }); } catch (err) { console.warn("nook kill on exit failed", nookId, err); }
  try { await invoke("app.layoutMutate", { op: "close", shoreId: shore.id, nookId, targetNookId: "", newNookId: "", orientation: "", name: "", dir: 0 }); } catch (err) { console.warn("layout close on exit failed", nookId, err); }
  disposeNook(nookId);
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
  if (!activeShoreId) { console.warn("split requested with no active shore"); return; }
  const target = focusedNookId ?? activeLeafIds()[0];
  if (!target) { console.warn("split requested with no target nook"); return; }
  let nookId: string;
  let nookType = "terminal";
  if (kind === "terminal") {
    nookId = (await spawnNook({ command: "", cwd: "", inheritCwdFrom: target, cols: 80, rows: 24, adapter: "", agentName: "", bay: "", shore: "" })).nookId;
  } else if (kind.startsWith("adapter:")) {
    const name = kind.slice("adapter:".length);
    const tile = detectedHarnessTiles(buildAdapterTiles(launcherAdapters)).find((t) => t.adapterName === name);
    if (!tile) { console.warn("split chooser: unknown adapter", name); return; }
    const launch = await buildAdapterLaunch({ name: tile.adapterName, displayName: tile.label, accent: tile.accent, binary: tile.binary });
    nookId = (await spawnNook({ command: launch.command, args: launch.args, cwd: "", inheritCwdFrom: target, cols: 80, rows: 24, adapter: tile.adapterName, agentName: tile.label, bay: "", shore: "", yolo: launch.yolo })).nookId;
  } else if (kind === "browser") {
    nookId = (await invoke<{ nookId: string; currentUrl: string }>("cove://commands/browser.create", { url: "https://duckduckgo.com" })).nookId;
    nookType = "browser";
  } else {
    nookId = (await spawnNook({ command: "", cwd: "", inheritCwdFrom: "", cols: 80, rows: 24, adapter: "", agentName: "", bay: "", shore: "" })).nookId;
    nookType = kind;
  }
  await invoke("app.layoutMutate", { op: "split", shoreId: activeShoreId, targetNookId: target, newNookId: nookId, orientation: dir, name: "", nookId: "", dir: 0, nookType });
  await reload();
  focusNook(nookId);
}

async function closeFocused(): Promise<void> {
  if (!focusedNookId || !activeShoreId) return;
  const nookId = focusedNookId;
  await invoke("app.nookKill", { nookId });
  await invoke("app.layoutMutate", { op: "close", shoreId: activeShoreId, nookId, targetNookId: "", newNookId: "", orientation: "", name: "", dir: 0 });
  disposeNook(nookId);
  await reload();
}

async function closeOthers(keepNookId: string): Promise<void> {
  if (!activeShoreId) return;
  const shore = activeShore();
  if (!shore) return;
  const others = collectLeafIds(shore.layoutTree).filter((id) => id !== keepNookId);
  for (const id of others) {
    try { await invoke("app.nookKill", { nookId: id }); } catch { void 0; }
    try { await invoke("app.layoutMutate", { op: "close", shoreId: activeShoreId, nookId: id, targetNookId: "", newNookId: "", orientation: "", name: "", dir: 0 }); } catch { void 0; }
    disposeNook(id);
  }
  focusNook(keepNookId);
  await reload();
}

async function toggleZoom(): Promise<void> {
  if (!focusedNookId || !activeShoreId) return;
  const shore = activeShore();
  if (shore && shore.zoomedNookId === focusedNookId) {
    await invoke("app.layoutMutate", { op: "unzoom", shoreId: activeShoreId, nookId: "", targetNookId: "", newNookId: "", orientation: "", name: "", dir: 0 });
  } else {
    await invoke("app.layoutMutate", { op: "zoom", shoreId: activeShoreId, nookId: focusedNookId, targetNookId: "", newNookId: "", orientation: "", name: "", dir: 0 });
  }
  await reload();
}

function cycleFocus(d: number): void {
  const leaves = activeLeafIds();
  if (leaves.length === 0) return;
  const idx = focusedNookId ? leaves.indexOf(focusedNookId) : -1;
  const next = leaves[(idx + d + leaves.length) % leaves.length];
  focusNook(next);
}

function newPlaceholderId(): string {
  const rnd = (globalThis.crypto && "randomUUID" in globalThis.crypto) ? globalThis.crypto.randomUUID() : Math.random().toString(36).slice(2);
  return "empty-" + rnd;
}

async function newShore(): Promise<void> {
  const placeholder = newPlaceholderId();
  const r = await invoke<{ shoreId: string }>("app.layoutMutate", { op: "createShore", newNookId: placeholder, name: nextShoreName(), shoreId: "", targetNookId: "", orientation: "", nookId: "", dir: 0, nookType: "empty" });
  activeShoreId = r.shoreId;
  focusedNookId = null;
  await reload();
}

function safeReplaceTarget(shoreId: string, placeholderId: string | null): string | null {
  if (!placeholderId) return null;
  const shore = layout?.shores.find((r) => r.id === shoreId);
  if (!shore) { console.warn("replace target shore missing", shoreId, placeholderId); return null; }
  if (!isPlaceholderLeaf(shore.layoutTree, placeholderId)) { console.warn("refusing to replace a live nook leaf", shoreId, placeholderId); return null; }
  return placeholderId;
}

async function placeNookIntoShore(shoreId: string, placeholderId: string | null, nookId: string, nookType: string, shoreName?: string): Promise<void> {
  const safePlaceholder = safeReplaceTarget(shoreId, placeholderId);
  if (safePlaceholder) {
    await invoke("app.layoutMutate", { op: "replace", shoreId, targetNookId: safePlaceholder, newNookId: nookId, orientation: "", name: "", nookId: "", dir: 0, nookType });
    if (shoreName) {
      try { await invoke("app.layoutMutate", { op: "rename", shoreId, name: shoreName, targetNookId: "", newNookId: "", orientation: "", nookId: "", dir: 0 }); } catch (err) { console.warn("shore rename after place failed", err); }
    }
  } else {
    await invoke("app.layoutMutate", { op: "createShore", newNookId: nookId, name: shoreName === "Shore" || !shoreName ? nextShoreName() : shoreName, shoreId: "", targetNookId: "", orientation: "", nookId: "", dir: 0, nookType });
  }
  activeShoreId = shoreId;
  await reload();
  focusNook(nookId);
}

async function launchTileInto(shoreId: string | null, placeholderId: string | null, action: string): Promise<void> {
  const placeable = placeableNookForAction(action);
  if (!placeable) { runAction(action); return; }
  let nookId: string;
  if (placeable.kind === "browser") {
    const bp = await invoke<{ nookId: string; currentUrl: string }>("cove://commands/browser.create", { url: "https://duckduckgo.com" });
    nookId = bp.nookId;
  } else {
    nookId = (await spawnNook({ command: "", cwd: "", inheritCwdFrom: "", cols: 80, rows: 24, adapter: "", agentName: "", bay: "", shore: "" })).nookId;
  }
  if (shoreId) {
    await placeNookIntoShore(shoreId, placeholderId, nookId, placeable.nookType, placeable.shoreName);
  } else {
    const r = await invoke<{ shoreId: string }>("app.layoutMutate", { op: "createShore", newNookId: nookId, name: placeable.shoreName === "Shore" ? nextShoreName() : placeable.shoreName, shoreId: "", targetNookId: "", orientation: "", nookId: "", dir: 0, nookType: placeable.nookType });
    activeShoreId = r.shoreId;
    await reload();
    focusNook(nookId);
  }
}

async function newBrowserShore(url: string): Promise<void> {
  const bp = await invoke<{ nookId: string; currentUrl: string }>("cove://commands/browser.create", { url });
  const r = await invoke<{ shoreId: string }>("app.layoutMutate", { op: "createShore", newNookId: bp.nookId, name: "Browser", shoreId: "", targetNookId: "", orientation: "", nookId: "", dir: 0, nookType: "browser" });
  activeShoreId = r.shoreId;
  await reload();
  focusNook(bp.nookId);
}

async function closeShore(shoreId: string): Promise<void> {
  const shore = layout?.shores.find((r) => r.id === shoreId);
  if (!shore) return;
  const leaves = collectLeafIds(shore.layoutTree);
  for (const id of leaves) {
    await closeBrowserWebview(id);
    try { await invoke("app.nookKill", { nookId: id }); } catch { void 0; }
    disposeNook(id);
  }
  try { await invoke("app.layoutMutate", { op: "closeShore", shoreId, nookId: "", targetNookId: "", newNookId: "", orientation: "", name: "", dir: 0 }); } catch { void 0; }
  if (activeShoreId === shoreId) activeShoreId = null;
  await reload();
}

let sidebarModel: SidebarModel = initialSidebarModel();
const sidebarScrollOffsets = new Map<SidebarMode, number>();
const collapsedTreeShores = new Set<string>(JSON.parse(localStorage.getItem("cove.tree.collapsedShores") ?? "[]"));
let agentCards: AgentCard[] = [];
const acknowledgedDoneNooks = new Set<string>();
const needsInputNooks = new Set<string>();
let agentPollTimer: ReturnType<typeof setInterval> | null = null;
let bayBoxItems: BayBoxInput[] = [];

async function loadBayBoxes(): Promise<void> {
  try {
    const res = await invoke<{ bays: { id: string; name: string; projectDir?: string; iconKind?: string | null; iconValue?: string | null }[] }>("cove://commands/bay.list", {});
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
  try { await invoke("cove://commands/bay.reorder", { orderedIds: ids }); } catch (err) { console.warn("bay reorder failed", err); }
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
      try { await invoke("app.layoutMutate", { op: "rename", shoreId, name: newName, nookId: "", targetNookId: "", newNookId: "", orientation: "", dir: 0 }); }
      catch (e) { console.warn("shore rename failed", shoreId, e); }
      await reload();
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
      try { await invoke("cove://commands/bay.rename", { id: wsId, name: newName }); }
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
    await invoke("cove://commands/bay.delete", { id: wsId });
    await loadBayBoxes();
    await reload();
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
  else if (mode === "notepad") renderNotepadContent(content);
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
  void invoke<ScmSummary>("app.gitSummary", { path: dir })
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
  void invoke<BaySnapshot>("cove://commands/layout.get", { bayId: wsId })
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
  void invoke("cove://commands/activity.acknowledge", { nookId }).catch((err) => console.warn("activity.acknowledge failed", nookId, err));
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
    const accent = launcherAdapters.find((adapter) => adapter.name === agent?.adapter)?.accent;
    nook.el.style.setProperty("--agent-accent", accent || AGENT_STATE_META[state].color);
  }
}

function adapterDisplayLabel(adapterName: string): string {
  return launcherAdapters.find((a) => a.name === adapterName)?.displayName ?? adapterName.replace(/-/g, " ");
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
    openContextMenuAt(e, [
      { id: "focus", label: "Go to" },
      { id: "copy-id", label: "Copy nook id" },
      { id: "close", label: "Close", danger: true },
    ], (id) => {
      if (id === "focus") focusTreeRow("nook", row.shoreId, nookId);
      else if (id === "copy-id") { if (navigator.clipboard) void navigator.clipboard.writeText(nookId); }
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
  const activeId = resolveActiveBayId(entries, layout?.id ?? null);
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
    if (emoji) await invoke("cove://commands/bay.set-icon", { id: wsId, kind: "emoji", value: emoji });
    else await invoke("cove://commands/bay.set-icon", { id: wsId, kind: "", value: "" });
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
    openContextMenuAt(e, [
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
  const sourceShores = isActive ? (layout?.shores ?? []) : (wsSnapshotCache.get(ws.id)?.shores ?? []);
  const shores: TreeShoreInput[] = sourceShores.map((r) => ({ id: r.id, name: shoreTabName(r), leaves: shoreLeaves(r) }));
  const rows = buildBayTree({
    bayName: ws.name,
    activeShoreId,
    focusedNookId,
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
      if (isActive) onTreeRowClick(row.kind, row.shoreId, row.nookId, row.expandable);
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
      openContextMenuAt(e, [
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

function onTreeRowClick(kind: string, shoreId: string | null, nookId: string | null, expandable: boolean): void {
  if (kind === "bay") {
    console.warn("tree click: bay rows are not rendered in card mode");
    return;
  }
  if (kind === "nook" && nookId) { revealNook(nookId); return; }
  if (kind === "shore" && shoreId) {
    const shore = layout?.shores.find((r) => r.id === shoreId);
    if (!shore) { console.warn("tree click: unknown shore", shoreId); return; }
    activeShoreId = shoreId;
    const f = firstLeafOf(shore);
    if (f) focusedNookId = f;
    renderShore();
    renderShoreTabs();
    renderSidebar();
    if (f) focusNook(f);
  }
}

function focusTreeRow(kind: string, shoreId: string | null, nookId: string | null): void {
  if (kind === "nook" && nookId) { revealNook(nookId); return; }
  if (kind === "shore" && shoreId) {
    const shore = layout?.shores.find((r) => r.id === shoreId);
    if (!shore) { console.warn("tree focus: unknown shore", shoreId); return; }
    activeShoreId = shoreId;
    const f = firstLeafOf(shore);
    if (f) focusedNookId = f;
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
  if (layout?.id === bayId) {
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
    try { await invoke("app.nookKill", { nookId: id }); }
    catch (err) { console.warn("inactive bay nook kill failed", bayId, id, err); }
    disposeNook(id);
  }
  try {
    await invoke("app.layoutMutate", {
      op: kind === "shore" ? "closeShore" : "close",
      shoreId,
      nookId: kind === "nook" ? nookIds[0] : "",
      targetNookId: "",
      newNookId: "",
      orientation: "",
      name: "",
      dir: 0,
    });
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

async function refreshAgents(): Promise<void> {
  const previousCards = agentCards;
  let nextCards: AgentCard[];
  try {
    const res = await invoke<{ cards: AgentCard[] }>("cove://commands/activity.list", {});
    nextCards = res.cards ?? [];
  } catch { nextCards = []; }
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
      syncTitlebarWorkspaceOffset();
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
  if (agentPollTimer === null) agentPollTimer = setInterval(() => {
    if (!agentsVisible()) return;
    void refreshAgents();
    for (const bay of bayBoxItems) {
      if (bay.projectDir) requestScmSummary(bay.projectDir);
    }
  }, 3000);
}

const pinnedShoreIds = new Set<string>(JSON.parse(localStorage.getItem("cove.pinnedShores") ?? "[]"));
function savePinnedShores(): void { localStorage.setItem("cove.pinnedShores", JSON.stringify([...pinnedShoreIds])); }

interface WingInfo { id: string; name: string; }
let wings: WingInfo[] = [];
let activeWingId: string | null = "main";
let wingSwitcherExpanded = false;
let shoreWingSummaries: { id: string; wingId: string; pinned: boolean }[] = [];
async function loadWings(): Promise<void> {
  const wsId = layout?.id ?? "default";
  try {
    const res = await invoke<{ wings: { id: string; name: string }[] }>("cove://commands/wing.list", { bayId: wsId });
    wings = res.wings ?? [{ id: "main", name: "main" }];
  } catch { wings = [{ id: "main", name: "main" }]; }
  try {
    const list = await invoke<{ shores: { id: string; wingId: string; pinned: boolean }[] }>("cove://commands/shore.list", { bayId: wsId });
    shoreWingSummaries = list.shores ?? [];
  } catch { shoreWingSummaries = []; }
}
async function switchWingActive(wingId: string): Promise<void> {
  activeWingId = wingId;
  try { await invoke("cove://commands/wing.switch", { bayId: "default", wingId }); } catch { void 0; }
  await loadWings();
  await reload();
  renderShoreTabs();
}

function shoreTabName(shore: ShoreSnapshot): string {
  if (shore.name.trim().length > 0) return shore.name;
  const leaves = collectLeafIds(shore.layoutTree);
  const first = leaves[0] ? nooks.get(leaves[0]) : undefined;
  return (first && first.title) || "Shore";
}

function nextShoreName(): string {
  return "Shore " + ((layout?.shores.length ?? 0) + 1);
}

function renderShoreTabs(): void {
  const activeRename = shoreTabsEl.querySelector(".rtab-rename-input");
  if (activeRename && activeRename === document.activeElement) return;
  shoreTabsEl.innerHTML = "";
  const allShores = layout?.shores ?? [];
  const wingModel = buildWingModel(wings, shoreWingSummaries, activeWingId);
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
    tab.className = "rtab" + (shoreId === activeShoreId ? " active" : "") + (isPinned ? " pinned" : "");
    tab.draggable = false;
    tab.title = shoreTabName(shore);

    const glyph = document.createElement("span");
    glyph.className = "rtab-glyph";
    glyph.innerHTML = iconForNookType(shoreLeaves(shore)[0]?.nookType ?? "terminal");
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
        activeShoreId = shoreId;
        const f = firstLeafOf(shore);
        if (f) focusedNookId = f;
        renderShore();
        renderShoreTabs();
        renderSidebar();
        if (f) focusNook(f);
        return;
      }
      if (shoreId === activeShoreId) {
        clickCount++;
        if (clickCount >= 2) {
          startRename(shoreId, tab, nameEl);
          clickCount = 0;
        } else {
          setTimeout(() => { clickCount = 0; }, 400);
        }
      } else {
        activeShoreId = shoreId;
        const f = firstLeafOf(shore);
        if (f) focusedNookId = f;
        renderShore();
        renderShoreTabs();
        renderSidebar();
        if (f) focusNook(f);
      }
    });
    tab.addEventListener("contextmenu", (e) => {
      const pinned = pinnedShoreIds.has(shoreId);
      openContextMenuAt(e, [
        { id: "rename", label: "Rename" },
        { id: "pin", label: pinned ? "Unpin" : "Pin" },
        { id: "sep", label: "", separator: true },
        { id: "close", label: "Close", danger: true, disabled: pinned },
        { id: "close-others", label: "Close Others" },
      ], (id) => {
        if (id === "rename") startRename(shoreId, tab, tab.querySelector(".rtab-name") as HTMLElement);
        else if (id === "pin") { if (pinned) pinnedShoreIds.delete(shoreId); else pinnedShoreIds.add(shoreId); savePinnedShores(); renderShoreTabs(); }
        else if (id === "close") void closeShore(shoreId);
        else if (id === "close-others") void closeOtherShores(shoreId);
      });
    });
    tab.addEventListener("dragstart", () => { dragSrcId = shoreId; tab.classList.add("dragging"); });
    tab.addEventListener("dragend", () => { tab.classList.remove("dragging"); dragSrcId = null; });
    tab.addEventListener("dragover", (e) => {
      e.preventDefault();
      if (draggingNookId) {
        tab.classList.add("nook-drop-target");
        if (shoreId !== activeShoreId && tabSpringShoreId !== shoreId) {
          if (tabSpringTimer !== null) window.clearTimeout(tabSpringTimer);
          tabSpringShoreId = shoreId;
          tabSpringTimer = window.setTimeout(() => {
            tabSpringTimer = null;
            tabSpringShoreId = null;
            if (!draggingNookId) return;
            activeShoreId = shoreId;
            renderShore();
            renderShoreTabs();
            renderSidebar();
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
      const nookSrc = e.dataTransfer?.getData("text/cove-nook") || draggingNookId;
      if (nookSrc) {
        draggingNookId = null;
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
    revealSidebarMode("bays");
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

  if (wings.length > 1 || wingSwitcherExpanded) {
    const switcher = document.createElement("div");
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

  const addBtn = document.createElement("div");
  addBtn.className = "rbox-ctl rbox-add";
  addBtn.style.cssText = "margin-left:auto;";
  addBtn.innerHTML = iconSvg("plus");
  addBtn.title = "New shore (Cmd T)";
  addBtn.addEventListener("click", () => void newShore());
  shoreTabsEl.appendChild(addBtn);

  updateEdgeFade();
}

function updateEdgeFade(): void {
  shoreTabsEl.classList.remove("edge-fade-left", "edge-fade-right");
  if (shoreTabsEl.scrollWidth > shoreTabsEl.clientWidth) {
    if (shoreTabsEl.scrollLeft > 2) shoreTabsEl.classList.add("edge-fade-left");
    if (shoreTabsEl.scrollLeft + shoreTabsEl.clientWidth < shoreTabsEl.scrollWidth - 2) shoreTabsEl.classList.add("edge-fade-right");
  }
}
shoreTabsEl.addEventListener("scroll", updateEdgeFade);

async function reorderShores(fromId: string, toId: string): Promise<void> {
  if (!layout) return;
  const ids = layout.shores.map((r) => r.id);
  const fromIdx = ids.indexOf(fromId);
  const toIdx = ids.indexOf(toId);
  if (fromIdx < 0 || toIdx < 0) return;
  const reordered = reorderShore(layout.shores, fromIdx, toIdx);
  layout.shores = reordered;
  renderShoreTabs();
  renderSidebarContent("left");
  try {
    const newOrder = reordered.map((r) => r.id);
    await invoke("app.layoutMutate", { op: "reorder", shoreIds: newOrder, shoreId: "", targetNookId: "", newNookId: "", orientation: "", name: "", nookId: "", dir: 0 });
  } catch (err) { console.warn("shore reorder failed", err); }
  await reload();
}

function startRename(shoreId: string, tab: HTMLElement, nameEl: HTMLElement): void {
  const shore = layout?.shores.find((r) => r.id === shoreId);
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
        await invoke("app.layoutMutate", { op: "rename", shoreId, name: newName, nookId: "", targetNookId: "", newNookId: "", orientation: "", dir: 0 });
        await reload();
        return;
      } catch (err) { console.warn("shore rename failed", shoreId, err); }
    }
    renderShoreTabs();
    renderSidebar();
  };
  input.addEventListener("blur", commit);
  input.addEventListener("keydown", (e) => {
    e.stopPropagation();
    if (e.key === "Enter") input.blur();
    if (e.key === "Escape") { input.value = shore.name; input.blur(); }
  });
}

async function closeOtherShores(keepShoreId: string): Promise<void> {
  if (!layout) return;
  const toClose = layout.shores.filter((r) => r.id !== keepShoreId);
  for (const shore of toClose) {
    await closeShore(shore.id);
  }
}

interface Action { label: string; icon: string; key?: string; run: () => void; }

function baseActions(): Action[] {
  return [
    { label: "New terminal", icon: "+", key: "Cmd T", run: () => void newShore() },
    { label: "New browser", icon: "\uD83C\uDF10", run: () => void newBrowserShore("https://duckduckgo.com") },
    { label: "Split right", icon: "\u2502", key: "Cmd D", run: () => void splitActive("row") },
    { label: "Split down", icon: "\u2500", key: "Cmd Shift D", run: () => void splitActive("col") },
    { label: "Close nook", icon: "\u00d7", key: "Cmd W", run: () => void closeFocused() },
    { label: "Toggle left sidebar", icon: "\u25e7", key: "Cmd B", run: toggleLeftSidebar },
    { label: "Show notepad", icon: "\u270e", run: () => revealSidebarMode("notepad") },
    { label: "Show bays", icon: "\u25c9", key: "Cmd Shift A", run: () => revealSidebarMode("bays") },
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
  return (layout?.shores ?? []).map((r, i) => ({
    label: `Go to ${r.name}`,
    icon: "\u203a",
    key: i < 9 ? `Cmd ${i + 1}` : undefined,
    run: () => {
      activeShoreId = r.id;
      const f = firstLeafOf(r);
      if (f) { focusedNookId = f; renderShore(); renderSidebar(); focusNook(f); }
    },
  }));
}

let palSel = 0;
let palActions: PaletteItem[] = [];
const palMru = new MruTracker(JSON.parse(localStorage.getItem("cove.palette.mru") ?? "[]"));
let palCachedItems: PaletteItem[] | null = null;
const nookFilePaths = new Map<string, string>();
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
  if (focusedNookId) {
    const pv = nooks.get(focusedNookId);
    if (pv) pv.term.focus();
  }
}

async function paletteItems(): Promise<PaletteItem[]> {
  const items: PaletteItem[] = [];
  for (const a of baseActions()) {
    items.push({ id: `cmd:${a.label}`, label: a.label, category: "commands", icon: a.icon, key: a.key, run: () => { a.run(); } });
  }
  for (const a of jumpActions()) {
    items.push({ id: `shore:${a.label}`, label: a.label, category: "shores", icon: a.icon, key: a.key, run: () => { a.run(); } });
  }
  for (const [id, pv] of nooks) {
    items.push({ id: `nook:${id}`, label: pv.title || id, category: "nooks", icon: "\u25a0", run: () => focusNook(id) });
  }
  try {
    const wsResult = await invoke<{ bays: { id: string; name: string }[] }>("cove://commands/bay.list", {});
    for (const ws of wsResult.bays ?? []) {
      items.push({ id: `ws:${ws.id}`, label: ws.name, category: "bays", icon: "\u25c8", run: () => void switchBay(ws.id) });
    }
  } catch { void 0; }
  try {
    const taskResult = await invoke<{ cards: { id: string; title: string; humanId: string }[] }>("cove://commands/task.list", { bayId: "default" });
    for (const t of taskResult.cards ?? []) {
      items.push({ id: `task:${t.id}`, label: `${t.humanId}: ${t.title}`, category: "tasks", icon: "#", run: () => void openTaskInNook(t.id) });
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
    const result = await invoke<{ matches: { file: string; line: number; text: string }[] }>("cove://commands/search.query", { query, bayId: "default" });
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

async function openBayLauncher(wsId: string): Promise<void> {
  const navigation = bayHeadNavigation(layout?.id ?? null, wsId);
  if (navigation.switchRequired) {
    await switchBay(wsId, null, null, navigation.showLauncher);
    return;
  }
  bayOverviewVisible = navigation.showLauncher;
  activeShoreId = null;
  focusedNookId = null;
  renderShore();
  renderShoreTabs();
  reconcileBrowserBounds();
}

async function switchBay(
  wsId: string,
  targetShoreId: string | null = null,
  targetNookId: string | null = null,
  showLauncher = false,
): Promise<void> {
  try {
    const generation = ++reloadGeneration;
    await invoke("cove://commands/bay.switch", { id: wsId });
    bayOverviewVisible = showLauncher;
    activeShoreId = targetShoreId;
    focusedNookId = null;
    const snapshot = await invoke<BaySnapshot>("cove://commands/layout.get", { bayId: wsId });
    if (generation !== reloadGeneration) return;
    applyLayoutSnapshot(snapshot);
    void hydrateNookTitles(generation);
    if (targetNookId) revealNook(targetNookId);
    await loadWings();
    renderShoreTabs();
  } catch { void 0; }
}

async function openTaskInNook(taskId: string): Promise<void> {
  try {
    const sp = (await spawnNook({ command: "", cwd: "", inheritCwdFrom: "", cols: 80, rows: 24, adapter: "", agentName: "", bay: "", shore: "" })).nookId;
    const r = await invoke<{ shoreId: string }>("app.layoutMutate", { op: "createShore", newNookId: sp, name: "Task", shoreId: "", targetNookId: "", orientation: "", nookId: "", dir: 0, nookType: "tasks-kanban" });
    activeShoreId = r.shoreId;
    nookFilePaths.set(sp, taskId);
    await reload();
    focusNook(sp);
  } catch { void 0; }
}

async function openFileInEditor(filePath: string): Promise<void> {
  try {
    const sp = (await spawnNook({ command: "", cwd: "", inheritCwdFrom: "", cols: 80, rows: 24, adapter: "", agentName: "", bay: "", shore: "" })).nookId;
    const r = await invoke<{ shoreId: string }>("app.layoutMutate", { op: "createShore", newNookId: sp, name: filePath.split("/").pop() || "Editor", shoreId: "", targetNookId: "", orientation: "", nookId: "", dir: 0, nookType: "editor" });
    activeShoreId = r.shoreId;
    nookFilePaths.set(sp, filePath);
    await reload();
    focusNook(sp);
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
  if (focusedNookId) nooks.get(focusedNookId)?.term.focus();
}
function isRealSetting(e: ConfigSchemaEntry): boolean {
  return e.control !== "section" && e.type !== "object";
}

function renderSettings(): void {
  const schemaTabs = [...new Set(configSchema.filter(isRealSetting).map((e) => e.tab))].sort();
  const tabs = orderSettingsTabs(schemaTabs);
  if (tabs.length === 0) {
    setTabsEl.innerHTML = "";
    setBodyEl.innerHTML = `<div style="padding:20px;color:var(--muted);text-align:center;">No settings available</div>`;
    return;
  }
  activeSettingsTab = resolveActiveSettingsTab(tabs, activeSettingsTab);

  setTabsEl.innerHTML = "";
  for (const tab of tabs) {
    const el = document.createElement("div");
    el.className = "set-nav-item" + (tab === activeSettingsTab ? " active" : "");
    const dot = document.createElement("span");
    dot.className = "set-nav-dot";
    const label = document.createElement("span");
    label.textContent = settingsTabLabel(tab);
    el.appendChild(dot);
    el.appendChild(label);
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
  if (activeSettingsTab === "dictation") {
    renderDictationTab(setBodyEl);
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
  if (activeSettingsTab === "audio") renderAudioExtras(setBodyEl);
}

function renderAudioExtras(container: HTMLElement): void {
  const header = document.createElement("div");
  header.className = "set-section-header";
  header.style.cssText = "padding:12px 0 4px;font-size:11px;font-weight:600;color:var(--muted);text-transform:uppercase;letter-spacing:0.5px;border-bottom:1px solid var(--border);";
  header.textContent = "Sound";
  container.appendChild(header);

  const row = document.createElement("div");
  row.className = "set-row";
  const label = document.createElement("label");
  const labelText = document.createElement("span");
  labelText.textContent = "Agent chimes";
  const desc = document.createElement("span");
  desc.className = "set-desc";
  desc.textContent = "Play a soft tone when an agent finishes or needs input. On by default.";
  label.appendChild(labelText);
  label.appendChild(desc);

  const controls = document.createElement("div");
  controls.style.cssText = "display:flex;align-items:center;gap:8px;";
  const preview = document.createElement("button");
  preview.className = "diag-btn";
  preview.style.marginTop = "0";
  preview.textContent = "Preview";
  preview.addEventListener("click", () => playChime("done"));
  const toggle = document.createElement("button");
  const paint = (): void => {
    const on = agentChimesEnabled();
    toggle.className = "diag-toggle" + (on ? " on" : "");
    toggle.textContent = on ? "On" : "Off";
    preview.disabled = !on;
  };
  toggle.addEventListener("click", () => {
    const next = !agentChimesEnabled();
    setAgentChimesEnabled(next);
    if (next) playChime("done");
    paint();
  });
  paint();
  controls.appendChild(preview);
  controls.appendChild(toggle);

  row.appendChild(label);
  row.appendChild(controls);
  container.appendChild(row);
}

interface ToolsListResponse { adapters: ToolsAdapter[]; }

async function renderToolsTab(container: HTMLElement): Promise<void> {
  container.innerHTML = "";

  const actions = document.createElement("div");
  actions.className = "tools-actions";
  actions.style.cssText = "display:flex;gap:8px;flex-wrap:wrap;margin-bottom:12px;";
  const rescanBtn = document.createElement("button");
  rescanBtn.className = "diag-btn";
  rescanBtn.style.marginTop = "0";
  rescanBtn.textContent = "Re-scan";
  rescanBtn.addEventListener("click", () => void doRescanAdapters(container, rescanBtn));
  const addBtn = document.createElement("button");
  addBtn.className = "diag-btn";
  addBtn.style.marginTop = "0";
  addBtn.textContent = "Add adapter from folder…";
  addBtn.addEventListener("click", () => void doAddAdapterFromFolder(container));
  const wizardBtn = document.createElement("button");
  wizardBtn.className = "diag-btn";
  wizardBtn.style.marginTop = "0";
  wizardBtn.textContent = "Re-run setup wizard";
  wizardBtn.addEventListener("click", () => rerunOnboarding());
  actions.appendChild(rescanBtn);
  actions.appendChild(addBtn);
  actions.appendChild(wizardBtn);
  container.appendChild(actions);

  let adapters: ToolsAdapter[];
  try {
    const result = await invoke<ToolsListResponse>("cove://commands/adapter.tools-list", {});
    adapters = result.adapters ?? [];
  } catch (err) {
    console.warn("adapter.tools-list failed for tools tab", err);
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
  for (const a of adapters) list.appendChild(buildToolsCard(a, container));
  container.appendChild(list);
}

function buildToolsCard(a: ToolsAdapter, container: HTMLElement): HTMLElement {
  const card = document.createElement("div");
  card.className = "tools-card";

  if (a.iconSvg) {
    const icon = document.createElement("span");
    icon.className = "tools-icon";
    icon.style.cssText = "width:18px;height:18px;display:inline-flex;color:" + (a.accent || "var(--accent)") + ";";
    icon.innerHTML = a.iconSvg;
    const svg = icon.querySelector("svg");
    if (svg) { svg.setAttribute("width", "18"); svg.setAttribute("height", "18"); }
    card.appendChild(icon);
  } else {
    const swatch = document.createElement("span");
    swatch.className = "tools-accent";
    swatch.style.background = a.accent || "var(--accent)";
    card.appendChild(swatch);
  }

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
  subtitle.textContent = toolsSubtitle(a.status, a.version, a.binaryPath, a.installHint);
  body.appendChild(subtitle);

  const manifestRow = document.createElement("div");
  manifestRow.className = "tools-manifest";
  manifestRow.style.cssText = "display:flex;align-items:center;gap:8px;justify-content:space-between;";
  const manifestName = document.createElement("span");
  manifestName.textContent = a.bundled ? `${a.name} · bundled` : a.name;
  manifestRow.appendChild(manifestName);
  if (a.removable) {
    const removeBtn = document.createElement("button");
    removeBtn.className = "diag-btn";
    removeBtn.style.cssText = "margin-top:0;padding:2px 8px;font-size:11px;";
    removeBtn.textContent = "Remove";
    removeBtn.addEventListener("click", () => openRemoveAdapterDialog(a, container));
    manifestRow.appendChild(removeBtn);
  }
  body.appendChild(manifestRow);

  if (retentionChipVisible(a.retention)) body.appendChild(buildRetentionChip(a, container));

  void buildProfilesSection(a, container).then((el) => body.appendChild(el));

  card.appendChild(body);
  return card;
}

const launcherProfileSlugKey = (adapter: string) => `cove:launcher-profile:${adapter}`;

interface ProfileListResult { profiles: LaunchProfileListItem[] }

async function resolveLauncherProfileSlug(adapter: string): Promise<string> {
  const stored = localStorage.getItem(launcherProfileSlugKey(adapter));
  const cached = launcherProfiles.get(adapter);
  if (cached) return selectedLauncherProfile(launcherProfileChoices(adapter, cached), stored)?.slug ?? "default";
  try {
    const result = await invoke<ProfileListResult>("cove://commands/launch-profile.list", { adapter });
    return selectedLauncherProfile(launcherProfileChoices(adapter, result.profiles ?? []), stored)?.slug ?? "default";
  } catch (err) {
    console.warn("launch-profile.list failed", adapter, err);
    return "default";
  }
}

async function buildProfilesSection(a: ToolsAdapter, container: HTMLElement): Promise<HTMLElement> {
  const section = document.createElement("div");
  section.className = "tools-profiles";
  section.style.cssText = "margin-top:10px;border-top:1px solid var(--border);padding-top:8px;";

  const header = document.createElement("div");
  header.style.cssText = "display:flex;align-items:center;justify-content:space-between;margin-bottom:6px;";
  const label = document.createElement("span");
  label.className = "tools-subtitle";
  label.textContent = "Launch profiles";
  header.appendChild(label);

  const newBtn = document.createElement("button");
  newBtn.className = "diag-btn";
  newBtn.style.cssText = "margin-top:0;padding:2px 8px;font-size:11px;";
  newBtn.textContent = "New profile";
  newBtn.addEventListener("click", () => openProfileEditor(a, null, () => renderToolsTab(container)));
  header.appendChild(newBtn);
  section.appendChild(header);

  const listEl = document.createElement("div");
  listEl.style.cssText = "display:flex;flex-direction:column;gap:4px;";
  section.appendChild(listEl);

  try {
    const result = await invoke<ProfileListResult>("cove://commands/launch-profile.list", { adapter: a.name });
    renderProfileList(a, result.profiles ?? [], listEl, container);
  } catch (err) {
    console.warn("launch-profile.list failed", a.name, err);
    const note = document.createElement("div");
    note.className = "tools-subtitle";
    note.textContent = "profiles unavailable";
    listEl.appendChild(note);
  }
  return section;
}

function renderProfileList(
  a: ToolsAdapter,
  profiles: LaunchProfileListItem[],
  listEl: HTMLElement,
  container: HTMLElement,
): void {
  listEl.innerHTML = "";
  if (profiles.length === 0) {
    const empty = document.createElement("div");
    empty.className = "tools-subtitle";
    empty.textContent = "no profiles — using defaults";
    listEl.appendChild(empty);
    return;
  }
  for (const p of profiles) {
    const row = document.createElement("div");
    row.style.cssText = "display:flex;align-items:center;gap:6px;font-size:12px;";
    const radio = document.createElement("input");
    radio.type = "radio";
    radio.name = `profile-radio-${a.name}`;
    const storedSlug = localStorage.getItem(launcherProfileSlugKey(a.name));
    radio.checked = storedSlug ? storedSlug === p.slug : p.isDefault;
    radio.addEventListener("change", () => {
      localStorage.setItem(launcherProfileSlugKey(a.name), p.slug);
      void invoke("cove://commands/launch-profile.set-default", { adapter: a.name, slug: p.slug }).catch((err) => console.warn("launch-profile.set-default failed", a.name, p.slug, err));
    });
    row.appendChild(radio);
    const name = document.createElement("span");
    name.textContent = profilePickerLabel(p);
    name.style.flex = "1";
    row.appendChild(name);
    const editBtn = document.createElement("button");
    editBtn.className = "diag-btn";
    editBtn.style.cssText = "margin-top:0;padding:1px 6px;font-size:11px;";
    editBtn.textContent = "Edit";
    editBtn.addEventListener("click", () => openProfileEditor(a, p.slug, () => renderToolsTab(container)));
    row.appendChild(editBtn);
    if (profiles.length > 1) {
      const delBtn = document.createElement("button");
      delBtn.className = "diag-btn";
      delBtn.style.cssText = "margin-top:0;padding:1px 6px;font-size:11px;";
      delBtn.textContent = "Delete";
      delBtn.addEventListener("click", async () => {
        await invoke("cove://commands/launch-profile.delete", { adapter: a.name, slug: p.slug });
        await renderToolsTab(container);
      });
      row.appendChild(delBtn);
    }
    listEl.appendChild(row);
  }
}

function openProfileEditor(
  a: { name: string; binary: string },
  slug: string | null,
  onSaved: (savedSlug: string) => void | Promise<void>,
): void {
  const overlay = document.createElement("div");
  overlay.style.cssText = "position:fixed;inset:0;background:rgba(0,0,0,0.5);display:flex;align-items:center;justify-content:center;z-index:1000;";
  const dialog = document.createElement("div");
  dialog.className = "settings-dialog";
  dialog.style.cssText = "background:var(--bg);border:1px solid var(--border);border-radius:8px;padding:16px;min-width:380px;max-width:520px;display:flex;flex-direction:column;gap:10px;";
  dialog.addEventListener("click", (e) => e.stopPropagation());

  const title = document.createElement("div");
  title.style.fontWeight = "600";
  title.textContent = slug ? `Edit profile · ${slug}` : `New profile · ${a.name}`;
  dialog.appendChild(title);

  const nameInput = textRow(dialog, "Profile name", "", "e.g. Claude Code (ccx)",
    "What this profile is called in the launcher");
  const slugInput = textRow(dialog, "Profile ID", slug ?? "", "auto-generated from the name",
    "Lowercase identifier used by the CLI and config; leave empty to generate it from the name");
  slugInput.disabled = slug !== null;
  const commandInput = textRow(dialog, "Command", "", `defaults to ${a.binary}`,
    `Program to launch instead of ${a.binary} — a name on PATH or a full path like ~/bin/my-wrapper`);
  const modelInput = textRow(dialog, "Model", "", "e.g. opus, sonnet, default",
    "Passed to the CLI as its model flag; empty uses the tool's own default");
  const effortInput = textRow(dialog, "Reasoning effort", "", "e.g. low, medium, high, max",
    "Passed to the CLI when set; empty uses the tool's own default");
  const agentInput = textRow(dialog, "Agent", "", "optional",
    "ID of a saved agent definition to launch with this profile");

  const envLabel = document.createElement("div");
  envLabel.className = "tools-subtitle";
  envLabel.textContent = "Environment variables (KEY=VALUE, one per line)";
  dialog.appendChild(envLabel);
  const envArea = document.createElement("textarea");
  envArea.rows = 4;
  envArea.style.cssText = "width:100%;font-family:var(--font-mono,monospace);font-size:12px;background:var(--bg-alt);color:var(--fg);border:1px solid var(--border);border-radius:4px;padding:4px;";
  dialog.appendChild(envArea);

  const argsLabel = document.createElement("div");
  argsLabel.className = "tools-subtitle";
  argsLabel.textContent = "Extra CLI args (one per line)";
  dialog.appendChild(argsLabel);
  const argsArea = document.createElement("textarea");
  argsArea.rows = 3;
  argsArea.style.cssText = "width:100%;font-family:var(--font-mono,monospace);font-size:12px;background:var(--bg-alt);color:var(--fg);border:1px solid var(--border);border-radius:4px;padding:4px;";
  dialog.appendChild(argsArea);

  const defaultCb = document.createElement("input");
  defaultCb.type = "checkbox";
  const defaultLabel = document.createElement("label");
  defaultLabel.style.cssText = "display:flex;align-items:center;gap:6px;font-size:12px;";
  defaultLabel.appendChild(defaultCb);
  const defaultText = document.createElement("span");
  defaultText.textContent = "Set as default profile";
  defaultLabel.appendChild(defaultText);
  dialog.appendChild(defaultLabel);

  const errorEl = document.createElement("div");
  errorEl.style.cssText = "color:#e0a44a;font-size:11px;min-height:14px;";
  dialog.appendChild(errorEl);

  const buttonRow = document.createElement("div");
  buttonRow.style.cssText = "display:flex;gap:8px;justify-content:flex-end;";
  const cancelBtn = document.createElement("button");
  cancelBtn.className = "diag-btn";
  cancelBtn.textContent = "Cancel";
  cancelBtn.addEventListener("click", () => overlay.remove());
  const saveBtn = document.createElement("button");
  saveBtn.className = "diag-btn";
  saveBtn.textContent = slug ? "Save" : "Create";
  saveBtn.addEventListener("click", async () => {
    errorEl.textContent = "";
    const newSlug = deriveProfileSlug(slugInput.value) || deriveProfileSlug(nameInput.value);
    if (!slug && !isValidProfileSlug(newSlug)) {
      errorEl.textContent = "enter a profile name, or an ID using lowercase letters, digits, and dashes";
      return;
    }
    const envRows: Array<{ key: string; value: string }> = [];
    for (const line of envArea.value.split("\n")) {
      const trimmed = line.trim();
      if (!trimmed) continue;
      const eq = trimmed.indexOf("=");
      if (eq < 0) { errorEl.textContent = `env line "${trimmed}" must be KEY=VALUE`; return; }
      envRows.push({ key: trimmed.slice(0, eq).trim(), value: trimmed.slice(eq + 1).trim() });
    }
    const env = envMapFromRows(envRows);
    const extraArgLines = argsArea.value.split("\n").map((s) => s.trim()).filter((s) => s.length > 0);
    const commandValue = commandInput.value.trim();
    const cliArgs = commandValue
      ? [commandValue, ...extraArgLines]
      : extraArgLines.length > 0
        ? [a.binary, ...extraArgLines]
        : [];
    const base = {
      adapter: a.name,
      slug: slug ?? newSlug,
      name: nameInput.value.trim() || (slug ?? newSlug),
      model: modelInput.value.trim() || null,
      effort: effortInput.value.trim() || null,
      cliArgs,
      env,
      agent: agentInput.value.trim() || null,
      isDefault: defaultCb.checked,
    };
    try {
      if (slug) {
        const update: UpdateProfileInput = base;
        await invoke("cove://commands/launch-profile.update", update);
      } else {
        const create: CreateProfileInput = base;
        await invoke("cove://commands/launch-profile.create", create);
      }
      overlay.remove();
      await onSaved(base.slug);
    } catch (err) {
      errorEl.textContent = String(err);
    }
  });
  buttonRow.appendChild(cancelBtn);
  buttonRow.appendChild(saveBtn);
  dialog.appendChild(buttonRow);

  overlay.appendChild(dialog);
  overlay.addEventListener("click", () => overlay.remove());
  document.body.appendChild(overlay);

  if (slug) {
    void loadProfileIntoEditor(a.name, slug, { nameInput, commandInput, modelInput, effortInput, agentInput, envArea, argsArea, defaultCb });
  } else {
    defaultCb.checked = true;
  }
}

interface EditorFields {
  nameInput: HTMLInputElement;
  commandInput: HTMLInputElement;
  modelInput: HTMLInputElement;
  effortInput: HTMLInputElement;
  agentInput: HTMLInputElement;
  envArea: HTMLTextAreaElement;
  argsArea: HTMLTextAreaElement;
  defaultCb: HTMLInputElement;
}

async function loadProfileIntoEditor(adapter: string, slug: string, f: EditorFields): Promise<void> {
  try {
    const detail = await invoke<LaunchProfileDetail>("cove://commands/launch-profile.get", { adapter, slug });
    f.nameInput.value = detail.name;
    f.modelInput.value = detail.model ?? "";
    f.effortInput.value = detail.effort ?? "";
    f.agentInput.value = detail.agent ?? "";
    f.envArea.value = Object.entries(detail.env).map(([k, v]) => `${k}=${v}`).join("\n");
    const cliArgs = detail.cliArgs ?? [];
    f.commandInput.value = cliArgs[0] ?? "";
    f.argsArea.value = cliArgs.slice(1).join("\n");
    f.defaultCb.checked = detail.isDefault;
  } catch (err) {
    console.warn("launch-profile.get failed", adapter, slug, err);
  }
}

function textRow(parent: HTMLElement, label: string, value: string, placeholder: string, hint?: string): HTMLInputElement {
  const row = document.createElement("div");
  row.style.cssText = "display:flex;flex-direction:column;gap:2px;";
  const lab = document.createElement("label");
  lab.className = "tools-subtitle";
  lab.textContent = label;
  row.appendChild(lab);
  if (hint) {
    const hintEl = document.createElement("div");
    hintEl.style.cssText = "font-size:10.5px;color:var(--muted);opacity:0.85;";
    hintEl.textContent = hint;
    row.appendChild(hintEl);
  }
  const input = document.createElement("input");
  input.type = "text";
  input.value = value;
  input.placeholder = placeholder;
  input.style.cssText = "width:100%;background:var(--bg-alt);color:var(--fg);border:1px solid var(--border);border-radius:4px;padding:4px;font-size:12px;";
  row.appendChild(input);
  parent.appendChild(row);
  return input;
}

function buildRetentionChip(a: ToolsAdapter, container: HTMLElement): HTMLElement {
  const chip = document.createElement("div");
  chip.className = "tools-retention";
  chip.style.cssText = "display:flex;align-items:center;gap:8px;margin-top:6px;";
  const label = document.createElement("span");
  label.className = "set-desc";
  label.textContent = retentionChipLabel(a.retention);
  chip.appendChild(label);

  if (a.retention.editable) {
    const input = document.createElement("input");
    input.type = "text";
    input.className = "diag-input";
    input.style.cssText = "width:64px;padding:2px 6px;margin:0;";
    input.value = a.retention.value ?? "";
    input.placeholder = a.retention.recommended ?? "";
    const save = document.createElement("button");
    save.className = "diag-btn";
    save.style.cssText = "margin-top:0;padding:2px 8px;font-size:11px;";
    save.textContent = "Extend";
    save.addEventListener("click", () => void doSetRetention(a.name, input.value, container));
    chip.appendChild(input);
    chip.appendChild(save);
  }
  return chip;
}

async function doRescanAdapters(container: HTMLElement, btn: HTMLButtonElement): Promise<void> {
  btn.disabled = true;
  try {
    await invoke("cove://commands/adapter.rescan", {});
  } catch (e) {
    console.warn("adapter.rescan failed", e);
    showInAppToast("Re-scan failed", (e as Error).message, () => {});
  } finally {
    btn.disabled = false;
    await renderToolsTab(container);
  }
}

async function doAddAdapterFromFolder(container: HTMLElement): Promise<void> {
  let picked: unknown;
  try {
    picked = await window.__ryn.invoke("dialog.openFolder", { initialPath: activeProjectDir() || "/" });
  } catch (e) {
    console.warn("adapter folder picker failed", e);
    return;
  }
  if (picked === null) return;
  const path = typeof picked === "string" ? picked.trim() : "";
  if (!path) { console.warn("adapter folder picker returned nothing", picked); return; }
  try {
    const res = await invoke<{ name: string }>("cove://commands/adapter.install-local", { path });
    showInAppToast("Adapter added", `${res.name} installed from folder.`, () => {});
  } catch (e) {
    showInAppToast("Adapter not added", (e as Error).message, () => {});
  }
  await renderToolsTab(container);
}

function openRemoveAdapterDialog(a: ToolsAdapter, container: HTMLElement): void {
  const scrim = document.createElement("div");
  scrim.className = "modal-scrim open";
  scrim.style.cssText = "position:fixed;inset:0;background:rgba(0,0,0,0.4);display:flex;align-items:center;justify-content:center;z-index:9999;";
  const box = document.createElement("div");
  box.style.cssText = "background:var(--surface,#1e1e2e);border:1px solid var(--border);border-radius:10px;padding:18px;width:320px;max-width:90vw;";
  const title = document.createElement("div");
  title.style.cssText = "font-weight:600;margin-bottom:8px;";
  title.textContent = `Remove ${a.displayName || a.name}?`;
  const desc = document.createElement("div");
  desc.className = "set-desc";
  desc.style.marginBottom = "12px";
  desc.textContent = "This deletes the adapter folder. Bundled adapters are not affected.";
  const purgeLabel = document.createElement("label");
  purgeLabel.style.cssText = "display:flex;align-items:center;gap:8px;font-size:12px;margin-bottom:14px;";
  const purge = document.createElement("input");
  purge.type = "checkbox";
  const purgeText = document.createElement("span");
  purgeText.textContent = "Also delete session records for this adapter";
  purgeLabel.appendChild(purge);
  purgeLabel.appendChild(purgeText);
  const btnRow = document.createElement("div");
  btnRow.style.cssText = "display:flex;gap:8px;justify-content:flex-end;";
  const cancel = document.createElement("button");
  cancel.className = "diag-btn";
  cancel.style.marginTop = "0";
  cancel.textContent = "Cancel";
  const confirm = document.createElement("button");
  confirm.className = "diag-btn";
  confirm.style.marginTop = "0";
  confirm.textContent = "Remove";
  const close = (): void => scrim.remove();
  cancel.addEventListener("click", close);
  scrim.addEventListener("mousedown", (e) => { if (e.target === scrim) close(); });
  confirm.addEventListener("click", () => {
    close();
    void doRemoveAdapter(a.name, purge.checked, container);
  });
  btnRow.appendChild(cancel);
  btnRow.appendChild(confirm);
  box.appendChild(title);
  box.appendChild(desc);
  box.appendChild(purgeLabel);
  box.appendChild(btnRow);
  scrim.appendChild(box);
  document.body.appendChild(scrim);
}

async function doRemoveAdapter(name: string, purgeSessions: boolean, container: HTMLElement): Promise<void> {
  try {
    const res = await invoke<{ name: string; purgedSessions: number }>("cove://commands/adapter.remove", { name, purgeSessions });
    const suffix = res.purgedSessions > 0 ? ` (${res.purgedSessions} session records purged)` : "";
    showInAppToast("Adapter removed", `${res.name} removed${suffix}.`, () => {});
  } catch (e) {
    showInAppToast("Remove failed", (e as Error).message, () => {});
  }
  await renderToolsTab(container);
}

async function doSetRetention(name: string, value: string, container: HTMLElement): Promise<void> {
  try {
    await invoke("cove://commands/adapter.retention-set", { name, value: value.trim() });
    showInAppToast("Retention updated", `${name} retention set to ${value.trim()}.`, () => {});
  } catch (e) {
    showInAppToast("Retention not saved", (e as Error).message, () => {});
  }
  await renderToolsTab(container);
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
  note.textContent = "In-page flame graphs are not available yet: a bundle's optional trace is a binary .nettrace with no in-webview parser or viewer — open it in an external profiler such as PerfView or dotnet-trace. Per-nook element inspection is available now from any browser nook menu (DevTools).";
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
  if (changedKey === null || changedKey === "appearance.nookLight") { const pl = await get("appearance.nookLight") === "true"; root.style.setProperty("--nook-light", pl ? "1" : "0"); }
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
  for (const pv of nooks.values()) { pv.term.options.theme = termTheme; }
}

function revertThemeVars(): void {
  if (!themeAppliedVars) return;
  const root = document.documentElement;
  for (const k of Object.keys(themeAppliedVars)) { root.style.removeProperty(k); }
  themeAppliedVars = null;
  activeThemeDto = null;
  if (themeAppliedTermTheme) {
    const restored = { ...THEME, background: themeBackgroundWithOpacity(settings.backgroundOpacity) };
    for (const pv of nooks.values()) { pv.term.options.theme = restored; }
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

  if (step.id === "harness") { void renderHarnessStep(body); }
  if (step.id === "permissions") { void renderPermissionsStep(body); }
  if (step.id === "appearance") { renderAppearanceStep(body); }
  if (step.id === "sound") { renderSoundStep(body); }
  if (step.id === "dictation") { renderDictationStep(body); }

  const prevBtn = onboardingEl.querySelector(".ob-prev") as HTMLButtonElement;
  const nextBtn = onboardingEl.querySelector(".ob-next") as HTMLButtonElement;
  prevBtn.disabled = isFirstStep(onboardingState);
  nextBtn.textContent = isLastStep(onboardingState) ? "Finish" : "Next";
}

async function loadWizardAdapters(): Promise<ToolsAdapter[]> {
  try {
    const result = await invoke<ToolsListResponse>("cove://commands/adapter.tools-list", {});
    return result.adapters ?? [];
  } catch (e) {
    console.warn("wizard adapter list failed", e);
    return [];
  }
}

async function renderHarnessStep(body: HTMLElement): Promise<void> {
  const grid = document.createElement("div");
  grid.className = "ob-adapter-list";
  body.appendChild(grid);
  const adapters = await loadWizardAdapters();
  grid.className = "ob-adapter-list" + (adapters.length > 4 ? " ob-grid-2" : "");
  if (adapters.length === 0) {
    const none = document.createElement("div");
    none.className = "ob-adapter";
    none.textContent = "No tools detected yet — add one later from Settings → Tools.";
    grid.appendChild(none);
  }
  for (const a of adapters) {
    const el = document.createElement("div");
    el.className = "ob-adapter";
    const meta = adapterStatusMeta(a.status);
    const name = document.createElement("span");
    name.className = "ob-adapter-name";
    name.textContent = a.displayName || a.name;
    const dot = document.createElement("span");
    dot.className = "tools-dot";
    dot.style.cssText = `background:${meta.cssColor};margin-left:8px;`;
    name.appendChild(dot);
    el.appendChild(name);
    grid.appendChild(el);
  }

  try {
    const listed = await invoke<AdapterListResult>("app.adapterList", {});
    const installable = mapLauncherAdapters(listed.adapters).filter((a) => a.status !== "detected" && (a.installCommand ?? "").trim().length > 0);
    if (installable.length > 0) {
      const installLabel = document.createElement("div");
      installLabel.className = "tools-subtitle";
      installLabel.style.marginTop = "12px";
      installLabel.textContent = "Install more harnesses";
      body.appendChild(installLabel);
      const installGrid = document.createElement("div");
      installGrid.className = "ob-adapter-list" + (installable.length > 4 ? " ob-grid-2" : "");
      for (const a of installable) {
        const row = document.createElement("div");
        row.className = "ob-adapter";
        const rowName = document.createElement("span");
        rowName.className = "ob-adapter-name";
        rowName.textContent = a.displayName || a.name;
        const installBtn = document.createElement("button");
        installBtn.className = "diag-btn ob-install-btn";
        installBtn.textContent = "+";
        installBtn.title = a.installCommand ?? "";
        installBtn.addEventListener("click", () => {
          completeOnboarding();
          void launchHarnessShellTask(a.installCommand ?? "", `Install ${a.displayName || a.name}`);
        });
        row.appendChild(rowName);
        row.appendChild(installBtn);
        installGrid.appendChild(row);
      }
      body.appendChild(installGrid);
    }
  } catch (err) {
    console.warn("onboarding install list failed", err);
  }

  const dirRow = document.createElement("div");
  dirRow.style.cssText = "display:flex;gap:8px;align-items:center;margin-top:12px;";
  const dirInput = document.createElement("input");
  dirInput.type = "text";
  dirInput.className = "diag-input";
  dirInput.style.cssText = "flex:1;margin:0;padding:4px 8px;";
  dirInput.placeholder = "Default bay directory";
  dirInput.value = onboardingState.defaultBayDir ?? "";
  dirInput.addEventListener("input", () => { onboardingState = setDefaultBayDir(onboardingState, dirInput.value.trim() || null); });
  const browse = document.createElement("button");
  browse.className = "diag-btn";
  browse.style.marginTop = "0";
  browse.textContent = "Browse…";
  browse.addEventListener("click", async () => {
    try {
      const picked = await window.__ryn.invoke("dialog.openFolder", { initialPath: dirInput.value.trim() || "/" });
      if (typeof picked === "string" && picked.trim()) {
        dirInput.value = picked.trim();
        onboardingState = setDefaultBayDir(onboardingState, picked.trim());
      }
    } catch (e) { console.warn("wizard folder picker failed", e); }
  });
  dirRow.appendChild(dirInput);
  dirRow.appendChild(browse);
  body.appendChild(dirRow);
}

async function renderPermissionsStep(body: HTMLElement): Promise<void> {
  const list = document.createElement("div");
  list.className = "ob-adapter-list";
  body.appendChild(list);
  const adapters = await loadWizardAdapters();
  if (adapters.length === 0) {
    const none = document.createElement("div");
    none.className = "ob-adapter";
    none.textContent = "No adapters to configure yet.";
    list.appendChild(none);
    return;
  }
  for (const a of adapters) {
    const row = document.createElement("label");
    row.className = "ob-telemetry-toggle";
    row.style.cssText = "display:flex;align-items:center;gap:8px;justify-content:space-between;";
    const name = document.createElement("span");
    name.textContent = `${a.displayName || a.name} — bypass permissions (YOLO)`;
    name.style.fontSize = "12px";
    const cb = document.createElement("input");
    cb.type = "checkbox";
    cb.checked = onboardingState.adapterYolo[a.name] ?? launcherYolo(a.name);
    cb.addEventListener("change", () => { onboardingState = setAdapterYolo(onboardingState, a.name, cb.checked); });
    row.appendChild(name);
    row.appendChild(cb);
    list.appendChild(row);
  }
}

function renderAppearanceStep(body: HTMLElement): void {
  const backdropRow = document.createElement("div");
  backdropRow.style.cssText = "display:flex;gap:8px;align-items:center;margin-top:10px;";
  const backdropLabel = document.createElement("span");
  backdropLabel.style.cssText = "font-size:12px;min-width:80px;";
  backdropLabel.textContent = "Backdrop";
  const backdropSel = document.createElement("select");
  backdropSel.className = "diag-input";
  backdropSel.style.cssText = "margin:0;padding:4px 8px;";
  for (const m of ["none", "blur", "acrylic", "mica"]) {
    const opt = document.createElement("option");
    opt.value = m;
    opt.textContent = m;
    backdropSel.appendChild(opt);
  }
  backdropSel.value = onboardingState.backdrop || backdropMaterial;
  backdropSel.addEventListener("change", () => {
    onboardingState = setOnboardingBackdrop(onboardingState, backdropSel.value);
    backdropMaterial = coerceMaterial(backdropSel.value);
    void setBackdropMaterial(backdropMaterial, backdropDeps);
  });
  backdropRow.appendChild(backdropLabel);
  backdropRow.appendChild(backdropSel);
  body.appendChild(backdropRow);

  const themeRow = document.createElement("div");
  themeRow.style.cssText = "display:flex;gap:8px;align-items:center;margin-top:10px;";
  const themeLabel = document.createElement("span");
  themeLabel.style.cssText = "font-size:12px;min-width:80px;";
  themeLabel.textContent = "Theme";
  const themeSel = document.createElement("select");
  themeSel.className = "diag-input";
  themeSel.style.cssText = "margin:0;padding:4px 8px;";
  themeRow.appendChild(themeLabel);
  themeRow.appendChild(themeSel);
  body.appendChild(themeRow);
  void invoke<{ themes: ThemeDto[] }>("cove://commands/theme.list", {}).then((r) => {
    for (const t of r.themes ?? []) {
      const opt = document.createElement("option");
      opt.value = t.name;
      opt.textContent = t.name;
      themeSel.appendChild(opt);
    }
    if (onboardingState.theme) themeSel.value = onboardingState.theme;
    else if (themeActiveName) themeSel.value = themeActiveName;
  }).catch(() => { void 0; });
  themeSel.addEventListener("change", () => {
    onboardingState = setOnboardingTheme(onboardingState, themeSel.value);
    void invoke("cove://commands/theme.set-active", { name: themeSel.value }).catch((e) => console.warn("wizard theme set failed", e));
  });
}

function renderSoundStep(body: HTMLElement): void {
  const toggle = document.createElement("label");
  toggle.className = "ob-telemetry-toggle";
  toggle.style.cssText = "display:flex;align-items:center;gap:8px;";
  const cb = document.createElement("input");
  cb.type = "checkbox";
  cb.checked = onboardingState.agentChimes;
  const label = document.createElement("span");
  label.textContent = "Agent chimes — soft tone when an agent finishes or needs input";
  label.style.cssText = "font-size:12px;color:var(--fg);";
  cb.addEventListener("change", () => {
    onboardingState = setOnboardingAgentChimes(onboardingState, cb.checked);
    setAgentChimesEnabled(cb.checked);
    if (cb.checked) playChime("done");
  });
  toggle.appendChild(cb);
  toggle.appendChild(label);
  body.appendChild(toggle);
}

interface DictationStatusResult { state?: string; modelReady?: boolean }

async function dictationStatus(): Promise<DictationStatusResult> {
  try {
    return JSON.parse(String(await window.__ryn.invoke("app.dictationStatus", {}))) as DictationStatusResult;
  } catch (e) {
    console.warn("dictation status failed", e);
    return {};
  }
}

function dictationPrefRow(key: string, title: string, hint: string): HTMLElement {
  const row = document.createElement("div");
  row.className = "set-row";
  const label = document.createElement("label");
  label.style.cssText = "display:flex;flex-direction:column;gap:2px;";
  const name = document.createElement("span");
  name.textContent = title;
  const sub = document.createElement("span");
  sub.style.cssText = "font-size:11px;color:var(--muted);";
  sub.textContent = hint;
  label.appendChild(name);
  label.appendChild(sub);
  const cb = document.createElement("input");
  cb.type = "checkbox";
  cb.checked = dictationToggleEnabled(localStorage.getItem(key));
  cb.addEventListener("change", () => localStorage.setItem(key, cb.checked ? "true" : "false"));
  row.appendChild(label);
  row.appendChild(cb);
  return row;
}

let dictationModelError: string | null = null;

function buildDictationModelControls(container: HTMLElement): void {
  const status = document.createElement("div");
  status.style.cssText = "display:flex;gap:10px;align-items:center;margin-top:10px;";
  const text = document.createElement("span");
  text.style.cssText = "font-size:12px;color:var(--muted);";
  text.textContent = "Speech model: checking…";
  const btn = document.createElement("button");
  btn.className = "diag-btn";
  btn.style.cssText = "margin:0;";
  btn.textContent = "Download now";
  btn.style.display = "none";
  status.appendChild(text);
  status.appendChild(btn);
  container.appendChild(status);

  const refresh = async (): Promise<void> => {
    const s = await dictationStatus();
    if (!status.isConnected) return;
    if (s.modelReady) {
      text.textContent = "Speech model: Parakeet TDT 0.6B v3 — downloaded";
      btn.style.display = "none";
    } else {
      text.textContent = "Speech model: Parakeet TDT 0.6B v3 (487 MB) — not downloaded";
      btn.style.display = "";
    }
  };
  void refresh();
  btn.addEventListener("click", () => {
    btn.disabled = true;
    btn.textContent = "Downloading…";
    dictationModelError = null;
    const fail = (msg: string): void => {
      if (!status.isConnected) return;
      text.textContent = `Speech model: download failed — ${msg}`;
      btn.disabled = false;
      btn.textContent = "Retry";
      btn.style.display = "";
    };
    void window.__ryn.invoke("app.dictationEnsureModel", {}).catch((e) => {
      console.warn("dictation model download failed", e);
      fail(String(e));
    });
    const poll = window.setInterval(() => {
      void dictationStatus().then((s) => {
        if (!status.isConnected) {
          clearInterval(poll);
          return;
        }
        const outcome = modelPollOutcome(s.modelReady, dictationModelError);
        if (outcome.kind === "ready") {
          clearInterval(poll);
          btn.disabled = false;
          btn.textContent = "Download now";
          void refresh();
        } else if (outcome.kind === "failed") {
          clearInterval(poll);
          fail(outcome.error);
        }
      });
    }, 2000);
  });
}

function renderDictationTab(container: HTMLElement): void {
  container.innerHTML = "";
  const info = document.createElement("p");
  info.style.cssText = "font-size:12px;color:var(--muted);margin:12px 0;line-height:1.5;";
  info.textContent = "Hold F9 — or hold Space in a terminal or text field — to dictate. Speech is recognized on this machine with NVIDIA Parakeet; audio never leaves it. Words stream in live and settle when you release.";
  container.appendChild(info);
  container.appendChild(dictationPrefRow(DICTATION_SPACE_KEY, "Hold Space to dictate", "Long-press Space (~300 ms) starts dictation; a quick tap still types a space."));
  container.appendChild(dictationPrefRow(DICTATION_LIVE_TYPING_KEY, "Type live preview into the focused target", "Off shows the running transcript in the status pill only; text lands on release."));
  buildDictationModelControls(container);
}

function renderDictationStep(body: HTMLElement): void {
  const toggle = document.createElement("label");
  toggle.className = "ob-telemetry-toggle";
  toggle.style.cssText = "display:flex;align-items:center;gap:8px;";
  const cb = document.createElement("input");
  cb.type = "checkbox";
  cb.checked = dictationToggleEnabled(localStorage.getItem(DICTATION_SPACE_KEY));
  cb.addEventListener("change", () => localStorage.setItem(DICTATION_SPACE_KEY, cb.checked ? "true" : "false"));
  const label = document.createElement("span");
  label.textContent = "Hold Space to dictate — a quick tap still types a space (F9 always works)";
  label.style.cssText = "font-size:12px;color:var(--fg);";
  toggle.appendChild(cb);
  toggle.appendChild(label);
  body.appendChild(toggle);
  buildDictationModelControls(body);
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
  for (const [adapter, on] of Object.entries(onboardingState.adapterYolo)) {
    localStorage.setItem(launcherYoloKey(adapter), String(on));
  }
  setAgentChimesEnabled(onboardingState.agentChimes);
  try {
    await invoke("app.configSet", { key: ONBOARDING_COMPLETED_KEY, value: "true" });
    if (onboardingState.defaultBayDir) { await invoke("app.configSet", { key: "bays.defaultDir", value: onboardingState.defaultBayDir }); }
    await invoke("app.configSet", { key: BACKDROP_PREF_KEY, value: onboardingState.backdrop });
    if (onboardingState.theme) { await invoke("app.configSet", { key: "appearance.theme", value: onboardingState.theme }); }
  } catch (e) { console.warn("onboarding persist failed", e); }
}

function rerunOnboarding(): void {
  onboardingState = { ...INITIAL_ONBOARDING_STATE, backdrop: backdropMaterial, theme: themeActiveName, agentChimes: agentChimesEnabled() };
  onboardingEl.classList.add("open");
  renderOnboarding();
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
function activeSearch(): SearchAddon | null { return focusedNookId ? (nooks.get(focusedNookId)?.search ?? null) : null; }
function openFind() { findEl.classList.add("open"); findInput.focus(); findInput.select(); }
function closeFind() { findEl.classList.remove("open"); activeSearch()?.clearDecorations(); if (focusedNookId) nooks.get(focusedNookId)?.term.focus(); }
async function doFind(dir: number) {
  const s = activeSearch();
  const q = findInput.value;
  if (!s || !q) return;
  const nookId = focusedNookId!;
  try {
    const res = await invoke<{ matches: { line: number; text: string }[] }>("app.nookSearch", { nookId, query: q, caseSensitive: false });
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
function closeLauncher() { launcherEl.classList.remove("open"); if (focusedNookId) nooks.get(focusedNookId)?.term.focus(); }
launcherEl.addEventListener("mousedown", (e) => { if (e.target === launcherEl) closeLauncher(); });
launcherEl.addEventListener("keydown", (e) => { if (e.key === "Escape") closeLauncher(); });

const launchAgentsEl = document.getElementById("launch-agents")!;
interface AdapterInfo { name: string; displayName: string; accent: string; binary: string; status?: string | null; version?: string | null; binaryPath?: string | null; updateCommand?: string | null; installCommand?: string | null; uninstallCommand?: string | null; }
interface AdapterListResult { adapters: AdapterInfo[]; }
function mapLauncherAdapters(adapters: AdapterInfo[] | null | undefined): LauncherAdapter[] {
  return (adapters ?? []).map((a) => ({
    name: a.name,
    displayName: a.displayName,
    accent: a.accent,
    binary: a.binary,
    version: a.version ?? "",
    status: a.status ?? "",
    updateCommand: a.updateCommand ?? "",
    installCommand: a.installCommand ?? "",
    uninstallCommand: a.uninstallCommand ?? "",
  }));
}
async function launchHarnessUpdate(tile: LauncherTile): Promise<void> {
  if (!tile.updateCommand) return;
  try {
    const sp = (await spawnNook({ command: "", args: [], shellCommand: tile.updateCommand, cwd: "", inheritCwdFrom: "", cols: 80, rows: 24, adapter: "", agentName: `Update ${tile.label}`, bay: "", shore: "" })).nookId;
    const r = await invoke<{ shoreId: string }>("app.layoutMutate", { op: "createShore", newNookId: sp, name: `Update ${tile.label}`, shoreId: "", targetNookId: "", orientation: "", nookId: "", dir: 0, nookType: "terminal" });
    activeShoreId = r.shoreId;
    await reload();
    focusNook(sp);
  } catch (err) {
    console.warn("harness update launch failed", tile.adapterName, err);
    showInAppToast("Update not started", (err as Error).message, () => {});
  }
}

async function launchHarnessShellTask(commandLine: string, shoreName: string): Promise<void> {
  try {
    const sp = (await spawnNook({ command: "", args: [], shellCommand: commandLine, cwd: "", inheritCwdFrom: "", cols: 80, rows: 24, adapter: "", agentName: shoreName, bay: "", shore: "" })).nookId;
    const r = await invoke<{ shoreId: string }>("app.layoutMutate", { op: "createShore", newNookId: sp, name: shoreName, shoreId: "", targetNookId: "", orientation: "", nookId: "", dir: 0, nookType: "terminal" });
    activeShoreId = r.shoreId;
    await reload();
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
      await invoke("cove://commands/adapter.rescan", {});
      const result = await invoke<AdapterListResult>("app.adapterList", {});
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
    const result = await invoke<AdapterListResult>("app.adapterList", {});
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
    const built = await invoke<{ command: string; args: string[] }>("cove://commands/launch.build", {
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
      await invoke("app.layoutMutate", { op: "replace", shoreId, targetNookId: safePlaceholder, newNookId: sp, orientation: "", name: "", nookId: "", dir: 0, nookType: "terminal" });
    }
    activeShoreId = shoreId;
  } else {
    const r = await invoke<{ shoreId: string }>("app.layoutMutate", { op: "createShore", newNookId: sp, name: nextShoreName(), shoreId: "", targetNookId: "", orientation: "", nookId: "", dir: 0 });
    activeShoreId = r.shoreId;
  }
  await reload();
  focusNook(sp);
}

let launcherAdapters: LauncherAdapter[] = [];
let launcherRecents: RecentSessionRow[] = [];
interface SessionRecentResult { sessions: RecentSessionRow[]; }
async function loadLauncherAdapters(): Promise<void> {
  try {
    const result = await invoke<AdapterListResult>("app.adapterList", {});
    launcherAdapters = mapLauncherAdapters(result.adapters);
  } catch { launcherAdapters = []; }
  await Promise.all([loadLauncherRecents(), loadLauncherProfiles()]);
  if ((layout?.shores ?? []).length === 0) renderShore();
}

let launcherRecentsCwd: string | null = null;
let launcherRecentsAt = 0;
async function loadLauncherRecents(): Promise<void> {
  const cwd = activeProjectDir();
  try {
    const res = await invoke<SessionRecentResult>("cove://commands/session.recent", { cwd, limit: 0 });
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
  if (JSON.stringify(launcherRecents) !== before) renderShore();
}

const launcherProfiles = new Map<string, LaunchProfileListItem[]>();
let launcherProfilesAt = 0;
async function loadLauncherProfiles(): Promise<void> {
  const lists = await Promise.all(launcherAdapters.map(async (a) => {
    try {
      const result = await invoke<ProfileListResult>("cove://commands/launch-profile.list", { adapter: a.name });
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

interface LauncherContext {
  targetShoreId: string | null;
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
  void spawnAgentInto(ctx.targetShoreId, ctx.targetPlaceholderId, { name: tile.adapterName, displayName: tile.label, accent: tile.accent, binary: tile.binary });
}

function launchToolTile(ctx: LauncherContext, tile: LauncherTile): void {
  void launchTileInto(ctx.targetShoreId, ctx.targetPlaceholderId, tile.action);
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
  void refreshLauncherRecents();
  void refreshLauncherProfiles();
  const ctx: LauncherContext = { targetShoreId, targetPlaceholderId };
  const wrap = document.createElement("div");
  wrap.className = "box-launcher";
  wrap.tabIndex = 0;
  if (targetShoreId) wrap.dataset.shoreId = targetShoreId;
  if (targetPlaceholderId) wrap.dataset.placeholderId = targetPlaceholderId;
  paintBoxLauncher(wrap, ctx);
  const ro = new ResizeObserver(() => {
    if (!document.body.contains(wrap)) { ro.disconnect(); return; }
    const count = Math.max(1, launcherTileSets().harness.length);
    const cols = computeLauncherCols(wrap.clientWidth || 680, count, LAUNCHER_HARNESS_COLS);
    if (cols !== launcherCols) paintBoxLauncher(wrap, ctx);
  });
  ro.observe(wrap);
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
  brand.src = brandLogoAt(brandIndex);
  const tip = document.createElement("span");
  tip.className = "cl-tip";
  setLauncherTip(tip, tipAt(launcherTipIndex));
  const bayChip = document.createElement("span");
  bayChip.className = "cl-hint cl-bay-chip";
  bayChip.textContent = layout?.name?.trim() || "default";
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
  onboardingState = { ...INITIAL_ONBOARDING_STATE, backdrop: backdropMaterial, theme: themeActiveName, agentChimes: agentChimesEnabled() };
  onboardingEl.classList.add("open");
  renderOnboarding();
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
    const options = installableHarnesses();
    if (options.length === 0) return;
    openContextMenuAt(e, options.map((a) => ({ id: `install:${a.name}`, label: a.displayName || a.name })), (id) => {
      const picked = options.find((a) => `install:${a.name}` === id);
      if (!picked?.installCommand) return;
      void launchHarnessShellTask(picked.installCommand, `Install ${picked.displayName || picked.name}`);
    });
  });
  return el;
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
document.addEventListener("click", closeLauncherDropdowns);


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
    for (const s of shaped) {
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
      menu.appendChild(row);
    }
    dd.appendChild(menu);
    trigger.addEventListener("click", (e) => {
      e.stopPropagation();
      const wasOpen = dd.classList.contains("open");
      closeLauncherDropdowns();
      if (!wasOpen) dd.classList.add("open");
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
    bay: layout?.name ?? "",
    shore: activeShore()?.name ?? "",
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

async function spawnFeedbackAgent(tile: LauncherTile, prompt: string, shoreName: string): Promise<void> {
  const launch = await buildAdapterLaunch({ name: tile.adapterName, displayName: tile.label, accent: tile.accent, binary: tile.binary });
  const sp = (await spawnNook({ command: launch.command, args: [...launch.args, prompt], cwd: "", inheritCwdFrom: "", cols: 80, rows: 24, adapter: tile.adapterName, agentName: tile.label, bay: "", shore: "", yolo: launch.yolo })).nookId;
  const r = await invoke<{ shoreId: string }>("app.layoutMutate", { op: "createShore", newNookId: sp, name: shoreName, shoreId: "", targetNookId: "", orientation: "", nookId: "", dir: 0, nookType: "terminal" });
  activeShoreId = r.shoreId;
  await reload();
  focusNook(sp);
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

async function openToolShore(nookType: string, name: string): Promise<void> {
  try {
    const sp = (await spawnNook({ command: "", cwd: "", inheritCwdFrom: "", cols: 80, rows: 24, adapter: "", agentName: "", bay: "", shore: "" })).nookId;
    const r = await invoke<{ shoreId: string }>("app.layoutMutate", { op: "createShore", newNookId: sp, name, shoreId: "", targetNookId: "", orientation: "", nookId: "", dir: 0, nookType });
    activeShoreId = r.shoreId;
    await reload();
    focusNook(sp);
  } catch (e) { console.warn("openToolShore failed", nookType, e); }
}

function scrollActiveNook(toTop: boolean): void {
  if (!focusedNookId) { console.warn("scroll requested with no focused nook"); return; }
  const pv = nooks.get(focusedNookId);
  if (!pv) { console.warn("scroll requested for unknown nook", focusedNookId); return; }
  if (toTop) pv.term.scrollToTop();
  else pv.term.scrollToBottom();
}

function nextShore(dir: number): void {
  const shores = layout?.shores ?? [];
  if (shores.length === 0) { console.warn("shore cycle requested with no shores"); return; }
  const idx = shores.findIndex((r) => r.id === activeShoreId);
  const next = shores[((idx < 0 ? 0 : idx) + dir + shores.length) % shores.length];
  activeShoreId = next.id;
  const f = firstLeafOf(next);
  if (f) { focusedNookId = f; renderShore(); renderSidebar(); renderShoreTabs(); focusNook(f); }
}

function pinActiveShore(): void {
  if (!activeShoreId) { console.warn("pin requested with no active shore"); return; }
  if (pinnedShoreIds.has(activeShoreId)) pinnedShoreIds.delete(activeShoreId);
  else pinnedShoreIds.add(activeShoreId);
  savePinnedShores();
  renderShoreTabs();
}

const wsCreateEl = document.getElementById("ws-create")!;
const wscNameEl = document.getElementById("wsc-name") as HTMLInputElement;
const wscPathEl = document.getElementById("wsc-path") as HTMLInputElement;
const wscErrorEl = document.getElementById("wsc-error")!;

let wscSelectedIcon: string | null = null;

function renderWscIconGrid(): void {
  const host = document.getElementById("wsc-icon-grid");
  if (!host) { console.warn("wsc-icon-grid element missing"); return; }
  host.textContent = "";
  host.appendChild(buildBayIconGrid(wscSelectedIcon, (emoji) => { wscSelectedIcon = emoji; }));
}

function closeBayDialog(): void {
  wsCreateEl.classList.remove("open");
}

function newBay(): void {
  wscNameEl.value = "";
  wscPathEl.value = "";
  wscErrorEl.textContent = "";
  wscSelectedIcon = null;
  renderWscIconGrid();
  wsCreateEl.classList.add("open");
  wscNameEl.focus();
}

async function browseBayDir(): Promise<void> {
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

async function submitBayDialog(): Promise<void> {
  const name = wscNameEl.value.trim();
  const path = wscPathEl.value.trim();
  if (!name) { wscErrorEl.textContent = "Name is required."; wscNameEl.focus(); return; }
  if (!path) { wscErrorEl.textContent = "Directory is required."; wscPathEl.focus(); return; }
  try {
    const created = await invoke<{ id: string }>("cove://commands/bay.create", { name, projectDir: path, collectionId: "" });
    closeBayDialog();
    if (wscSelectedIcon && created?.id) {
      try { await invoke("cove://commands/bay.set-icon", { id: created.id, kind: "emoji", value: wscSelectedIcon }); }
      catch (iconErr) {
        console.warn("bay.set-icon failed", created.id, iconErr);
        showInAppToast("Icon not set", "Bay created without the chosen icon.", () => {});
      }
    }
    await loadBayBoxes();
    await reload();
  } catch (e) {
    console.warn("bay.create failed", e);
    wscErrorEl.textContent = "Could not create bay at that directory.";
  }
}

document.getElementById("wsc-close")!.addEventListener("click", closeBayDialog);
document.getElementById("wsc-cancel")!.addEventListener("click", closeBayDialog);
document.getElementById("wsc-browse")!.addEventListener("click", () => void browseBayDir());
document.getElementById("wsc-create")!.addEventListener("click", () => void submitBayDialog());
wsCreateEl.addEventListener("mousedown", (e) => { if (e.target === wsCreateEl) closeBayDialog(); });
wsCreateEl.addEventListener("keydown", (e) => {
  if (e.key === "Escape") { e.stopPropagation(); closeBayDialog(); }
  else if (e.key === "Enter") { e.stopPropagation(); void submitBayDialog(); }
});

async function switchBayByIndex(n: number): Promise<void> {
  try {
    const res = await invoke<{ bays: { id: string }[] }>("cove://commands/bay.list", {});
    const ws = (res.bays ?? [])[n - 1];
    if (!ws) { console.warn("no bay at index", n); return; }
    await switchBay(ws.id);
  } catch (e) { console.warn("bay switch by index failed", e); }
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
  if (action.startsWith("bay.switch-")) {
    const n = Number(action.slice("bay.switch-".length));
    if (Number.isFinite(n)) void switchBayByIndex(n);
    return;
  }
  switch (action) {
    case "shore.new": void newShore(); break;
    case "shore.close": if (activeShoreId) void closeShore(activeShoreId); break;
    case "shore.next": nextShore(1); break;
    case "shore.prev": nextShore(-1); break;
    case "shore.pin": pinActiveShore(); break;
    case "shore.omni-jump": openPalette(); break;
    case "nook.close": void closeFocused(); break;
    case "nook.split-right": void splitActive("row"); break;
    case "nook.split-down": void splitActive("col"); break;
    case "nook.focus-next": cycleFocus(1); break;
    case "nook.focus-prev": cycleFocus(-1); break;
    case "nook.find": openFind(); break;
    case "nook.scroll-top": scrollActiveNook(true); break;
    case "nook.scroll-bottom": scrollActiveNook(false); break;
    case "nook.maximize": void toggleZoom(); break;
    case "bay.create": void newBay(); break;
    case "view.toggle-sidebar": toggleLeftSidebar(); break;
    case "view.toggle-notepad": revealSidebarMode("notepad"); break;
    case "view.zen-mode": doToggleZen(); break;
    case "view.zoom-in": settings.fontSize = Math.min(24, settings.fontSize + 1); applySettings(); break;
    case "view.zoom-out": settings.fontSize = Math.max(9, settings.fontSize - 1); applySettings(); break;
    case "view.zoom-reset": settings.fontSize = 13; applySettings(); break;
    case "view.toggle-backdrop": void toggleBackdrop(); break;
    case "tool.inspect": startInspectMode(); break;
    case "tool.git": void openToolShore("git", "Source Control"); break;
    case "tool.search": void openToolShore("search", "Search"); break;
    case "tool.tasks": void openToolShore("tasks-list", "Tasks"); break;
    case "tool.library": void openToolShore("library", "Library"); break;
    case "tool.browser": void newBrowserShore("https://duckduckgo.com"); break;
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
  const wordmark = document.getElementById("wordmark");
  cluster.replaceChildren();
  right.replaceChildren();
  if (wordmark) cluster.appendChild(wordmark);
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
        pct.setAttribute("aria-label", "Current app zoom");
        pct.setAttribute("data-webview-ignore", "");
        pct.textContent = `${Math.round(appZoom * 100)}%`;
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
  localStorage.setItem("cove.appZoom", String(appZoom));
  const label = document.getElementById("tb-zoom-label");
  if (label) label.textContent = `${Math.round(appZoom * 100)}%`;
  void window.__ryn.invoke("window.setPageZoom", { factor: appZoom })
    .then(() => {
      syncTitlebarWorkspaceOffset();
      fitAll();
      reconcileBrowserBounds();
    })
    .catch((err) => console.warn("window.setPageZoom failed", err));
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
  const count = needsInputNooks.size;
  if (count === 0) invoke("badge.clear", {}).catch(() => void 0);
  else invoke("badge.setCount", count).catch(() => void 0);
  syncAgentNookStateClasses();
  if (agentsVisible()) renderSidebarContent("left");
}

function setupBadge(): void {
  engineEventHandlers.set("dock.badge", (payload) => {
    const evt = payload as { nookId?: string };
    if (evt?.nookId) { needsInputNooks.add(evt.nookId); onNeedsInputChanged(); }
  });
  engineEventHandlers.set("needs-input.clear", (payload) => {
    const evt = payload as { nookId?: string };
    if (evt?.nookId) { needsInputNooks.delete(evt.nookId); onNeedsInputChanged(); }
  });
  engineEventHandlers.set("dock.badge.clear", () => { needsInputNooks.clear(); onNeedsInputChanged(); });
  engineEventHandlers.set("state.changed", () => { if (agentsVisible()) void refreshAgents(); });
  engineEventHandlers.set("restore.summary", (payload) => {
    const p = payload as { restored?: number; fresh?: number; skipped?: number; bootedAt?: string };
    presentRestoreToast(p.restored ?? 0, p.fresh ?? 0, p.skipped ?? 0, p.bootedAt ?? "");
  });
}

const RESTORE_SHOWN_KEY = "cove.restore.lastShownBoot";

function presentRestoreToast(restored: number, fresh: number, skipped: number, bootedAt: string): void {
  const text = restoredSummaryText(restored, fresh, skipped);
  let lastShown: string | null = null;
  try { lastShown = localStorage.getItem(RESTORE_SHOWN_KEY); } catch { lastShown = null; }
  if (!shouldShowRestoreToast(bootedAt, lastShown, text)) return;
  try { localStorage.setItem(RESTORE_SHOWN_KEY, bootedAt); } catch { void 0; }
  showInAppToast("Sessions restored", text, () => {});
}

async function maybeShowRestoreToast(): Promise<void> {
  try {
    const r = await invoke<{ present?: boolean; restored?: number; fresh?: number; skipped?: number; bootedAt?: string }>("cove://commands/restore.summary.get", {});
    if (!r?.present) return;
    presentRestoreToast(r.restored ?? 0, r.fresh ?? 0, r.skipped ?? 0, r.bootedAt ?? "");
  } catch (e) { console.warn("restore summary pull failed", e); }
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

function revealNook(nookId: string): void {
  if (!layout) return;
  const match = layout.shores.map((shore) => ({ shore, location: findNookLocation(shore.layoutTree, nookId) })).find((item) => item.location !== null);
  if (!match?.location) { console.warn("nook reveal: no shore for nook", nookId); return; }
  const { shore, location } = match;
  const activatesSubtab = location.subtabIndex >= 0 && location.leaf.activeSubtab !== location.subtabIndex;
  if (activatesSubtab) location.leaf.activeSubtab = location.subtabIndex;
  if (bayOverviewVisible || activeShoreId !== shore.id || activatesSubtab) {
    bayOverviewVisible = false;
    activeShoreId = shore.id;
    renderShore();
    renderShoreTabs();
    renderSidebar();
  }
  if (activatesSubtab) {
    void invoke("app.layoutMutate", { op: "activateSubtab", shoreId: shore.id, nookId: location.leaf.nookId, targetNookId: "", newNookId: "", orientation: "", name: "", dir: location.subtabIndex })
      .catch((error) => { console.warn("nook subtab activation failed", nookId, error); void reload(); });
  }
  acknowledgeAgentAttention(nookId);
  focusNook(nookId);
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
    reveal: (nookId) => revealNook(nookId),
    toast: (payload) => showInAppToast(payload.title, payload.body, () => revealNook(payload.nookId)),
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
    const webviewId = browserWebviewRegistry.get(ev.nookId);
    if (!webviewId) {
      resultJson = JSON.stringify({ ok: false, error: `no live webview for nook ${ev.nookId}` });
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

let notepadGroups: { bayId: string; bayName: string; notes: NoteListItem[] }[] = [];
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
    const res = await invoke<{ notes: NoteListItem[] }>("cove://commands/note.list", { bayId: "default" });
    notepadGroups = groupByBay(res.notes ?? [], { default: "Default" });
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
    groupEl.className = "ns-group" + (collapsedGroups.has(group.bayId) ? " collapsed" : "");

    const head = document.createElement("div");
    head.className = "ns-group-head";
    head.innerHTML = `<span class="chevron">\u25bc</span><span class="ns-group-name"></span><span class="ns-group-count"></span>`;
    head.querySelector(".ns-group-name")!.textContent = group.bayName;
    head.querySelector(".ns-group-count")!.textContent = String(group.notes.length);
    head.addEventListener("click", () => {
      if (collapsedGroups.has(group.bayId)) collapsedGroups.delete(group.bayId);
      else collapsedGroups.add(group.bayId);
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
        void openNoteInNook(note.id, note.bayId);
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
    if (note) void openNoteInNook(note.id, note.bayId);
  }
}

async function openNoteInNook(noteId: string, bayId: string): Promise<void> {
  try {
    const sp = (await spawnNook({ command: "", cwd: "", inheritCwdFrom: "", cols: 80, rows: 24, adapter: "", agentName: "", bay: "", shore: "" })).nookId;
    const r = await invoke<{ shoreId: string }>("app.layoutMutate", { op: "createShore", newNookId: sp, name: "Note", shoreId: "", targetNookId: "", orientation: "", nookId: "", dir: 0, nookType: "notepad" });
    activeShoreId = r.shoreId;
    await reload();
    focusNook(sp);
    await waitForElement(".notepad-editor", 3000);
    await openNote(bayId, noteId);
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
    await invoke("cove://commands/note.create", { title: "Untitled", bayId: "default", source: "user", content: "", kind: "markdown" });
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
  setupDictation({
    invoke: (cmd, args) => window.__ryn.invoke(cmd, args ?? {}),
    getFocusedNookId: () => focusedNookId,
    writeNook: (nookId, dataBase64) => enqueueNookWrite(nookId, dataBase64, (id, b64) => invoke("app.nookWrite", { nookId: id, dataBase64: b64 }).then(() => undefined)),
  });
  window.__ryn.on("engine.event", (data: unknown) => {
    const evt = data as { channel?: string; payload?: unknown };
    if (evt?.channel !== "dictation.model") return;
    const payload = evt.payload as { ready?: boolean; error?: string } | undefined;
    if (payload?.error) dictationModelError = payload.error;
    else if (payload?.ready) dictationModelError = null;
  });
  void setupBackdrop();
  void loadWings();
  void loadBayBoxes();
  void loadLauncherAdapters();
  await reload();
  startAgentPolling();
  void maybeShowRestoreToast();
  void maybeShowOnboarding();
})();
