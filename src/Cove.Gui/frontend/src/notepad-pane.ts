import { invoke } from "./invoke";

interface Note {
  id: string;
  title: string;
  content: string;
  workspaceId: string;
  source: string;
  kind: string;
  createdAt: string;
  updatedAt: string;
}

interface NoteListResult { notes: Note[]; }
interface NoteGetStateResult { state: string | null; }

let currentNoteId: string | null = null;
let currentWorkspaceId: string | null = null;
let viewport: { scrollX: number; scrollY: number; zoom: number } = { scrollX: 0, scrollY: 0, zoom: 1 };

export async function renderNotepadPane(workspaceId: string): Promise<HTMLElement> {
  const el = document.createElement("div");
  el.className = "notepad-pane";
  el.style.cssText = "display:flex;height:100%;background:#0b1622;color:#e5e9f0;font-family:system-ui,sans-serif;";

  currentWorkspaceId = workspaceId;

  const sidebar = await buildSidebar(workspaceId);
  el.appendChild(sidebar);
  el.appendChild(buildEditor());

  return el;
}

async function buildSidebar(workspaceId: string): Promise<HTMLElement> {
  const sidebar = document.createElement("div");
  sidebar.className = "notepad-sidebar";
  sidebar.style.cssText = "width:240px;border-right:1px solid #1e2d3f;display:flex;flex-direction:column;overflow:hidden;";

  const header = document.createElement("div");
  header.style.cssText = "padding:8px 12px;font-size:12px;color:#6b7d8f;text-transform:uppercase;letter-spacing:0.5px;border-bottom:1px solid #1e2d3f;";
  header.textContent = "Notes";
  sidebar.appendChild(header);

  const newBtn = document.createElement("button");
  newBtn.textContent = "+ New Note";
  newBtn.style.cssText = "margin:4px 8px;padding:4px 8px;background:#2563eb;border:1px solid #3b82f6;border-radius:4px;color:#fff;cursor:pointer;font-size:12px;";
  newBtn.addEventListener("click", () => createNote(workspaceId));
  sidebar.appendChild(newBtn);

  const list = document.createElement("div");
  list.className = "notepad-list";
  list.style.cssText = "flex:1;overflow-y:auto;";

  try {
    const result = await invoke<NoteListResult>("cove://commands/note.list", { workspaceId });
    for (const note of result.notes || []) {
      list.appendChild(buildNoteRow(note, workspaceId));
    }
    if ((result.notes || []).length === 0) {
      const empty = document.createElement("div");
      empty.style.cssText = "padding:12px;color:#6b7d8f;font-size:12px;text-align:center;";
      empty.textContent = "No notes yet";
      list.appendChild(empty);
    }
  } catch (e) {
    list.innerHTML = `<div style="padding:12px;color:#ef4444;font-size:12px;">Failed to load: ${(e as Error).message}</div>`;
  }

  sidebar.appendChild(list);
  return sidebar;
}

function buildNoteRow(note: Note, workspaceId: string): HTMLElement {
  const row = document.createElement("div");
  row.style.cssText = "padding:8px 12px;border-bottom:1px solid #14202e;cursor:pointer;display:flex;gap:8px;align-items:center;";
  row.addEventListener("mouseenter", () => row.style.background = "#14202e");
  row.addEventListener("mouseleave", () => row.style.background = "");

  const icon = document.createElement("span");
  icon.style.cssText = "font-size:14px;";
  icon.textContent = note.kind === "markdown" ? "📝" : note.kind === "sketch" ? "✏️" : note.kind === "canvas" ? "🎨" : note.kind === "html" ? "🌐" : "📄";
  row.appendChild(icon);

  const info = document.createElement("div");
  info.style.cssText = "flex:1;min-width:0;";

  const title = document.createElement("div");
  title.style.cssText = "font-size:13px;color:#e5e9f0;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;";
  title.textContent = note.title || "Untitled";
  info.appendChild(title);

  const updated = document.createElement("div");
  updated.style.cssText = "font-size:11px;color:#6b7d8f;";
  updated.textContent = new Date(note.updatedAt || note.createdAt).toLocaleDateString();
  info.appendChild(updated);

  row.appendChild(info);
  row.addEventListener("click", () => openNote(workspaceId, note.id));
  return row;
}

