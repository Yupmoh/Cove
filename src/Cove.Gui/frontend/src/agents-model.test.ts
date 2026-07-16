import { describe, it, expect } from "vitest";
import {
  mapAgentState,
  agentDisplayName,
  buildAgentRows,
  sortAgentRows,
  agentStateCounts,
  needsInputCount,
  agentCardsEqual,
  AGENT_STATE_META,
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
    expect(mapAgentState("done")).toBe("done");
    expect(mapAgentState("stopped")).toBe("done");
    expect(mapAgentState("crashed")).toBe("done");
  });

  it("acknowledges a finished agent as idle without turning questions into idle", () => {
    const rows = buildAgentRows(
      [
        { nookId: "done", adapter: "x", name: "Done", status: "done" },
        { nookId: "question", adapter: "x", name: "Question", status: "waitingforinput" },
      ],
      new Set(),
      new Set(["done", "question"]),
    );
    expect(rows.find((row) => row.nookId === "done")?.state).toBe("idle");
    expect(rows.find((row) => row.nookId === "question")?.state).toBe("needs-input");
  });
  it("treats an unknown status as idle", () => {
    expect(mapAgentState("something-else")).toBe("idle");
  });
});

describe("agentDisplayName", () => {
  it("prefers name, then adapter, then a shortened nook id", () => {
    expect(agentDisplayName({ nookId: "p", adapter: "claude", name: "Reviewer", status: "idle" })).toBe("Reviewer");
    expect(agentDisplayName({ nookId: "p", adapter: "claude", name: "", status: "idle" })).toBe("claude");
    expect(agentDisplayName({ nookId: "nook-1234567890", adapter: "", name: null, status: "idle" })).toBe("nook-123…");
  });
});

describe("buildAgentRows", () => {
  const cards: AgentCard[] = [
    { nookId: "a", adapter: "claude", name: "Zeta", status: "working" },
    { nookId: "b", adapter: "codex", name: "Alpha", status: "idle" },
    { nookId: "c", adapter: "gemini", name: "Beta", status: "stopped" },
  ];

  it("maps activity cards to rows sorted by state then name", () => {
    const rows = buildAgentRows(cards, new Set());
    expect(rows.map((r) => r.nookId)).toEqual(["a", "b", "c"]);
    expect(rows.map((r) => r.state)).toEqual(["running", "idle", "done"]);
  });

  it("forces needs-input state for nooks with a live blocked signal", () => {
    const rows = buildAgentRows(cards, new Set(["b"]));
    const b = rows.find((r) => r.nookId === "b")!;
    expect(b.state).toBe("needs-input");
    expect(rows[0].nookId).toBe("b");
  });
});

describe("sortAgentRows", () => {
  it("orders needs-input first, then running, idle, done", () => {
    const rows: AgentRow[] = [
      { nookId: "1", name: "d", adapter: "", state: "done", rawStatus: "stopped" },
      { nookId: "2", name: "i", adapter: "", state: "idle", rawStatus: "idle" },
      { nookId: "3", name: "n", adapter: "", state: "needs-input", rawStatus: "waitingforinput" },
      { nookId: "4", name: "r", adapter: "", state: "running", rawStatus: "working" },
    ];
    expect(sortAgentRows(rows).map((r) => r.state)).toEqual(["needs-input", "running", "idle", "done"]);
  });
});

describe("counts", () => {
  it("tallies states and the blocked total", () => {
    const rows = buildAgentRows(
      [
        { nookId: "a", adapter: "x", name: "A", status: "working" },
        { nookId: "b", adapter: "x", name: "B", status: "waitingforinput" },
        { nookId: "c", adapter: "x", name: "C", status: "waitingforinput" },
      ],
      new Set(),
    );
    expect(agentStateCounts(rows)).toEqual({ "needs-input": 2, running: 1, idle: 0, done: 0 });
    expect(needsInputCount(rows)).toBe(2);
  });
});

describe("agentCardsEqual", () => {
  it("treats identical display data as unchanged", () => {
    const previous: AgentCard[] = [{ nookId: "a", adapter: "codex", name: "Work", status: "working", bay: "Cove", shore: "Shore 1" }];
    const next = previous.map((card) => ({ ...card }));
    expect(agentCardsEqual(previous, next)).toBe(true);
  });

  it("detects sidebar-visible changes", () => {
    const previous: AgentCard[] = [{ nookId: "a", adapter: "codex", name: "Work", status: "working" }];
    expect(agentCardsEqual(previous, [{ ...previous[0], status: "idle" }])).toBe(false);
    expect(agentCardsEqual(previous, [{ ...previous[0], name: "Review" }])).toBe(false);
    expect(agentCardsEqual(previous, [])).toBe(false);
  });
});

describe("AGENT_STATE_META", () => {
  it("labels the four states per the sidebar contract", () => {
    expect(AGENT_STATE_META["needs-input"].label).toBe("needs input");
    expect(AGENT_STATE_META.running.label).toBe("running");
    expect(AGENT_STATE_META.idle.label).toBe("idle");
    expect(AGENT_STATE_META.done.label).toBe("needs attention");
  });
});
