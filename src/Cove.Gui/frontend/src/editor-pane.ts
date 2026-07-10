import type * as Monaco from "monaco-editor";
import { invoke } from "./invoke";
import { MonacoLoader, detectLanguage, defineCoveMonacoTheme } from "./monaco-loader";
import {
  buildBreadcrumbs,
  toggleWordWrap,
  wordWrapStorageKey,
  minimapStorageKey,
  latestAgentEdit,
  formatAgentEditChip,
  type WordWrap,
  type AttributionEntryLike,
} from "./editor-chrome";
import {
  parseDiffDecorations,
  blameForLine,
  formatBlameHover,
  type BlameLineLike,
} from "./git-decorations";

interface ScmDiffResult {
  filePath: string;
  newContent: string | null;
}

interface ScmBlameResult {
  filePath: string;
  lines: { line: number; commit: string; author: string; relativeTime: string }[];
}

const GUTTER_STYLE_ID = "cove-editor-gutter-style";

function ensureGutterStyle(): void {
  if (document.getElementById(GUTTER_STYLE_ID)) return;
  const style = document.createElement("style");
  style.id = GUTTER_STYLE_ID;
  style.textContent =
    ".cove-gutter-added{border-left:3px solid #3fb950;margin-left:3px;}" +
    ".cove-gutter-modified{border-left:3px solid #d29922;margin-left:3px;}" +
    ".cove-gutter-deleted{border-left:3px solid #f85149;margin-left:3px;}";
  document.head.appendChild(style);
}

interface AttributionListResult {
  entries: AttributionEntryLike[];
}

function loadWordWrap(paneId: string, fallback: WordWrap): WordWrap {
  try {
    const v = localStorage.getItem(wordWrapStorageKey(paneId));
    return v === "on" || v === "off" ? v : fallback;
  } catch {
    return fallback;
  }
}

function storeWordWrap(paneId: string, value: WordWrap): void {
  try {
    localStorage.setItem(wordWrapStorageKey(paneId), value);
  } catch {
    void 0;
  }
}

function loadMinimap(paneId: string, fallback: boolean): boolean {
  try {
    const v = localStorage.getItem(minimapStorageKey(paneId));
    return v === null ? fallback : v === "on";
  } catch {
    return fallback;
  }
}

