import { invoke } from "./invoke";

interface NoteReadResult {
  id: string;
  title: string;
  content: string;
  kind: string;
  format: string | null;
}

interface CanvasElement {
  id: string;
  type: string;
  props: Record<string, unknown>;
  children?: CanvasElement[];
}

interface CanvasState {
  root: { elements: CanvasElement[] };
  state: Record<string, unknown>;
}

const COMPONENT_CATALOG: Record<string, string[]> = {
  "layout.row": ["direction", "gap", "align"],
  "layout.column": ["gap", "align"],
  "layout.stack": ["gap"],
  "layout.grid": ["columns", "rows", "gap"],
  "layout.divider": [],
  "layout.spacer": ["size"],
  "layout.card": ["padding", "border"],
  "layout.tabs": ["activeTab"],
  "text.heading": ["level", "text"],
  "text.paragraph": ["text"],
  "text.label": ["text"],
  "text.code": ["text", "language"],
  "text.markdown": ["content"],
  "text.link": ["href", "text"],
  "text.badge": ["text", "color"],
  "form.input": ["bind", "label", "placeholder"],
  "form.textarea": ["bind", "label", "rows"],
  "form.checkbox": ["bind", "label"],
  "form.select": ["bind", "label", "options"],
  "form.slider": ["bind", "label", "min", "max"],
  "form.button": ["label", "action"],
  "form.toggle": ["bind", "label"],
  "form.datepicker": ["bind", "label"],
  "display.image": ["src", "alt"],
  "display.progress": ["value", "max"],
  "display.spinner": ["size"],
  "display.stat": ["label", "value"],
  "display.chart": ["type", "data"],
  "display.list": ["items"],
  "display.table": ["columns", "rows"],
  "display.tree": ["nodes"],
  "display.timeline": ["events"],
  "display.kanban": ["columns"],
  "display.calendar": ["date"],
  "display.map": ["points"],
  "media.video": ["src"],
  "media.audio": ["src"],
  "interaction.tooltip": ["text"],
  "interaction.popover": ["trigger", "content"],
  "interaction.contextmenu": ["items"],
  "interaction.draggable": ["handle"],
  "interaction.resizable": ["min", "max"],
  "navigation.breadcrumb": ["items"],
  "navigation.pagination": ["page", "total"],
};

let currentNoteId: string | null = null;
let currentWorkspaceId: string | null = null;
let canvasState: CanvasState = { root: { elements: [] }, state: {} };

