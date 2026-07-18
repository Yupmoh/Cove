import type * as Monaco from "monaco-editor";
import { invoke } from "./invoke";
import { FrontendCommand } from "./app/frontend-command";
import { MonacoLoader, defineCoveMonacoTheme } from "./monaco-loader";

interface Note {
  id: string;
  title: string;
  content: string;
  bayId: string;
  source: string;
  kind: string;
  createdAt: string;
  updatedAt: string;
}

interface NoteListResult { notes: Note[]; }
interface NoteGetStateResult { state: string | null; }

let currentNoteId: string | null = null;
let currentBayId: string | null = null;
let noteEditor: Monaco.editor.IStandaloneCodeEditor | null = null;
let viewport: { scrollX: number; scrollY: number; zoom: number } = { scrollX: 0, scrollY: 0, zoom: 1 };

export async function renderNotepadNook(bayId: string): Promise<HTMLElement> {
  const el = document.createElement("div");
  el.className = "notepad-nook";
  el.style.cssText = "display:flex;flex:1 1 0;min-width:0;min-height:0;height:100%;background:var(--panel);color:var(--fg);";

  currentBayId = bayId;

  const sidebar = await buildSidebar(bayId);
  el.appendChild(sidebar);
  el.appendChild(buildEditor());

  return el;
}

async function buildSidebar(bayId: string): Promise<HTMLElement> {
  const sidebar = document.createElement("div");
  sidebar.className = "notepad-sidebar";
  sidebar.style.cssText = "width:240px;border-right:1px solid var(--border);display:flex;flex-direction:column;overflow:hidden;";

  const header = document.createElement("div");
  header.style.cssText = "padding:8px 12px;font-size:11px;color:var(--muted);text-transform:uppercase;letter-spacing:0.5px;font-weight:700;border-bottom:1px solid var(--border);";
  header.textContent = "Notes";
  sidebar.appendChild(header);

  const newBtn = document.createElement("button");
  newBtn.textContent = "+ New Note";
  newBtn.style.cssText = "margin:6px 8px;padding:5px 8px;background:var(--accent);border:none;border-radius:6px;color:#000;font-weight:600;cursor:pointer;font-size:12px;";
  newBtn.addEventListener("click", () => createNote(bayId));
  sidebar.appendChild(newBtn);

  const list = document.createElement("div");
  list.className = "notepad-list";
  list.style.cssText = "flex:1;overflow-y:auto;";

  try {
    const result = await invoke<NoteListResult>(FrontendCommand.NoteList, { bayId });
    for (const note of result.notes || []) {
      list.appendChild(buildNoteRow(note, bayId));
    }
    if ((result.notes || []).length === 0) {
      const empty = document.createElement("div");
      empty.style.cssText = "padding:12px;color:var(--muted);font-size:12px;text-align:center;";
      empty.textContent = "No notes yet";
      list.appendChild(empty);
    }
  } catch (e) {
    console.warn("note list failed", bayId, e);
    const failed = document.createElement("div");
    failed.style.cssText = "padding:12px;color:var(--muted);font-size:12px;";
    failed.textContent = `Failed to load: ${(e as Error).message}`;
    list.appendChild(failed);
  }

  sidebar.appendChild(list);
  return sidebar;
}

function buildNoteRow(note: Note, bayId: string): HTMLElement {
  const row = document.createElement("div");
  row.style.cssText = "padding:8px 12px;border-bottom:1px solid var(--border);cursor:pointer;display:flex;gap:8px;align-items:center;";
  row.addEventListener("mouseenter", () => row.style.background = "var(--panel-2)");
  row.addEventListener("mouseleave", () => row.style.background = "");

  const icon = document.createElement("span");
  icon.style.cssText = "font-size:14px;";
  icon.textContent = note.kind === "markdown" ? "📝" : note.kind === "sketch" ? "✏️" : note.kind === "canvas" ? "🎨" : note.kind === "html" ? "🌐" : "📄";
  row.appendChild(icon);

  const info = document.createElement("div");
  info.style.cssText = "flex:1;min-width:0;";

  const title = document.createElement("div");
  title.style.cssText = "font-size:13px;color:var(--fg);white-space:nowrap;overflow:hidden;text-overflow:ellipsis;";
  title.textContent = note.title || "Untitled";
  info.appendChild(title);

  const updated = document.createElement("div");
  updated.style.cssText = "font-size:11px;color:var(--muted);";
  updated.textContent = new Date(note.updatedAt || note.createdAt).toLocaleDateString();
  info.appendChild(updated);

  row.appendChild(info);
  row.addEventListener("click", () => openNote(bayId, note.id));
  return row;
}

function buildEditor(): HTMLElement {
  const editor = document.createElement("div");
  editor.className = "notepad-editor";
  editor.style.cssText = "flex:1;display:flex;flex-direction:column;overflow:hidden;";

  const placeholder = document.createElement("div");
  placeholder.style.cssText = "flex:1;display:flex;align-items:center;justify-content:center;color:var(--muted);font-size:14px;";
  placeholder.textContent = "Select a note to edit";
  editor.appendChild(placeholder);

  return editor;
}

