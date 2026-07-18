import { invoke } from "./invoke";
import { FrontendCommand } from "./app/frontend-command";
import { diffRowSets, isTaskLikeKey, type RowDiff } from "./snapshot-row-diff";

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

interface SnapshotListResult { snapshots: SnapshotListItem[] }
interface SnapshotInspectResult { diffs: SnapshotDiffItem[] }
interface SnapshotRestoreResult { content: Record<string, string> }

export async function renderSnapshotInspector(bayId: string): Promise<HTMLElement> {
  const el = document.createElement("div");
  el.className = "snapshot-inspector";
  el.style.cssText = "display:flex;flex-direction:column;height:100%;background:#0b1622;color:#e5e9f0;font-family:system-ui,sans-serif;";

  const header = document.createElement("div");
  header.style.cssText = "padding:8px 12px;border-bottom:1px solid #1e2d3f;display:flex;gap:8px;align-items:center;";
  const title = document.createElement("span");
  title.style.cssText = "font-size:14px;font-weight:600;";
  title.textContent = "Snapshots";
  header.appendChild(title);
  const refreshBtn = document.createElement("button");
  refreshBtn.textContent = "↻";
  refreshBtn.style.cssText = "background:#14202e;border:1px solid #2b3d52;color:#4a9eff;border-radius:3px;padding:2px 8px;cursor:pointer;";
  header.appendChild(refreshBtn);
  el.appendChild(header);

  const listEl = document.createElement("div");
  listEl.style.cssText = "flex:0 0 40%;overflow-y:auto;border-bottom:1px solid #1e2d3f;";
  el.appendChild(listEl);

  const diffEl = document.createElement("div");
  diffEl.style.cssText = "flex:1;overflow-y:auto;padding:8px 12px;";
  el.appendChild(diffEl);

  const refresh = async () => {
    try {
      const result = await invoke<SnapshotListResult>(FrontendCommand.SnapshotList, {});
      listEl.innerHTML = "";
      diffEl.innerHTML = "";
      if (result.snapshots.length === 0) {
        const empty = document.createElement("div");
        empty.style.cssText = "padding:20px;color:#6b7d8f;text-align:center;font-size:13px;";
        empty.textContent = "No snapshots";
        listEl.appendChild(empty);
        return;
      }
      for (const snap of result.snapshots) {
        listEl.appendChild(buildSnapshotRow(snap, diffEl));
      }
    } catch (e) {
      listEl.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed: ${(e as Error).message}</div>`;
    }
  };

  refreshBtn.addEventListener("click", refresh);
  await refresh();
  return el;
}

function buildSnapshotRow(snap: SnapshotListItem, diffEl: HTMLElement): HTMLElement {
  const row = document.createElement("div");
  row.style.cssText = "padding:8px 12px;border-bottom:1px solid #14202e;cursor:pointer;display:flex;gap:8px;align-items:center;";
  row.addEventListener("mouseenter", () => row.style.background = "#14202e");
  row.addEventListener("mouseleave", () => row.style.background = "");

  const icon = document.createElement("span");
  icon.style.cssText = "font-size:14px;";
  icon.textContent = snap.trigger === "pre-restore" ? "↩️" : snap.pinned ? "📌" : "📸";
  row.appendChild(icon);

  const info = document.createElement("div");
  info.style.cssText = "flex:1;min-width:0;";

  const triggerEl = document.createElement("div");
  triggerEl.style.cssText = "font-size:13px;color:#e5e9f0;";
  triggerEl.textContent = snap.trigger;
  info.appendChild(triggerEl);

  const meta = document.createElement("div");
  meta.style.cssText = "font-size:11px;color:#6b7d8f;";
  const date = new Date(snap.takenAtUtc);
  meta.textContent = `${date.toLocaleString()} · ${snap.id}`;
  info.appendChild(meta);

  row.appendChild(info);

  const restoreBtn = document.createElement("button");
  restoreBtn.textContent = "Restore";
  restoreBtn.style.cssText = "padding:4px 10px;background:#2b3d52;border:1px solid #4a9eff;color:#4a9eff;border-radius:3px;font-size:11px;cursor:pointer;";
  restoreBtn.addEventListener("click", (ev) => {
    ev.stopPropagation();
    invoke<SnapshotRestoreResult>(FrontendCommand.SnapshotRestore, { id: snap.id })
      .then(r => {
        const undoEl = document.createElement("div");
        undoEl.style.cssText = "padding:8px 12px;background:#1a2e1a;color:#4ade80;font-size:12px;";
        const keys = Object.keys(r.content || {}).length;
        undoEl.textContent = `Restored ${snap.id} (${keys} files). Pre-restore undo snapshot created.`;
        diffEl.prepend(undoEl);
      })
      .catch(e => {
        const errEl = document.createElement("div");
        errEl.style.cssText = "padding:8px 12px;background:#2e1a1a;color:#ef4444;font-size:12px;";
        errEl.textContent = `Restore failed: ${(e as Error).message}`;
        diffEl.prepend(errEl);
      });
  });
  row.appendChild(restoreBtn);

  row.addEventListener("click", () => {
    invoke<SnapshotInspectResult>(FrontendCommand.SnapshotInspect, { id: snap.id })
      .then(r => renderDiffs(r.diffs, diffEl, snap.id))
      .catch(e => {
        diffEl.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed: ${(e as Error).message}</div>`;
      });
  });

  return row;
}

