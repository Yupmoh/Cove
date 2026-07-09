import { Terminal } from "@xterm/xterm";
import { FitAddon } from "@xterm/addon-fit";
import { WebglAddon } from "@xterm/addon-webgl";
import { SearchAddon } from "@xterm/addon-search";
import "@xterm/xterm/css/xterm.css";
import { toBase64Utf8, parseRelayText } from "./wsproto";
import { renderKanbanBoard } from "./tasks-kanban";
import { renderTaskList } from "./tasks-list";
import { renderTimelineFeed } from "./timeline-feed";
import { renderMarkdownNote } from "./markdown-note";
import { renderSketchNote } from "./sketch-note";
import { renderCanvasNote } from "./canvas-note";
import { renderHtmlNote } from "./html-note";
import { renderNotepadPane } from "./notepad-pane";
import { renderMermaidNote } from "./mermaid-note";
import { renderSessionPicker } from "./session-picker";
import { renderLibraryPopover } from "./library-popover";
import { renderSnapshotInspector } from "./snapshot-inspector";
import { renderDiffReviewPane } from "./diff-review-pane";
import { renderEditorPane } from "./editor-pane";
import { renderSourceControlPane } from "./source-control-pane";
import { renderSearchPane } from "./search-pane";
import { renderBrowserPane } from "./browser-pane";
import { renderDiffViewerPane } from "./diff-viewer-pane";
import { renderMarkdownPane } from "./markdown-pane";
import { partitionPinned, togglePin, reorderRoom, buildMiniDiagram, accentForPaneType, type MiniDiagramNode } from "./room-tabs";

const CREDIT_THRESHOLD = 131072;

const THEME_BG = "#0b1622";
const THEME = {
  background: THEME_BG,
  foreground: "#e5e9f0",
  cursor: "#4cc2d6",
  cursorAccent: THEME_BG,
  selectionBackground: "#2b6d7a",
};
function themeBackgroundWithOpacity(opacity: number): string {
  const n = opacity >= 0 && opacity <= 1 ? opacity : 1;
  const r = parseInt(THEME_BG.slice(1, 3), 16);
  const g = parseInt(THEME_BG.slice(3, 5), 16);
  const b = parseInt(THEME_BG.slice(5, 7), 16);
  return `rgba(${r}, ${g}, ${b}, ${n})`;
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
  const backgroundOpacity = clampFloat(await get("terminal.backgroundOpacity"), 0, 1, defaultSettings.backgroundOpacity);
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

const sessionsEl = document.getElementById("sessions")!;
const gridEl = document.getElementById("grid")!;
const titleEl = document.getElementById("tb-title")!;
const footEl = document.getElementById("foot-count")!;
const paletteEl = document.getElementById("palette")!;
const roomTabsEl = document.getElementById("room-tabs")!;
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
    pv.term.options.theme = { ...THEME, background: themeBackgroundWithOpacity(settings.backgroundOpacity) };
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
      else if (m.t === "end") { pane.term.write(`\r\n\x1b[38;5;244m[process exited: ${m.code}]\x1b[0m\r\n`); }
      return;
    }
    const bytes = new Uint8Array(ev.data as ArrayBuffer);
    pane.term.write(bytes, () => {
      pane.consumed += bytes.length;
      if (pane.consumed - pane.lastAck >= CREDIT_THRESHOLD) sendAck();
    });
  };
  setInterval(sendAck, 100);
  pane.term.onData((d) => { void invoke("app.paneWrite", { paneId: pane.paneId, dataBase64: toBase64Utf8(d) }); });
  pane.term.onResize(({ cols, rows }) => { void invoke("app.paneResize", { paneId: pane.paneId, cols, rows }); });
}

