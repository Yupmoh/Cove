import { invoke } from "./invoke";

interface TimelineEntry {
  id: string;
  workspaceId: string;
  kind: string;
  source: string;
  scope: string | null;
  summary: string | null;
  jsonPayload: string | null;
  tags: string[] | null;
  timestamp: string;
}

interface TimelineListResult { entries: TimelineEntry[]; }

let searchQuery = "";
let scopeFilter = "";
let allEntries: TimelineEntry[] = [];

export async function renderTimelineFeed(workspaceId: string): Promise<HTMLElement> {
  const el = document.createElement("div");
  el.className = "timeline-feed-view";
  el.style.cssText = "display:flex;flex-direction:column;height:100%;background:#0b1622;color:#e5e9f0;font-family:system-ui,sans-serif;";

  await refreshFeed(el, workspaceId);
  return el;
}

async function refreshFeed(el: HTMLElement, workspaceId: string): Promise<void> {
  try {
    const result = await invoke<TimelineListResult>("cove://commands/timeline.list", { workspaceId });
    allEntries = result.entries || [];
    el.innerHTML = "";
    el.appendChild(buildToolbar(workspaceId, el));
    el.appendChild(buildFeed());
  } catch (e) {
    el.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load timeline: ${(e as Error).message}</div>`;
  }
}

function buildToolbar(workspaceId: string, el: HTMLElement): HTMLElement {
  const toolbar = document.createElement("div");
  toolbar.style.cssText = "padding:8px 12px;display:flex;gap:8px;align-items:center;border-bottom:1px solid #1e2d3f;flex-wrap:wrap;";

  const search = document.createElement("input");
  search.type = "text";
  search.placeholder = "Search timeline...";
  search.value = searchQuery;
  search.style.cssText = "flex:1;min-width:150px;padding:4px 8px;background:#14202e;border:1px solid #2b3d52;border-radius:4px;color:#e5e9f0;font-size:12px;";
  search.addEventListener("input", () => {
    searchQuery = search.value;
    const feed = el.querySelector(".timeline-feed-list");
    if (feed) feed.replaceWith(buildFeed());
  });
  toolbar.appendChild(search);

  const scopeSelect = document.createElement("select");
  scopeSelect.style.cssText = "padding:4px 8px;background:#14202e;border:1px solid #2b3d52;border-radius:4px;color:#e5e9f0;font-size:12px;";
  const scopes = ["", "workspace", "room", "pane", "task", "session"];
  for (const s of scopes) {
    const opt = document.createElement("option");
    opt.value = s;
    opt.textContent = s === "" ? "All scopes" : s;
    if (s === scopeFilter) opt.selected = true;
    scopeSelect.appendChild(opt);
  }
  scopeSelect.addEventListener("change", () => {
    scopeFilter = scopeSelect.value;
    const feed = el.querySelector(".timeline-feed-list");
    if (feed) feed.replaceWith(buildFeed());
  });
  toolbar.appendChild(scopeSelect);

  const refresh = document.createElement("button");
  refresh.textContent = "↻";
  refresh.style.cssText = "padding:4px 8px;background:#1e2d3f;border:1px solid #2b3d52;border-radius:4px;color:#e5e9f0;cursor:pointer;font-size:12px;";
  refresh.addEventListener("click", () => refreshFeed(el, workspaceId));
  toolbar.appendChild(refresh);

  return toolbar;
}

function buildFeed(): HTMLElement {
  const feed = document.createElement("div");
  feed.className = "timeline-feed-list";
  feed.style.cssText = "flex:1;overflow-y:auto;padding:4px 0;";

  let filtered = allEntries;
  if (scopeFilter) {
    filtered = filtered.filter(e => e.scope?.startsWith(scopeFilter) || e.scope === scopeFilter);
  }
  if (searchQuery) {
    const q = searchQuery.toLowerCase();
    filtered = filtered.filter(e =>
      e.summary?.toLowerCase().includes(q) ||
      e.kind.toLowerCase().includes(q) ||
      e.tags?.some(t => t.toLowerCase().includes(q))
    );
  }

  const grouped = groupByDay(filtered);
  for (const [day, entries] of grouped) {
    const dayHeader = document.createElement("div");
    dayHeader.style.cssText = "padding:6px 12px;font-size:11px;color:#6b7d8f;text-transform:uppercase;letter-spacing:0.5px;border-bottom:1px solid #1e2d3f;";
    dayHeader.textContent = day;
    feed.appendChild(dayHeader);

    for (const entry of entries) {
      feed.appendChild(buildEntryRow(entry));
    }
  }

  if (filtered.length === 0) {
    const empty = document.createElement("div");
    empty.style.cssText = "padding:20px;color:#6b7d8f;text-align:center;";
    empty.textContent = "No timeline entries";
    feed.appendChild(empty);
  }

  return feed;
}

function buildEntryRow(entry: TimelineEntry): HTMLElement {
  const row = document.createElement("div");
  row.style.cssText = "padding:8px 12px;border-bottom:1px solid #14202e;display:flex;gap:8px;align-items:flex-start;cursor:pointer;";
  row.addEventListener("mouseenter", () => row.style.background = "#14202e");
  row.addEventListener("mouseleave", () => row.style.background = "");

  const icon = document.createElement("span");
  icon.style.cssText = "font-size:14px;flex-shrink:0;";
  icon.textContent = kindIcon(entry.kind);
  row.appendChild(icon);

  const content = document.createElement("div");
  content.style.cssText = "flex:1;min-width:0;";

  const summary = document.createElement("div");
  summary.style.cssText = "font-size:13px;color:#e5e9f0;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;";
  summary.textContent = entry.summary || entry.kind;
  content.appendChild(summary);

  const meta = document.createElement("div");
  meta.style.cssText = "font-size:11px;color:#6b7d8f;display:flex;gap:6px;flex-wrap:wrap;";
  const time = new Date(entry.timestamp).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
  meta.textContent = `${time} · ${entry.kind}`;
  if (entry.scope) {
    const scopeSpan = document.createElement("span");
    scopeSpan.style.cssText = "color:#4a9eff;";
    scopeSpan.textContent = `· ${entry.scope}`;
    meta.appendChild(scopeSpan);
  }
  content.appendChild(meta);

  if (entry.tags && entry.tags.length > 0) {
    const tags = document.createElement("div");
    tags.style.cssText = "display:flex;gap:4px;flex-wrap:wrap;margin-top:2px;";
    for (const tag of entry.tags) {
      const pill = document.createElement("span");
      pill.style.cssText = "font-size:10px;padding:1px 6px;background:#1e2d3f;border-radius:8px;color:#6b7d8f;";
      pill.textContent = tag;
      tags.appendChild(pill);
    }
    content.appendChild(tags);
  }

  row.appendChild(content);
  return row;
}

function groupByDay(entries: TimelineEntry[]): Map<string, TimelineEntry[]> {
  const grouped = new Map<string, TimelineEntry[]>();
  for (const entry of entries) {
    const date = new Date(entry.timestamp);
    const day = date.toLocaleDateString([], { weekday: "long", month: "short", day: "numeric" });
    if (!grouped.has(day)) grouped.set(day, []);
    grouped.get(day)!.push(entry);
  }
  return grouped;
}

function kindIcon(kind: string): string {
  if (kind.startsWith("git.")) return "📋";
  if (kind.startsWith("synthesis.")) return "✨";
  if (kind.startsWith("note.")) return "📝";
  if (kind.startsWith("task.")) return "✓";
  if (kind.startsWith("run.")) return "▶";
  return "•";
}
