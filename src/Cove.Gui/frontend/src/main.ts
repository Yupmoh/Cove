import { Terminal } from "@xterm/xterm";
import { FitAddon } from "@xterm/addon-fit";
import { WebglAddon } from "@xterm/addon-webgl";
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

interface Pane {
  paneId: string;
  term: Terminal;
  fit: FitAddon;
  ws: WebSocket;
  paneEl: HTMLElement;
  consumed: number;
  lastAck: number;
  title: string;
}

interface Session {
  id: number;
  name: string;
  dir: "row" | "col";
  panes: Pane[];
  gridEl: HTMLElement;
  sessEl: HTMLElement;
}

const sessions: Session[] = [];
let activeSession: Session | null = null;
let focusedPane: Pane | null = null;
let seq = 0;

const sessionsEl = document.getElementById("sessions")!;
const gridEl = document.getElementById("grid")!;
const titleEl = document.getElementById("tb-title")!;
const footEl = document.getElementById("foot-count")!;
const paletteEl = document.getElementById("palette")!;
const palInput = document.getElementById("pal-input") as HTMLInputElement;
const palList = document.getElementById("pal-list")!;

function fit(session: Session) {
  requestAnimationFrame(() => {
    for (const p of session.panes) {
      try { p.fit.fit(); } catch { void 0; }
    }
  });
}

