import type * as Monaco from "monaco-editor";
import { invoke } from "./invoke";
import { MonacoLoader } from "./monaco-loader";
import { MarkdownViewMode } from "./markdown-view-mode";

interface MarkdownState {
  filePath: string;
  viewMode: string;
  scroll: number;
}

export async function renderMarkdownPane(paneId: string, filePath: string): Promise<HTMLElement> {
  const el = document.createElement("div");
  el.className = "markdown-pane";
  el.style.cssText = "display:flex;flex-direction:column;height:100%;background:#1e1e1e;color:#d4d4d4;";

  const header = document.createElement("div");
  header.style.cssText = "padding:6px 12px;border-bottom:1px solid #303030;display:flex;gap:8px;align-items:center;flex-shrink:0;";
  const titleEl = document.createElement("span");
  titleEl.style.cssText = "font-size:13px;font-weight:600;";
  titleEl.textContent = filePath.split("/").pop() || filePath;
  header.appendChild(titleEl);
  el.appendChild(header);

  const toolbar = document.createElement("div");
  toolbar.style.cssText = "padding:4px 12px;border-bottom:1px solid #303030;display:flex;gap:8px;align-items:center;flex-shrink:0;";
  const viewToggle = document.createElement("button");
  viewToggle.textContent = "Source";
  viewToggle.style.cssText = "padding:4px 10px;border:1px solid #303030;border-radius:4px;background:transparent;color:#d4d4d4;cursor:pointer;font-size:12px;";
  toolbar.appendChild(viewToggle);
  el.appendChild(toolbar);

  const rteContainer = document.createElement("div");
  rteContainer.style.cssText = "flex:1;min-height:0;overflow:auto;padding:12px 24px;font-family:ui-sans-serif,system-ui,sans-serif;font-size:14px;line-height:1.6;";
  rteContainer.contentEditable = "true";
  rteContainer.style.outline = "none";
  el.appendChild(rteContainer);

  const sourceContainer = document.createElement("div");
  sourceContainer.style.cssText = "flex:1;min-height:0;position:relative;display:none;";
  el.appendChild(sourceContainer);

  const statusBar = document.createElement("div");
  statusBar.style.cssText = "padding:4px 12px;border-top:1px solid #303030;display:flex;gap:12px;font-size:11px;color:#858585;flex-shrink:0;";
  const saveStatus = document.createElement("span");
  saveStatus.textContent = "Saved";
  statusBar.appendChild(saveStatus);
  const modeLabel = document.createElement("span");
  modeLabel.textContent = MarkdownViewMode.Rte;
  statusBar.appendChild(modeLabel);
  el.appendChild(statusBar);

  let content = "";
  let dirty = false;
  let saveTimer: ReturnType<typeof setTimeout> | undefined;
  let viewMode: string = MarkdownViewMode.Rte;
  let monaco: typeof Monaco | null = null;
  let sourceEditor: Monaco.editor.IStandaloneCodeEditor | null = null;
  let sourceModel: Monaco.editor.ITextModel | null = null;

  try {
    const result = await invoke<{ content: string }>("cove://commands/editor.open", { filePath, paneId });
    content = result.content ?? "";
  } catch (e) {
    content = `Failed to open: ${(e as Error).message}`;
  }

  rteContainer.innerHTML = renderMarkdownPreview(content);

  const doSave = async () => {
    const saveContent = viewMode === MarkdownViewMode.Source && sourceModel ? sourceModel.getValue() : extractText(rteContainer);
    try {
      await invoke("cove://commands/editor.save", { filePath, paneId, content: saveContent });
      dirty = false;
      saveStatus.textContent = "Saved";
      saveStatus.style.color = "#858585";
    } catch (e) {
      saveStatus.textContent = `Save failed: ${(e as Error).message}`;
      saveStatus.style.color = "#f85149";
    }
  };

  const scheduleSave = () => {
    dirty = true;
    saveStatus.textContent = "Modified";
    saveStatus.style.color = "#cca766";
    clearTimeout(saveTimer);
    saveTimer = setTimeout(() => { void doSave(); }, 2000);
  };

  rteContainer.addEventListener("input", scheduleSave);

  const switchToSource = async () => {
    if (!monaco) {
      monaco = await MonacoLoader.load();
    }
    if (!sourceModel) {
      sourceModel = monaco.editor.createModel(extractText(rteContainer), "markdown");
    } else {
      sourceModel.setValue(extractText(rteContainer));
    }
    if (!sourceEditor) {
      sourceEditor = monaco.editor.create(sourceContainer, {
        model: sourceModel,
        theme: "vs-dark",
        fontSize: 13,
        language: "markdown",
        wordWrap: "on",
        automaticLayout: true,
      });
      sourceModel.onDidChangeContent(scheduleSave);
    }
    rteContainer.style.display = "none";
    sourceContainer.style.display = "block";
    viewMode = MarkdownViewMode.Source;
    modeLabel.textContent = viewMode;
    viewToggle.textContent = "Preview";
  };

  const switchToRte = () => {
    if (sourceModel) {
      rteContainer.innerHTML = renderMarkdownPreview(sourceModel.getValue());
    }
    sourceContainer.style.display = "none";
    rteContainer.style.display = "block";
    viewMode = MarkdownViewMode.Rte;
    modeLabel.textContent = viewMode;
    viewToggle.textContent = "Source";
  };

  viewToggle.addEventListener("click", () => {
    if (viewMode === MarkdownViewMode.Rte) {
      void switchToSource();
    } else {
      switchToRte();
    }
  });

  try {
    const state = await invoke<MarkdownState | null>("cove://commands/editor.get-state", { paneId });
    if (state?.viewMode === MarkdownViewMode.Source) {
      void switchToSource();
    }
    if (state?.scroll) {
      rteContainer.scrollTop = state.scroll;
    }
  } catch { void 0; }

  return el;
}

function renderMarkdownPreview(md: string): string {
  return md
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/^### (.+)$/gm, "<h3>$1</h3>")
    .replace(/^## (.+)$/gm, "<h2>$1</h2>")
    .replace(/^# (.+)$/gm, "<h1>$1</h1>")
    .replace(/\*\*(.+?)\*\*/g, "<strong>$1</strong>")
    .replace(/\*(.+?)\*/g, "<em>$1</em>")
    .replace(/`(.+?)`/g, "<code>$1</code>")
    .replace(/\n/g, "<br>");
}

function extractText(container: HTMLElement): string {
  return container.innerText;
}
