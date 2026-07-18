import { invoke } from "./invoke";
import { FrontendCommand } from "./app/frontend-command";

interface NoteReadResult {
  id: string;
  title: string;
  content: string;
  kind: string;
  format: string | null;
}

interface SketchState {
  elements: unknown[];
  appState: {
    zoom: number;
    scrollX: number;
    scrollY: number;
  };
}

let currentNoteId: string | null = null;
let currentBayId: string | null = null;

export async function renderSketchNote(bayId: string, noteId: string): Promise<HTMLElement> {
  const el = document.createElement("div");
  el.className = "sketch-note-editor";
  el.style.cssText = "display:flex;flex-direction:column;height:100%;background:#0b1622;color:#e5e9f0;font-family:system-ui,sans-serif;";

  currentNoteId = noteId;
  currentBayId = bayId;

  try {
    const result = await invoke<NoteReadResult>(FrontendCommand.NoteRead, {
      bayId,
      id: noteId,
    });

    el.appendChild(buildToolbar(bayId));
    el.appendChild(buildCanvas(result.content));
  } catch (e) {
    el.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load sketch: ${(e as Error).message}</div>`;
  }

  return el;
}

function buildToolbar(bayId: string): HTMLElement {
  const toolbar = document.createElement("div");
  toolbar.style.cssText = "padding:8px 12px;display:flex;gap:8px;align-items:center;border-bottom:1px solid #1e2d3f;flex-wrap:wrap;";

  const save = document.createElement("button");
  save.textContent = "Save";
  save.style.cssText = "padding:4px 12px;background:#2563eb;border:1px solid #3b82f6;border-radius:4px;color:#fff;cursor:pointer;font-size:12px;";
  save.addEventListener("click", () => saveSketch());
  toolbar.appendChild(save);

  const exportSvg = document.createElement("button");
  exportSvg.textContent = "Export SVG";
  exportSvg.style.cssText = "padding:4px 12px;background:#1e2d3f;border:1px solid #2b3d52;border-radius:4px;color:#e5e9f0;cursor:pointer;font-size:12px;";
  exportSvg.addEventListener("click", () => exportFormat("svg"));
  toolbar.appendChild(exportSvg);

  const exportPng = document.createElement("button");
  exportPng.textContent = "Export PNG";
  exportPng.style.cssText = "padding:4px 12px;background:#1e2d3f;border:1px solid #2b3d52;border-radius:4px;color:#e5e9f0;cursor:pointer;font-size:12px;";
  exportPng.addEventListener("click", () => exportFormat("png"));
  toolbar.appendChild(exportPng);

  const zoomLabel = document.createElement("span");
  zoomLabel.style.cssText = "font-size:12px;color:#6b7d8f;margin-left:8px;";
  zoomLabel.textContent = "Zoom:";
  toolbar.appendChild(zoomLabel);

  const zoomIn = document.createElement("button");
  zoomIn.textContent = "+";
  zoomIn.style.cssText = "padding:4px 8px;background:#1e2d3f;border:1px solid #2b3d52;border-radius:4px;color:#e5e9f0;cursor:pointer;font-size:12px;";
  zoomIn.addEventListener("click", () => adjustZoom(0.1));
  toolbar.appendChild(zoomIn);

  const zoomOut = document.createElement("button");
  zoomOut.textContent = "−";
  zoomOut.style.cssText = "padding:4px 8px;background:#1e2d3f;border:1px solid #2b3d52;border-radius:4px;color:#e5e9f0;cursor:pointer;font-size:12px;";
  zoomOut.addEventListener("click", () => adjustZoom(-0.1));
  toolbar.appendChild(zoomOut);

  return toolbar;
}

function buildCanvas(content: string): HTMLElement {
  const container = document.createElement("div");
  container.className = "sketch-canvas-container";
  container.style.cssText = "flex:1;position:relative;overflow:hidden;background:#1a1a2e;";

  let state: SketchState;
  try {
    state = JSON.parse(content) as SketchState;
  } catch {
    state = { elements: [], appState: { zoom: 1, scrollX: 0, scrollY: 0 } };
  }

  const canvas = document.createElement("canvas");
  canvas.className = "sketch-canvas";
  canvas.style.cssText = "position:absolute;top:0;left:0;width:100%;height:100%;cursor:crosshair;";
  container.appendChild(canvas);

  const ctx = canvas.getContext("2d");
  if (ctx) {
    const resizeCanvas = () => {
      canvas.width = container.clientWidth;
      canvas.height = container.clientHeight;
      renderSketch(ctx, state, canvas.width, canvas.height);
    };
    resizeCanvas();
    window.addEventListener("resize", resizeCanvas);

    let isDragging = false;
    let lastX = 0;
    let lastY = 0;

    canvas.addEventListener("mousedown", (e) => {
      isDragging = true;
      lastX = e.offsetX;
      lastY = e.offsetY;
    });

    canvas.addEventListener("mousemove", (e) => {
      if (!isDragging) return;
      state.appState.scrollX += (e.offsetX - lastX) / state.appState.zoom;
      state.appState.scrollY += (e.offsetY - lastY) / state.appState.zoom;
      lastX = e.offsetX;
      lastY = e.offsetY;
      renderSketch(ctx, state, canvas.width, canvas.height);
    });

    canvas.addEventListener("mouseup", () => {
      isDragging = false;
      saveViewportState(state);
    });

    canvas.addEventListener("wheel", (e) => {
      e.preventDefault();
      const delta = e.deltaY > 0 ? -0.1 : 0.1;
      state.appState.zoom = Math.max(0.1, Math.min(5, state.appState.zoom + delta));
      renderSketch(ctx, state, canvas.width, canvas.height);
      saveViewportState(state);
    });
  }

  return container;
}

function renderSketch(ctx: CanvasRenderingContext2D, state: SketchState, width: number, height: number): void {
  ctx.fillStyle = "#1a1a2e";
  ctx.fillRect(0, 0, width, height);

  ctx.save();
  ctx.scale(state.appState.zoom, state.appState.zoom);
  ctx.translate(state.appState.scrollX, state.appState.scrollY);

  ctx.strokeStyle = "#e5e9f0";
  ctx.lineWidth = 2;
  ctx.lineCap = "round";

  const elements = state.elements as Array<{ type: string; x1: number; y1: number; x2: number; y2: number }>;
  for (const el of elements) {
    if (el.type === "line" || el.type === "arrow") {
      ctx.beginPath();
      ctx.moveTo(el.x1, el.y1);
      ctx.lineTo(el.x2, el.y2);
      ctx.stroke();
      if (el.type === "arrow") {
        const angle = Math.atan2(el.y2 - el.y1, el.x2 - el.x1);
        const headLen = 10;
        ctx.beginPath();
        ctx.moveTo(el.x2, el.y2);
        ctx.lineTo(el.x2 - headLen * Math.cos(angle - Math.PI / 6), el.y2 - headLen * Math.sin(angle - Math.PI / 6));
        ctx.moveTo(el.x2, el.y2);
        ctx.lineTo(el.x2 - headLen * Math.cos(angle + Math.PI / 6), el.y2 - headLen * Math.sin(angle + Math.PI / 6));
        ctx.stroke();
      }
    }
  }

  ctx.restore();

  ctx.fillStyle = "#6b7d8f";
  ctx.font = "12px system-ui";
  ctx.fillText(`Zoom: ${Math.round(state.appState.zoom * 100)}%  Pan: ${Math.round(state.appState.scrollX)}, ${Math.round(state.appState.scrollY)}`, 8, height - 8);
}

function adjustZoom(delta: number): void {
  const canvas = document.querySelector(".sketch-canvas") as HTMLCanvasElement;
  if (!canvas) return;
  const ctx = canvas.getContext("2d");
  if (!ctx) return;
  const stateStr = canvas.getAttribute("data-state");
  if (!stateStr) return;
  const state = JSON.parse(stateStr) as SketchState;
  state.appState.zoom = Math.max(0.1, Math.min(5, state.appState.zoom + delta));
  renderSketch(ctx, state, canvas.width, canvas.height);
  saveViewportState(state);
}

async function saveViewportState(state: SketchState): Promise<void> {
  if (!currentBayId || !currentNoteId) return;
  try {
    await invoke(FrontendCommand.NoteWrite, {
      bayId: currentBayId,
      id: currentNoteId,
      content: JSON.stringify(state),
    });
  } catch (e) {
    console.error("Viewport save failed:", e);
  }
}

async function saveSketch(): Promise<void> {
  if (!currentBayId || !currentNoteId) return;
  const canvas = document.querySelector(".sketch-canvas") as HTMLCanvasElement;
  if (!canvas) return;
  const stateStr = canvas.getAttribute("data-state");
  if (!stateStr) return;
  try {
    await invoke(FrontendCommand.NoteWrite, {
      bayId: currentBayId,
      id: currentNoteId,
      content: stateStr,
    });
  } catch (e) {
    console.error("Save failed:", e);
  }
}

async function exportFormat(format: string): Promise<void> {
  if (!currentBayId || !currentNoteId) return;
  try {
    const result = await invoke<NoteReadResult>(FrontendCommand.NoteRead, {
      bayId: currentBayId,
      id: currentNoteId,
      format,
    });
    if (format === "svg") {
      const blob = new Blob([result.content], { type: "image/svg+xml" });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `${currentNoteId}.svg`;
      a.click();
      URL.revokeObjectURL(url);
    } else if (format === "png") {
      const byteString = atob(result.content);
      const bytes = new Uint8Array(byteString.length);
      for (let i = 0; i < byteString.length; i++) bytes[i] = byteString.charCodeAt(i);
      const blob = new Blob([bytes], { type: "image/png" });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `${currentNoteId}.png`;
      a.click();
      URL.revokeObjectURL(url);
    }
  } catch (e) {
    console.error("Export failed:", e);
  }
}