function makePane(paneId: string, since: number): PaneView {
  const term = new Terminal({ allowTransparency: true, scrollback: settings.scrollback, convertEol: false, fontFamily: settings.fontFamily || "ui-monospace, SFMono-Regular, Menlo, monospace", fontSize: settings.fontSize, lineHeight: settings.lineHeight, cursorStyle: settings.cursorStyle, cursorBlink: settings.cursorBlink, theme: { ...THEME, background: themeBackgroundWithOpacity(settings.backgroundOpacity) } });
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
  header.appendChild(moreBtn);
  el.appendChild(header);
  const host = document.createElement("div");
  host.className = "term-host";
  el.appendChild(host);
  term.open(host);

  const ws = new WebSocket(`ws://${location.host}/pty?pane=${encodeURIComponent(paneId)}&since=${since}`);
  const pv: PaneView = { paneId, term, fit: fitAddon, ws, el, consumed: 0, lastAck: 0, title: "", customTitle: "", headerTitleEl: titleSpan, search: searchAddon };

  el.addEventListener("mousedown", () => focusPane(paneId));
  attachWs(pv);
  const overrides = loadKeybindings();
  term.attachCustomKeyEventHandler((e) => {
    if (e.type !== "keydown") return true;
    const chord = normalizeChord(e);
    const action = overrides[chord];
    if (action && action.startsWith("send-text:")) { void invoke("app.paneWrite", { paneId, dataBase64: toBase64Utf8(action.slice("send-text:".length)) }); return false; }
    if (e.shiftKey && e.key === "Enter") { void invoke("app.paneWrite", { paneId, dataBase64: toBase64Utf8("\n") }); return false; }
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
  term.onTitleChange((t) => { pv.title = t; setTitle(); refreshTitles(); });
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
  renderSessionPicker("default").then(el => {
    placeholder.replaceWith(el);
  }).catch(e => {
    placeholder.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load session picker: ${(e as Error).message}</div>`;
  });
  return placeholder;
}
function renderLibraryPane(paneId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "library-pane-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  renderLibraryPopover("default").then(el => {
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
  renderEditorPane(paneId, paneId).then(el => {
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
function renderGitPaneWrapper(paneId: string): HTMLElement {
  const placeholder = document.createElement("div");
  placeholder.className = "git-pane-placeholder";
  placeholder.style.cssText = "flex:1 1 0;min-width:0;min-height:0;overflow:hidden;";
  renderSourceControlPane("default").then(el => {
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
    if (active.paneType === "git") return renderGitPaneWrapper(active.documentId);
    if (active.paneType === "search") return renderSearchPaneWrapper(active.documentId);
    if (active.paneType === "browser") return renderBrowserPaneWrapper(active.documentId, active.title ?? "about:blank");
    if (active.paneType === "diff") return renderDiffViewerPaneWrapper(active.documentId, active.title ?? "");
    if (isEmpty) return emptyPaneStrip(node.paneId);
    const activeEl = getPane(subs[activeIdx].documentId).el;
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
  let zoomed = false;
  if (room && room.layoutTree) {
    const treeIds = collectLeafIds(room.layoutTree);
    const zid = room.zoomedPaneId;
    if (zid && treeIds.includes(zid)) {
      gridEl.appendChild(getPane(zid).el);
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
  }
  for (const [id, pv] of panes) {
    pv.el.classList.toggle("focused", id === focusedPaneId);
  }
  fitAll();
}

function focusPane(paneId: string): void {
  focusedPaneId = paneId;
  for (const [id, pv] of panes) {
    pv.el.classList.toggle("focused", id === paneId);
  }
  panes.get(paneId)?.term.focus();
  refreshTitles();
  if (activeRoomId) {
    void invoke("app.layoutMutate", { op: "focus", roomId: activeRoomId, paneId, targetPaneId: "", newPaneId: "", orientation: "", name: "", dir: 0 });
  }
}

function refreshTitles(): void {
  const room = activeRoom();
  if (room) {
    const leaves = collectLeafIds(room.layoutTree);
    const count = leaves.length;
    const focused = focusedPaneId ? panes.get(focusedPaneId) : undefined;
    const label = (focused && focused.title) || room.name;
    titleEl.innerHTML = `${label}` + (count > 1 ? ` <span class="sub">${count} panes</span>` : "");
  }
  const sessEls = sessionsEl.querySelectorAll<HTMLElement>(".sess");
  (layout?.rooms ?? []).forEach((r, i) => {
    const sessEl = sessEls[i];
    if (!sessEl) return;
    const leaves = collectLeafIds(r.layoutTree);
    const first = leaves[0] ? panes.get(leaves[0]) : undefined;
    const name = (first && first.title) || r.name;
    const nameEl = sessEl.querySelector<HTMLElement>(".name");
    if (nameEl) nameEl.textContent = name;
  });
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

async function newRoom(): Promise<void> {
  const sp = (await invoke<{ paneId: string }>("app.paneSpawn", { command: "", cwd: "", inheritCwdFrom: "", cols: 80, rows: 24, adapter: "", agentName: "", workspace: "", room: "" })).paneId;
  const r = await invoke<{ roomId: string }>("app.layoutMutate", { op: "createRoom", newPaneId: sp, name: "Terminal " + (layout ? layout.rooms.length + 1 : 1), roomId: "", targetPaneId: "", orientation: "", paneId: "", dir: 0 });
  activeRoomId = r.roomId;
  await reload();
  focusPane(sp);
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
    try { await invoke("app.paneKill", { paneId: id }); } catch { void 0; }
    try { await invoke("app.layoutMutate", { op: "close", roomId, paneId: id, targetPaneId: "", newPaneId: "", orientation: "", name: "", dir: 0 }); } catch { void 0; }
  }
  if (activeRoomId === roomId) activeRoomId = null;
  await reload();
  if (!layout || layout.rooms.length === 0) await newRoom();
}

function renderSidebar(): void {
  sessionsEl.innerHTML = "";
  const rooms = layout?.rooms ?? [];
  for (const room of rooms) {
    const sessEl = document.createElement("div");
    sessEl.className = "sess" + (room.id === activeRoomId ? " active" : "");
    const leaves = collectLeafIds(room.layoutTree);
    const count = leaves.length;
    sessEl.innerHTML = `<span class="dot"></span><span class="name"></span><span class="n"></span><span class="x">&times;</span>`;
    const nameEl = sessEl.querySelector<HTMLElement>(".name");
    if (nameEl) {
      const first = leaves[0] ? panes.get(leaves[0]) : undefined;
      nameEl.textContent = (first && first.title) || room.name;
    }
    const nEl = sessEl.querySelector<HTMLElement>(".n");
    if (nEl) nEl.textContent = count > 1 ? String(count) : "";
    sessEl.addEventListener("click", (e) => {
      if ((e.target as HTMLElement).classList.contains("x")) { void closeRoom(room.id); return; }
      activeRoomId = room.id;
      const f = firstLeafOf(room);
      if (f) focusedPaneId = f;
      renderRoom();
      renderRoomTabs();
      renderSidebar();
      if (f) focusPane(f);
    });
    sessionsEl.appendChild(sessEl);
  }
  footEl.textContent = `${rooms.length} terminal${rooms.length === 1 ? "" : "s"}`;
  refreshTitles();
}

const pinnedRoomIds = new Set<string>(JSON.parse(localStorage.getItem("cove.pinnedRooms") ?? "[]"));
function savePinnedRooms(): void { localStorage.setItem("cove.pinnedRooms", JSON.stringify([...pinnedRoomIds])); }

interface WingInfo { id: string; name: string; }
let wings: WingInfo[] = [];
let activeWingId: string | null = "main";
let wingSwitcherExpanded = false;
async function loadWings(): Promise<void> {
  try {
    const res = await invoke<{ wings: { id: string; name: string }[] }>("cove://commands/wing.list", { workspaceId: "default" });
    wings = res.wings ?? [{ id: "main", name: "main" }];
  } catch { wings = [{ id: "main", name: "main" }]; }
}
async function switchWingActive(wingId: string): Promise<void> {
  activeWingId = wingId;
  try { await invoke("cove://commands/wing.switch", { workspaceId: "default", wingId }); } catch { void 0; }
  await reload();
  renderRoomTabs();
}

function roomTabName(room: RoomSnapshot): string {
  const leaves = collectLeafIds(room.layoutTree);
  const first = leaves[0] ? panes.get(leaves[0]) : undefined;
  return (first && first.title) || room.name;
}

function renderMiniDiagramFor(room: RoomSnapshot): HTMLElement {
  const container = document.createElement("div");
  container.className = "rtab-mini";
  const node = layoutTreeToMiniNode(room.layoutTree);
  const cells = buildMiniDiagram(node, { x: 0, y: 0, w: 18, h: 12 });
  for (const c of cells) {
    const cell = document.createElement("div");
    cell.className = "rtab-mini-cell";
    cell.style.cssText = `width:${Math.max(1, c.w)}px;height:${Math.max(1, c.h)}px;background:${c.accent};`;
    container.appendChild(cell);
  }
  return container;
}

function layoutTreeToMiniNode(node: MosaicNode): MiniDiagramNode {
  if (node.kind === "leaf") {
    const subs = node.subtabs.length > 0 ? node.subtabs : [{ documentId: node.paneId, paneType: "terminal", title: null }];
    const activeIdx = Math.min(Math.max(0, node.activeSubtab), subs.length - 1);
    return { kind: "leaf", paneType: subs[activeIdx]?.paneType ?? "terminal" };
  }
  return {
    kind: "split",
    orientation: node.orientation,
    ratio: node.ratio,
    childA: layoutTreeToMiniNode(node.childA),
    childB: layoutTreeToMiniNode(node.childB),
  };
}

function renderRoomTabs(): void {
  roomTabsEl.innerHTML = "";
  const rooms = layout?.rooms ?? [];
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

    if (!isPinned) tab.appendChild(renderMiniDiagramFor(room));

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
      e.preventDefault();
      showRoomContextMenu(e, roomId, tab);
    });
    tab.addEventListener("dragstart", () => { dragSrcId = roomId; tab.classList.add("dragging"); });
    tab.addEventListener("dragend", () => { tab.classList.remove("dragging"); dragSrcId = null; });
    tab.addEventListener("dragover", (e) => { e.preventDefault(); tab.classList.add("drag-over"); });
    tab.addEventListener("dragleave", () => { tab.classList.remove("drag-over"); });
    tab.addEventListener("drop", (e) => {
      e.preventDefault();
      tab.classList.remove("drag-over");
      if (dragSrcId && dragSrcId !== roomId) {
        void reorderRooms(dragSrcId, roomId);
      }
    });
    return tab;
  };

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
  addBtn.className = "tbtn";
  addBtn.style.cssText = "margin-left:auto;flex-shrink:0;";
  addBtn.innerHTML = "+";
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
  try {
    const newOrder = reordered.map((r) => r.id);
    await invoke("app.layoutMutate", { op: "reorder", roomIds: newOrder, roomId: "", targetPaneId: "", newPaneId: "", orientation: "", name: "", paneId: "", dir: 0 });
  } catch { void 0; }
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
      try { await invoke("app.layoutMutate", { op: "rename", roomId, name: newName, paneId: "", targetPaneId: "", newPaneId: "", orientation: "", dir: 0 }); } catch { void 0; }
    }
    renderRoomTabs();
    renderSidebar();
  };
  input.addEventListener("blur", commit);
  input.addEventListener("keydown", (e) => {
    if (e.key === "Enter") input.blur();
    if (e.key === "Escape") { renderRoomTabs(); }
  });
}

function showRoomContextMenu(e: MouseEvent, roomId: string, tab: HTMLElement): void {
  const existing = document.querySelector(".rtab-context-menu");
  if (existing) existing.remove();
  const menu = document.createElement("div");
  menu.className = "rtab-context-menu pmenu";
  menu.style.cssText = `position:fixed;top:${e.clientY}px;left:${e.clientX}px;z-index:50;`;
  const room = layout?.rooms.find((r) => r.id === roomId);
  if (!room) return;
  const isPinned = pinnedRoomIds.has(roomId);

  const pinItem = document.createElement("div");
  pinItem.className = "pmenu-item";
  pinItem.style.cssText = "padding:5px 10px;cursor:pointer;border-radius:4px;font-size:12px;";
  pinItem.textContent = isPinned ? "Unpin" : "Pin";
  pinItem.addEventListener("mouseenter", () => pinItem.style.background = "var(--panel)");
  pinItem.addEventListener("mouseleave", () => pinItem.style.background = "none");
  pinItem.addEventListener("click", () => {
    if (isPinned) pinnedRoomIds.delete(roomId);
    else pinnedRoomIds.add(roomId);
    savePinnedRooms();
    renderRoomTabs();
    menu.remove();
  });
  menu.appendChild(pinItem);

  const renameItem = document.createElement("div");
  renameItem.className = "pmenu-item";
  renameItem.style.cssText = "padding:5px 10px;cursor:pointer;border-radius:4px;font-size:12px;";
  renameItem.textContent = "Rename";
  renameItem.addEventListener("mouseenter", () => renameItem.style.background = "var(--panel)");
  renameItem.addEventListener("mouseleave", () => renameItem.style.background = "none");
  renameItem.addEventListener("click", () => { menu.remove(); startRename(roomId, tab, tab.querySelector(".rtab-name") as HTMLElement); });
  menu.appendChild(renameItem);

  const closeAllItem = document.createElement("div");
  closeAllItem.className = "pmenu-item";
  closeAllItem.style.cssText = "padding:5px 10px;cursor:pointer;border-radius:4px;font-size:12px;";
  closeAllItem.textContent = "Close All (keep pinned)";
  closeAllItem.addEventListener("mouseenter", () => closeAllItem.style.background = "var(--panel)");
  closeAllItem.addEventListener("mouseleave", () => closeAllItem.style.background = "none");
  closeAllItem.addEventListener("click", () => {
    menu.remove();
    void closeAllUnpinned();
  });
  menu.appendChild(closeAllItem);

  document.body.appendChild(menu);
  const close = (ev: MouseEvent) => {
    if (!menu.contains(ev.target as Node)) { menu.remove(); document.removeEventListener("click", close); }
  };
  setTimeout(() => document.addEventListener("click", close), 0);
}

async function closeAllUnpinned(): Promise<void> {
  if (!layout) return;
  const toClose = layout.rooms.filter((r) => !pinnedRoomIds.has(r.id));
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
    { label: "Toggle sidebar", icon: "\u25e7", key: "Cmd B", run: toggleSidebar },
    { label: "Increase font size", icon: "+", key: "Cmd =", run: () => { settings.fontSize = Math.min(24, settings.fontSize + 1); applySettings(); } },
    { label: "Decrease font size", icon: "-", key: "Cmd -", run: () => { settings.fontSize = Math.max(9, settings.fontSize - 1); applySettings(); } },
    { label: "Reset font size", icon: "\u21ba", key: "Cmd 0", run: () => { settings.fontSize = 13; applySettings(); } },
    { label: "Settings", icon: "\u2699", key: "Cmd ,", run: openSettings },
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
let palActions: Action[] = [];

function openPalette() {
  paletteEl.classList.add("open");
  palInput.value = "";
  palSel = 0;
  renderPalette();
  palInput.focus();
}

function closePalette() {
  paletteEl.classList.remove("open");
  if (focusedPaneId) {
    const pv = panes.get(focusedPaneId);
    if (pv) pv.term.focus();
  }
}

function renderPalette() {
  const q = palInput.value.trim().toLowerCase();
  const all = baseActions().concat(jumpActions());
  palActions = q ? all.filter((a) => a.label.toLowerCase().includes(q)) : all;
  if (palSel >= palActions.length) palSel = Math.max(0, palActions.length - 1);
  palList.innerHTML = "";
  palActions.forEach((a, i) => {
    const el = document.createElement("div");
    el.className = "pal-item" + (i === palSel ? " sel" : "");
    el.innerHTML = `<span class="ic"></span><span class="lbl"></span>${a.key ? `<span class="key">${a.key}</span>` : ""}`;
    (el.querySelector(".ic") as HTMLElement).textContent = a.icon;
    (el.querySelector(".lbl") as HTMLElement).textContent = a.label;
    el.addEventListener("click", () => { closePalette(); a.run(); });
    palList.appendChild(el);
  });
}


function toggleSidebar() { document.body.classList.toggle("sidebar-hidden"); fitAll(); }

document.getElementById("side-add")!.addEventListener("click", () => void newRoom());
document.getElementById("tb-split-r")!.addEventListener("click", () => void splitActive("row"));
document.getElementById("tb-split-d")!.addEventListener("click", () => void splitActive("col"));
document.getElementById("tb-sidebar")!.addEventListener("click", toggleSidebar);
document.getElementById("tb-pal")!.addEventListener("click", openPalette);

palInput.addEventListener("input", () => { palSel = 0; renderPalette(); });
palInput.addEventListener("keydown", (e) => {
  if (e.key === "Escape") { e.preventDefault(); closePalette(); }
  else if (e.key === "Enter") { e.preventDefault(); const a = palActions[palSel]; closePalette(); if (a) a.run(); }
  else if (e.key === "ArrowDown") { e.preventDefault(); palSel = Math.min(palActions.length - 1, palSel + 1); renderPalette(); }
  else if (e.key === "ArrowUp") { e.preventDefault(); palSel = Math.max(0, palSel - 1); renderPalette(); }
});
paletteEl.addEventListener("mousedown", (e) => { if (e.target === paletteEl) closePalette(); });

const settingsEl = document.getElementById("settings")!;
const setFontFam = document.getElementById("set-fontFamily") as HTMLInputElement;
const setFont = document.getElementById("set-fontSize") as HTMLInputElement;
const setLine = document.getElementById("set-lineHeight") as HTMLInputElement;
const setCursor = document.getElementById("set-cursorStyle") as HTMLSelectElement;
const setBlink = document.getElementById("set-cursorBlink") as HTMLInputElement;
const setLig = document.getElementById("set-ligatures") as HTMLInputElement;
const setScroll = document.getElementById("set-scrollbackLines") as HTMLInputElement;
const setPad = document.getElementById("set-padding") as HTMLInputElement;
const setBgOp = document.getElementById("set-backgroundOpacity") as HTMLInputElement;

function openSettings() {
  setFontFam.value = settings.fontFamily;
  setFont.value = String(settings.fontSize);
  setLine.value = String(settings.lineHeight);
  setCursor.value = settings.cursorStyle;
  setBlink.checked = settings.cursorBlink;
  setLig.checked = settings.ligatures;
  setScroll.value = String(settings.scrollback);
  setPad.value = String(settings.padding);
  setBgOp.value = String(settings.backgroundOpacity);
  settingsEl.classList.add("open");
}
function closeSettings() { settingsEl.classList.remove("open"); if (focusedPaneId) panes.get(focusedPaneId)?.term.focus(); }
function readSettings() {
  settings.fontFamily = setFontFam.value.trim();
  const fs = Number(setFont.value); if (fs >= 9 && fs <= 24) settings.fontSize = fs;
  const lh = Number(setLine.value); if (lh >= 1 && lh <= 2) settings.lineHeight = lh;
  const cs = setCursor.value; if (cs === "block" || cs === "bar" || cs === "underline") settings.cursorStyle = cs;
  settings.cursorBlink = setBlink.checked;
  settings.ligatures = setLig.checked;
  const sb = Number(setScroll.value); if (sb >= 100 && sb <= 100000) settings.scrollback = sb;
  const pd = Number(setPad.value); if (pd >= 0 && pd <= 40) settings.padding = pd;
  const bo = Number(setBgOp.value); if (bo >= 0 && bo <= 1) settings.backgroundOpacity = bo;
  applySettings();
}
for (const ctl of [setFontFam, setFont, setLine, setCursor, setBlink, setLig, setScroll, setPad, setBgOp]) ctl.addEventListener("change", readSettings);
settingsEl.addEventListener("mousedown", (e) => { if (e.target === settingsEl) closeSettings(); });
document.getElementById("set-close")!.addEventListener("click", closeSettings);
settingsEl.addEventListener("keydown", (e) => { if (e.key === "Escape") closeSettings(); });

const findEl = document.getElementById("findbar")!;
const findInput = document.getElementById("find-input") as HTMLInputElement;
const findDecor = { matchBackground: "#2b6d7a", activeMatchBackground: "#4cc2d6", matchOverviewRuler: "#4cc2d6", activeMatchColorOverviewRuler: "#4cc2d6" };
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
interface AdapterInfo { name: string; displayName: string; accent: string; binary: string; }
interface AdapterListResult { adapters: AdapterInfo[]; }
async function loadLauncherAgents(): Promise<void> {
  try {
    const result = await invoke<AdapterListResult>("app.adapterList", {});
    launchAgentsEl.innerHTML = "";
    for (const a of result.adapters ?? []) {
      const tile = document.createElement("div");
      tile.className = "launch-tile";
      tile.innerHTML = `<span class="ic" style="color:${a.accent || "#4cc2d6"}">&#9881;</span><span class="lbl">${a.displayName || a.name}</span>`;
      tile.addEventListener("click", () => { closeLauncher(); void spawnAgent(a); });
      launchAgentsEl.appendChild(tile);
    }
  } catch { void 0; }
}
async function spawnAgent(a: AdapterInfo): Promise<void> {
  const sp = (await invoke<{ paneId: string }>("app.paneSpawn", { command: a.binary, args: [] as string[], cwd: "", inheritCwdFrom: "", cols: 80, rows: 24, adapter: a.name, agentName: a.displayName, workspace: "", room: "" })).paneId;
  const r = await invoke<{ roomId: string }>("app.layoutMutate", { op: "createRoom", newPaneId: sp, name: a.displayName || a.name, roomId: "", targetPaneId: "", orientation: "", paneId: "", dir: 0 });
  activeRoomId = r.roomId;
  await reload();
  focusPane(sp);
}

window.addEventListener("keydown", (e) => {
  if (!e.metaKey) return;
  const k = e.key.toLowerCase();
  if (k === "k") { e.preventDefault(); paletteEl.classList.contains("open") ? closePalette() : openPalette(); return; }
  if (paletteEl.classList.contains("open")) return;
  if (k === "t") { e.preventDefault(); void newRoom(); }
  else if (k === "z" && e.shiftKey) { e.preventDefault(); document.body.classList.toggle("zen-mode"); fitAll(); }
  else if (k === "z" && !e.shiftKey) { e.preventDefault(); void toggleZoom(); }
  else if (k === "d" && e.shiftKey) { e.preventDefault(); void splitActive("col"); }
  else if (k === "d") { e.preventDefault(); void splitActive("row"); }
  else if (k === "b" && e.shiftKey) { e.preventDefault(); void newBrowserRoom("https://duckduckgo.com"); }
  else if (k === "w") { e.preventDefault(); void closeFocused(); }
  else if (k === "b") { e.preventDefault(); toggleSidebar(); }
  else if (k === "]") { e.preventDefault(); cycleFocus(1); }
  else if (k === "[") { e.preventDefault(); cycleFocus(-1); }
  else if (k === "=" || k === "+") { e.preventDefault(); settings.fontSize = Math.min(24, settings.fontSize + 1); applySettings(); }
  else if (k === "-") { e.preventDefault(); settings.fontSize = Math.max(9, settings.fontSize - 1); applySettings(); }
  else if (k === "0") { e.preventDefault(); settings.fontSize = 13; applySettings(); }
  else if (k === ",") { e.preventDefault(); openSettings(); }
  else if (k === "f") { e.preventDefault(); openFind(); }
  else if (k === "l") { e.preventDefault(); launcherEl.classList.contains("open") ? closeLauncher() : openLauncher(); }
  else if (k >= "1" && k <= "9") {
    const i = Number(k) - 1;
    const rooms = layout?.rooms ?? [];
    if (rooms[i]) {
      e.preventDefault();
      activeRoomId = rooms[i].id;
      const f = firstLeafOf(rooms[i]);
      if (f) { focusedPaneId = f; renderRoom(); renderSidebar(); focusPane(f); }
    }
  }
}, true);

window.addEventListener("resize", () => fitAll());
function setupMenuBar(): void {
  const menu = [
    { role: "appMenu" },
    {
      label: "File",
      items: [
        { id: "new-room", label: "New Room", accelerator: "CmdOrCtrl+T" },
        { id: "new-browser", label: "New Browser", accelerator: "CmdOrCtrl+Shift+B" },
        { separator: true },
        { id: "close-pane", label: "Close Pane", accelerator: "CmdOrCtrl+W" },
      ],
    },
    { role: "editMenu" },
    {
      label: "View",
      items: [
        { id: "toggle-sidebar", label: "Toggle Sidebar", accelerator: "CmdOrCtrl+B" },
        { id: "toggle-zen", label: "Toggle Zen Mode", accelerator: "CmdOrCtrl+Shift+Z" },
        { separator: true },
        { id: "zoom-in", label: "Zoom In", accelerator: "CmdOrCtrl+=" },
        { id: "zoom-out", label: "Zoom Out", accelerator: "CmdOrCtrl+-" },
        { id: "zoom-reset", label: "Reset Zoom", accelerator: "CmdOrCtrl+0" },
      ],
    },
    {
      label: "Pane",
      items: [
        { id: "split-right", label: "Split Right", accelerator: "CmdOrCtrl+D" },
        { id: "split-down", label: "Split Down", accelerator: "CmdOrCtrl+Shift+D" },
        { separator: true },
        { id: "next-pane", label: "Next Pane", accelerator: "CmdOrCtrl+]" },
        { id: "prev-pane", label: "Previous Pane", accelerator: "CmdOrCtrl+[" },
        { separator: true },
        { id: "zoom-pane", label: "Zoom Pane", accelerator: "CmdOrCtrl+Z" },
      ],
    },
    {
      label: "Go",
      items: [
        { id: "command-palette", label: "Command Palette…", accelerator: "CmdOrCtrl+K" },
        { id: "launcher", label: "Launcher…", accelerator: "CmdOrCtrl+L" },
        { separator: true },
        { id: "find", label: "Find…", accelerator: "CmdOrCtrl+F" },
        { separator: true },
        { id: "settings", label: "Settings…", accelerator: "CmdOrCtrl+," },
      ],
    },
    { role: "windowMenu" },
  ];
  invoke("menubar.setMenu", { items: menu }).catch(() => void 0);

  window.__ryn.on("menubar.itemClicked", (data: unknown) => {
    const id = data as string;
    if (!id) return;
    switch (id) {
      case "new-room": void newRoom(); break;
      case "new-browser": void newBrowserRoom("https://duckduckgo.com"); break;
      case "close-pane": void closeFocused(); break;
      case "toggle-sidebar": toggleSidebar(); break;
      case "toggle-zen": document.body.classList.toggle("zen-mode"); fitAll(); break;
      case "zoom-in": settings.fontSize = Math.min(24, settings.fontSize + 1); applySettings(); break;
      case "zoom-out": settings.fontSize = Math.max(9, settings.fontSize - 1); applySettings(); break;
      case "zoom-reset": settings.fontSize = 13; applySettings(); break;
      case "split-right": void splitActive("row"); break;
      case "split-down": void splitActive("col"); break;
      case "next-pane": cycleFocus(1); break;
      case "prev-pane": cycleFocus(-1); break;
      case "zoom-pane": void toggleZoom(); break;
      case "command-palette": paletteEl.classList.contains("open") ? closePalette() : openPalette(); break;
      case "launcher": launcherEl.classList.contains("open") ? closeLauncher() : openLauncher(); break;
      case "find": openFind(); break;
      case "settings": openSettings(); break;
    }
  });
}

function setupBadge(): void {
  const needsInputPanes = new Set<string>();
  function updateBadge(): void {
    const count = needsInputPanes.size;
    if (count === 0)
      invoke("badge.clear", {}).catch(() => void 0);
    else
      invoke("badge.setCount", count).catch(() => void 0);
  }
  window.__ryn.on("dock.badge", (data: unknown) => {
    const evt = data as { paneId?: string };
    if (evt?.paneId) { needsInputPanes.add(evt.paneId); updateBadge(); }
  });
  window.__ryn.on("needs-input.clear", (data: unknown) => {
    const evt = data as { paneId?: string };
    if (evt?.paneId) { needsInputPanes.delete(evt.paneId); updateBadge(); }
  });
  window.__ryn.on("dock.badge.clear", () => { needsInputPanes.clear(); updateBadge(); });
}

(async () => {
  settings = await loadSettings();
  applySettings();
  setupMenuBar();
  setupBadge();
  void loadWings();
  const snap = await reload();
  if (snap.rooms.length === 0) {
    await newRoom();
  }
})();
