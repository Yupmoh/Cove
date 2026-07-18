import { Terminal } from "@xterm/xterm";
import { FitAddon } from "@xterm/addon-fit";
import { WebglAddon } from "@xterm/addon-webgl";
import { CanvasAddon } from "@xterm/addon-canvas";
import "@xterm/xterm/css/xterm.css";
import { FrontendCommand } from "../app/frontend-command";
import { computeStats, fps, throughputMBs, round1 } from "./instrument";
import { detectWebgl2 } from "./renderer";
import type { RendererKind } from "./renderer";

const RENDERERS: RendererKind[] = ["webgl", "canvas", "dom"];
const NOOK_COUNTS = [1, 5, 10, 20];
const SCENARIOS = ["idle", "yesSpam", "resizeStorm"] as const;
const CELL_DURATION_MS = 3000;
const RESIZE_TICK_MS = 100;
const YES_CHUNK = "y\n".repeat(512);

type Scenario = (typeof SCENARIOS)[number] | "yesSpamHidden";
interface Row { renderer: RendererKind; nooks: number; visible: number; scenario: Scenario; fps: number; frameP95Ms: number; longtaskMs: number; throughputMBs: number; glContextLoss: number; }

const grid = document.getElementById("grid")!;
const status = document.getElementById("status")!;
const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms));

function makeNook(renderer: RendererKind): { term: Terminal; fit: FitAddon; el: HTMLDivElement; loss: () => number } {
  const el = document.createElement("div");
  el.className = "nook";
  grid.appendChild(el);
  const term = new Terminal({ scrollback: 1000, fontSize: 11, fontFamily: "monospace" });
  const fit = new FitAddon();
  term.loadAddon(fit);
  let losses = 0;
  if (renderer === "webgl") {
    try { const gl = new WebglAddon(); gl.onContextLoss(() => losses++); term.loadAddon(gl); } catch { void 0; }
  } else if (renderer === "canvas") {
    try { term.loadAddon(new CanvasAddon()); } catch { void 0; }
  }
  term.open(el);
  try { fit.fit(); } catch { void 0; }
  return { term, fit, el, loss: () => losses };
}

async function runCell(renderer: RendererKind, nooks: number, scenario: Scenario): Promise<Row> {
  const visible = scenario === "yesSpamHidden" ? 4 : nooks;
  const cells: { term: Terminal; fit: FitAddon; el: HTMLDivElement; loss: () => number }[] = [];
  for (let i = 0; i < nooks; i++) {
    const p = makeNook(renderer);
    if (i >= visible) p.el.style.display = "none";
    cells.push(p);
  }
  await sleep(100);

  const deltas: number[] = [];
  let last = performance.now();
  let running = true;
  const rafLoop = (now: number) => { deltas.push(now - last); last = now; if (running) requestAnimationFrame(rafLoop); };
  requestAnimationFrame(rafLoop);

  let longtaskMs = 0;
  const obs = new PerformanceObserver((list) => { for (const e of list.getEntries()) longtaskMs += e.duration; });
  try { obs.observe({ entryTypes: ["longtask"] }); } catch { void 0; }

  let bytesWritten = 0;
  let feeding = scenario === "yesSpam" || scenario === "yesSpamHidden";
  if (feeding) {
    for (let i = 0; i < visible; i++) {
      const t = cells[i].term;
      const pump = () => { if (!feeding) return; t.write(YES_CHUNK, () => { bytesWritten += YES_CHUNK.length; pump(); }); };
      pump();
    }
  }
  let resizer = 0, wide = true;
  if (scenario === "resizeStorm") {
    resizer = window.setInterval(() => { wide = !wide; grid.style.width = wide ? "100%" : "70%"; for (const c of cells) { try { c.fit.fit(); } catch { void 0; } } }, RESIZE_TICK_MS);
  }

  await sleep(CELL_DURATION_MS);

  running = false;
  feeding = false;
  if (resizer) { clearInterval(resizer); grid.style.width = "100%"; }
  obs.disconnect();

  const row: Row = {
    renderer, nooks, visible, scenario,
    fps: round1(fps(deltas)),
    frameP95Ms: round1(computeStats(deltas).p95),
    longtaskMs: Math.round(longtaskMs),
    throughputMBs: round1(throughputMBs(bytesWritten, CELL_DURATION_MS)),
    glContextLoss: cells.reduce((a, c) => a + c.loss(), 0),
  };
  for (const c of cells) { c.term.dispose(); c.el.remove(); }
  await sleep(150);
  return row;
}

function toMarkdown(rows: Row[], meta: string): string {
  const head = "| renderer | nooks | visible | scenario | fps | frameP95(ms) | longtask(ms) | throughput(MB/s) | glLoss |\n|---|---|---|---|---|---|---|---|---|";
  const body = rows.map((r) => `| ${r.renderer} | ${r.nooks} | ${r.visible} | ${r.scenario} | ${r.fps} | ${r.frameP95Ms} | ${r.longtaskMs} | ${r.throughputMBs} | ${r.glContextLoss} |`).join("\n");
  return `${meta}\n\n${head}\n${body}\n`;
}

(async () => {
  const webgl2 = detectWebgl2();
  const meta = `machine: ${navigator.userAgent}\nwebgl2Available: ${webgl2}\ntimestamp: ${new Date().toISOString()}`;
  const rows: Row[] = [];
  const plan: [RendererKind, number, Scenario][] = [];
  for (const r of RENDERERS) {
    for (const n of NOOK_COUNTS) for (const s of SCENARIOS) plan.push([r, n, s]);
    plan.push([r, 20, "yesSpamHidden"]);
  }
  for (let i = 0; i < plan.length; i++) {
    const [r, n, s] = plan[i];
    status.textContent = `running ${i + 1}/${plan.length}: ${r} ${n}p ${s}`;
    rows.push(await runCell(r, n, s));
  }
  const md = toMarkdown(rows, meta);
  const dir = (await window.__ryn.invoke(FrontendCommand.AppSavePerf, { json: JSON.stringify({ done: true, meta, rows }, null, 2), markdown: md })) as string;
  status.textContent = `PERF_DONE -> ${dir}`;
})();