function attachWs(pane: Pane) {
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

function makePaneEl(session: Session, paneId: string, since: number): Pane {
  const term = new Terminal({ scrollback: 5000, convertEol: false, fontFamily: "ui-monospace, SFMono-Regular, Menlo, monospace", fontSize: 13, theme: THEME });
  const fitAddon = new FitAddon();
  term.loadAddon(fitAddon);
  try { term.loadAddon(new WebglAddon()); } catch { void 0; }

  const paneEl = document.createElement("div");
  paneEl.className = "pane";
  paneEl.style.flexGrow = "1";
  const host = document.createElement("div");
  host.className = "term-host";
  paneEl.appendChild(host);
  session.gridEl.appendChild(paneEl);
  term.open(host);

  const ws = new WebSocket(`ws://${location.host}/pty?pane=${encodeURIComponent(paneId)}&since=${since}`);
  const pane: Pane = { paneId, term, fit: fitAddon, ws, paneEl, consumed: 0, lastAck: 0, title: "" };

  paneEl.addEventListener("mousedown", () => focusPane(pane));
  attachWs(pane);
  term.onTitleChange((t) => { pane.title = t; refreshTitles(); });
  return pane;
}

function relayout(session: Session) {
  session.gridEl.querySelectorAll(".divider").forEach((d) => d.remove());
  session.panes.forEach((p, i) => {
    if (!p.paneEl.style.flexGrow) p.paneEl.style.flexGrow = "1";
    if (i < session.panes.length - 1) {
      const div = document.createElement("div");
      div.className = "divider";
      p.paneEl.after(div);
      wireDivider(session, div, p, session.panes[i + 1]);
    }
  });
  fit(session);
}

function wireDivider(session: Session, div: HTMLElement, a: Pane, b: Pane) {
  div.addEventListener("mousedown", (e) => {
    e.preventDefault();
    const horiz = session.dir === "row";
    const rect = session.gridEl.getBoundingClientRect();
    const total = horiz ? rect.width : rect.height;
    const start = horiz ? e.clientX : e.clientY;
    const ga = parseFloat(a.paneEl.style.flexGrow || "1");
    const gb = parseFloat(b.paneEl.style.flexGrow || "1");
    const sum = ga + gb;
    const onMove = (m: MouseEvent) => {
      const frac = ((horiz ? m.clientX : m.clientY) - start) / total;
      const na = Math.max(sum * 0.12, Math.min(sum * 0.88, ga + frac * sum));
      a.paneEl.style.flexGrow = String(na);
      b.paneEl.style.flexGrow = String(sum - na);
      a.fit.fit();
      b.fit.fit();
    };
    const onUp = () => {
      document.removeEventListener("mousemove", onMove);
      document.removeEventListener("mouseup", onUp);
      fit(session);
    };
    document.addEventListener("mousemove", onMove);
    document.addEventListener("mouseup", onUp);
  });
}


function focusPane(pane: Pane) {
  focusedPane = pane;
  for (const s of sessions) for (const p of s.panes) p.paneEl.classList.toggle("focused", p === pane);
  pane.term.focus();
  refreshTitles();
}

function activateSession(session: Session) {
  activeSession = session;
  for (const s of sessions) {
    s.gridEl.classList.toggle("active", s === session);
    s.sessEl.classList.toggle("active", s === session);
  }
  session.gridEl.classList.toggle("col", session.dir === "col");
  const last = session.panes[session.panes.length - 1];
  if (last) focusPane(last);
  fit(session);
  updateChrome();
  refreshTitles();
}

async function newSession(): Promise<void> {
  seq += 1;
  const grid = document.createElement("div");
  grid.className = "sgrid";
  gridEl.appendChild(grid);

  const sessEl = document.createElement("div");
  sessEl.className = "sess";
  const session: Session = { id: seq, name: `Terminal ${seq}`, dir: "row", panes: [], gridEl: grid, sessEl };

  sessEl.innerHTML = `<span class="dot"></span><span class="name"></span><span class="n"></span><span class="x">&times;</span>`;
  (sessEl.querySelector(".name") as HTMLElement).textContent = session.name;
  sessEl.addEventListener("click", (e) => {
    if ((e.target as HTMLElement).classList.contains("x")) { void closeSession(session); return; }
    activateSession(session);
  });
  sessionsEl.appendChild(sessEl);
  sessions.push(session);

  const paneId = (await invoke<{ paneId: string }>("app.paneSpawn", { command: "", cols: 80, rows: 24 })).paneId;
  const pane = makePaneEl(session, paneId, 0);
  session.panes.push(pane);
  activateSession(session);
  renderSidebar();
}

function adoptSession(paneId: string): void {
  seq += 1;
  const grid = document.createElement("div");
  grid.className = "sgrid";
  gridEl.appendChild(grid);
  const sessEl = document.createElement("div");
  sessEl.className = "sess";
  const session: Session = { id: seq, name: `Terminal ${seq}`, dir: "row", panes: [], gridEl: grid, sessEl };
  sessEl.innerHTML = `<span class="dot"></span><span class="name"></span><span class="n"></span><span class="x">&times;</span>`;
  (sessEl.querySelector(".name") as HTMLElement).textContent = session.name;
  sessEl.addEventListener("click", (e) => {
    if ((e.target as HTMLElement).classList.contains("x")) { void closeSession(session); return; }
    activateSession(session);
  });
  sessionsEl.appendChild(sessEl);
  sessions.push(session);
  const pane = makePaneEl(session, paneId, 0);
  session.panes.push(pane);
  renderSidebar();
}

async function splitActive(dir: "row" | "col"): Promise<void> {
  if (!activeSession) { await newSession(); return; }
  const session = activeSession;
  session.dir = dir;
  session.gridEl.classList.toggle("col", dir === "col");
  const paneId = (await invoke<{ paneId: string }>("app.paneSpawn", { command: "", cols: 80, rows: 24 })).paneId;
  const pane = makePaneEl(session, paneId, 0);
  session.panes.push(pane);
  focusPane(pane);
  relayout(session);
  renderSidebar();
}

async function closePane(pane: Pane): Promise<void> {
  const session = sessions.find((s) => s.panes.includes(pane));
  if (!session) return;
  try { await invoke("app.paneKill", { paneId: pane.paneId }); } catch { void 0; }
  try { pane.ws.close(); } catch { void 0; }
  pane.term.dispose();
  pane.paneEl.remove();
  session.panes.splice(session.panes.indexOf(pane), 1);
  if (session.panes.length === 0) { await removeSession(session); return; }
  focusPane(session.panes[session.panes.length - 1]);
  relayout(session);
  renderSidebar();
}

async function closeSession(session: Session): Promise<void> {
  for (const p of session.panes.slice()) {
    try { await invoke("app.paneKill", { paneId: p.paneId }); } catch { void 0; }
    try { p.ws.close(); } catch { void 0; }
    p.term.dispose();
    p.paneEl.remove();
  }
  session.panes.length = 0;
  await removeSession(session);
}

async function removeSession(session: Session): Promise<void> {
  session.gridEl.remove();
  session.sessEl.remove();
  sessions.splice(sessions.indexOf(session), 1);
  if (sessions.length === 0) { await newSession(); return; }
  if (activeSession === session) activateSession(sessions[sessions.length - 1]);
  renderSidebar();
}

function renderSidebar() {
  for (const s of sessions) {
    const n = s.sessEl.querySelector(".n") as HTMLElement;
    n.textContent = s.panes.length > 1 ? String(s.panes.length) : "";
  }
  footEl.textContent = `${sessions.length} terminal${sessions.length === 1 ? "" : "s"}`;
  updateChrome();
}

function updateChrome() {
  if (!activeSession) return;
  const count = activeSession.panes.length;
  titleEl.innerHTML = `${activeSession.name}` + (count > 1 ? ` <span class="sub">${count} panes</span>` : "");
}
function refreshTitles() {
  if (activeSession) {
    const count = activeSession.panes.length;
    const label = (focusedPane && focusedPane.title) || activeSession.name;
    titleEl.innerHTML = `${label}` + (count > 1 ? ` <span class="sub">${count} panes</span>` : "");
  }
  for (const s of sessions) {
    const first = s.panes[0];
    const name = (first && first.title) || s.name;
    const el = s.sessEl.querySelector(".name") as HTMLElement | null;
    if (el) el.textContent = name;
  }
}

interface Action { label: string; icon: string; key?: string; run: () => void; }

function baseActions(): Action[] {
  return [
    { label: "New terminal", icon: "+", key: "Cmd T", run: () => void newSession() },
    { label: "Split right", icon: "\u2502", key: "Cmd D", run: () => void splitActive("row") },
    { label: "Split down", icon: "\u2500", key: "Cmd Shift D", run: () => void splitActive("col") },
    { label: "Close pane", icon: "\u00d7", key: "Cmd W", run: () => { if (focusedPane) void closePane(focusedPane); } },
    { label: "Toggle sidebar", icon: "\u25e7", key: "Cmd B", run: toggleSidebar },
  ];
}

function jumpActions(): Action[] {
  return sessions.map((s, i) => ({
    label: `Go to ${s.name}`,
    icon: "\u203a",
    key: i < 9 ? `Cmd ${i + 1}` : undefined,
    run: () => activateSession(s),
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
  if (focusedPane) focusedPane.term.focus();
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


function toggleSidebar() { document.body.classList.toggle("sidebar-hidden"); if (activeSession) fit(activeSession); }

document.getElementById("side-add")!.addEventListener("click", () => void newSession());
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

window.addEventListener("keydown", (e) => {
  if (!e.metaKey) return;
  const k = e.key.toLowerCase();
  if (k === "k") { e.preventDefault(); paletteEl.classList.contains("open") ? closePalette() : openPalette(); return; }
  if (paletteEl.classList.contains("open")) return;
  if (k === "t") { e.preventDefault(); void newSession(); }
  else if (k === "d" && e.shiftKey) { e.preventDefault(); void splitActive("col"); }
  else if (k === "d") { e.preventDefault(); void splitActive("row"); }
  else if (k === "w") { e.preventDefault(); if (focusedPane) void closePane(focusedPane); }
  else if (k === "b") { e.preventDefault(); toggleSidebar(); }
  else if (k === "]" && activeSession && activeSession.panes.length) {
    e.preventDefault();
    const panes = activeSession.panes;
    const idx = focusedPane ? panes.indexOf(focusedPane) : -1;
    focusPane(panes[(idx + 1) % panes.length]);
  }
  else if (k === "[" && activeSession && activeSession.panes.length) {
    e.preventDefault();
    const panes = activeSession.panes;
    const idx = focusedPane ? panes.indexOf(focusedPane) : 0;
    focusPane(panes[(idx - 1 + panes.length) % panes.length]);
  }
  else if (k >= "1" && k <= "9") { const i = Number(k) - 1; if (sessions[i]) { e.preventDefault(); activateSession(sessions[i]); } }
}, true);

window.addEventListener("resize", () => { if (activeSession) fit(activeSession); });

(async () => {
  const list = await invoke<{ panes: { paneId: string }[] }>("app.paneList", {});
  if (list.panes.length > 0) {
    for (const info of list.panes) adoptSession(info.paneId);
    activateSession(sessions[0]);
  } else {
    await newSession();
  }
})();
