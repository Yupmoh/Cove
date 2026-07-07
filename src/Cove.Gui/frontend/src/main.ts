import { Terminal } from "@xterm/xterm";
import { FitAddon } from "@xterm/addon-fit";
import { WebglAddon } from "@xterm/addon-webgl";
import { SearchAddon } from "@xterm/addon-search";
import "@xterm/xterm/css/xterm.css";
import { toBase64Utf8, parseRelayText } from "./wsproto";

const CREDIT_THRESHOLD = 131072;

const THEME = {
  background: "#0b1622",
  foreground: "#e5e9f0",
  cursor: "#4cc2d6",
  cursorAccent: "#0b1622",
  selectionBackground: "#2b6d7a",
};

async function invoke<T>(cmd: string, args: unknown): Promise<T> {
  return JSON.parse((await window.__ryn.invoke(cmd, args as Record<string, unknown>)) as string) as T;
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
interface TermSettings { fontSize: number; lineHeight: number; cursorStyle: "block" | "bar" | "underline"; cursorBlink: boolean; scrollback: number; }
const defaultSettings: TermSettings = { fontSize: 13, lineHeight: 1.35, cursorStyle: "block", cursorBlink: false, scrollback: 5000 };
function loadSettings(): TermSettings {
  try { const raw = localStorage.getItem("cove.settings"); if (raw) return { ...defaultSettings, ...(JSON.parse(raw) as Partial<TermSettings>) }; } catch { void 0; }
  const fs = Number(localStorage.getItem("cove.fontSize"));
  return { ...defaultSettings, fontSize: fs >= 9 && fs <= 24 ? fs : 13 };
}
let settings = loadSettings();

const sessionsEl = document.getElementById("sessions")!;
const gridEl = document.getElementById("grid")!;
const titleEl = document.getElementById("tb-title")!;
const footEl = document.getElementById("foot-count")!;
const paletteEl = document.getElementById("palette")!;
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
    pv.term.options.fontSize = settings.fontSize;
    pv.term.options.lineHeight = settings.lineHeight;
    pv.term.options.cursorStyle = settings.cursorStyle;
    pv.term.options.cursorBlink = settings.cursorBlink;
    pv.term.options.scrollback = settings.scrollback;
  }
  fitAll();
  localStorage.setItem("cove.settings", JSON.stringify(settings));
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
  const term = new Terminal({ scrollback: settings.scrollback, convertEol: false, fontFamily: "ui-monospace, SFMono-Regular, Menlo, monospace", fontSize: settings.fontSize, lineHeight: settings.lineHeight, cursorStyle: settings.cursorStyle, cursorBlink: settings.cursorBlink, theme: THEME });
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
  term.attachCustomKeyEventHandler((e) => {
    if (e.type !== "keydown" || !e.metaKey || e.altKey || e.ctrlKey) return true;
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
      if (commit) pv.customTitle = input.value.trim();
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
  const sp = (await invoke<{ paneId: string }>("app.paneSpawn", { command: "", cwd: "", inheritCwdFrom: termPaneId, cols: 80, rows: 24 })).paneId;
  await invoke("app.layoutMutate", { op: "addSubtab", roomId: activeRoomId, paneId: leafId, newPaneId: sp, targetPaneId: "", orientation: "", name: "", dir: 0 });
  await reload();
  focusPane(sp);
}

function renderNode(node: MosaicNode): HTMLElement {
  if (node.kind === "leaf") {
    const subs = node.subtabs.length > 0 ? node.subtabs : [{ documentId: node.paneId, paneType: "terminal", title: null }];
    const activeIdx = Math.min(Math.max(0, node.activeSubtab), subs.length - 1);
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
}

async function reload(): Promise<WorkspaceSnapshot> {
  layout = await invoke<WorkspaceSnapshot>("app.layoutGet", {});
  if (!activeRoomId) {
    activeRoomId = layout.activeRoomId ?? layout.rooms[0]?.id ?? null;
  }
  const leaves = activeLeafIds();
  if (!focusedPaneId || !leaves.includes(focusedPaneId)) {
    focusedPaneId = leaves[0] ?? null;
  }
  renderRoom();
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
  const sp = (await invoke<{ paneId: string }>("app.paneSpawn", { command: "", cwd: "", inheritCwdFrom: src, cols: 80, rows: 24 })).paneId;
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
  const sp = (await invoke<{ paneId: string }>("app.paneSpawn", { command: "", cwd: "", inheritCwdFrom: "", cols: 80, rows: 24 })).paneId;
  const r = await invoke<{ roomId: string }>("app.layoutMutate", { op: "createRoom", newPaneId: sp, name: "Terminal " + (layout ? layout.rooms.length + 1 : 1), roomId: "", targetPaneId: "", orientation: "", paneId: "", dir: 0 });
  activeRoomId = r.roomId;
  await reload();
  focusPane(sp);
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
      renderSidebar();
      if (f) focusPane(f);
    });
    sessionsEl.appendChild(sessEl);
  }
  footEl.textContent = `${rooms.length} terminal${rooms.length === 1 ? "" : "s"}`;
  refreshTitles();
}

