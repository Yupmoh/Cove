import { LifecycleScope, type NookContentHandle } from "./app/lifecycle";
import { FrontendCommand } from "./app/frontend-command";
import { invoke } from "./invoke";

interface NoteReadResult {
  id: string;
  title: string;
  content: string;
  kind: string;
  format: string | null;
}

export async function renderHtmlNote(bayId: string, noteId: string): Promise<NookContentHandle> {
  const lifecycle = new LifecycleScope();
  const el = document.createElement("div");
  el.className = "html-note-editor";
  el.style.cssText = "display:flex;flex-direction:column;height:100%;background:#0b1622;color:#e5e9f0;font-family:system-ui,sans-serif;";

  let content = "";
  let runtimeState: Record<string, unknown> = {};
  let sourceMode = false;
  let contentScope: LifecycleScope | null = null;

  const persistRuntimeState = async (): Promise<void> => {
    if (lifecycle.isDisposed) return;
    try {
      await invoke(FrontendCommand.NoteSaveState, {
        bayId,
        id: noteId,
        stateJson: JSON.stringify(runtimeState),
      });
    } catch (e) {
      if (!lifecycle.isDisposed) console.error("State persist failed:", e);
    }
  };

  const buildSrcDoc = (): string => {
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
    return `<!DOCTYPE html><html><head><meta charset="utf-8"><base target="_blank">${stateScript}</head><body>${content}${stateCapture}</body></html>`;
  };

  const buildContent = (): HTMLElement => {
    void contentScope?.dispose();
    contentScope = new LifecycleScope();
    const scope = contentScope;
    const container = document.createElement("div");
    container.className = "html-note-container";
    container.style.cssText = "flex:1;overflow:hidden;position:relative;";

    if (sourceMode) {
      const textarea = document.createElement("textarea");
      textarea.value = content;
      textarea.style.cssText = "width:100%;height:100%;padding:12px;background:#0b1622;border:none;color:#e5e9f0;font-family:'SF Mono',Monaco,monospace;font-size:13px;resize:none;outline:none;";
      scope.listen(textarea, "input", () => { content = textarea.value; });
      container.appendChild(textarea);
    } else {
      const iframe = document.createElement("iframe");
      iframe.sandbox.add("allow-scripts");
      iframe.sandbox.remove("allow-same-origin");
      iframe.style.cssText = "width:100%;height:100%;border:none;background:#fff;";
      iframe.srcdoc = buildSrcDoc();
      container.appendChild(iframe);
      scope.own(() => {
        iframe.removeAttribute("srcdoc");
        iframe.src = "about:blank";
      });
      scope.listen(window, "message", (event) => {
        const e = event as MessageEvent;
        if (e.source !== iframe.contentWindow) return;
        const data = e.data;
        if (!data || typeof data !== "object") return;
        if (data.type === "cove:state-snapshot" && data.state) {
          runtimeState = data.state as Record<string, unknown>;
          void persistRuntimeState();
        }
      });
    }
    return container;
  };

  const rerender = (): void => {
    if (lifecycle.isDisposed) return;
    const container = el.querySelector<HTMLElement>(".html-note-container");
    if (container) container.replaceWith(buildContent());
  };

  const saveHtmlNote = async (): Promise<void> => {
    if (lifecycle.isDisposed) return;
    try {
      await invoke(FrontendCommand.NoteWrite, { bayId, id: noteId, content });
      await persistRuntimeState();
    } catch (e) {
      if (!lifecycle.isDisposed) console.error("Save failed:", e);
    }
  };

  const buildToolbar = (): HTMLElement => {
    const toolbar = document.createElement("div");
    toolbar.style.cssText = "padding:8px 12px;display:flex;gap:8px;align-items:center;border-bottom:1px solid #1e2d3f;flex-wrap:wrap;";
    const save = document.createElement("button");
    save.textContent = "Save";
    save.style.cssText = "padding:4px 12px;background:#2563eb;border:1px solid #3b82f6;border-radius:4px;color:#fff;cursor:pointer;font-size:12px;";
    lifecycle.listen(save, "click", () => { void saveHtmlNote(); });
    toolbar.appendChild(save);
    const sourceToggle = document.createElement("button");
    sourceToggle.textContent = sourceMode ? "Preview" : "Source";
    sourceToggle.style.cssText = "padding:4px 12px;background:#1e2d3f;border:1px solid #2b3d52;border-radius:4px;color:#e5e9f0;cursor:pointer;font-size:12px;";
    lifecycle.listen(sourceToggle, "click", () => {
      sourceMode = !sourceMode;
      sourceToggle.textContent = sourceMode ? "Preview" : "Source";
      rerender();
    });
    toolbar.appendChild(sourceToggle);
    return toolbar;
  };

  try {
    const result = await invoke<NoteReadResult>(FrontendCommand.NoteRead, { bayId, id: noteId });
    if (!lifecycle.isDisposed) {
      content = result.content;
      const stateJson = await invoke<{ state: string | null }>(FrontendCommand.NoteGetState, { bayId, id: noteId }).catch(() => ({ state: null }));
      if (stateJson.state) {
        try { runtimeState = JSON.parse(stateJson.state); } catch { runtimeState = {}; }
      }
      el.appendChild(buildToolbar());
      el.appendChild(buildContent());
    }
  } catch (e) {
    if (!lifecycle.isDisposed) el.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load HTML note: ${(e as Error).message}</div>`;
  }

  lifecycle.own(() => contentScope?.dispose());
  lifecycle.own(() => el.remove());
  return { element: el, dispose: () => lifecycle.dispose() };
}
