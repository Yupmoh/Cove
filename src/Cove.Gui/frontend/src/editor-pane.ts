import type * as Monaco from "monaco-editor";
import { invoke } from "./invoke";
import { MonacoLoader, detectLanguage } from "./monaco-loader";

interface EditorState {
  filePath: string;
  cursor: string | null;
  scroll: string | null;
  fold: string | null;
  undo: string | null;
  readOnly: boolean;
}

const LARGE_FILE_THRESHOLD = 5 * 1024 * 1024;

export async function renderEditorPane(paneId: string, filePath: string): Promise<HTMLElement> {
  const el = document.createElement("div");
  el.className = "editor-pane";
  el.style.cssText = "display:flex;flex-direction:column;height:100%;background:#1e1e1e;color:#d4d4d4;font-family:ui-monospace,monospace;";

  const header = document.createElement("div");
  header.style.cssText = "padding:6px 12px;border-bottom:1px solid #303030;display:flex;gap:8px;align-items:center;flex-shrink:0;";
  const titleEl = document.createElement("span");
  titleEl.style.cssText = "font-size:13px;font-weight:600;color:#d4d4d4;";
  titleEl.textContent = filePath.split("/").pop() || filePath;
  header.appendChild(titleEl);
  const pathEl = document.createElement("span");
  pathEl.style.cssText = "font-size:11px;color:#858585;";
  pathEl.textContent = filePath;
  header.appendChild(pathEl);
  el.appendChild(header);

  const readOnlyBanner = document.createElement("div");
  readOnlyBanner.style.cssText = "padding:4px 12px;background:#3a1a1a;color:#f85149;font-size:11px;display:none;";
  readOnlyBanner.textContent = "Read-only file";
  el.appendChild(readOnlyBanner);

  const largeFileBanner = document.createElement("div");
  largeFileBanner.style.cssText = "padding:4px 12px;background:#3a2a1a;color:#cca766;font-size:11px;display:none;";
  largeFileBanner.textContent = "Large file: syntax highlighting may be limited";
  el.appendChild(largeFileBanner);

  const container = document.createElement("div");
  container.style.cssText = "flex:1;min-height:0;position:relative;";
  el.appendChild(container);

  const statusBar = document.createElement("div");
  statusBar.style.cssText = "padding:4px 12px;border-top:1px solid #303030;display:flex;gap:12px;font-size:11px;color:#858585;flex-shrink:0;";
  const cursorPos = document.createElement("span");
  cursorPos.textContent = "Ln 1, Col 1";
  statusBar.appendChild(cursorPos);
  const langLabel = document.createElement("span");
  langLabel.textContent = detectLanguage(filePath);
  statusBar.appendChild(langLabel);
  const saveStatus = document.createElement("span");
  saveStatus.textContent = "Saved";
  statusBar.appendChild(saveStatus);
  el.appendChild(statusBar);

  const monaco = await MonacoLoader.load();
  const language = detectLanguage(filePath);

  let content = "";
  let readOnly = false;
  let largeFile = false;

  try {
    const result = await invoke<{ content: string; size: number }>("cove://commands/editor.open", { filePath, paneId });
    content = result.content ?? "";
    readOnly = (result.size ?? 0) > LARGE_FILE_THRESHOLD;
    largeFile = readOnly;
  } catch (e) {
    content = "";
    readOnlyBanner.style.display = "block";
    readOnlyBanner.textContent = `Failed to open: ${(e as Error).message}`;
  }

  const model = monaco.editor.createModel(content, readOnly ? "plaintext" : language);
  const editor = monaco.editor.create(container, {
    model,
    theme: "vs-dark",
    fontSize: 13,
    lineHeight: 1.5 * 13,
    minimap: { enabled: !largeFile },
    wordWrap: largeFile ? "on" : "off",
    readOnly,
    automaticLayout: true,
    scrollBeyondLastLine: false,
  });

  if (largeFile) largeFileBanner.style.display = "block";
  if (readOnly) readOnlyBanner.style.display = "block";

  let dirty = false;
  let saveTimer: ReturnType<typeof setTimeout> | undefined;

  model.onDidChangeContent(() => {
    dirty = true;
    saveStatus.textContent = "Modified";
    saveStatus.style.color = "#cca766";
    clearTimeout(saveTimer);
    saveTimer = setTimeout(() => { void doSave(); }, 2000);
  });

  editor.onDidChangeCursorPosition((e: Monaco.editor.ICursorPositionChangedEvent) => {
    cursorPos.textContent = `Ln ${e.position.lineNumber}, Col ${e.position.column}`;
  });

  const doSave = async () => {
    if (readOnly) return;
    try {
      await invoke("cove://commands/editor.save", { filePath, paneId, content: model.getValue() });
      dirty = false;
      saveStatus.textContent = "Saved";
      saveStatus.style.color = "#858585";
    } catch (e) {
      saveStatus.textContent = `Save failed: ${(e as Error).message}`;
      saveStatus.style.color = "#f85149";
    }
  };

  editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyS, () => { void doSave(); });

  try {
    const state = await invoke<EditorState | null>("cove://commands/editor.get-state", { paneId });
    if (state) {
      if (state.cursor) {
        try {
          const sel = JSON.parse(state.cursor) as { startLineNumber: number; startColumn: number; endLineNumber: number; endColumn: number };
          editor.setSelection(new monaco.Selection(sel.startLineNumber, sel.startColumn, sel.endLineNumber, sel.endColumn));
        } catch { void 0; }
      }
      if (state.scroll) {
        try { editor.setScrollTop(JSON.parse(state.scroll) as number); } catch { void 0; }
      }
    }
  } catch { void 0; }

  const saveState = async () => {
    try {
      const cursor = JSON.stringify(editor.getSelection());
      const scroll = JSON.stringify(editor.getScrollTop());
      await invoke("cove://commands/editor.set-state", { paneId, cursor, scroll });
    } catch { void 0; }
  };

  const observer = new MutationObserver(() => {
    if (!document.body.contains(el)) {
      void saveState();
      observer.disconnect();
    }
  });
  observer.observe(document.body, { childList: true, subtree: true });

  return el;
}
