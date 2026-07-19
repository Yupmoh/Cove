import { invoke } from "./invoke";
import { FrontendCommand } from "./app/frontend-command";
import { LifecycleScope, type NookContentHandle } from "./app/lifecycle";

interface TaskCard {
  id: string;
  title: string;
  description: string;
  taskNumber: number;
  bayId: string;
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
  bayId: string;
  name: string;
  color: string;
  position: number;
  hidden: boolean;
}

interface TaskListResult { cards: TaskCard[]; }
interface StatusListResult { statuses: StatusRow[]; }

const PRIORITY_COLORS = ["#ef4444", "#f97316", "#eab308", "#6b7280"];
const PRIORITY_LABELS = ["critical", "high", "medium", "low"];
const SIZE_LABELS = ["xs", "s", "m", "l", "xl"];

let activeQuickActions: { scope: LifecycleScope; owner: LifecycleScope } | null = null;

export async function renderKanbanBoard(bayId: string): Promise<NookContentHandle> {
  const lifecycle = new LifecycleScope();
  const el = document.createElement("div");
  el.className = "kanban-board";
  el.style.cssText = "display:flex;gap:12px;padding:12px;overflow-x:auto;height:100%;background:#0b1622;color:#e5e9f0;font-family:system-ui,sans-serif;";

  await refreshBoard(el, bayId, lifecycle);

  const refreshFn = () => refreshBoard(el, bayId, lifecycle);
  el.dataset.refreshFn = "kanban-refresh";
  (window as unknown as Record<string, unknown>).__coveTaskRefresh = refreshFn;

  lifecycle.own(async () => {
    let menuDisposal: Promise<void> | null = null;
    if (activeQuickActions?.owner === lifecycle) {
      menuDisposal = activeQuickActions.scope.dispose();
      activeQuickActions = null;
    }
    const globals = window as unknown as Record<string, unknown>;
    if (globals.__coveTaskRefresh === refreshFn) delete globals.__coveTaskRefresh;
    el.remove();
    await menuDisposal;
  });
  return { element: el, dispose: () => lifecycle.dispose() };
}

