import { invoke } from "./invoke";
import {
  initialCursor,
  nextFile,
  prevFile,
  nextHunk,
  prevHunk,
  resolveDiffStackKey,
  type DiffStackCursor,
  type DiffStackFile,
} from "./diff-stack-nav";

interface ScmFileStatus {
  filePath: string;
  status: string;
  oldPath: string;
}

interface ScmStatusResult {
  repoRoot: string;
  staged: ScmFileStatus[];
  unstaged: ScmFileStatus[];
}

interface ScmDiffResult {
  filePath: string;
  oldContent: string | null;
  newContent: string | null;
  oldRef: string | null;
}

interface ScmCommitResult {
  message: string;
  success: boolean;
}

interface FileRow {
  file: ScmFileStatus;
  isStaged: boolean;
  row: HTMLElement;
  hunkCount: number;
  reviewed: boolean;
}

function hunkCount(patch: string | null): number {
  if (!patch) return 0;
  let count = 0;
  for (const line of patch.split("\n")) if (line.startsWith("@@")) count++;
  return count;
}

export async function renderSourceControlPane(workspaceId: string, openFile?: (path: string) => void): Promise<HTMLElement> {
  const el = document.createElement("div");
  el.className = "source-control-pane";
  el.tabIndex = 0;
  el.style.cssText = "display:flex;flex-direction:column;height:100%;background:#0d1117;color:#e6edf3;font-family:system-ui,sans-serif;outline:none;";

  const header = document.createElement("div");
  header.style.cssText = "padding:8px 12px;border-bottom:1px solid #21262d;display:flex;gap:8px;align-items:center;";
  const title = document.createElement("span");
  title.style.cssText = "font-size:14px;font-weight:600;";
  title.textContent = "Source Control";
  header.appendChild(title);
  const refreshBtn = document.createElement("button");
  refreshBtn.textContent = "↻";
  refreshBtn.style.cssText = "background:#21262d;border:1px solid #30363d;color:#58a6ff;border-radius:3px;padding:2px 8px;cursor:pointer;";
  header.appendChild(refreshBtn);
  el.appendChild(header);

  const commitBox = document.createElement("div");
  commitBox.style.cssText = "padding:8px 12px;border-bottom:1px solid #21262d;";
  const commitInput = document.createElement("textarea");
  commitInput.placeholder = "Commit message...";
  commitInput.style.cssText = "width:100%;height:40px;padding:6px;background:#161b22;border:1px solid #30363d;border-radius:4px;color:#e6edf3;font-size:12px;resize:none;box-sizing:border-box;";
  commitBox.appendChild(commitInput);
  const commitBtnRow = document.createElement("div");
  commitBtnRow.style.cssText = "display:flex;gap:4px;margin-top:4px;";
  const commitBtn = document.createElement("button");
  commitBtn.textContent = "Commit";
  commitBtn.style.cssText = "flex:1;padding:4px;background:#238636;border:none;color:#fff;border-radius:4px;font-size:12px;cursor:pointer;";
  const amendBtn = document.createElement("button");
  amendBtn.textContent = "Amend";
  amendBtn.style.cssText = "padding:4px 8px;background:#21262d;border:1px solid #30363d;color:#e6edf3;border-radius:4px;font-size:12px;cursor:pointer;";
  commitBtnRow.appendChild(commitBtn);
  commitBtnRow.appendChild(amendBtn);
  commitBox.appendChild(commitBtnRow);
  el.appendChild(commitBox);

  const fileList = document.createElement("div");
  fileList.style.cssText = "flex:1;overflow-y:auto;";
  el.appendChild(fileList);

  const navStatus = document.createElement("div");
  navStatus.style.cssText = "padding:4px 12px;border-top:1px solid #21262d;font-size:11px;color:#6e7681;flex-shrink:0;";
  navStatus.textContent = "j/k file · n/p hunk · Enter open · Space review";
  el.appendChild(navStatus);

  let rows: FileRow[] = [];
  let cursor: DiffStackCursor = initialCursor();

  const files = (): DiffStackFile[] => rows.map((r) => ({ filePath: r.file.filePath, hunkCount: Math.max(r.hunkCount, 1) }));

  const paintFocus = () => {
    rows.forEach((r, i) => {
      r.row.style.background = i === cursor.fileIndex ? "#1f2937" : "";
      r.row.style.boxShadow = i === cursor.fileIndex ? "inset 3px 0 0 #58a6ff" : "";
    });
    const current = rows[cursor.fileIndex];
    if (current) {
      navStatus.textContent = `file ${cursor.fileIndex + 1}/${rows.length} · hunk ${current.hunkCount > 0 ? cursor.hunkIndex + 1 : 0}/${current.hunkCount} · ${current.file.filePath}`;
    } else {
      navStatus.textContent = "j/k file · n/p hunk · Enter open · Space review";
    }
  };

  const ensureHunks = async (index: number) => {
    const target = rows[index];
    if (!target || target.hunkCount > 0) return;
    try {
      const diff = await invoke<ScmDiffResult>("cove://commands/scm.diff", { repoRoot: workspaceId, filePath: target.file.filePath, ref: "HEAD" });
      target.hunkCount = hunkCount(diff.newContent);
    } catch {
      target.hunkCount = 0;
    }
  };

  const refresh = async () => {
    try {
      const result = await invoke<ScmStatusResult>("cove://commands/scm.status", { repoRoot: workspaceId });
      rows = renderFileList(result.staged, result.unstaged, fileList, workspaceId, (idx) => {
        cursor = { fileIndex: idx, hunkIndex: 0 };
        void ensureHunks(idx).then(paintFocus);
        paintFocus();
      });
      cursor = initialCursor();
      await ensureHunks(0);
      paintFocus();
    } catch (e) {
      rows = [];
      fileList.innerHTML = `<div style="padding:20px;color:#f85149;">Failed: ${(e as Error).message}</div>`;
    }
  };

  el.addEventListener("keydown", (e) => {
    const action = resolveDiffStackKey(e);
    if (!action) return;
    if (document.activeElement === commitInput) return;
    e.preventDefault();
    if (action === "next-file") { cursor = nextFile(cursor, files()); void ensureHunks(cursor.fileIndex).then(paintFocus); paintFocus(); }
    else if (action === "prev-file") { cursor = prevFile(cursor, files()); void ensureHunks(cursor.fileIndex).then(paintFocus); paintFocus(); }
    else if (action === "next-hunk") { cursor = nextHunk(cursor, files()); void ensureHunks(cursor.fileIndex).then(paintFocus); paintFocus(); }
    else if (action === "prev-hunk") { cursor = prevHunk(cursor, files()); paintFocus(); }
    else if (action === "open") {
      const current = rows[cursor.fileIndex];
      if (current && openFile) openFile(current.file.filePath);
    } else if (action === "mark-reviewed") {
      const current = rows[cursor.fileIndex];
      if (current) {
        current.reviewed = !current.reviewed;
        current.row.style.opacity = current.reviewed ? "0.5" : "1";
        cursor = nextFile(cursor, files());
        void ensureHunks(cursor.fileIndex).then(paintFocus);
        paintFocus();
      }
    }
  });

  refreshBtn.addEventListener("click", refresh);
  commitBtn.addEventListener("click", () => doCommit(commitInput.value, false, workspaceId, refresh));
  amendBtn.addEventListener("click", () => doCommit(commitInput.value, true, workspaceId, refresh));

  await refresh();
  return el;
}

