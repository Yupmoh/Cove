import { invoke } from "./invoke";

interface TaskCard {
  id: string;
  title: string;
  description: string;
  taskNumber: number;
  workspaceId: string;
  statusId: string;
  priority: number;
  size: number;
  assignee: string | null;
  source: string;
  currentPrimaryRunId: string | null;
  createdAt: string;
  updatedAt: string;
}

interface StatusRow {
  id: string;
  workspaceId: string;
  name: string;
  color: string;
  position: number;
  hidden: boolean;
}

interface TaskListResult { cards: TaskCard[]; }
interface StatusListResult { statuses: StatusRow[]; }

const PRIORITY_LABELS = ["critical", "high", "medium", "low"];
const SIZE_LABELS = ["xs", "s", "m", "l", "xl"];
const SORT_OPTIONS = ["updated", "created", "priority", "number"];

let currentSort = "updated";
let searchQuery = "";
let statusFilter = "";
let selectedRow = 0;
let allCards: TaskCard[] = [];
let statusMap: Record<string, StatusRow> = {};

export async function renderTaskList(workspaceId: string): Promise<HTMLElement> {
  const el = document.createElement("div");
  el.className = "task-list-view";
  el.style.cssText = "display:flex;flex-direction:column;height:100%;background:#0b1622;color:#e5e9f0;font-family:system-ui,sans-serif;";

  await refreshList(el, workspaceId);
  return el;
}

async function refreshList(el: HTMLElement, workspaceId: string): Promise<void> {
  try {
    const [statusResult, cardResult] = await Promise.all([
      invoke<StatusListResult>("cove://commands/task.status.list", { workspaceId }),
      invoke<TaskListResult>("cove://commands/task.list", { workspaceId }),
    ]);

    statusMap = {};
    for (const s of statusResult.statuses) statusMap[s.id] = s;
    allCards = cardResult.cards || [];

    el.innerHTML = "";
    el.appendChild(buildToolbar(workspaceId, el));
    el.appendChild(buildTable(workspaceId));
  } catch (e) {
    el.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load tasks: ${(e as Error).message}</div>`;
  }
}

function buildToolbar(workspaceId: string, el: HTMLElement): HTMLElement {

  const toolbar = document.createElement("div");
  toolbar.style.cssText = "padding:8px 12px;display:flex;gap:8px;align-items:center;border-bottom:1px solid #1e2d3f;flex-wrap:wrap;";

  const search = document.createElement("input");
  search.type = "text";
  search.placeholder = "Search title or COVE-#...";
  search.value = searchQuery;
  search.style.cssText = "flex:1;min-width:150px;padding:4px 8px;background:#14202e;border:1px solid #2b3d52;border-radius:4px;color:#e5e9f0;font-size:12px;";
  search.addEventListener("input", () => {
    searchQuery = search.value;
    const table = el.querySelector(".task-table");
    if (table) table.replaceWith(buildTable(workspaceId));
  });
  toolbar.appendChild(search);

  const statusSelect = document.createElement("select");
  statusSelect.style.cssText = "padding:4px;background:#14202e;border:1px solid #2b3d52;border-radius:4px;color:#e5e9f0;font-size:12px;";
  statusSelect.innerHTML = '<option value="">All statuses</option>';
  for (const s of Object.values(statusMap).sort((a, b) => a.position - b.position)) {
    const opt = document.createElement("option");
    opt.value = s.id;
    opt.textContent = s.name;
    statusSelect.appendChild(opt);
  }
  statusSelect.value = statusFilter;
  statusSelect.addEventListener("change", () => {
    statusFilter = statusSelect.value;
    const table = el.querySelector(".task-table");
    if (table) table.replaceWith(buildTable(workspaceId));
  });
  toolbar.appendChild(statusSelect);

  const sortSelect = document.createElement("select");
  sortSelect.style.cssText = "padding:4px;background:#14202e;border:1px solid #2b3d52;border-radius:4px;color:#e5e9f0;font-size:12px;";
  for (const s of SORT_OPTIONS) {
    const opt = document.createElement("option");
    opt.value = s;
    opt.textContent = "Sort: " + s;
    sortSelect.appendChild(opt);
  }
  sortSelect.value = currentSort;
  sortSelect.addEventListener("change", () => {
    currentSort = sortSelect.value;
    const table = el.querySelector(".task-table");
    if (table) table.replaceWith(buildTable(workspaceId));
  });
  toolbar.appendChild(sortSelect);

  return toolbar;
}

function buildTable(workspaceId: string): HTMLElement {
  const container = document.createElement("div");
  container.className = "task-table";
  container.style.cssText = "flex:1;overflow:auto;";

  let filtered = allCards;
  if (searchQuery) {
    const q = searchQuery.toLowerCase();
    filtered = filtered.filter(c => c.title.toLowerCase().includes(q) || `cove-${c.taskNumber}`.includes(q));
  }
  if (statusFilter) {
    filtered = filtered.filter(c => c.statusId === statusFilter);
  }
  filtered = sortCards(filtered);

  const table = document.createElement("table");
  table.style.cssText = "width:100%;border-collapse:collapse;font-size:12px;";
  const thead = document.createElement("thead");
  thead.innerHTML = `<tr style="text-align:left;border-bottom:1px solid #1e2d3f;">
    <th style="padding:6px 8px;">#</th>
    <th style="padding:6px 8px;">Title</th>
    <th style="padding:6px 8px;">Status</th>
    <th style="padding:6px 8px;">Priority</th>
    <th style="padding:6px 8px;">Size</th>
    <th style="padding:6px 8px;">Assignee</th>
    <th style="padding:6px 8px;">Updated</th>
  </tr>`;
  table.appendChild(thead);

  const tbody = document.createElement("tbody");
  filtered.forEach((card, i) => {
    const tr = document.createElement("tr");
    tr.style.cssText = `cursor:pointer;border-bottom:1px solid #14202e;${i === selectedRow ? "background:#1e2d3f;" : ""}`;
    tr.addEventListener("mouseenter", () => { tr.style.background = "#1a2838"; });
    tr.addEventListener("mouseleave", () => { tr.style.background = i === selectedRow ? "#1e2d3f" : ""; });
    tr.addEventListener("click", () => {
      selectedRow = i;
      openDetailModal(card, workspaceId);
    });

    const status = statusMap[card.statusId];
    const updated = new Date(card.updatedAt).toLocaleDateString();

    tr.innerHTML = `
      <td style="padding:6px 8px;color:#6b7280;">COVE-${card.taskNumber}</td>
      <td style="padding:6px 8px;">${escapeHtml(card.title)}</td>
      <td style="padding:6px 8px;"><span style="color:${status?.color || "#6b7280"};">${status?.name || card.statusId}</span></td>
      <td style="padding:6px 8px;">${PRIORITY_LABELS[card.priority] || "medium"}</td>
      <td style="padding:6px 8px;">${SIZE_LABELS[card.size] || "m"}</td>
      <td style="padding:6px 8px;">${card.assignee ? "@" + escapeHtml(card.assignee) : ""}</td>
      <td style="padding:6px 8px;color:#6b7280;">${updated}</td>
    `;
    tbody.appendChild(tr);
  });
  table.appendChild(tbody);
  container.appendChild(table);
  return container;
}

