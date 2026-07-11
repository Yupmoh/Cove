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
import {
  shortSha,
  truncateCommitMessage,
  syncSectionHeader,
  isInSync,
  type SyncCommit,
  type ScmLogResult,
} from "./scm-sync";

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

export async function renderSourceControlPane(repoRoot: string, openFile?: (path: string) => void): Promise<HTMLElement> {
  const el = document.createElement("div");
  el.className = "source-control-pane";
  el.tabIndex = 0;
  el.style.cssText = "display:flex;flex-direction:column;height:100%;background:var(--panel);color:var(--fg);outline:none;";

  const header = document.createElement("div");
  header.style.cssText = "padding:8px 12px;border-bottom:1px solid var(--border);display:flex;gap:8px;align-items:center;";
  const title = document.createElement("span");
  title.style.cssText = "font-size:14px;font-weight:600;";
  title.textContent = "Source Control";
  header.appendChild(title);
  const refreshBtn = document.createElement("button");
  refreshBtn.textContent = "↻";
  refreshBtn.style.cssText = "background:var(--panel-2);border:1px solid var(--border);color:var(--accent);border-radius:5px;padding:2px 8px;cursor:pointer;";
  header.appendChild(refreshBtn);
  el.appendChild(header);

  const commitBox = document.createElement("div");
  commitBox.style.cssText = "padding:8px 12px;border-bottom:1px solid var(--border);";
  const commitInput = document.createElement("textarea");
  commitInput.placeholder = "Commit message...";
  commitInput.style.cssText = "width:100%;height:40px;padding:6px;background:var(--panel-2);border:1px solid var(--border);border-radius:6px;color:var(--fg);font-size:12px;resize:none;box-sizing:border-box;outline:none;";
  commitBox.appendChild(commitInput);
  const commitBtnRow = document.createElement("div");
  commitBtnRow.style.cssText = "display:flex;gap:4px;margin-top:4px;";
  const commitBtn = document.createElement("button");
  commitBtn.textContent = "Commit";
  commitBtn.style.cssText = "flex:1;padding:4px;background:var(--accent);border:none;color:#000;border-radius:6px;font-size:12px;font-weight:600;cursor:pointer;";
  const amendBtn = document.createElement("button");
  amendBtn.textContent = "Amend";
  amendBtn.style.cssText = "padding:4px 8px;background:var(--panel-2);border:1px solid var(--border);color:var(--fg);border-radius:6px;font-size:12px;cursor:pointer;";
  commitBtnRow.appendChild(commitBtn);
  commitBtnRow.appendChild(amendBtn);
  commitBox.appendChild(commitBtnRow);
  el.appendChild(commitBox);

  const fileList = document.createElement("div");
  fileList.style.cssText = "flex:1;overflow-y:auto;";
  el.appendChild(fileList);

  const syncBox = document.createElement("div");
  syncBox.style.cssText = "border-top:1px solid var(--border);max-height:40%;overflow-y:auto;flex-shrink:0;";
  el.appendChild(syncBox);

  const navStatus = document.createElement("div");
  navStatus.style.cssText = "padding:4px 12px;border-top:1px solid var(--border);font-size:11px;color:var(--muted);flex-shrink:0;";
  navStatus.textContent = "j/k file · n/p hunk · Enter open · Space review";
  el.appendChild(navStatus);

  let rows: FileRow[] = [];
  let cursor: DiffStackCursor = initialCursor();

  const files = (): DiffStackFile[] => rows.map((r) => ({ filePath: r.file.filePath, hunkCount: Math.max(r.hunkCount, 1) }));

  const paintFocus = () => {
    rows.forEach((r, i) => {
      r.row.style.background = i === cursor.fileIndex ? "var(--panel-2)" : "";
      r.row.style.boxShadow = i === cursor.fileIndex ? "inset 3px 0 0 var(--accent)" : "";
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
      const diff = await invoke<ScmDiffResult>("cove://commands/scm.diff", { repoRoot, filePath: target.file.filePath, ref: "HEAD" });
      target.hunkCount = hunkCount(diff.newContent);
    } catch {
      target.hunkCount = 0;
    }
  };

  const refreshSync = async () => {
    if (!repoRoot) {
      renderSyncUnavailable(syncBox);
      return;
    }
    try {
      const log = await invoke<ScmLogResult>("cove://commands/scm.log", { repoRoot });
      renderSyncSection(syncBox, log.unpushed ?? [], log.unpulled ?? []);
    } catch (err) {
      console.warn("scm log failed", repoRoot, err);
      renderSyncUnavailable(syncBox);
    }
  };

  const refresh = async () => {
    void refreshSync();
    try {
      const result = await invoke<ScmStatusResult>("cove://commands/scm.status", { repoRoot });
      rows = renderFileList(result.staged, result.unstaged, fileList, repoRoot, (idx) => {
        cursor = { fileIndex: idx, hunkIndex: 0 };
        void ensureHunks(idx).then(paintFocus);
        paintFocus();
      }, () => void refresh());
      cursor = initialCursor();
      await ensureHunks(0);
      paintFocus();
    } catch (e) {
      rows = [];
      fileList.innerHTML = `<div style="padding:20px;color:var(--muted);font-size:12px;">${repoRoot ? `Not a usable git repository: ${repoRoot}` : "This workspace has no directory"}</div>`;
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
  commitBtn.addEventListener("click", () => doCommit(commitInput.value, false, repoRoot, refresh));
  amendBtn.addEventListener("click", () => doCommit(commitInput.value, true, repoRoot, refresh));

  await refresh();
  return el;
}

function renderFileList(
  staged: ScmFileStatus[],
  unstaged: ScmFileStatus[],
  container: HTMLElement,
  repoRoot: string,
  onFocus: (index: number) => void,
  onChanged?: () => void,
): FileRow[] {
  container.innerHTML = "";
  const rows: FileRow[] = [];

  const addSection = (label: string, files: ScmFileStatus[], isStaged: boolean) => {
    if (files.length === 0) return;
    const header = document.createElement("div");
    header.style.cssText = "padding:6px 12px;font-size:11px;color:var(--muted);text-transform:uppercase;font-weight:600;";
    header.textContent = `${label} (${files.length})`;
    container.appendChild(header);
    for (const file of files) {
      const index = rows.length;
      const row = buildFileRow(file, repoRoot, isStaged, () => onFocus(index), onChanged);
      rows.push({ file, isStaged, row, hunkCount: 0, reviewed: false });
      container.appendChild(row);
    }
  };

  addSection("Staged Changes", staged, true);
  addSection("Changes", unstaged, false);

  if (rows.length === 0) {
    const empty = document.createElement("div");
    empty.style.cssText = "padding:20px;color:var(--muted);text-align:center;font-size:13px;";
    empty.textContent = "No changes";
    container.appendChild(empty);
  }
  return rows;
}

function buildFileRow(file: ScmFileStatus, repoRoot: string, isStaged: boolean, onFocus: () => void, onChanged?: () => void): HTMLElement {
  const row = document.createElement("div");
  row.style.cssText = "padding:4px 12px;display:flex;gap:8px;align-items:center;cursor:pointer;";

  const statusColor = file.status === "M" ? "#d29922" : file.status === "A" ? "#3fb950" : file.status === "D" ? "#f85149" : "#6e7681";
  const statusEl = document.createElement("span");
  statusEl.style.cssText = `color:${statusColor};font-weight:600;width:16px;text-align:center;`;
  statusEl.textContent = file.status;
  row.appendChild(statusEl);

  const nameEl = document.createElement("span");
  nameEl.style.cssText = "flex:1;font-size:12px;color:var(--fg);white-space:nowrap;overflow:hidden;text-overflow:ellipsis;";
  nameEl.textContent = file.filePath;
  row.appendChild(nameEl);

  const stageBtn = document.createElement("button");
  stageBtn.textContent = isStaged ? "−" : "+";
  stageBtn.style.cssText = "padding:2px 6px;background:var(--panel-2);border:1px solid var(--border);color:var(--fg);border-radius:5px;font-size:11px;cursor:pointer;";
  stageBtn.addEventListener("click", (e) => {
    e.stopPropagation();
    invoke("cove://commands/scm.stage", { repoRoot, filePath: file.filePath, unstage: isStaged })
      .then(() => { if (onChanged) onChanged(); })
      .catch((err) => console.warn("scm stage failed", file.filePath, err));
  });
  row.appendChild(stageBtn);

  row.addEventListener("click", () => {
    onFocus();
    invoke<ScmDiffResult>("cove://commands/scm.diff", { repoRoot, filePath: file.filePath, ref: "HEAD" }).catch(() => {});
  });

  return row;
}

function renderSyncUnavailable(container: HTMLElement): void {
  container.innerHTML = "";
  const header = document.createElement("div");
  header.style.cssText = "padding:6px 12px;font-size:11px;color:var(--muted);text-transform:uppercase;font-weight:600;";
  header.textContent = "Sync";
  container.appendChild(header);
  const line = document.createElement("div");
  line.style.cssText = "padding:6px 12px;font-size:12px;color:var(--muted);";
  line.textContent = "sync info unavailable";
  container.appendChild(line);
}

function renderSyncSection(container: HTMLElement, unpushed: SyncCommit[], unpulled: SyncCommit[]): void {
  container.innerHTML = "";
  const header = document.createElement("div");
  header.style.cssText = "padding:6px 12px;font-size:11px;color:var(--muted);text-transform:uppercase;font-weight:600;";
  header.textContent = "Sync";
  container.appendChild(header);

  if (isInSync(unpushed, unpulled)) {
    const line = document.createElement("div");
    line.style.cssText = "padding:6px 12px;font-size:12px;color:var(--muted);";
    line.textContent = "in sync with upstream";
    container.appendChild(line);
    return;
  }

  container.appendChild(buildSyncSubSection("Unpushed", unpushed));
  container.appendChild(buildSyncSubSection("Incoming", unpulled));
}

function buildSyncSubSection(label: string, commits: SyncCommit[]): HTMLElement {
  const section = document.createElement("details");
  section.open = commits.length > 0;

  const summary = document.createElement("summary");
  summary.style.cssText = "padding:4px 12px;font-size:11px;color:var(--muted);font-weight:600;cursor:pointer;list-style:none;";
  summary.textContent = syncSectionHeader(label, commits.length);
  section.appendChild(summary);

  for (const commit of commits) {
    section.appendChild(buildCommitRow(commit));
  }
  return section;
}

function buildCommitRow(commit: SyncCommit): HTMLElement {
  const row = document.createElement("div");
  row.style.cssText = "padding:3px 12px 3px 20px;display:flex;gap:8px;align-items:baseline;font-size:12px;";

  const shaEl = document.createElement("span");
  shaEl.style.cssText = "font-family:var(--mono,monospace);color:var(--muted);flex-shrink:0;";
  shaEl.textContent = shortSha(commit.sha);
  row.appendChild(shaEl);

  const msgEl = document.createElement("span");
  msgEl.style.cssText = "flex:1;color:var(--fg);white-space:nowrap;overflow:hidden;text-overflow:ellipsis;";
  msgEl.textContent = truncateCommitMessage(commit.message);
  msgEl.title = commit.message;
  row.appendChild(msgEl);

  const authorEl = document.createElement("span");
  authorEl.style.cssText = "color:var(--muted);flex-shrink:0;";
  authorEl.textContent = commit.author;
  row.appendChild(authorEl);

  return row;
}

async function doCommit(message: string, amend: boolean, repoRoot: string, onDone: () => void): Promise<void> {
  if (!message.trim() && !amend) { console.warn("commit skipped: empty message"); return; }
  try {
    const result = await invoke<ScmCommitResult>("cove://commands/scm.commit", { repoRoot, message, amend, sign: false });
    if (result.success) onDone();
    else console.warn("scm commit reported failure", result.message);
  } catch (err) {
    console.warn("scm commit failed", err);
  }
}
