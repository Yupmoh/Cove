import { invoke } from "./invoke";
import { FrontendCommand } from "./app/frontend-command";

interface SnapshotListItem {
  id: string;
  hash: string;
  trigger: string;
  takenAtUtc: string;
  pinned: boolean;
}

interface SnapshotDiffItem {
  key: string;
  oldValue: string | null;
  newValue: string | null;
  changeType: string;
}

interface ReviewCommentDto {
  id: string;
  rootId: string;
  parentId: string | null;
  commitSha: string;
  filePath: string;
  line: number;
  author: string;
  body: string;
  state: string;
  createdAt: string;
  orphanedAt: string | null;
  hunkId: string | null;
  contextHash: string | null;
}

interface AttributionEntryDto {
  id: string;
  sessionId: string;
  toolUseId: string;
  filePath: string;
  startLine: number;
  endLine: number;
  at: string;
}

interface SnapshotListResult { snapshots: SnapshotListItem[] }
interface SnapshotInspectResult { diffs: SnapshotDiffItem[] }
interface ReviewListCommentsResult { comments: ReviewCommentDto[] }
interface AttributionListResult { entries: AttributionEntryDto[] }

export async function renderDiffReviewNook(bayId: string): Promise<HTMLElement> {
  const el = document.createElement("div");
  el.className = "diff-review-nook";
  el.style.cssText = "display:flex;flex-direction:column;height:100%;background:#0b1622;color:#e5e9f0;font-family:system-ui,sans-serif;";

  const header = buildHeader();
  el.appendChild(header);

  const commitInput = buildCommitInput();
  el.appendChild(commitInput);

  const diffContainer = document.createElement("div");
  diffContainer.style.cssText = "flex:1;overflow-y:auto;padding:8px 12px;";
  el.appendChild(diffContainer);

  const loadBtn = commitInput.querySelector("button")!;
  const commitField = commitInput.querySelector("input")!;

  let currentCommit: string | null = null;
  let pollTimer: ReturnType<typeof setInterval> | null = null;

  const triggerLoad = () => {
    const commitSha = commitField.value.trim();
    if (commitSha) {
      currentCommit = commitSha;
      loadDiffReview(commitSha, diffContainer);
      startPolling();
    }
  };

  const startPolling = () => {
    if (pollTimer) clearInterval(pollTimer);
    pollTimer = setInterval(() => {
      if (!el.isConnected) {
        clearInterval(pollTimer!);
        pollTimer = null;
        return;
      }
      if (currentCommit) {
        loadDiffReview(currentCommit, diffContainer, false);
      }
    }, 3000);
  };

  loadBtn.addEventListener("click", triggerLoad);
  commitField.addEventListener("keydown", (e) => {
    if (e.key === "Enter") triggerLoad();
  });

  return el;
}
function buildHeader(): HTMLElement {
  const header = document.createElement("div");
  header.style.cssText = "padding:8px 12px;border-bottom:1px solid #1e2d3f;display:flex;gap:8px;align-items:center;";
  const title = document.createElement("span");
  title.style.cssText = "font-size:14px;font-weight:600;";
  title.textContent = "Diff Review";
  header.appendChild(title);
  const liveIndicator = document.createElement("span");
  liveIndicator.style.cssText = "font-size:10px;color:#4ade80;padding:2px 6px;background:#1a2e1a;border-radius:3px;";
  liveIndicator.textContent = "● LIVE";
  header.appendChild(liveIndicator);
  return header;
}
function buildCommitInput(): HTMLElement {
  const container = document.createElement("div");
  container.style.cssText = "display:flex;gap:8px;padding:8px 12px;border-bottom:1px solid #1e2d3f;";
  const input = document.createElement("input");
  input.type = "text";
  input.placeholder = "Commit SHA...";
  input.style.cssText = "flex:1;padding:6px 10px;background:#14202e;border:1px solid #2b3d52;border-radius:4px;color:#e5e9f0;font-size:13px;";
  container.appendChild(input);
  const btn = document.createElement("button");
  btn.textContent = "Load";
  btn.style.cssText = "padding:6px 14px;background:#2b3d52;border:1px solid #4a9eff;color:#4a9eff;border-radius:4px;font-size:12px;cursor:pointer;";
  container.appendChild(btn);
  return container;
}