function sortCards(cards: TaskCard[]): TaskCard[] {
  const sorted = [...cards];
  switch (currentSort) {
    case "created": sorted.sort((a, b) => b.createdAt.localeCompare(a.createdAt)); break;
    case "priority": sorted.sort((a, b) => a.priority - b.priority); break;
    case "number": sorted.sort((a, b) => a.taskNumber - b.taskNumber); break;
    default: sorted.sort((a, b) => b.updatedAt.localeCompare(a.updatedAt)); break;
  }
  return sorted;
}

function openDetailModal(card: TaskCard, workspaceId: string): void {
  const existing = document.querySelector(".task-detail-modal");
  if (existing) existing.remove();

  const overlay = document.createElement("div");
  overlay.className = "task-detail-modal";
  overlay.style.cssText = "position:fixed;inset:0;background:rgba(0,0,0,0.5);display:flex;align-items:center;justify-content:center;z-index:1000;";

  const modal = document.createElement("div");
  modal.style.cssText = "background:#14202e;border-radius:8px;padding:20px;min-width:500px;max-width:700px;max-height:80vh;overflow-y:auto;";

  const title = document.createElement("h2");
  title.style.cssText = "margin:0 0 12px;font-size:18px;";
  title.textContent = `COVE-${card.taskNumber}: ${card.title}`;
  modal.appendChild(title);

  const desc = document.createElement("div");
  desc.style.cssText = "margin-bottom:12px;font-size:13px;white-space:pre-wrap;color:#a0aec0;";
  desc.textContent = card.description || "(no description)";
  modal.appendChild(desc);

  const statusLabel = document.createElement("div");
  statusLabel.style.cssText = "margin-bottom:12px;font-size:12px;";
  const status = statusMap[card.statusId];
  statusLabel.innerHTML = `<span style="color:${status?.color || "#6b7280"};">● ${status?.name || card.statusId}</span>`;
  modal.appendChild(statusLabel);

  const launchBtn = document.createElement("button");
  launchBtn.textContent = "Save & Launch";
  launchBtn.style.cssText = "padding:6px 16px;background:#4cc2d6;border:none;border-radius:4px;color:#0b1622;cursor:pointer;font-size:13px;margin-right:8px;";
  launchBtn.addEventListener("click", async () => {
    try {
      await invoke("cove://commands/task.launch", { cardId: card.id });
      overlay.remove();
    } catch (e) {
      alert("Launch failed: " + (e as Error).message);
    }
  });
  modal.appendChild(launchBtn);

  const closeBtn = document.createElement("button");
  closeBtn.textContent = "Close";
  closeBtn.style.cssText = "padding:6px 16px;background:#2b3d52;border:none;border-radius:4px;color:#e5e9f0;cursor:pointer;font-size:13px;";
  closeBtn.addEventListener("click", () => overlay.remove());
  modal.appendChild(closeBtn);

  overlay.appendChild(modal);
  overlay.addEventListener("click", (e) => { if (e.target === overlay) overlay.remove(); });
  document.body.appendChild(overlay);
}

function escapeHtml(text: string): string {
  const div = document.createElement("div");
  div.textContent = text;
  return div.innerHTML;
}
