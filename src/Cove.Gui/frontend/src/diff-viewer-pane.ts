import type * as Monaco from "monaco-editor";
import { invoke } from "./invoke";
import { MonacoLoader, detectLanguage } from "./monaco-loader";
import { DiffViewMode, parseRefSpec } from "./diff-view-model";


interface DiffContent {
  originalContent: string;
  modifiedContent: string;
}

export async function renderDiffViewerPane(paneId: string, filePath: string, refInput: string): Promise<HTMLElement> {
  const el = document.createElement("div");
  el.className = "diff-viewer-pane";
  el.style.cssText = "display:flex;flex-direction:column;height:100%;background:#1e1e1e;color:#d4d4d4;";

  const header = document.createElement("div");
  header.style.cssText = "padding:6px 12px;border-bottom:1px solid #303030;display:flex;gap:8px;align-items:center;flex-shrink:0;";
  const titleEl = document.createElement("span");
  titleEl.style.cssText = "font-size:13px;font-weight:600;";
  titleEl.textContent = `Diff: ${filePath.split("/").pop()}`;
  header.appendChild(titleEl);
  const refLabel = document.createElement("span");
  refLabel.style.cssText = "font-size:11px;color:#858585;";
  const refSpec = parseRefSpec(refInput);
  refLabel.textContent = refSpec.isWorkingTree ? `${refSpec.ref} → working copy` : `${refSpec.ref} → working copy`;
  header.appendChild(refLabel);
  el.appendChild(header);

  const toolbar = document.createElement("div");
  toolbar.style.cssText = "padding:4px 12px;border-bottom:1px solid #303030;display:flex;gap:8px;align-items:center;flex-shrink:0;";
  const toggleBtn = document.createElement("button");
  toggleBtn.textContent = "Side-by-side";
  toggleBtn.style.cssText = "padding:4px 10px;border:1px solid #303030;border-radius:4px;background:transparent;color:#d4d4d4;cursor:pointer;font-size:12px;";
  toolbar.appendChild(toggleBtn);
  const findBtn = document.createElement("button");
  findBtn.textContent = "Find";
  findBtn.style.cssText = "padding:4px 10px;border:1px solid #303030;border-radius:4px;background:transparent;color:#d4d4d4;cursor:pointer;font-size:12px;";
  toolbar.appendChild(findBtn);
  el.appendChild(toolbar);

  const container = document.createElement("div");
  container.style.cssText = "flex:1;min-height:0;position:relative;";
  el.appendChild(container);

  const statusBar = document.createElement("div");
  statusBar.style.cssText = "padding:4px 12px;border-top:1px solid #303030;display:flex;gap:12px;font-size:11px;color:#858585;flex-shrink:0;";
  const changeCount = document.createElement("span");
  changeCount.textContent = "Computing diff\u2026";
  statusBar.appendChild(changeCount);
  el.appendChild(statusBar);

  const monaco = await MonacoLoader.load();
  const language = detectLanguage(filePath);

  let diff: DiffContent = { originalContent: "", modifiedContent: "" };
  try {
    const result = await invoke<DiffContent>("cove://commands/scm.diff", { filePath, ref: refSpec.ref, paneId });
    diff = result;
  } catch (e) {
    diff = { originalContent: "", modifiedContent: `Failed to load: ${(e as Error).message}` };
  }

  const originalModel = monaco.editor.createModel(diff.originalContent, language);
  const modifiedModel = monaco.editor.createModel(diff.modifiedContent, language);

  let mode: string = DiffViewMode.SideBySide;
  let diffEditor = monaco.editor.createDiffEditor(container, {
    originalEditable: false,
    renderSideBySide: true,
    theme: "vs-dark",
    fontSize: 13,
    automaticLayout: true,
  });
  diffEditor.setModel({ original: originalModel, modified: modifiedModel });

  const updateChangeCount = () => {
    const changes = diffEditor.getLineChanges();
    const added = changes ? changes.reduce((sum, c) => sum + (c.originalEndLineNumber === 0 ? 1 : 0), 0) : 0;
    const removed = changes ? changes.reduce((sum, c) => sum + (c.modifiedEndLineNumber === 0 ? 1 : 0), 0) : 0;
    changeCount.textContent = `+${added} −${removed}`;
  };

  diffEditor.onDidUpdateDiff(() => { updateChangeCount(); });

  toggleBtn.addEventListener("click", () => {
    mode = DiffViewMode.toggle(mode);
    toggleBtn.textContent = mode === DiffViewMode.SideBySide ? "Side-by-side" : "Unified";
    diffEditor.dispose();
    diffEditor = monaco.editor.createDiffEditor(container, {
      originalEditable: false,
      renderSideBySide: mode === DiffViewMode.SideBySide,
      theme: "vs-dark",
      fontSize: 13,
      automaticLayout: true,
    });
    diffEditor.setModel({ original: originalModel, modified: modifiedModel });
  });

  findBtn.addEventListener("click", () => {
    const modifiedEditor = diffEditor.getModifiedEditor();
    modifiedEditor.getAction("actions.find")?.run();
  });

  return el;
}
