import { describe, it, expect } from "vitest";
import { buildWorkspaceTree, paneLabel, workspaceTreeEmptyMessage, NO_WORKSPACES_MESSAGE, type WorkspaceTreeInput } from "./workspace-tree";

function baseInput(overrides: Partial<WorkspaceTreeInput> = {}): WorkspaceTreeInput {
  return {
    workspaceName: "Cove",
    activeRoomId: "r1",
    focusedPaneId: "p1",
    workspaceCollapsed: false,
    collapsedRoomIds: new Set<string>(),
    rooms: [
      { id: "r1", name: "shell", leaves: [{ paneId: "p1", paneType: "terminal", title: "" }] },
      {
        id: "r2",
        name: "split",
        leaves: [
          { paneId: "p2", paneType: "terminal", title: "bash" },
          { paneId: "p3", paneType: "git", title: "" },
        ],
      },
    ],
    ...overrides,
  };
}

describe("paneLabel", () => {
  it("prefers a non-empty title", () => {
    expect(paneLabel({ paneId: "p", paneType: "terminal", title: "vim" })).toBe("vim");
  });
  it("falls back to a friendly pane-type label", () => {
    expect(paneLabel({ paneId: "p", paneType: "git", title: "" })).toBe("source control");
  });
  it("falls back to the raw type for unknown kinds", () => {
    expect(paneLabel({ paneId: "p", paneType: "custom", title: "" })).toBe("custom");
  });
});

describe("buildWorkspaceTree", () => {
  it("emits a workspace root then rooms with pane children including single-pane rooms", () => {
    const rows = buildWorkspaceTree(baseInput());
    expect(rows.map((r) => r.kind)).toEqual(["workspace", "room", "pane", "room", "pane", "pane"]);
    const single = rows.find((r) => r.roomId === "r1" && r.kind === "room")!;
    expect(single.expandable).toBe(true);
    const multi = rows.find((r) => r.roomId === "r2" && r.kind === "room")!;
    expect(multi.expandable).toBe(true);
    expect(multi.count).toBe(2);
  });

  it("skips placeholder empty panes but keeps the room row", () => {
    const rows = buildWorkspaceTree(baseInput({ rooms: [{ id: "r9", name: "empty room", leaves: [{ paneId: "e1", paneType: "empty", title: "" }] }] }));
    expect(rows.map((r) => r.kind)).toEqual(["workspace", "room"]);
    expect(rows[1].expandable).toBe(false);
  });

  it("marks the active room and focused pane", () => {
    const rows = buildWorkspaceTree(baseInput());
    expect(rows.find((r) => r.roomId === "r1" && r.kind === "room")!.active).toBe(true);
    expect(rows.find((r) => r.paneId === "p1")!.active).toBe(true);
    expect(rows.filter((r) => r.kind === "pane" && r.paneId !== "p1").every((r) => r.active === false)).toBe(true);
  });

  it("hides room children when the room is collapsed", () => {
    const rows = buildWorkspaceTree(baseInput({ collapsedRoomIds: new Set(["r2"]) }));
    expect(rows.filter((r) => r.kind === "pane").map((r) => r.paneId)).toEqual(["p1"]);
    expect(rows.find((r) => r.roomId === "r2" && r.kind === "room")!.collapsed).toBe(true);
  });

  it("hides all rooms when the workspace is collapsed", () => {
    const rows = buildWorkspaceTree(baseInput({ workspaceCollapsed: true }));
    expect(rows).toHaveLength(1);
    expect(rows[0].kind).toBe("workspace");
    expect(rows[0].collapsed).toBe(true);
  });

  it("renders a lone workspace row with a zero count when it has no rooms", () => {
    const rows = buildWorkspaceTree(baseInput({ rooms: [] }));
    expect(rows).toHaveLength(1);
    expect(rows[0].kind).toBe("workspace");
    expect(rows[0].count).toBe(0);
    expect(rows[0].expandable).toBe(false);
  });
});

describe("workspaceTreeEmptyMessage", () => {
  it("returns the calm empty message when there are no workspaces", () => {
    expect(workspaceTreeEmptyMessage(0)).toBe(NO_WORKSPACES_MESSAGE);
  });
  it("returns null once at least one workspace exists", () => {
    expect(workspaceTreeEmptyMessage(1)).toBeNull();
    expect(workspaceTreeEmptyMessage(5)).toBeNull();
  });
});