function renderFileList(
  staged: ScmFileStatus[],
  unstaged: ScmFileStatus[],
  container: HTMLElement,
  repoRoot: string,
  onFocus: (index: number) => void,
): FileRow[] {
  container.innerHTML = "";
  const rows: FileRow[] = [];

  const addSection = (label: string, files: ScmFileStatus[], isStaged: boolean) => {
    if (files.length === 0) return;
    const header = document.createElement("div");
    header.style.cssText = "padding:6px 12px;font-size:11px;color:#6e7681;text-transform:uppercase;font-weight:600;";
    header.textContent = `${label} (${files.length})`;
    container.appendChild(header);
    for (const file of files) {
      const index = rows.length;
      const row = buildFileRow(file, repoRoot, isStaged, () => onFocus(index));
      rows.push({ file, isStaged, row, hunkCount: 0, reviewed: false });
      container.appendChild(row);
    }
  };

  addSection("Staged Changes", staged, true);
  addSection("Changes", unstaged, false);

  if (rows.length === 0) {
    const empty = document.createElement("div");
    empty.style.cssText = "padding:20px;color:#6e7681;text-align:center;font-size:13px;";
    empty.textContent = "No changes";
    container.appendChild(empty);
  }
  return rows;
}

function buildFileRow(file: ScmFileStatus, repoRoot: string, isStaged: boolean, onFocus: () => void): HTMLElement {
  const row = document.createElement("div");
  row.style.cssText = "padding:4px 12px;display:flex;gap:8px;align-items:center;cursor:pointer;";

  const statusColor = file.status === "M" ? "#d29922" : file.status === "A" ? "#3fb950" : file.status === "D" ? "#f85149" : "#6e7681";
  const statusEl = document.createElement("span");
  statusEl.style.cssText = `color:${statusColor};font-weight:600;width:16px;text-align:center;`;
  statusEl.textContent = file.status;
  row.appendChild(statusEl);

  const nameEl = document.createElement("span");
  nameEl.style.cssText = "flex:1;font-size:12px;color:#e6edf3;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;";
  nameEl.textContent = file.filePath;
  row.appendChild(nameEl);

  const stageBtn = document.createElement("button");
  stageBtn.textContent = isStaged ? "−" : "+";
  stageBtn.style.cssText = "padding:2px 6px;background:#21262d;border:1px solid #30363d;color:#e6edf3;border-radius:3px;font-size:11px;cursor:pointer;";
  stageBtn.addEventListener("click", (e) => {
    e.stopPropagation();
    invoke("cove://commands/scm.stage", { repoRoot, filePath: file.filePath, unstage: isStaged }).catch(() => {});
  });
  row.appendChild(stageBtn);

  row.addEventListener("click", () => {
    onFocus();
    invoke<ScmDiffResult>("cove://commands/scm.diff", { repoRoot, filePath: file.filePath, ref: "HEAD" }).catch(() => {});
  });

  return row;
}

async function doCommit(message: string, amend: boolean, repoRoot: string, onDone: () => void): Promise<void> {
  if (!message.trim() && !amend) return;
  try {
    const result = await invoke<ScmCommitResult>("cove://commands/scm.commit", { repoRoot, message, amend, sign: false });
    if (result.success) {
      onDone();
    }
  } catch {
  }
}