interface Action { label: string; icon: string; key?: string; run: () => void; }

function baseActions(): Action[] {
  return [
    { label: "New terminal", icon: "+", key: "Cmd T", run: () => void newRoom() },
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
const setFont = document.getElementById("set-fontSize") as HTMLInputElement;
const setLine = document.getElementById("set-lineHeight") as HTMLInputElement;
const setCursor = document.getElementById("set-cursorStyle") as HTMLSelectElement;
const setBlink = document.getElementById("set-cursorBlink") as HTMLInputElement;
const setScroll = document.getElementById("set-scrollback") as HTMLInputElement;

function openSettings() {
  setFont.value = String(settings.fontSize);
  setLine.value = String(settings.lineHeight);
  setCursor.value = settings.cursorStyle;
  setBlink.checked = settings.cursorBlink;
  setScroll.value = String(settings.scrollback);
  settingsEl.classList.add("open");
}
function closeSettings() { settingsEl.classList.remove("open"); if (focusedPaneId) panes.get(focusedPaneId)?.term.focus(); }
function readSettings() {
  const fs = Number(setFont.value); if (fs >= 9 && fs <= 24) settings.fontSize = fs;
  const lh = Number(setLine.value); if (lh >= 1 && lh <= 2) settings.lineHeight = lh;
  const cs = setCursor.value; if (cs === "block" || cs === "bar" || cs === "underline") settings.cursorStyle = cs;
  settings.cursorBlink = setBlink.checked;
  const sb = Number(setScroll.value); if (sb >= 100 && sb <= 100000) settings.scrollback = sb;
  applySettings();
}
for (const ctl of [setFont, setLine, setCursor, setBlink, setScroll]) ctl.addEventListener("change", readSettings);
settingsEl.addEventListener("mousedown", (e) => { if (e.target === settingsEl) closeSettings(); });
document.getElementById("set-close")!.addEventListener("click", closeSettings);
settingsEl.addEventListener("keydown", (e) => { if (e.key === "Escape") closeSettings(); });

const findEl = document.getElementById("findbar")!;
const findInput = document.getElementById("find-input") as HTMLInputElement;
const findDecor = { matchBackground: "#2b6d7a", activeMatchBackground: "#4cc2d6", matchOverviewRuler: "#4cc2d6", activeMatchColorOverviewRuler: "#4cc2d6" };
function activeSearch(): SearchAddon | null { return focusedPaneId ? (panes.get(focusedPaneId)?.search ?? null) : null; }
function openFind() { findEl.classList.add("open"); findInput.focus(); findInput.select(); }
function closeFind() { findEl.classList.remove("open"); activeSearch()?.clearDecorations(); if (focusedPaneId) panes.get(focusedPaneId)?.term.focus(); }
function doFind(dir: number) {
  const s = activeSearch();
  const q = findInput.value;
  if (!s || !q) return;
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
  else if (k === "z" && !e.shiftKey) { e.preventDefault(); void toggleZoom(); }
  else if (k === "d" && e.shiftKey) { e.preventDefault(); void splitActive("col"); }
  else if (k === "d") { e.preventDefault(); void splitActive("row"); }
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

(async () => {
  const snap = await reload();
  if (snap.rooms.length === 0) {
    await newRoom();
  }
})();