function renderDiffs(diffs: SnapshotDiffItem[], diffEl: HTMLElement, snapId: string): void {
  diffEl.innerHTML = "";
  const header = document.createElement("div");
  header.style.cssText = "padding:4px 0 8px;font-size:12px;color:#6b7d8f;border-bottom:1px solid #1e2d3f;margin-bottom:8px;";
  header.textContent = `Diff vs current state — snapshot ${snapId}`;
  diffEl.appendChild(header);

  if (diffs.length === 0) {
    const empty = document.createElement("div");
    empty.style.cssText = "padding:20px;color:#6b7d8f;text-align:center;font-size:13px;";
    empty.textContent = "No changes — snapshot matches current state";
    diffEl.appendChild(empty);
    return;
  }
  for (const diff of diffs) {
    diffEl.appendChild(buildDiffRow(diff));
  }
}

function buildDiffRow(diff: SnapshotDiffItem): HTMLElement {
  const row = document.createElement("div");
  row.style.cssText = "padding:6px 0;border-bottom:1px solid #14202e;font-family:monospace;font-size:12px;";

  const keyEl = document.createElement("div");
  keyEl.style.cssText = "color:#6b7d8f;";
  keyEl.textContent = diff.key;
  row.appendChild(keyEl);

  const rowDiffs = diffRowSets(diff.oldValue, diff.newValue);
  if (rowDiffs && (isTaskLikeKey(diff.key) || rowDiffs.length > 0)) {
    row.appendChild(buildRowTable(rowDiffs));
    return row;
  }

  const changeColor = diff.changeType === "added" ? "#4ade80" : diff.changeType === "removed" ? "#ef4444" : "#fbbf24";
  const changeSymbol = diff.changeType === "added" ? "+" : diff.changeType === "removed" ? "-" : "~";

  if (diff.oldValue) {
    const oldEl = document.createElement("div");
    oldEl.style.cssText = `color:#ef4444;white-space:pre-wrap;word-break:break-all;`;
    oldEl.textContent = `- ${diff.oldValue}`;
    row.appendChild(oldEl);
  }
  if (diff.newValue) {
    const newEl = document.createElement("div");
    newEl.style.cssText = `color:${changeColor};white-space:pre-wrap;word-break:break-all;`;
    newEl.textContent = `${changeSymbol} ${diff.newValue}`;
    row.appendChild(newEl);
  }

  return row;
}

function buildRowTable(rowDiffs: RowDiff[]): HTMLElement {
  const table = document.createElement("div");
  table.style.cssText = "margin-top:4px;display:flex;flex-direction:column;gap:2px;";

  const changed = rowDiffs.filter((r) => r.changeType !== "unchanged");
  if (changed.length === 0) {
    const empty = document.createElement("div");
    empty.style.cssText = "color:#6b7d8f;font-size:11px;padding:2px 0;";
    empty.textContent = `${rowDiffs.length} rows · no row changes`;
    table.appendChild(empty);
    return table;
  }

  for (const rd of changed) {
    const rowEl = document.createElement("div");
    rowEl.style.cssText = "display:flex;flex-direction:column;padding:3px 6px;border-left:2px solid #1e2d3f;";

    const badgeColor = rd.changeType === "added" ? "#4ade80" : rd.changeType === "removed" ? "#ef4444" : "#fbbf24";
    const label = document.createElement("div");
    label.style.cssText = `color:${badgeColor};font-size:11px;`;
    const fields = rd.changedFields.length > 0 ? ` [${rd.changedFields.join(", ")}]` : "";
    label.textContent = `${rd.changeType.toUpperCase()} · row ${rd.id}${fields}`;
    rowEl.appendChild(label);

    if (rd.changeType === "changed") {
      for (const field of rd.changedFields) {
        const line = document.createElement("div");
        line.style.cssText = "font-size:11px;white-space:pre-wrap;word-break:break-all;";
        const before = rd.before ? JSON.stringify(rd.before[field]) : "∅";
        const after = rd.after ? JSON.stringify(rd.after[field]) : "∅";
        line.innerHTML = `<span style="color:#6b7d8f;">${field}:</span> <span style="color:#ef4444;">${escapeHtml(before)}</span> <span style="color:#6b7d8f;">→</span> <span style="color:#4ade80;">${escapeHtml(after)}</span>`;
        rowEl.appendChild(line);
      }
    } else {
      const payload = rd.changeType === "added" ? rd.after : rd.before;
      const line = document.createElement("div");
      line.style.cssText = `font-size:11px;white-space:pre-wrap;word-break:break-all;color:${rd.changeType === "added" ? "#4ade80" : "#ef4444"};`;
      line.textContent = JSON.stringify(payload);
      rowEl.appendChild(line);
    }

    table.appendChild(rowEl);
  }
  return table;
}

function escapeHtml(s: string): string {
  return s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
}