async function loadDiffReview(commitSha: string, container: HTMLElement, isInitial: boolean = true): Promise<void> {
  if (isInitial) {
    container.innerHTML = "";
    const loading = document.createElement("div");
    loading.style.cssText = "padding:20px;color:#6b7d8f;text-align:center;";
    loading.textContent = "Loading diff...";
    container.appendChild(loading);
  }

  try {
    const [snapshots, comments, attribution] = await Promise.all([
      invoke<SnapshotListResult>(FrontendCommand.SnapshotList, {}),
      invoke<ReviewListCommentsResult>(FrontendCommand.ReviewListComments, { commitSha }).catch(() => ({ comments: [] as ReviewCommentDto[] })),
      invoke<AttributionListResult>(FrontendCommand.AttributionFindByRange, { filePath: "", startLine: 1, endLine: 999999 }).catch(() => ({ entries: [] as AttributionEntryDto[] })),
    ]);

    const detached = document.createElement("div");
    detached.style.cssText = "padding:8px 12px;";

    const latestSnap = snapshots.snapshots[0];
    if (latestSnap) {
      const diffResult = await invoke<SnapshotInspectResult>(FrontendCommand.SnapshotInspect, { id: latestSnap.id });
      renderDiffLines(diffResult.diffs, detached, comments.comments, attribution.entries);
    }

    if (comments.comments.length > 0) {
      renderCommentThread(comments.comments, detached);
    }
    if (!latestSnap && comments.comments.length === 0) {
      const empty = document.createElement("div");
      empty.style.cssText = "padding:20px;color:#6b7d8f;text-align:center;font-size:13px;";
      empty.textContent = "No snapshots or comments for this commit";
      detached.appendChild(empty);
    }

    const savedScroll = container.scrollTop;
    container.innerHTML = "";
    container.appendChild(detached);
    container.scrollTop = savedScroll;
  } catch (e) {
    if (isInitial) {
      container.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed: ${(e as Error).message}</div>`;
    }
  }
}

function renderDiffLines(diffs: SnapshotDiffItem[], container: HTMLElement, comments: ReviewCommentDto[], attribution: AttributionEntryDto[]): void {
  if (diffs.length === 0) return;

  const section = document.createElement("div");
  section.style.cssText = "margin-bottom:16px;";
  const sectionHeader = document.createElement("div");
  sectionHeader.style.cssText = "padding:4px 0 8px;font-size:12px;color:#6b7d8f;border-bottom:1px solid #1e2d3f;margin-bottom:8px;";
  sectionHeader.textContent = "Changed files";
  section.appendChild(sectionHeader);

  for (const diff of diffs) {
    const row = document.createElement("div");
    row.style.cssText = "padding:6px 0;border-bottom:1px solid #14202e;font-family:monospace;font-size:12px;";

    const keyEl = document.createElement("div");
    keyEl.style.cssText = "color:#6b7d8f;";
    keyEl.textContent = diff.key;
    row.appendChild(keyEl);

    const changeColor = diff.changeType === "added" ? "#4ade80" : diff.changeType === "removed" ? "#ef4444" : "#fbbf24";
    if (diff.oldValue) {
      const oldEl = document.createElement("div");
      oldEl.style.cssText = "color:#ef4444;white-space:pre-wrap;word-break:break-all;";
      oldEl.textContent = `- ${diff.oldValue}`;
      row.appendChild(oldEl);
    }
    if (diff.newValue) {
      const newEl = document.createElement("div");
      newEl.style.cssText = `color:${changeColor};white-space:pre-wrap;word-break:break-all;`;
      newEl.textContent = `+ ${diff.newValue}`;
      row.appendChild(newEl);
    }

    const linkedComments = comments.filter(c => c.filePath === diff.key);
    for (const cmt of linkedComments) {
      const commentChip = document.createElement("div");
      commentChip.style.cssText = "padding:4px 8px;margin-top:4px;background:#1a2e1a;border-left:3px solid #4ade80;border-radius:2px;font-size:11px;";
      commentChip.textContent = `💬 ${cmt.author}: ${cmt.body} (${cmt.state})`;
      row.appendChild(commentChip);
    }

    const linkedAttribution = attribution.filter(a => a.filePath === diff.key);
    for (const attr of linkedAttribution) {
      const attrChip = document.createElement("div");
      attrChip.style.cssText = "padding:4px 8px;margin-top:4px;background:#1a1a2e;border-left:3px solid #a78bfa;border-radius:2px;font-size:11px;";
      attrChip.textContent = `🤖 ${attr.toolUseId} (session ${attr.sessionId}, lines ${attr.startLine}-${attr.endLine})`;
      row.appendChild(attrChip);
    }

    section.appendChild(row);
  }
  container.appendChild(section);
}

function renderCommentThread(comments: ReviewCommentDto[], container: HTMLElement): void {
  const section = document.createElement("div");
  section.style.cssText = "margin-bottom:16px;";
  const header = document.createElement("div");
  header.style.cssText = "padding:4px 0 8px;font-size:12px;color:#6b7d8f;border-bottom:1px solid #1e2d3f;margin-bottom:8px;";
  header.textContent = `Review comments (${comments.length})`;
  section.appendChild(header);

  const roots = comments.filter(c => c.parentId === null);
  for (const root of roots) {
    section.appendChild(buildCommentNode(root, comments));
  }
  container.appendChild(section);
}

function buildCommentNode(comment: ReviewCommentDto, allComments: ReviewCommentDto[]): HTMLElement {
  const node = document.createElement("div");
  node.style.cssText = "padding:8px;margin-bottom:4px;background:#14202e;border-radius:4px;border-left:3px solid " + (comment.state === "resolved" ? "#4ade80" : comment.state === "orphaned" ? "#ef4444" : "#4a9eff");

  const author = document.createElement("div");
  author.style.cssText = "font-size:11px;color:#6b7d8f;margin-bottom:4px;";
  author.textContent = `${comment.author} · ${comment.state} · ${comment.filePath}:${comment.line}`;
  node.appendChild(author);

  const body = document.createElement("div");
  body.style.cssText = "font-size:13px;color:#e5e9f0;";
  body.textContent = comment.body;
  node.appendChild(body);

  const replies = allComments.filter(c => c.parentId === comment.id);
  if (replies.length > 0) {
    const replyContainer = document.createElement("div");
    replyContainer.style.cssText = "margin-left:16px;margin-top:4px;";
    for (const reply of replies) {
      replyContainer.appendChild(buildCommentNode(reply, allComments));
    }
    node.appendChild(replyContainer);
  }

  return node;
}
