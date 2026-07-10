export type AgentState = "needs-input" | "running" | "idle" | "done";

export interface AgentCard {
  paneId: string;
  adapter: string;
  name: string | null;
  status: string;
  workspace?: string | null;
  room?: string | null;
}

export interface AgentRow {
  paneId: string;
  name: string;
  adapter: string;
  state: AgentState;
  rawStatus: string;
}

export interface AgentStateMeta {
  state: AgentState;
  label: string;
  color: string;
  order: number;
}

export const AGENT_STATE_META: Record<AgentState, AgentStateMeta> = {
  "needs-input": { state: "needs-input", label: "needs input", color: "#e0a44a", order: 0 },
  running: { state: "running", label: "running", color: "#34c2b0", order: 1 },
  idle: { state: "idle", label: "idle", color: "#8ca2a9", order: 2 },
  done: { state: "done", label: "finished", color: "#5fc08a", order: 3 },
};

export const AGENT_STATE_ORDER: AgentState[] = ["needs-input", "running", "idle", "done"];

export function mapAgentState(status: string): AgentState {
  switch (status.toLowerCase()) {
    case "waitingforinput":
    case "needspermission":
    case "needs-input":
      return "needs-input";
    case "working":
    case "active":
    case "tool-running":
      return "running";
    case "stopped":
    case "crashed":
    case "error":
      return "done";
    case "idle":
    default:
      return "idle";
  }
}

export function agentDisplayName(card: AgentCard): string {
  const name = (card.name ?? "").trim();
  if (name.length > 0) return name;
  const adapter = card.adapter.trim();
  if (adapter.length > 0) return adapter;
  return card.paneId.length > 8 ? `${card.paneId.slice(0, 8)}…` : card.paneId;
}

export function buildAgentRows(cards: AgentCard[], needsInputPaneIds: Set<string>): AgentRow[] {
  const rows: AgentRow[] = cards.map((card) => {
    const forced = needsInputPaneIds.has(card.paneId);
    const state = forced ? "needs-input" : mapAgentState(card.status);
    return {
      paneId: card.paneId,
      name: agentDisplayName(card),
      adapter: card.adapter,
      state,
      rawStatus: card.status,
    };
  });
  return sortAgentRows(rows);
}

export function sortAgentRows(rows: AgentRow[]): AgentRow[] {
  return [...rows].sort((a, b) => {
    const ord = AGENT_STATE_META[a.state].order - AGENT_STATE_META[b.state].order;
    if (ord !== 0) return ord;
    return a.name.localeCompare(b.name);
  });
}

export function agentStateCounts(rows: AgentRow[]): Record<AgentState, number> {
  const counts: Record<AgentState, number> = { "needs-input": 0, running: 0, idle: 0, done: 0 };
  for (const row of rows) counts[row.state]++;
  return counts;
}

export function needsInputCount(rows: AgentRow[]): number {
  return rows.reduce((n, row) => (row.state === "needs-input" ? n + 1 : n), 0);
}
