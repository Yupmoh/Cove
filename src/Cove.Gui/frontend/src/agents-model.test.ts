import { describe, it, expect } from "vitest";
import {
  mapAgentState,
  agentDisplayName,
  buildAgentRows,
  sortAgentRows,
  agentStateCounts,
  needsInputCount,
  type AgentCard,
  type AgentRow,
} from "./agents-model";

describe("mapAgentState", () => {
  it("maps engine status strings to the four agent states", () => {
    expect(mapAgentState("waitingforinput")).toBe("needs-input");
    expect(mapAgentState("needspermission")).toBe("needs-input");
    expect(mapAgentState("working")).toBe("running");
    expect(mapAgentState("tool-running")).toBe("running");
    expect(mapAgentState("idle")).toBe("idle");
    expect(mapAgentState("stopped")).toBe("done");
    expect(mapAgentState("crashed")).toBe("done");
  });
  it("treats an unknown status as idle", () => {
    expect(mapAgentState("something-else")).toBe("idle");
  });
});

describe("agentDisplayName", () => {
  it("prefers name, then adapter, then a shortened pane id", () => {
    expect(agentDisplayName({ paneId: "p", adapter: "claude", name: "Reviewer", status: "idle" })).toBe("Reviewer");
    expect(agentDisplayName({ paneId: "p", adapter: "claude", name: "", status: "idle" })).toBe("claude");
    expect(agentDisplayName({ paneId: "pane-1234567890", adapter: "", name: null, status: "idle" })).toBe("pane-123…");
  });
});

describe("buildAgentRows", () => {
  const cards: AgentCard[] = [
    { paneId: "a", adapter: "claude", name: "Zeta", status: "working" },
    { paneId: "b", adapter: "codex", name: "Alpha", status: "idle" },
    { paneId: "c", adapter: "gemini", name: "Beta", status: "stopped" },
  ];

  it("maps activity cards to rows sorted by state then name", () => {
    const rows = buildAgentRows(cards, new Set());
    expect(rows.map((r) => r.paneId)).toEqual(["a", "b", "c"]);
    expect(rows.map((r) => r.state)).toEqual(["running", "idle", "done"]);
  });

  it("forces needs-input state for panes with a live blocked signal", () => {
    const rows = buildAgentRows(cards, new Set(["b"]));
    const b = rows.find((r) => r.paneId === "b")!;
    expect(b.state).toBe("needs-input");
    expect(rows[0].paneId).toBe("b");
  });
});

describe("sortAgentRows", () => {
  it("orders needs-input first, then running, idle, done", () => {
    const rows: AgentRow[] = [
      { paneId: "1", name: "d", adapter: "", state: "done", rawStatus: "stopped" },
      { paneId: "2", name: "i", adapter: "", state: "idle", rawStatus: "idle" },
      { paneId: "3", name: "n", adapter: "", state: "needs-input", rawStatus: "waitingforinput" },
      { paneId: "4", name: "r", adapter: "", state: "running", rawStatus: "working" },
    ];
    expect(sortAgentRows(rows).map((r) => r.state)).toEqual(["needs-input", "running", "idle", "done"]);
  });
});

describe("counts", () => {
  it("tallies states and the blocked total", () => {
    const rows = buildAgentRows(
      [
        { paneId: "a", adapter: "x", name: "A", status: "working" },
        { paneId: "b", adapter: "x", name: "B", status: "waitingforinput" },
        { paneId: "c", adapter: "x", name: "C", status: "waitingforinput" },
      ],
      new Set(),
    );
    expect(agentStateCounts(rows)).toEqual({ "needs-input": 2, running: 1, idle: 0, done: 0 });
    expect(needsInputCount(rows)).toBe(2);
  });
});
