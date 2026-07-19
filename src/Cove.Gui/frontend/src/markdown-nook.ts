import type * as Monaco from "monaco-editor";
import { invoke } from "./invoke";
import { FrontendCommand } from "./app/frontend-command";
import { MonacoLoader } from "./monaco-loader";
import { MarkdownViewMode } from "./markdown-view-mode";
import { parseComments, insertComment, resolveComment, deleteComment, type CommentEntry } from "./markdown-comments";
import { buildImageMarkdown, insertAt, pastedImageFileName } from "./image-paste";
import { LifecycleScope, type NookContentHandle } from "./app/lifecycle";

interface MarkdownState {
  filePath: string;
  viewMode: string;
  scroll: number;
}

export interface MarkdownSettings {
  defaultFont: string;
  fontSize: number;
  textAlign: string;
  bookView: boolean;
  bookViewWidth: string;
  bookViewMargin: string;
  defaultViewMode: string;
}

const DEFAULT_MARKDOWN_SETTINGS: MarkdownSettings = {
  defaultFont: "",
  fontSize: 14,
  textAlign: "left",
  bookView: false,
  bookViewWidth: "720px",
  bookViewMargin: "auto",
  defaultViewMode: MarkdownViewMode.Rte,
};

export function resolveMarkdownSettings(config: Record<string, string>): MarkdownSettings {
  const num = (v: string | undefined, lo: number, hi: number, dflt: number): number => {
    const n = Number(v);
    if (!Number.isFinite(n) || n < lo) return dflt;
    return Math.trunc(Math.min(hi, n));
  };
  const bool = (v: string | undefined): boolean => (v ?? "").trim().toLowerCase() === "true";
  const text = (v: string | undefined, dflt: string): string => {
    const t = (v ?? "").trim();
    return t.length > 0 ? t : dflt;
  };
  const mode = (v: string | undefined): string =>
    (v ?? "").trim().toLowerCase() === MarkdownViewMode.Source ? MarkdownViewMode.Source : MarkdownViewMode.Rte;
  return {
    defaultFont: text(config["markdown_editor.defaultFont"], DEFAULT_MARKDOWN_SETTINGS.defaultFont),
    fontSize: num(config["markdown_editor.fontSize"], 1, 28, DEFAULT_MARKDOWN_SETTINGS.fontSize),
    textAlign: text(config["markdown_editor.textAlign"], DEFAULT_MARKDOWN_SETTINGS.textAlign),
    bookView: bool(config["markdown_editor.bookView"]),
    bookViewWidth: text(config["markdown_editor.bookViewWidth"], DEFAULT_MARKDOWN_SETTINGS.bookViewWidth),
    bookViewMargin: text(config["markdown_editor.bookViewMargin"], DEFAULT_MARKDOWN_SETTINGS.bookViewMargin),
    defaultViewMode: mode(config["markdown_editor.defaultViewMode"]),
  };
}

export function markdownEditorCss(s: MarkdownSettings): string {
  const fontFamily = s.defaultFont ? s.defaultFont : "ui-sans-serif,system-ui,sans-serif";
  const parts: string[] = [
    `font-family:${fontFamily}`,
    `font-size:${s.fontSize}px`,
    `line-height:1.6`,
    `text-align:${s.textAlign}`,
  ];
  if (s.bookView) {
    parts.push(`max-width:${s.bookViewWidth}`);
    parts.push(`margin:${s.bookViewMargin} auto`);
  }
  return parts.join(";");
}

export function resolveInitialViewMode(value: string | null): string {
  return (value ?? "").trim().toLowerCase() === MarkdownViewMode.Source ? MarkdownViewMode.Source : MarkdownViewMode.Rte;
}


interface MarkdownNookHandle {
  reapply: (settings: MarkdownSettings) => void;
}

const markdownNookRegistry = new Map<string, MarkdownNookHandle>();