async function refreshBoard(el: HTMLElement, bayId: string, lifecycle: LifecycleScope): Promise<void> {
  if (lifecycle.isDisposed) return;
  try {
    const [statusResult, cardResult] = await Promise.all([
      invoke<StatusListResult>(FrontendCommand.TaskStatusList, { bayId }),
      invoke<TaskListResult>(FrontendCommand.TaskList, { bayId }),
    ]);

    if (lifecycle.isDisposed) return;
    const statuses = statusResult.statuses.filter(s => !s.hidden).sort((a, b) => a.position - b.position);
    const cards = cardResult.cards || [];

    el.innerHTML = "";
    for (const status of statuses) {
      const column = createColumn(status, cards.filter(c => c.statusId === status.id), bayId, lifecycle);
      el.appendChild(column);
    }
  } catch (e) {
    if (!lifecycle.isDisposed) el.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed to load board: ${(e as Error).message}</div>`;
  }
}

function createColumn(status: StatusRow, cards: TaskCard[], bayId: string, lifecycle: LifecycleScope): HTMLElement {
  const col = document.createElement("div");
  col.className = "kanban-column";
  col.style.cssText = "min-width:220px;flex:1;display:flex;flex-direction:column;background:#14202e;border-radius:8px;overflow:hidden;";
  col.dataset.statusId = status.id;

  const header = document.createElement("div");
  header.style.cssText = "padding:10px 12px;font-weight:600;font-size:13px;display:flex;justify-content:space-between;align-items:center;border-bottom:1px solid #1e2d3f;";
  const colorDot = document.createElement("span");
  colorDot.style.cssText = `display:inline-block;width:8px;height:8px;border-radius:50%;background:${status.color};margin-right:8px;`;
  const titleSpan = document.createElement("span");
  titleSpan.appendChild(colorDot);
  titleSpan.appendChild(document.createTextNode(status.name));
  const count = document.createElement("span");
  count.style.cssText = "color:#6b7280;font-size:12px;";
  count.textContent = String(cards.length);
  header.appendChild(titleSpan);
  header.appendChild(count);
  col.appendChild(header);

  const cardList = document.createElement("div");
  cardList.style.cssText = "flex:1;overflow-y:auto;padding:8px;display:flex;flex-direction:column;gap:8px;min-height:60px;";
  cardList.dataset.statusId = status.id;
  cardList.addEventListener("dragover", (e) => { e.preventDefault(); cardList.style.background = "#1e2d3f"; });
  cardList.addEventListener("dragleave", () => { cardList.style.background = ""; });
  cardList.addEventListener("drop", async (e) => {
    e.preventDefault();
    cardList.style.background = "";
    if (lifecycle.isDisposed) return;
    const cardId = e.dataTransfer?.getData("text/plain");
    if (cardId) {
      await invoke(FrontendCommand.TaskUpdate, { id: cardId, bayId, statusId: status.id, source: "user:gui" });
      await refreshBoard(col.parentElement as HTMLElement, bayId, lifecycle);
    }
  });

  for (const card of cards) {
    cardList.appendChild(createCard(card, bayId, lifecycle));
  }

  col.appendChild(cardList);
  return col;
}

function createCard(card: TaskCard, bayId: string, lifecycle: LifecycleScope): HTMLElement {
  const el = document.createElement("div");
  el.className = "kanban-card";
  el.draggable = true;
  el.style.cssText = "padding:10px;background:#1a2838;border-radius:6px;cursor:grab;border-left:3px solid " + (PRIORITY_COLORS[card.priority] || "#6b7280") + ";";
  el.dataset.cardId = card.id;

  el.addEventListener("dragstart", (e) => {
    e.dataTransfer?.setData("text/plain", card.id);
    el.style.opacity = "0.5";
  });
  el.addEventListener("dragend", () => { el.style.opacity = "1"; });

  const title = document.createElement("div");
  title.style.cssText = "font-size:13px;font-weight:500;margin-bottom:4px;";
  title.textContent = card.title;

  const meta = document.createElement("div");
  meta.style.cssText = "display:flex;gap:8px;align-items:center;font-size:11px;color:#6b7280;";
  const num = document.createElement("span");
  num.textContent = `COVE-${card.taskNumber}`;
  const size = document.createElement("span");
  size.textContent = SIZE_LABELS[card.size] || "m";
  if (card.assignee) {
    const assignee = document.createElement("span");
    assignee.textContent = `@${card.assignee}`;
    meta.appendChild(assignee);
  }
  meta.appendChild(num);
  meta.appendChild(size);

  if (card.currentPrimaryRunId) {
    const glow = document.createElement("span");
    glow.style.cssText = "width:6px;height:6px;border-radius:50%;background:#4cc2d6;box-shadow:0 0 4px #4cc2d6;";
    glow.title = "Active run";
    meta.appendChild(glow);
  }

  el.appendChild(title);
  el.appendChild(meta);

  el.addEventListener("contextmenu", (e) => {
    e.preventDefault();
    showQuickActions(card, bayId, e.clientX, e.clientY, lifecycle);
  });

  return el;
}

function showQuickActions(card: TaskCard, bayId: string, x: number, y: number, owner: LifecycleScope): void {
  if (owner.isDisposed) return;
  if (activeQuickActions) void activeQuickActions.scope.dispose();
  const scope = new LifecycleScope();
  activeQuickActions = { scope, owner };

  const menu = document.createElement("div");
  menu.className = "quick-actions-menu";
  menu.style.cssText = `position:fixed;left:${x}px;top:${y}px;background:#1a2838;border:1px solid #2b3d52;border-radius:6px;padding:4px;z-index:1000;min-width:140px;box-shadow:0 4px 12px rgba(0,0,0,0.4);`;

  const actions: { label: string; action: () => Promise<void> }[] = [
    { label: "Set In-Review", action: async () => { await invoke(FrontendCommand.TaskSetInReview, { runId: card.currentPrimaryRunId }); } },
    { label: "Set Done", action: async () => { await invoke(FrontendCommand.TaskSetDone, { runId: card.currentPrimaryRunId }); } },
    { label: "Claim", action: async () => { await invoke(FrontendCommand.TaskClaim, { cardId: card.id }); } },
    { label: "Launch", action: async () => { await invoke(FrontendCommand.TaskLaunch, { cardId: card.id }); } },
  ];

  for (const a of actions) {
    const btn = document.createElement("div");
    btn.textContent = a.label;
    btn.style.cssText = "padding:6px 10px;cursor:pointer;border-radius:4px;font-size:12px;";
    btn.addEventListener("mouseenter", () => { btn.style.background = "#2b3d52"; });
    btn.addEventListener("mouseleave", () => { btn.style.background = ""; });
    scope.listen(btn, "click", async () => {
      await scope.dispose();
      if (activeQuickActions?.scope === scope) activeQuickActions = null;
      if (owner.isDisposed) return;
      await a.action();
      const board = document.querySelector(".kanban-board") as HTMLElement | null;
      if (board) await refreshBoard(board, bayId, owner);
    });
    menu.appendChild(btn);
  }

  document.body.appendChild(menu);
  scope.own(() => menu.remove());
  const closeHandler = (event: Event) => {
    if (!menu.contains(event.target as Node)) {
      void scope.dispose();
      if (activeQuickActions?.scope === scope) activeQuickActions = null;
    }
  };
  scope.timeout(() => {
    if (!scope.isDisposed) scope.listen(document, "click", closeHandler);
  }, 0);
}