function storeMinimap(paneId: string, value: boolean): void {
  try {
    localStorage.setItem(minimapStorageKey(paneId), value ? "on" : "off");
  } catch {
    void 0;
  }
}

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
  el.style.cssText = "display:flex;flex-direction:column;height:100%;background:var(--panel);color:var(--fg);";

  const header = document.createElement("div");
  header.style.cssText = "padding:6px 12px;border-bottom:1px solid var(--border);display:flex;gap:8px;align-items:center;flex-shrink:0;";

  const breadcrumbs = document.createElement("div");
  breadcrumbs.className = "editor-breadcrumbs";
  breadcrumbs.style.cssText = "display:flex;gap:2px;align-items:center;flex:1;min-width:0;overflow:hidden;font-size:11px;";
  const segments = buildBreadcrumbs(filePath);
  segments.forEach((seg, i) => {
    if (i > 0) {
      const sep = document.createElement("span");
      sep.style.cssText = "color:var(--muted);";
      sep.textContent = "›";
      breadcrumbs.appendChild(sep);
    }
    const crumb = document.createElement("span");
    const isLast = i === segments.length - 1;
    crumb.style.cssText = `color:${isLast ? "var(--fg)" : "var(--muted)"};font-weight:${isLast ? "600" : "400"};cursor:pointer;white-space:nowrap;`;
    crumb.textContent = seg.label;
    crumb.title = seg.path;
    crumb.addEventListener("click", () => {
      invoke("cove://commands/sidebar.reveal", { path: seg.path }).catch(() => {});
    });
    breadcrumbs.appendChild(crumb);
  });
  header.appendChild(breadcrumbs);

  const agentChip = document.createElement("span");
  agentChip.className = "editor-agent-chip";
  agentChip.style.cssText = "display:none;font-size:10px;color:var(--accent);background:color-mix(in srgb, var(--accent) 14%, transparent);border-radius:4px;padding:2px 6px;white-space:nowrap;";
  header.appendChild(agentChip);

  const wrapBtn = document.createElement("button");
  wrapBtn.className = "editor-wordwrap-toggle";
  wrapBtn.style.cssText = "background:var(--panel-2);border:1px solid var(--border);color:var(--muted);border-radius:5px;padding:2px 6px;font-size:11px;cursor:pointer;flex-shrink:0;";
  header.appendChild(wrapBtn);

  const minimapBtn = document.createElement("button");
  minimapBtn.className = "editor-minimap-toggle";
  minimapBtn.style.cssText = "background:var(--panel-2);border:1px solid var(--border);color:var(--muted);border-radius:5px;padding:2px 6px;font-size:11px;cursor:pointer;flex-shrink:0;";
  header.appendChild(minimapBtn);

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
  statusBar.style.cssText = "padding:4px 12px;border-top:1px solid var(--border);display:flex;gap:12px;font-size:11px;color:var(--muted);flex-shrink:0;";
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
  let wordWrap: WordWrap = loadWordWrap(paneId, largeFile ? "on" : "off");
  let minimapOn = loadMinimap(paneId, !largeFile);
  const editor = monaco.editor.create(container, {
    model,
    theme: defineCoveMonacoTheme(monaco),
    fontSize: 13,
    lineHeight: 1.5 * 13,
    minimap: { enabled: minimapOn },
    wordWrap,
    readOnly,
    automaticLayout: true,
    scrollBeyondLastLine: false,
  });

  const renderWrapBtn = () => {
    wrapBtn.textContent = `Wrap: ${wordWrap === "on" ? "On" : "Off"}`;
    wrapBtn.style.color = wordWrap === "on" ? "var(--accent)" : "var(--muted)";
  };
  renderWrapBtn();
  wrapBtn.addEventListener("click", () => {
    wordWrap = toggleWordWrap(wordWrap);
    editor.updateOptions({ wordWrap });
    storeWordWrap(paneId, wordWrap);
    renderWrapBtn();
  });

  const renderMinimapBtn = () => {
    minimapBtn.textContent = `Minimap: ${minimapOn ? "On" : "Off"}`;
    minimapBtn.style.color = minimapOn ? "var(--accent)" : "var(--muted)";
  };
  renderMinimapBtn();
  minimapBtn.addEventListener("click", () => {
    minimapOn = !minimapOn;
    editor.updateOptions({ minimap: { enabled: minimapOn } });
    storeMinimap(paneId, minimapOn);
    renderMinimapBtn();
  });

  invoke<AttributionListResult>("cove://commands/attribution.find-by-range", { filePath, startLine: 1, endLine: 1000000 })
    .then((res) => {
      const chip = latestAgentEdit(res.entries ?? []);
      if (chip) {
        agentChip.textContent = formatAgentEditChip(chip);
        agentChip.title = `Last agent edit — session ${chip.sessionId}, lines ${chip.lineRange}`;
        agentChip.style.display = "";
      }
    })
    .catch(() => {});

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

  ensureGutterStyle();
  const slash = filePath.replace(/\\/g, "/").lastIndexOf("/");
  const repoDir = slash > 0 ? filePath.slice(0, slash) : filePath;
  const fileName = slash >= 0 ? filePath.slice(slash + 1) : filePath;
  const decorations = editor.createDecorationsCollection([]);
  let blameLines: BlameLineLike[] = [];

  const refreshDecorations = async () => {
    try {
      const diff = await invoke<ScmDiffResult>("cove://commands/scm.diff", { repoRoot: repoDir, filePath: fileName, ref: "HEAD" });
      const marks = parseDiffDecorations(diff.newContent ?? "");
      const lineCount = model.getLineCount();
      decorations.set(
        marks
          .filter((m) => m.line >= 1 && m.line <= lineCount)
          .map((m) => ({
            range: new monaco.Range(m.line, 1, m.line, 1),
            options: { isWholeLine: false, linesDecorationsClassName: `cove-gutter-${m.kind}` },
          })),
      );
    } catch {
      void 0;
    }
  };

  const refreshBlame = async () => {
    try {
      const blame = await invoke<ScmBlameResult>("cove://commands/scm.blame", { repoRoot: repoDir, filePath: fileName });
      blameLines = blame.lines ?? [];
    } catch {
      void 0;
    }
  };

  const blameTip = document.createElement("div");
  blameTip.style.cssText = "position:absolute;display:none;z-index:20;background:var(--panel-2);border:1px solid var(--border);color:var(--fg);font-size:11px;padding:3px 8px;border-radius:5px;pointer-events:none;white-space:nowrap;";
  container.appendChild(blameTip);

  editor.onMouseMove((e: Monaco.editor.IEditorMouseEvent) => {
    const line = e.target.position?.lineNumber;
    if (!line || blameLines.length === 0) {
      blameTip.style.display = "none";
      return;
    }
    const entry = blameForLine(blameLines, line);
    if (!entry) {
      blameTip.style.display = "none";
      return;
    }
    blameTip.textContent = formatBlameHover(entry);
    blameTip.style.display = "block";
    const be = e.event.browserEvent as MouseEvent;
    const rect = container.getBoundingClientRect();
    blameTip.style.left = `${be.clientX - rect.left + 12}px`;
    blameTip.style.top = `${be.clientY - rect.top + 12}px`;
  });
  editor.onMouseLeave(() => { blameTip.style.display = "none"; });
  editor.onMouseDown((e: Monaco.editor.IEditorMouseEvent) => {
    const line = e.target.position?.lineNumber;
    if (!line) return;
    const entry = blameForLine(blameLines, line);
    if (entry && e.event.leftButton && e.target.type === monaco.editor.MouseTargetType.GUTTER_LINE_DECORATIONS) {
      invoke("cove://commands/tool.git", { repoRoot: repoDir, commit: entry.commit }).catch(() => {});
    }
  });

  void refreshDecorations();
  void refreshBlame();

  const doSave = async () => {
    if (readOnly) return;
    try {
      await invoke("cove://commands/editor.save", { filePath, paneId, content: model.getValue() });
      dirty = false;
      saveStatus.textContent = "Saved";
      saveStatus.style.color = "var(--muted)";
      void refreshDecorations();
      void refreshBlame();
    } catch (e) {
      saveStatus.textContent = `Save failed: ${(e as Error).message}`;
      saveStatus.style.color = "#f85149";
    }
  };

  const decorationsPoll = setInterval(() => {
    if (!document.body.contains(el)) {
      clearInterval(decorationsPoll);
      return;
    }
    void refreshDecorations();
  }, 3000);

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