export async function applyMarkdownSettings(settings: MarkdownSettings): Promise<void> {
  for (const handle of markdownNookRegistry.values()) handle.reapply(settings);
}
export async function renderMarkdownNook(nookId: string, filePath: string): Promise<NookContentHandle> {
  const lifecycle = new LifecycleScope();
  const el = document.createElement("div");
  el.className = "markdown-nook";
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
  const viewToggle = button("Source");
  toolbar.appendChild(viewToggle);
  const commentBtn = button("Comment");
  toolbar.appendChild(commentBtn);
  const commentsToggle = button("Comments");
  toolbar.appendChild(commentsToggle);
  el.appendChild(toolbar);

  const bodyRow = document.createElement("div");
  bodyRow.style.cssText = "flex:1;min-height:0;display:flex;";
  el.appendChild(bodyRow);

  const editArea = document.createElement("div");
  editArea.style.cssText = "flex:1;min-width:0;min-height:0;display:flex;flex-direction:column;position:relative;";
  bodyRow.appendChild(editArea);

  const rteContainer = document.createElement("div");
  rteContainer.style.cssText = "flex:1;min-height:0;overflow:auto;padding:12px 24px;font-family:ui-sans-serif,system-ui,sans-serif;font-size:14px;line-height:1.6;";
  rteContainer.contentEditable = "true";
  rteContainer.style.outline = "none";
  editArea.appendChild(rteContainer);

  const sourceContainer = document.createElement("div");
  sourceContainer.style.cssText = "flex:1;min-height:0;position:relative;display:none;";
  editArea.appendChild(sourceContainer);

  const commentsPanel = document.createElement("div");
  commentsPanel.style.cssText = "width:250px;flex-shrink:0;border-left:1px solid #303030;overflow:auto;display:none;padding:8px;font-size:12px;";
  bodyRow.appendChild(commentsPanel);

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
  let sourceModelSubscription: Monaco.IDisposable | null = null;

  let mdSettings: MarkdownSettings = { ...DEFAULT_MARKDOWN_SETTINGS };
  try {
    const keys = [
      "markdown_editor.defaultFont", "markdown_editor.fontSize", "markdown_editor.textAlign",
      "markdown_editor.bookView", "markdown_editor.bookViewWidth", "markdown_editor.bookViewMargin",
      "markdown_editor.defaultViewMode",
    ];
    const raw: Record<string, string> = {};
    for (const k of keys) {
      try {
        const r = await invoke<{ value: string } | null>(FrontendCommand.ConfigGet, { key: k });
        if (r?.value) raw[k] = r.value;
      } catch { void 0; }
    }
    mdSettings = resolveMarkdownSettings(raw);
  } catch { void 0; }

  const applyEditorStyle = () => {
    rteContainer.style.cssText = `flex:1;min-height:0;overflow:auto;padding:12px 24px;${markdownEditorCss(mdSettings)}`;
    rteContainer.style.outline = "none";
    if (sourceEditor) sourceEditor.updateOptions({ fontFamily: mdSettings.defaultFont || "ui-monospace, monospace", fontSize: mdSettings.fontSize });
  };
  applyEditorStyle();
  const registryHandle: MarkdownNookHandle = {
    reapply: (next: MarkdownSettings) => { mdSettings = next; applyEditorStyle(); },
  };
  markdownNookRegistry.set(nookId, registryHandle);

  try {
    const result = await invoke<{ content: string }>(FrontendCommand.EditorOpen, { filePath, nookId });
    content = result.content ?? "";
  } catch (e) {
    content = `Failed to open: ${(e as Error).message}`;
  }

  rteContainer.innerHTML = renderMarkdownPreview(content);

  const canonicalMarkdown = (): string =>
    viewMode === MarkdownViewMode.Source && sourceModel ? sourceModel.getValue() : content;

  const doSave = async () => {
    if (lifecycle.isDisposed) return;
    const saveContent = canonicalMarkdown();
    content = saveContent;
    try {
      await invoke(FrontendCommand.EditorSave, { filePath, nookId, content: saveContent });
      if (lifecycle.isDisposed) return;
      dirty = false;
      saveStatus.textContent = "Saved";
      saveStatus.style.color = "#858585";
    } catch (e) {
      if (lifecycle.isDisposed) return;
      saveStatus.textContent = `Save failed: ${(e as Error).message}`;
      saveStatus.style.color = "#f85149";
    }
  };

  const scheduleSave = () => {
    if (lifecycle.isDisposed) return;
    dirty = true;
    saveStatus.textContent = "Modified";
    saveStatus.style.color = "#cca766";
    clearTimeout(saveTimer);
    saveTimer = setTimeout(() => { void doSave(); }, 2000);
  };

  rteContainer.addEventListener("input", () => { content = extractText(rteContainer); scheduleSave(); refreshComments(); });

  const ensureSourceModel = async () => {
    if (!monaco) monaco = await MonacoLoader.load();
    if (lifecycle.isDisposed) return;
    if (!sourceModel) {
      sourceModel = monaco.editor.createModel(content, "markdown");
      sourceModelSubscription = sourceModel.onDidChangeContent(() => {
        if (lifecycle.isDisposed) return;
        content = sourceModel!.getValue();
        scheduleSave();
        refreshComments();
      });
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
    }
  };

  const switchToSource = async () => {
    if (lifecycle.isDisposed) return;
    const text = viewMode === MarkdownViewMode.Source ? content : extractText(rteContainer);
    content = text;
    await ensureSourceModel();
    if (lifecycle.isDisposed || !sourceModel) return;
    if (sourceModel!.getValue() !== content) sourceModel!.setValue(content);
    rteContainer.style.display = "none";
    sourceContainer.style.display = "block";
    viewMode = MarkdownViewMode.Source;
    modeLabel.textContent = viewMode;
    viewToggle.textContent = "Preview";
  };

  const switchToRte = () => {
    if (sourceModel) content = sourceModel.getValue();
    rteContainer.innerHTML = renderMarkdownPreview(content);
    sourceContainer.style.display = "none";
    rteContainer.style.display = "block";
    viewMode = MarkdownViewMode.Rte;
    modeLabel.textContent = viewMode;
    viewToggle.textContent = "Source";
  };

  viewToggle.addEventListener("click", () => {
    if (viewMode === MarkdownViewMode.Rte) void switchToSource();
    else switchToRte();
  });

  const applyMarkdown = async (next: string) => {
    if (lifecycle.isDisposed) return;
    content = next;
    await ensureSourceModel();
    if (lifecycle.isDisposed || !sourceModel) return;
    sourceModel!.setValue(next);
    if (viewMode !== MarkdownViewMode.Source) await switchToSource();
    else { sourceContainer.style.display = "block"; rteContainer.style.display = "none"; }
    scheduleSave();
    refreshComments();
  };

  function refreshComments() {
    const comments = parseComments(canonicalMarkdown());
    commentsPanel.innerHTML = "";
    if (comments.length === 0) {
      const empty = document.createElement("div");
      empty.style.cssText = "color:#585858;padding:8px;";
      empty.textContent = "No comments";
      commentsPanel.appendChild(empty);
      return;
    }
    for (const c of comments) commentsPanel.appendChild(commentCard(c));
  }

  function commentCard(c: CommentEntry): HTMLElement {
    const card = document.createElement("div");
    const resolved = c.state === "resolved";
    card.style.cssText = `margin-bottom:8px;padding:6px 8px;border:1px solid #303030;border-radius:4px;background:${resolved ? "#161b22" : "#1f2937"};opacity:${resolved ? "0.6" : "1"};`;
    const meta = document.createElement("div");
    meta.style.cssText = "color:#8b949e;font-size:10px;margin-bottom:2px;";
    meta.textContent = `${c.author}${resolved ? " · resolved" : ""}`;
    card.appendChild(meta);
    const anchor = document.createElement("div");
    anchor.style.cssText = "color:#cca766;font-size:11px;margin-bottom:4px;";
    anchor.textContent = c.anchorText;
    card.appendChild(anchor);
    const note = document.createElement("div");
    note.style.cssText = "color:#d4d4d4;margin-bottom:6px;";
    note.textContent = c.note;
    card.appendChild(note);
    const actions = document.createElement("div");
    actions.style.cssText = "display:flex;gap:6px;";
    if (!resolved) {
      const resolveB = button("Resolve");
      resolveB.addEventListener("click", () => { void applyMarkdown(resolveComment(canonicalMarkdown(), c.id)); });
      actions.appendChild(resolveB);
    }
    const deleteB = button("Delete");
    deleteB.addEventListener("click", () => { void applyMarkdown(deleteComment(canonicalMarkdown(), c.id)); });
    actions.appendChild(deleteB);
    card.appendChild(actions);
    return card;
  }

  commentsToggle.addEventListener("click", () => {
    const showing = commentsPanel.style.display !== "none";
    commentsPanel.style.display = showing ? "none" : "block";
    if (!showing) refreshComments();
  });

  commentBtn.addEventListener("click", async () => {
    await switchToSource();
    if (!sourceEditor || !sourceModel) return;
    const selection = sourceEditor.getSelection();
    if (!selection || selection.isEmpty()) {
      saveStatus.textContent = "Select text in source to comment";
      saveStatus.style.color = "#cca766";
      return;
    }
    const start = sourceModel.getOffsetAt(selection.getStartPosition());
    const end = sourceModel.getOffsetAt(selection.getEndPosition());
    const note = typeof window.prompt === "function" ? (window.prompt("Comment:") ?? "") : "";
    const id = `c${Date.now().toString(36)}`;
    const next = insertComment(sourceModel.getValue(), start, end, {
      id, author: "you", ts: new Date().toISOString(), note,
    });
    commentsPanel.style.display = "block";
    await applyMarkdown(next);
  });

  el.addEventListener("paste", (ev) => { void handlePaste(ev); });

  async function handlePaste(ev: ClipboardEvent) {
    if (lifecycle.isDisposed) return;
    const items = ev.clipboardData?.items;
    if (!items) return;
    let file: File | null = null;
    for (const it of Array.from(items)) {
      if (it.kind === "file" && it.type.startsWith("image/")) { file = it.getAsFile(); break; }
    }
    if (!file) return;
    ev.preventDefault();
    const base64 = await fileToBase64(file);
    if (lifecycle.isDisposed) return;
    if (!base64) {
      saveStatus.textContent = "Paste failed: could not read image";
      saveStatus.style.color = "#f85149";
      return;
    }
    try {
      const fileName = pastedImageFileName(file.type, Date.now());
      const res = await invoke<{ mediaPath: string }>(FrontendCommand.NoteMediaSave, {
        bayId: "default", id: nookId, fileName, base64Data: base64,
      });
      if (lifecycle.isDisposed) return;
      const link = buildImageMarkdown(res.mediaPath);
      await switchToSource();
      if (sourceEditor && sourceModel) {
        const pos = sourceEditor.getPosition();
        const offset = pos ? sourceModel.getOffsetAt(pos) : sourceModel.getValue().length;
        await applyMarkdown(insertAt(sourceModel.getValue(), offset, link));
      } else {
        await applyMarkdown(canonicalMarkdown() + "\n" + link + "\n");
      }
    } catch (e) {
      saveStatus.textContent = `Image save failed: ${(e as Error).message}`;
      saveStatus.style.color = "#f85149";
    }
  }

  try {
    const state = await invoke<MarkdownState | null>(FrontendCommand.EditorGetState, { nookId });
    if (state?.viewMode === MarkdownViewMode.Source) void switchToSource();
    else if (!state?.viewMode && mdSettings.defaultViewMode === MarkdownViewMode.Source) void switchToSource();
    if (state?.scroll) rteContainer.scrollTop = state.scroll;
  } catch { void 0; }

  lifecycle.own(() => {
    clearTimeout(saveTimer);
    sourceModelSubscription?.dispose();
    sourceEditor?.dispose();
    sourceModel?.dispose();
    if (markdownNookRegistry.get(nookId) === registryHandle) markdownNookRegistry.delete(nookId);
    el.remove();
  });
  return { element: el, dispose: () => lifecycle.dispose() };
}

function button(label: string): HTMLButtonElement {
  const b = document.createElement("button");
  b.textContent = label;
  b.style.cssText = "padding:4px 10px;border:1px solid #303030;border-radius:4px;background:transparent;color:#d4d4d4;cursor:pointer;font-size:12px;";
  return b;
}

function fileToBase64(file: File): Promise<string | null> {
  return new Promise((resolve) => {
    const reader = new FileReader();
    reader.onload = () => {
      const result = reader.result as string;
      const comma = result.indexOf(",");
      resolve(comma >= 0 ? result.slice(comma + 1) : null);
    };
    reader.onerror = () => resolve(null);
    reader.readAsDataURL(file);
  });
}

function renderMarkdownPreview(md: string): string {
  return md
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/:comment\[([^\]]*)\]\{([^}]*)\}/g, (_all, anchor: string, attrs: string) => {
      const noteMatch = attrs.match(/note="([^"]*)"/);
      const note = noteMatch ? noteMatch[1] : "";
      const resolved = /state=resolved/.test(attrs);
      const bg = resolved ? "#2d333b" : "#5a4a1f";
      return `<mark style="background:${bg};border-radius:2px;padding:0 2px;" title="${note}">${anchor}</mark>`;
    })
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