export async function renderCanvasNote(workspaceId: string, noteId: string): Promise<HTMLElement> {
  const el = document.createElement("div");
  el.className = "canvas-note-editor";
  el.style.cssText = "display:flex;flex-direction:column;height:100%;background:#0b1622;color:#e5e9f0;font-family:system-ui,sans-serif;";

  currentNoteId = noteId;
  currentWorkspaceId = workspaceId;

  try {
    const result = await invoke<NoteReadResult>("cove://commands/note.read", { workspaceId, id: noteId });
    try {
      canvasState = JSON.parse(result.content) as CanvasState;
    } catch {
      canvasState = { root: { elements: [] }, state: {} };
    }
    el.appendChild(buildToolbar());
    el.appendChild(buildCanvas());
  } catch (e) {
    el.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load canvas: ${(e as Error).message}</div>`;
  }

  return el;
}

function buildToolbar(): HTMLElement {
  const toolbar = document.createElement("div");
  toolbar.style.cssText = "padding:8px 12px;display:flex;gap:8px;align-items:center;border-bottom:1px solid #1e2d3f;flex-wrap:wrap;";

  const save = document.createElement("button");
  save.textContent = "Save";
  save.style.cssText = "padding:4px 12px;background:#2563eb;border:1px solid #3b82f6;border-radius:4px;color:#fff;cursor:pointer;font-size:12px;";
  save.addEventListener("click", saveCanvas);
  toolbar.appendChild(save);

  const addBtn = document.createElement("select");
  addBtn.style.cssText = "padding:4px 8px;background:#14202e;border:1px solid #2b3d52;border-radius:4px;color:#e5e9f0;font-size:12px;";
  const defaultOpt = document.createElement("option");
  defaultOpt.value = "";
  defaultOpt.textContent = "Add component...";
  addBtn.appendChild(defaultOpt);
  for (const type of Object.keys(COMPONENT_CATALOG)) {
    const opt = document.createElement("option");
    opt.value = type;
    opt.textContent = type;
    addBtn.appendChild(opt);
  }
  addBtn.addEventListener("change", () => {
    if (addBtn.value) {
      addComponent(addBtn.value);
      addBtn.value = "";
      rerender();
    }
  });
  toolbar.appendChild(addBtn);

  const sourceToggle = document.createElement("button");
  sourceToggle.textContent = "Source";
  sourceToggle.style.cssText = "padding:4px 12px;background:#1e2d3f;border:1px solid #2b3d52;border-radius:4px;color:#e5e9f0;cursor:pointer;font-size:12px;";
  sourceToggle.addEventListener("click", () => toggleSourceView());
  toolbar.appendChild(sourceToggle);

  return toolbar;
}

function buildCanvas(): HTMLElement {
  const container = document.createElement("div");
  container.className = "canvas-container";
  container.style.cssText = "flex:1;overflow-y:auto;padding:12px;";

  const rendered = renderElements(canvasState.root.elements, container);
  container.appendChild(rendered);
  return container;
}

function renderElements(elements: CanvasElement[], container: HTMLElement): HTMLElement {
  const root = document.createElement("div");
  root.style.cssText = "display:flex;flex-direction:column;gap:8px;";
  for (const el of elements) {
    root.appendChild(renderElement(el, container));
  }
  if (elements.length === 0) {
    const empty = document.createElement("div");
    empty.style.cssText = "padding:20px;color:#6b7d8f;text-align:center;";
    empty.textContent = "Add a component to start building your canvas.";
    root.appendChild(empty);
  }
  return root;
}

function renderElement(el: CanvasElement, container: HTMLElement): HTMLElement {
  const node = document.createElement("div");
  node.dataset.elementId = el.id;
  node.dataset.elementType = el.type;

  switch (el.type) {
    case "layout.row":
    case "layout.column":
    case "layout.stack": {
      node.style.cssText = `display:flex;flex-direction:${el.type === "layout.row" ? "row" : "column"};gap:${(el.props.gap as string) || "8px"};padding:8px;border:1px dashed #2b3d52;border-radius:4px;`;
      if (el.children) {
        for (const child of el.children) {
          node.appendChild(renderElement(child, container));
        }
      }
      break;
    }
    case "layout.grid": {
      const cols = (el.props.columns as number) || 2;
      node.style.cssText = `display:grid;grid-template-columns:repeat(${cols},1fr);gap:${(el.props.gap as string) || "8px"};padding:8px;border:1px dashed #2b3d52;border-radius:4px;`;
      if (el.children) {
        for (const child of el.children) {
          node.appendChild(renderElement(child, container));
        }
      }
      break;
    }
    case "layout.divider": {
      node.style.cssText = "height:1px;background:#2b3d52;margin:8px 0;";
      break;
    }
    case "layout.spacer": {
      node.style.cssText = `height:${(el.props.size as string) || "16px"};`;
      break;
    }
    case "layout.card": {
      node.style.cssText = "padding:12px;background:#14202e;border:1px solid #2b3d52;border-radius:8px;";
      if (el.children) {
        for (const child of el.children) {
          node.appendChild(renderElement(child, container));
        }
      }
      break;
    }
    case "text.heading": {
      const level = (el.props.level as number) || 1;
      const sizes: Record<number, string> = { 1: "24px", 2: "20px", 3: "16px", 4: "14px" };
      node.style.cssText = `font-size:${sizes[level] || "16px"};font-weight:600;color:#e5e9f0;`;
      node.textContent = (el.props.text as string) || "";
      break;
    }
    case "text.paragraph": {
      node.style.cssText = "font-size:14px;color:#e5e9f0;line-height:1.6;";
      node.textContent = (el.props.text as string) || "";
      break;
    }
    case "text.label": {
      node.style.cssText = "font-size:12px;color:#6b7d8f;";
      node.textContent = (el.props.text as string) || "";
      break;
    }
    case "text.code": {
      node.style.cssText = "font-family:'SF Mono',Monaco,monospace;font-size:13px;background:#14202e;padding:8px;border-radius:4px;color:#4ade80;";
      node.textContent = (el.props.text as string) || "";
      break;
    }
    case "text.badge": {
      const color = (el.props.color as string) || "#2563eb";
      node.style.cssText = `display:inline-block;padding:2px 8px;background:${color};border-radius:8px;font-size:11px;color:#fff;`;
      node.textContent = (el.props.text as string) || "";
      break;
    }
    case "form.input": {
      const bind = el.props.bind as string;
      const label = document.createElement("label");
      label.style.cssText = "display:block;font-size:12px;color:#6b7d8f;margin-bottom:4px;";
      label.textContent = (el.props.label as string) || bind || "";
      node.appendChild(label);
      const input = document.createElement("input");
      input.type = "text";
      input.value = (canvasState.state[bind] as string) || "";
      input.placeholder = (el.props.placeholder as string) || "";
      input.style.cssText = "width:100%;padding:4px 8px;background:#0b1622;border:1px solid #2b3d52;border-radius:4px;color:#e5e9f0;font-size:13px;";
      input.addEventListener("input", () => {
        canvasState.state[bind] = input.value;
      });
      node.appendChild(input);
      break;
    }
    case "form.textarea": {
      const bind = el.props.bind as string;
      const label = document.createElement("label");
      label.style.cssText = "display:block;font-size:12px;color:#6b7d8f;margin-bottom:4px;";
      label.textContent = (el.props.label as string) || bind || "";
      node.appendChild(label);
      const textarea = document.createElement("textarea");
      textarea.value = (canvasState.state[bind] as string) || "";
      textarea.rows = (el.props.rows as number) || 3;
      textarea.style.cssText = "width:100%;padding:4px 8px;background:#0b1622;border:1px solid #2b3d52;border-radius:4px;color:#e5e9f0;font-size:13px;resize:vertical;";
      textarea.addEventListener("input", () => {
        canvasState.state[bind] = textarea.value;
      });
      node.appendChild(textarea);
      break;
    }
    case "form.checkbox": {
      const bind = el.props.bind as string;
      const label = document.createElement("label");
      label.style.cssText = "display:flex;align-items:center;gap:6px;font-size:13px;color:#e5e9f0;cursor:pointer;";
      const checkbox = document.createElement("input");
      checkbox.type = "checkbox";
      checkbox.checked = (canvasState.state[bind] as boolean) || false;
      checkbox.addEventListener("change", () => {
        canvasState.state[bind] = checkbox.checked;
      });
      label.appendChild(checkbox);
      label.appendChild(document.createTextNode((el.props.label as string) || bind || ""));
      node.appendChild(label);
      break;
    }
    case "form.button": {
      const btn = document.createElement("button");
      btn.textContent = (el.props.label as string) || "Button";
      btn.style.cssText = "padding:4px 12px;background:#2563eb;border:1px solid #3b82f6;border-radius:4px;color:#fff;cursor:pointer;font-size:12px;";
      btn.addEventListener("click", () => dispatchAction(el));
      node.appendChild(btn);
      break;
    }
    case "form.select": {
      const bind = el.props.bind as string;
      const label = document.createElement("label");
      label.style.cssText = "display:block;font-size:12px;color:#6b7d8f;margin-bottom:4px;";
      label.textContent = (el.props.label as string) || bind || "";
      node.appendChild(label);
      const select = document.createElement("select");
      select.style.cssText = "width:100%;padding:4px 8px;background:#0b1622;border:1px solid #2b3d52;border-radius:4px;color:#e5e9f0;font-size:13px;";
      const options = (el.props.options as Array<{ value: string; label: string }>) || [];
      for (const opt of options) {
        const o = document.createElement("option");
        o.value = opt.value;
        o.textContent = opt.label;
        select.appendChild(o);
      }
      select.value = (canvasState.state[bind] as string) || "";
      select.addEventListener("change", () => {
        canvasState.state[bind] = select.value;
      });
      node.appendChild(select);
      break;
    }
    case "form.slider": {
      const bind = el.props.bind as string;
      const label = document.createElement("label");
      label.style.cssText = "display:block;font-size:12px;color:#6b7d8f;margin-bottom:4px;";
      label.textContent = (el.props.label as string) || bind || "";
      node.appendChild(label);
      const slider = document.createElement("input");
      slider.type = "range";
      slider.min = String((el.props.min as number) || 0);
      slider.max = String((el.props.max as number) || 100);
      slider.value = String((canvasState.state[bind] as number) || 0);
      slider.addEventListener("input", () => {
        canvasState.state[bind] = parseInt(slider.value, 10);
      });
      node.appendChild(slider);
      break;
    }
    case "form.toggle": {
      const bind = el.props.bind as string;
      const toggle = document.createElement("button");
      const isOn = (canvasState.state[bind] as boolean) || false;
      toggle.textContent = isOn ? "ON" : "OFF";
      toggle.style.cssText = `padding:4px 12px;background:${isOn ? "#16a34a" : "#1e2d3f"};border:1px solid ${isOn ? "#22c55e" : "#2b3d52"};border-radius:4px;color:#fff;cursor:pointer;font-size:12px;`;
      toggle.addEventListener("click", () => {
        canvasState.state[bind] = !canvasState.state[bind];
        rerender();
      });
      node.appendChild(toggle);
      break;
    }
    case "display.progress": {
      const value = (el.props.value as number) || 0;
      const max = (el.props.max as number) || 100;
      const pct = Math.min(100, (value / max) * 100);
      node.style.cssText = "height:8px;background:#14202e;border-radius:4px;overflow:hidden;";
      const bar = document.createElement("div");
      bar.style.cssText = `height:100%;width:${pct}%;background:#2563eb;`;
      node.appendChild(bar);
      break;
    }
    case "display.spinner": {
      node.style.cssText = "width:24px;height:24px;border:3px solid #2b3d52;border-top-color:#2563eb;border-radius:50%;animation:spin 1s linear infinite;";
      break;
    }
    case "display.stat": {
      node.style.cssText = "padding:8px;background:#14202e;border-radius:4px;";
      const label = document.createElement("div");
      label.style.cssText = "font-size:11px;color:#6b7d8f;";
      label.textContent = (el.props.label as string) || "";
      const value = document.createElement("div");
      value.style.cssText = "font-size:20px;font-weight:600;color:#e5e9f0;";
      value.textContent = String((el.props.value as string) || "");
      node.appendChild(label);
      node.appendChild(value);
      break;
    }
    case "display.list": {
      const items = (el.props.items as string[]) || [];
      node.style.cssText = "padding-left:16px;";
      for (const item of items) {
        const li = document.createElement("div");
        li.style.cssText = "font-size:13px;color:#e5e9f0;padding:2px 0;";
        li.textContent = `• ${item}`;
        node.appendChild(li);
      }
      break;
    }
    default: {
      node.style.cssText = "padding:8px;background:#14202e;border:1px solid #2b3d52;border-radius:4px;font-size:12px;color:#6b7d8f;";
      node.textContent = `[${el.type}]`;
    }
  }

  return node;
}

