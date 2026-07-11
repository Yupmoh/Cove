import { invoke } from "./invoke";

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

interface NoteReadResult {
  id: string;
  title: string;
  content: string;
  kind: string;
  format: string | null;
}

interface InlineComment {
  id: string;
  start: number;
  end: number;
  text: string;
  author: string;
  createdAt: string;
}

let currentNote: Note | null = null;
let comments: InlineComment[] = [];
let selectedRange: { start: number; end: number } | null = null;

export async function renderMarkdownNote(bayId: string, noteId: string): Promise<HTMLElement> {
  const el = document.createElement("div");
  el.className = "markdown-note-editor";
  el.style.cssText = "display:flex;flex-direction:column;height:100%;background:#0b1622;color:#e5e9f0;font-family:system-ui,sans-serif;";

  try {
    const result = await invoke<NoteReadResult>("cove://commands/note.read", {
      bayId,
      id: noteId,
    });
    currentNote = {
      id: result.id,
      title: result.title,
      content: result.content,
      bayId,
      source: "gui",
      kind: "markdown",
      createdAt: "",
      updatedAt: "",
    };
    comments = loadCommentsFromContent(result.content);
    el.appendChild(buildToolbar(bayId, el));
    el.appendChild(buildEditor(bayId));
    el.appendChild(buildCommentsPanel());
  } catch (e) {
    el.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load note: ${(e as Error).message}</div>`;
  }

  return el;
}

function buildToolbar(bayId: string, el: HTMLElement): HTMLElement {
  const toolbar = document.createElement("div");
  toolbar.style.cssText = "padding:8px 12px;display:flex;gap:8px;align-items:center;border-bottom:1px solid #1e2d3f;flex-wrap:wrap;";

  const title = document.createElement("input");
  title.type = "text";
  title.value = currentNote?.title || "";
  title.placeholder = "Untitled";
  title.style.cssText = "flex:1;min-width:150px;padding:4px 8px;background:#14202e;border:1px solid #2b3d52;border-radius:4px;color:#e5e9f0;font-size:14px;font-weight:600;";
  title.addEventListener("change", () => {
    if (currentNote) currentNote.title = title.value;
  });
  toolbar.appendChild(title);

  const save = document.createElement("button");
  save.textContent = "Save";
  save.style.cssText = "padding:4px 12px;background:#2563eb;border:1px solid #3b82f6;border-radius:4px;color:#fff;cursor:pointer;font-size:12px;";
  save.addEventListener("click", () => saveNote(bayId, el));
  toolbar.appendChild(save);

  const commentBtn = document.createElement("button");
  commentBtn.textContent = "Comment";
  commentBtn.style.cssText = "padding:4px 12px;background:#1e2d3f;border:1px solid #2b3d52;border-radius:4px;color:#e5e9f0;cursor:pointer;font-size:12px;";
  commentBtn.addEventListener("click", () => addCommentFromSelection());
  toolbar.appendChild(commentBtn);

  return toolbar;
}

function buildEditor(bayId: string): HTMLElement {
  const container = document.createElement("div");
  container.style.cssText = "flex:1;display:flex;overflow:hidden;";

  const editor = document.createElement("textarea");
  editor.className = "markdown-editor-textarea";
  editor.value = currentNote?.content || "";
  editor.style.cssText = "flex:1;padding:12px;background:#0b1622;border:none;color:#e5e9f0;font-family:'SF Mono',Monaco,monospace;font-size:13px;resize:none;outline:none;line-height:1.6;";
  editor.spellcheck = false;

  editor.addEventListener("input", () => {
    if (currentNote) currentNote.content = editor.value;
  });

  editor.addEventListener("mouseup", () => {
    const start = editor.selectionStart;
    const end = editor.selectionEnd;
    if (start !== end) {
      selectedRange = { start, end };
    } else {
      selectedRange = null;
    }
  });

  editor.addEventListener("paste", (e) => {
    const items = e.clipboardData?.items;
    if (!items) return;
    for (const item of items) {
      if (item.type.startsWith("image/")) {
        e.preventDefault();
        const file = item.getAsFile();
        if (file) handleImagePaste(file, bayId, editor);
        return;
      }
    }
  });

  container.appendChild(editor);
  return container;
}

function buildCommentsPanel(): HTMLElement {
  const panel = document.createElement("div");
  panel.className = "comments-panel";
  panel.style.cssText = "width:280px;border-left:1px solid #1e2d3f;display:flex;flex-direction:column;overflow-y:auto;";

  const header = document.createElement("div");
  header.style.cssText = "padding:8px 12px;font-size:12px;color:#6b7d8f;text-transform:uppercase;letter-spacing:0.5px;border-bottom:1px solid #1e2d3f;";
  header.textContent = `Inline Comments (${comments.length})`;
  panel.appendChild(header);

  const list = document.createElement("div");
  list.className = "comments-list";
  list.style.cssText = "flex:1;overflow-y:auto;";

  for (const comment of comments) {
    list.appendChild(buildCommentRow(comment));
  }

  if (comments.length === 0) {
    const empty = document.createElement("div");
    empty.style.cssText = "padding:12px;color:#6b7d8f;font-size:12px;text-align:center;";
    empty.textContent = "Select text and click Comment to add an inline comment.";
    list.appendChild(empty);
  }

  panel.appendChild(list);
  return panel;
}

function buildCommentRow(comment: InlineComment): HTMLElement {
  const row = document.createElement("div");
  row.style.cssText = "padding:8px 12px;border-bottom:1px solid #14202e;";

  const meta = document.createElement("div");
  meta.style.cssText = "font-size:11px;color:#6b7d8f;margin-bottom:4px;";
  meta.textContent = `${comment.author} · chars ${comment.start}-${comment.end}`;
  row.appendChild(meta);

  const text = document.createElement("div");
  text.style.cssText = "font-size:13px;color:#e5e9f0;";
  text.textContent = comment.text;
  row.appendChild(text);

  return row;
}

function addCommentFromSelection(): void {
  if (!selectedRange || !currentNote) return;

  const text = window.prompt("Comment text:");
  if (!text) return;

  const comment: InlineComment = {
    id: `cmt-${Date.now()}`,
    start: selectedRange.start,
    end: selectedRange.end,
    text,
    author: "user",
    createdAt: new Date().toISOString(),
  };
  comments.push(comment);
  refreshCommentsPanel();
  selectedRange = null;
}

function refreshCommentsPanel(): void {
  const panel = document.querySelector(".comments-panel");
  if (!panel) return;
  const newList = buildCommentsPanel();
  panel.replaceWith(newList);
}

async function handleImagePaste(file: File, bayId: string, editor: HTMLTextAreaElement): Promise<void> {
  const reader = new FileReader();
  reader.onload = async () => {
    const dataUrl = reader.result as string;
    const base64 = dataUrl.split(",")[1] || "";
    if (!base64 || !currentNote) return;

    try {
      const result = await invoke<{ mediaPath: string }>("cove://commands/note.media.save", {
        bayId,
        id: currentNote.id,
        fileName: file.name,
        base64Data: base64,
      });

      const insertText = `\n\n![${file.name}](${result.mediaPath})\n`;
      const pos = editor.selectionStart;
      editor.value = editor.value.slice(0, pos) + insertText + editor.value.slice(pos);
      currentNote.content = editor.value;

      await invoke("cove://commands/note.write", {
        bayId,
        id: currentNote.id,
        content: currentNote.content,
      });
    } catch (e) {
      console.error("Image paste failed:", e);
    }
  };
  reader.readAsDataURL(file);
}

async function saveNote(bayId: string, el: HTMLElement): Promise<void> {
  if (!currentNote) return;
  try {
    const contentWithComments = serializeContentWithComments(currentNote.content);
    await invoke("cove://commands/note.write", {
      bayId,
      id: currentNote.id,
      title: currentNote.title,
      content: contentWithComments,
    });
    const status = document.createElement("div");
    status.style.cssText = "position:absolute;bottom:8px;right:8px;padding:4px 12px;background:#1a4a2a;border-radius:4px;color:#4ade80;font-size:12px;";
    status.textContent = "Saved";
    el.style.position = "relative";
    el.appendChild(status);
    setTimeout(() => status.remove(), 2000);
  } catch (e) {
    console.error("Save failed:", e);
  }
}

function serializeContentWithComments(content: string): string {
  if (comments.length === 0) return content;
  const lines = content.split("\n");
  lines.push("");
  lines.push("<!-- cove:comments -->");
  for (const c of comments) {
    lines.push(`<!-- cove:comment id=${c.id} range=${c.start}-${c.end} author=${c.author} -->`);
    lines.push(c.text);
    lines.push("<!-- /cove:comment -->");
  }
  lines.push("<!-- /cove:comments -->");
  return lines.join("\n");
}

function loadCommentsFromContent(content: string): InlineComment[] {
  const result: InlineComment[] = [];
  const regex = /<!-- cove:comment id=([^\s]+) range=(\d+)-(\d+) author=([^\s]+) -->\n([\s\S]*?)\n<!-- \/cove:comment -->/g;
  let match;
  while ((match = regex.exec(content)) !== null) {
    result.push({
      id: match[1],
      start: parseInt(match[2], 10),
      end: parseInt(match[3], 10),
      author: match[4],
      text: match[5],
      createdAt: "",
    });
  }
  return result;
}
