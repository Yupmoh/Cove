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
  it("emits a workspace root then rooms, expanding multi-pane rooms only", () => {
    const rows = buildWorkspaceTree(baseInput());
    expect(rows.map((r) => r.kind)).toEqual(["workspace", "room", "room", "pane", "pane"]);
    const single = rows.find((r) => r.roomId === "r1" && r.kind === "room")!;
    expect(single.expandable).toBe(false);
    const multi = rows.find((r) => r.roomId === "r2" && r.kind === "room")!;
    expect(multi.expandable).toBe(true);
    expect(multi.count).toBe(2);
  });

  it("marks the active room and focused pane", () => {
    const rows = buildWorkspaceTree(baseInput());
    expect(rows.find((r) => r.roomId === "r1" && r.kind === "room")!.active).toBe(true);
    const focused = rows.filter((r) => r.kind === "pane");
    expect(focused.every((r) => r.active === false)).toBe(true);
  });

  it("hides room children when the room is collapsed", () => {
    const rows = buildWorkspaceTree(baseInput({ collapsedRoomIds: new Set(["r2"]) }));
    expect(rows.some((r) => r.kind === "pane")).toBe(false);
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