async function dispatchAction(el: CanvasElement): Promise<void> {
  const action = el.props.action as string | undefined;
  if (!action) return;

  const actionId = el.id;
  const payload = resolveFraming(action, canvasState.state, actionId);

  if (action.startsWith("send_to_agent:")) {
    const target = action.substring("send_to_agent:".length);
    try {
      await invoke("cove://commands/canvas.action", {
        action: "send_to_agent",
        targetPane: target,
        actionId,
        payload,
        state: canvasState.state,
      });
    } catch (e) {
      console.error("send_to_agent failed:", e);
    }
  } else if (action.startsWith("cove_command:")) {
    const uri = action.substring("cove_command:".length);
    try {
      await invoke("cove://commands/canvas.action", {
        action: "cove_command",
        uri: resolveFraming(uri, canvasState.state, actionId),
        actionId,
        payload,
        state: canvasState.state,
      });
    } catch (e) {
      console.error("cove_command failed:", e);
    }
  }
}

function resolveFraming(template: string, state: Record<string, unknown>, actionId: string): string {
  let result = template;
  result = result.replace(/\{actionId\}/g, actionId);
  result = result.replace(/\{payload\}/g, JSON.stringify(state));
  for (const [key, value] of Object.entries(state)) {
    result = result.replace(new RegExp(`\\{${key}\\}`, "g"), String(value));
  }
  return result;
}

