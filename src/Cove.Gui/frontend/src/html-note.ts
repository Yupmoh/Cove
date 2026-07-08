import { invoke } from "./invoke";

interface NoteReadResult {
  id: string;
  title: string;
  content: string;
  kind: string;
  format: string | null;
}

let currentNoteId: string | null = null;
let currentWorkspaceId: string | null = null;
let currentContent: string = "";
let runtimeState: Record<string, unknown> = {};
let sourceMode = false;

export async function renderHtmlNote(workspaceId: string, noteId: string): Promise<HTMLElement> {
  const el = document.createElement("div");
  el.className = "html-note-editor";
  el.style.cssText = "display:flex;flex-direction:column;height:100%;background:#0b1622;color:#e5e9f0;font-family:system-ui,sans-serif;";

  currentNoteId = noteId;
  currentWorkspaceId = workspaceId;

  try {
    const result = await invoke<NoteReadResult>("cove://commands/note.read", { workspaceId, id: noteId });
    currentContent = result.content;

    const stateJson = await invoke<{ state: string | null }>("cove://commands/note.get-state", {
      workspaceId,
      id: noteId,
    }).catch(() => ({ state: null }));
    if (stateJson.state) {
      try { runtimeState = JSON.parse(stateJson.state); } catch { runtimeState = {}; }
    }

    el.appendChild(buildToolbar());
    el.appendChild(buildContent());
  } catch (e) {
    el.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load HTML note: ${(e as Error).message}</div>`;
  }

  return el;
}

function buildToolbar(): HTMLElement {
  const toolbar = document.createElement("div");
  toolbar.style.cssText = "padding:8px 12px;display:flex;gap:8px;align-items:center;border-bottom:1px solid #1e2d3f;flex-wrap:wrap;";

  const save = document.createElement("button");
  save.textContent = "Save";
  save.style.cssText = "padding:4px 12px;background:#2563eb;border:1px solid #3b82f6;border-radius:4px;color:#fff;cursor:pointer;font-size:12px;";
  save.addEventListener("click", saveHtmlNote);
  toolbar.appendChild(save);

  const sourceToggle = document.createElement("button");
  sourceToggle.textContent = sourceMode ? "Preview" : "Source";
  sourceToggle.style.cssText = "padding:4px 12px;background:#1e2d3f;border:1px solid #2b3d52;border-radius:4px;color:#e5e9f0;cursor:pointer;font-size:12px;";
  sourceToggle.addEventListener("click", () => {
    sourceMode = !sourceMode;
    sourceToggle.textContent = sourceMode ? "Preview" : "Source";
    rerender();
  });
  toolbar.appendChild(sourceToggle);

  return toolbar;
}

function buildContent(): HTMLElement {
  const container = document.createElement("div");
  container.className = "html-note-container";
  container.style.cssText = "flex:1;overflow:hidden;position:relative;";

  if (sourceMode) {
    const textarea = document.createElement("textarea");
    textarea.value = currentContent;
    textarea.style.cssText = "width:100%;height:100%;padding:12px;background:#0b1622;border:none;color:#e5e9f0;font-family:'SF Mono',Monaco,monospace;font-size:13px;resize:none;outline:none;";
    textarea.addEventListener("input", () => {
      currentContent = textarea.value;
    });
    container.appendChild(textarea);
  } else {
    const iframe = createSandboxedIframe();
    container.appendChild(iframe);
    setupPostMessageListener(iframe);
  }

  return container;
}

function createSandboxedIframe(): HTMLIFrameElement {
  const iframe = document.createElement("iframe");
  iframe.sandbox.add("allow-scripts");
  iframe.sandbox.remove("allow-same-origin");
  iframe.style.cssText = "width:100%;height:100%;border:none;background:#fff;";
  iframe.srcdoc = buildSrcDoc();
  return iframe;
}

function buildSrcDoc(): string {
  const stateScript = `<script>window.__coveState = ${JSON.stringify(runtimeState)};</script>`;
  const stateCapture = `
    <script>
      window.addEventListener('beforeunload', () => {
        const state = window.__coveRuntimeState || {};
        window.parent.postMessage({ type: 'cove:state-snapshot', state }, '*');
      });
      setInterval(() => {
        const state = window.__coveRuntimeState || {};
        window.parent.postMessage({ type: 'cove:state-snapshot', state }, '*');
      }, 5000);
    </script>
  `;
  return `<!DOCTYPE html><html><head><meta charset="utf-8"><base target="_blank">${stateScript}</head><body>${currentContent}${stateCapture}</body></html>`;
}

function setupPostMessageListener(iframe: HTMLIFrameElement): void {
  window.addEventListener("message", (e) => {
    if (e.source !== iframe.contentWindow) return;
    const data = e.data;
    if (!data || typeof data !== "object") return;
    if (data.type === "cove:state-snapshot" && data.state) {
      runtimeState = data.state as Record<string, unknown>;
      persistRuntimeState();
    }
  });
}

async function persistRuntimeState(): Promise<void> {
  if (!currentWorkspaceId || !currentNoteId) return;
  try {
    await invoke("cove://commands/note.save-state", {
      workspaceId: currentWorkspaceId,
      id: currentNoteId,
      stateJson: JSON.stringify(runtimeState),
    });
  } catch (e) {
    console.error("State persist failed:", e);
  }
}

function rerender(): void {
  const container = document.querySelector(".html-note-container") as HTMLElement;
  if (!container) return;
  const newContent = buildContent();
  container.replaceWith(newContent);
}

async function saveHtmlNote(): Promise<void> {
  if (!currentWorkspaceId || !currentNoteId) return;
  try {
    await invoke("cove://commands/note.write", {
      workspaceId: currentWorkspaceId,
      id: currentNoteId,
      content: currentContent,
    });
    await persistRuntimeState();
  } catch (e) {
    console.error("Save failed:", e);
  }
}
