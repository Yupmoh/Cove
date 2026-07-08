import { invoke } from "./invoke";

interface EditorState {
  filePath: string;
  cursor: string | null;
  scroll: string | null;
  fold: string | null;
  undo: string | null;
  readOnly: boolean;
}

export async function renderEditorPane(paneId: string, filePath: string): Promise<HTMLElement> {
  const el = document.createElement("div");
  el.className = "editor-pane";
  el.style.cssText = "display:flex;flex-direction:column;height:100%;background:#0d1117;color:#e6edf3;font-family:ui-monospace,monospace;";

  const header = document.createElement("div");
  header.style.cssText = "padding:6px 12px;border-bottom:1px solid #21262d;display:flex;gap:8px;align-items:center;";
  const titleEl = document.createElement("span");
  titleEl.style.cssText = "font-size:13px;font-weight:600;color:#e6edf3;";
  titleEl.textContent = filePath.split("/").pop() || filePath;
  header.appendChild(titleEl);
  const pathEl = document.createElement("span");
  pathEl.style.cssText = "font-size:11px;color:#6e7681;";
  pathEl.textContent = filePath;
  header.appendChild(pathEl);
  el.appendChild(header);

  const readOnlyBanner = document.createElement("div");
  readOnlyBanner.style.cssText = "padding:4px 12px;background:#1a1520;color:#f85149;font-size:11px;display:none;";
  readOnlyBanner.textContent = "Read-only file";
  el.appendChild(readOnlyBanner);

  const textarea = document.createElement("textarea");
  textarea.style.cssText = "flex:1;padding:8px 12px;background:#0d1117;color:#e6edf3;border:none;outline:none;font-family:ui-monospace,monospace;font-size:13px;line-height:1.5;resize:none;tab-size:2;";
  textarea.spellcheck = false;
  el.appendChild(textarea);

  const statusBar = document.createElement("div");
  statusBar.style.cssText = "padding:4px 12px;border-top:1px solid #21262d;display:flex;gap:12px;font-size:11px;color:#6e7681;";
  const cursorPos = document.createElement("span");
  cursorPos.textContent = "Ln 1, Col 1";
  statusBar.appendChild(cursorPos);
  const saveStatus = document.createElement("span");
  saveStatus.textContent = "Saved";
  statusBar.appendChild(saveStatus);
  el.appendChild(statusBar);

  let dirty = false;
  let saveTimer: ReturnType<typeof setTimeout> | null = null;

  const updateCursor = () => {
    const pos = textarea.selectionStart;
    const text = textarea.value.substring(0, pos);
    const lines = text.split("\n");
    cursorPos.textContent = `Ln ${lines.length}, Col ${lines[lines.length - 1].length + 1}`;
  };

  const scheduleAutosave = () => {
    if (saveTimer) clearTimeout(saveTimer);
    dirty = true;
    saveStatus.textContent = "Modified";
    saveStatus.style.color = "#d29922";
    saveTimer = setTimeout(() => {
      doSave();
    }, 2000);
  };

  const doSave = async () => {
    const cursor = `${textarea.selectionStart}`;
    const scroll = `${textarea.scrollTop}`;
    try {
      await invoke("cove://commands/editor.save", {
        filePath,
        cursor,
        scroll,
        fold: null,
        undo: null,
        readOnly: false,
      });
      dirty = false;
      saveStatus.textContent = "Saved";
      saveStatus.style.color = "#6e7681";
    } catch (e) {
      saveStatus.textContent = `Save failed: ${(e as Error).message}`;
      saveStatus.style.color = "#f85149";
    }
  };

  textarea.addEventListener("input", () => {
    updateCursor();
    scheduleAutosave();
  });

  textarea.addEventListener("click", updateCursor);
  textarea.addEventListener("keyup", updateCursor);

  textarea.addEventListener("keydown", (e) => {
    if ((e.metaKey || e.ctrlKey) && e.key === "s") {
      e.preventDefault();
      if (saveTimer) clearTimeout(saveTimer);
      doSave();
    }
    if (e.key === "Tab") {
      e.preventDefault();
      const start = textarea.selectionStart;
      const end = textarea.selectionEnd;
      textarea.value = textarea.value.substring(0, start) + "  " + textarea.value.substring(end);
      textarea.selectionStart = textarea.selectionEnd = start + 2;
    }
  });

  textarea.addEventListener("scroll", () => {
    const scroll = `${textarea.scrollTop}`;
    invoke("cove://commands/editor.set-state", {
      filePath,
      cursor: `${textarea.selectionStart}`,
      scroll,
      fold: null,
      undo: null,
      readOnly: false,
    }).catch(() => {});
  });

  try {
    const result = await fetch(`cove://fs/read?path=${encodeURIComponent(filePath)}`);
    if (result.ok) {
      const content = await result.text();
      textarea.value = content;
    }
  } catch {
    textarea.value = "";
    textarea.placeholder = `Unable to load ${filePath}`;
  }

  updateCursor();

  return el;
}
