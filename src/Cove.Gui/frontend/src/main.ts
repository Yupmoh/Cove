import { Terminal } from "@xterm/xterm";
import { FitAddon } from "@xterm/addon-fit";
import { WebglAddon } from "@xterm/addon-webgl";
import "@xterm/xterm/css/xterm.css";
import { toBase64Utf8, parseRelayText } from "./wsproto";

const CREDIT_THRESHOLD = 131072;

async function invoke<T>(cmd: string, args: unknown): Promise<T> {
  return JSON.parse((await window.__ryn.invoke(cmd, args as Record<string, unknown>)) as string) as T;
}

const term = new Terminal({ scrollback: 5000, convertEol: false, fontFamily: "monospace", fontSize: 13 });
const fit = new FitAddon();
term.loadAddon(fit);
try { term.loadAddon(new WebglAddon()); } catch { void 0; }
term.open(document.getElementById("term")!);
fit.fit();

async function attach(paneId: string) {
  const ws = new WebSocket(`ws://${location.host}/pty?pane=${encodeURIComponent(paneId)}&since=0`);
  ws.binaryType = "arraybuffer";
  let base = 0, consumed = 0, lastAck = 0;
  const sendAck = () => { if (ws.readyState === 1 && consumed > lastAck) { ws.send(JSON.stringify({ t: "ack", off: consumed })); lastAck = consumed; } };
  ws.onmessage = (ev) => {
    if (typeof ev.data === "string") {
      const m = parseRelayText(ev.data);
      if (!m) return;
      if (m.t === "base") { base = m.off; consumed = m.off; lastAck = m.off; }
      else if (m.t === "resync") { term.reset(); base = m.base; consumed = m.base; lastAck = m.base; }
      else if (m.t === "end") { term.write(`\r\n[process exited: ${m.code}]\r\n`); }
      return;
    }
    const bytes = new Uint8Array(ev.data as ArrayBuffer);
    term.write(bytes, () => { consumed += bytes.length; if (consumed - lastAck >= CREDIT_THRESHOLD) sendAck(); });
  };
  setInterval(sendAck, 100);
  term.onData((d) => { void invoke("app.paneWrite", { paneId, dataBase64: toBase64Utf8(d) }); });
  term.onResize(({ cols, rows }) => { void invoke("app.paneResize", { paneId, cols, rows }); });
}

window.addEventListener("resize", () => fit.fit());

(async () => {
  const list = await invoke<{ panes: { paneId: string }[] }>("app.paneList", {});
  let paneId: string;
  if (list.panes.length > 0) paneId = list.panes[0].paneId;
  else { const r = await invoke<{ paneId: string }>("app.paneSpawn", { command: "", cols: term.cols, rows: term.rows }); paneId = r.paneId; }
  await attach(paneId);
})();