export async function openNote(bayId: string, noteId: string): Promise<void> {
  currentNoteId = noteId;
  currentBayId = bayId;

  const editor = document.querySelector(".notepad-editor") as HTMLElement;
  if (!editor) { console.warn("open note without a notepad editor host", noteId); return; }

  try {
    const note = await invoke<{ id: string; title: string; content: string; kind: string }>(FrontendCommand.NoteRead, { bayId, id: noteId });

    if (note.kind === "markdown" && (note.content.startsWith("http://") || note.content.startsWith("https://"))) {
      openUrlInBrowserSubtab(note.content);
      return;
    }

    const stateResult = await invoke<NoteGetStateResult>(FrontendCommand.NoteGetState, { bayId, id: noteId }).catch(() => ({ state: null }));
    if (stateResult.state) {
      try { viewport = JSON.parse(stateResult.state); } catch { viewport = { scrollX: 0, scrollY: 0, zoom: 1 }; }
    }

    if (noteEditor) { noteEditor.dispose(); noteEditor = null; }
    editor.innerHTML = "";

    const toolbar = document.createElement("div");
    toolbar.style.cssText = "padding:8px 12px;border-bottom:1px solid var(--border);display:flex;gap:8px;align-items:center;";

    const titleInput = document.createElement("input");
    titleInput.type = "text";
    titleInput.value = note.title;
    titleInput.spellcheck = false;
    titleInput.style.cssText = "flex:1;padding:5px 8px;background:var(--panel-2);border:1px solid var(--border);border-radius:6px;color:var(--fg);font-size:14px;font-weight:600;outline:none;";
    toolbar.appendChild(titleInput);

    const save = document.createElement("button");
    save.textContent = "Save";
    save.style.cssText = "padding:5px 14px;background:var(--accent);border:none;border-radius:6px;color:#000;font-weight:600;cursor:pointer;font-size:12px;";
    save.addEventListener("click", () => saveCurrentNote(titleInput.value, noteEditor?.getValue() ?? ""));
    toolbar.appendChild(save);

    editor.appendChild(toolbar);

    const host = document.createElement("div");
    host.style.cssText = "flex:1;min-height:0;position:relative;";
    editor.appendChild(host);

    const monaco = await MonacoLoader.load();
    const theme = defineCoveMonacoTheme(monaco);
    noteEditor = monaco.editor.create(host, {
      value: note.content,
      language: note.kind === "markdown" ? "markdown" : "plaintext",
      theme,
      wordWrap: "on",
      minimap: { enabled: false },
      lineNumbers: "off",
      folding: false,
      fontSize: 13,
      lineHeight: 21,
      padding: { top: 12, bottom: 12 },
      scrollBeyondLastLine: false,
      automaticLayout: true,
      renderLineHighlight: "none",
      occurrencesHighlight: "off",
      overviewRulerLanes: 0,
      hideCursorInOverviewRuler: true,
      scrollbar: { verticalScrollbarSize: 8, horizontalScrollbarSize: 8 },
    });
    noteEditor.setScrollTop(viewport.scrollY);
    noteEditor.onDidScrollChange((ev) => { viewport.scrollY = ev.scrollTop; viewport.scrollX = ev.scrollLeft; });
    noteEditor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyS, () => {
      void saveCurrentNote(titleInput.value, noteEditor?.getValue() ?? "");
    });
  } catch (e) {
    const msg = (e as Error).message ?? "";
    console.warn("open note failed", noteId, msg);
    editor.innerHTML = "";
    const info = document.createElement("div");
    info.style.cssText = "flex:1;display:flex;align-items:center;justify-content:center;color:var(--muted);font-size:14px;";
    info.textContent = msg.includes("not_found") || msg.includes("not found") ? "This note was deleted" : `Failed to open note: ${msg}`;
    editor.appendChild(info);
  }
}

async function saveCurrentNote(title: string, content: string): Promise<void> {
  if (!currentBayId || !currentNoteId) { console.warn("save skipped: no open note"); return; }
  try {
    await invoke(FrontendCommand.NoteWrite, {
      bayId: currentBayId,
      id: currentNoteId,
      title,
      content,
    });
    await invoke(FrontendCommand.NoteSaveState, {
      bayId: currentBayId,
      id: currentNoteId,
      stateJson: JSON.stringify(viewport),
    });
  } catch (e) {
    console.error("Save failed:", e);
  }
}

async function createNote(bayId: string): Promise<void> {
  try {
    const result = await invoke<{ id: string }>(FrontendCommand.NoteCreate, {
      title: "Untitled",
      bayId,
      source: "notepad",
      content: "",
      kind: "markdown",
    });
    await openNote(bayId, result.id);
    const sidebar = document.querySelector(".notepad-sidebar");
    if (sidebar) {
      const newSidebar = await buildSidebar(bayId);
      sidebar.replaceWith(newSidebar);
    }
  } catch (e) {
    console.error("Create note failed:", e);
  }
}

function openUrlInBrowserSubtab(url: string): void {
  invoke(FrontendCommand.BrowserOpen, { url }).catch(e => {
    console.error("Browser open failed:", e);
  });
}