function buildEditor(): HTMLElement {
  const editor = document.createElement("div");
  editor.className = "notepad-editor";
  editor.style.cssText = "flex:1;display:flex;flex-direction:column;overflow:hidden;";

  const placeholder = document.createElement("div");
  placeholder.style.cssText = "flex:1;display:flex;align-items:center;justify-content:center;color:#6b7d8f;font-size:14px;";
  placeholder.textContent = "Select a note to edit";
  editor.appendChild(placeholder);

  return editor;
}

async function openNote(workspaceId: string, noteId: string): Promise<void> {
  currentNoteId = noteId;
  currentWorkspaceId = workspaceId;

  const editor = document.querySelector(".notepad-editor") as HTMLElement;
  if (!editor) return;

  try {
    const note = await invoke<{ id: string; title: string; content: string; kind: string }>("cove://commands/note.read", { workspaceId, id: noteId });

    if (note.kind === "markdown" && (note.content.startsWith("http://") || note.content.startsWith("https://"))) {
      openUrlInBrowserSubtab(note.content);
      return;
    }

    const stateResult = await invoke<NoteGetStateResult>("cove://commands/note.get-state", { workspaceId, id: noteId }).catch(() => ({ state: null }));
    if (stateResult.state) {
      try { viewport = JSON.parse(stateResult.state); } catch { viewport = { scrollX: 0, scrollY: 0, zoom: 1 }; }
    }

    editor.innerHTML = "";

    const toolbar = document.createElement("div");
    toolbar.style.cssText = "padding:8px 12px;border-bottom:1px solid #1e2d3f;display:flex;gap:8px;align-items:center;";

    const titleInput = document.createElement("input");
    titleInput.type = "text";
    titleInput.value = note.title;
    titleInput.style.cssText = "flex:1;padding:4px 8px;background:#14202e;border:1px solid #2b3d52;border-radius:4px;color:#e5e9f0;font-size:14px;font-weight:600;";
    toolbar.appendChild(titleInput);

    const save = document.createElement("button");
    save.textContent = "Save";
    save.style.cssText = "padding:4px 12px;background:#2563eb;border:1px solid #3b82f6;border-radius:4px;color:#fff;cursor:pointer;font-size:12px;";
    save.addEventListener("click", () => saveCurrentNote(titleInput.value, textarea.value));
    toolbar.appendChild(save);

    editor.appendChild(toolbar);

    const textarea = document.createElement("textarea");
    textarea.value = note.content;
    textarea.style.cssText = "flex:1;padding:12px;background:#0b1622;border:none;color:#e5e9f0;font-family:'SF Mono',Monaco,monospace;font-size:13px;resize:none;outline:none;line-height:1.6;";
    textarea.spellcheck = false;

    textarea.addEventListener("scroll", () => {
      viewport.scrollX = textarea.scrollLeft;
      viewport.scrollY = textarea.scrollTop;
    });

    editor.appendChild(textarea);

    requestAnimationFrame(() => {
      textarea.scrollLeft = viewport.scrollX;
      textarea.scrollTop = viewport.scrollY;
    });
  } catch (e) {
    editor.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to open note: ${(e as Error).message}</div>`;
  }
}

async function saveCurrentNote(title: string, content: string): Promise<void> {
  if (!currentWorkspaceId || !currentNoteId) return;
  try {
    await invoke("cove://commands/note.write", {
      workspaceId: currentWorkspaceId,
      id: currentNoteId,
      title,
      content,
    });
    await invoke("cove://commands/note.save-state", {
      workspaceId: currentWorkspaceId,
      id: currentNoteId,
      stateJson: JSON.stringify(viewport),
    });
  } catch (e) {
    console.error("Save failed:", e);
  }
}

async function createNote(workspaceId: string): Promise<void> {
  try {
    const result = await invoke<{ id: string }>("cove://commands/note.create", {
      title: "Untitled",
      workspaceId,
      source: "notepad",
      content: "",
      kind: "markdown",
    });
    await openNote(workspaceId, result.id);
    const sidebar = document.querySelector(".notepad-sidebar");
    if (sidebar) {
      const newSidebar = await buildSidebar(workspaceId);
      sidebar.replaceWith(newSidebar);
    }
  } catch (e) {
    console.error("Create note failed:", e);
  }
}

function openUrlInBrowserSubtab(url: string): void {
  invoke("cove://commands/browser.open", { url }).catch(e => {
    console.error("Browser open failed:", e);
  });
}