function addComponent(type: string): void {
  const id = `el-${Date.now()}`;
  const props: Record<string, unknown> = {};
  const propNames = COMPONENT_CATALOG[type] || [];
  for (const prop of propNames) {
    if (prop === "text" || prop === "label" || prop === "content" || prop === "placeholder" || prop === "href" || prop === "alt" || prop === "src") {
      props[prop] = "";
    } else if (prop === "level" || prop === "rows" || prop === "columns" || prop === "min" || prop === "max" || prop === "page" || prop === "total" || prop === "size" || prop === "value") {
      props[prop] = 1;
    } else if (prop === "gap" || prop === "direction" || prop === "align" || prop === "color" || prop === "border" || prop === "padding" || prop === "activeTab" || prop === "trigger" || prop === "handle" || prop === "language" || prop === "action") {
      props[prop] = "";
    } else if (prop === "options" || prop === "items" || prop === "columns" || prop === "rows" || prop === "nodes" || prop === "events" || prop === "points" || prop === "data") {
      props[prop] = [];
    } else if (prop === "bind") {
      props[prop] = `field_${id}`;
    }
  }
  canvasState.root.elements.push({ id, type, props });
}

function toggleSourceView(): void {
  const container = document.querySelector(".canvas-container") as HTMLElement;
  if (!container) return;
  if (container.dataset.mode === "source") {
    container.dataset.mode = "render";
    rerender();
  } else {
    container.dataset.mode = "source";
    container.innerHTML = "";
    const textarea = document.createElement("textarea");
    textarea.value = JSON.stringify(canvasState, null, 2);
    textarea.style.cssText = "width:100%;height:100%;padding:12px;background:#0b1622;border:none;color:#e5e9f0;font-family:'SF Mono',Monaco,monospace;font-size:12px;resize:none;outline:none;";
    textarea.addEventListener("input", () => {
      try {
        canvasState = JSON.parse(textarea.value) as CanvasState;
      } catch {
        // invalid JSON, keep editing
      }
    });
    container.appendChild(textarea);
  }
}

function rerender(): void {
  const container = document.querySelector(".canvas-container") as HTMLElement;
  if (!container || container.dataset.mode === "source") return;
  const newCanvas = buildCanvas();
  container.replaceWith(newCanvas);
}

async function saveCanvas(): Promise<void> {
  if (!currentWorkspaceId || !currentNoteId) return;
  try {
    await invoke("cove://commands/note.write", {
      workspaceId: currentWorkspaceId,
      id: currentNoteId,
      content: JSON.stringify(canvasState, null, 2),
    });
  } catch (e) {
    console.error("Canvas save failed:", e);
  }
}

export function getCatalogChecklist(): Record<string, boolean> {
  const checklist: Record<string, boolean> = {};
  for (const type of Object.keys(COMPONENT_CATALOG)) {
    checklist[type] = true;
  }
  return checklist;
}
