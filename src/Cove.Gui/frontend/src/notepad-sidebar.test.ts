import { describe, it, expect } from "vitest";
import {
  groupByWorkspace,
  moveSelection,
  flattenNotes,
  selectedNote,
  kindIcon,
  kindColor,
  type NoteListItem,
} from "./notepad-sidebar";

const mkNote = (id: string, ws: string, kind = "markdown", updatedAt = "2026-01-01"): NoteListItem => ({
  id, title: id, workspaceId: ws, kind, updatedAt,
});

describe("groupByWorkspace", () => {
  it("groups notes by workspace", () => {
    const notes = [mkNote("a", "ws1"), mkNote("b", "ws2"), mkNote("c", "ws1")];
    const groups = groupByWorkspace(notes, { ws1: "Project 1", ws2: "Project 2" });
    expect(groups).toHaveLength(2);
    expect(groups[0].workspaceName).toBe("Project 1");
    expect(groups[0].notes).toHaveLength(2);
    expect(groups[1].notes).toHaveLength(1);
  });

  it("sorts groups by workspace name", () => {
    const notes = [mkNote("a", "zeta"), mkNote("b", "alpha")];
    const groups = groupByWorkspace(notes, { zeta: "Zeta", alpha: "Alpha" });
    expect(groups[0].workspaceName).toBe("Alpha");
    expect(groups[1].workspaceName).toBe("Zeta");
  });

  it("sorts notes by updatedAt descending", () => {
    const notes = [mkNote("old", "ws", "markdown", "2026-01-01"), mkNote("new", "ws", "markdown", "2026-06-01")];
    const groups = groupByWorkspace(notes, { ws: "WS" });
    expect(groups[0].notes[0].id).toBe("new");
    expect(groups[0].notes[1].id).toBe("old");
  });

  it("uses workspaceId as name when name missing", () => {
    const groups = groupByWorkspace([mkNote("a", "unknown")], {});
    expect(groups[0].workspaceName).toBe("unknown");
  });

  it("handles empty notes", () => {
    expect(groupByWorkspace([], {})).toEqual([]);
  });
});

describe("moveSelection", () => {
  const groups = [
    { workspaceId: "ws1", workspaceName: "WS1", notes: [mkNote("a", "ws1"), mkNote("b", "ws1")] },
    { workspaceId: "ws2", workspaceName: "WS2", notes: [mkNote("c", "ws2")] },
  ];

  it("moves down within a group", () => {
    const r = moveSelection(groups, { groupIdx: 0, noteIdx: 0 }, "down");
    expect(r).toEqual({ groupIdx: 0, noteIdx: 1 });
  });

  it("moves down to next group", () => {
    const r = moveSelection(groups, { groupIdx: 0, noteIdx: 1 }, "down");
    expect(r).toEqual({ groupIdx: 1, noteIdx: 0 });
  });

  it("stays at last note when at end", () => {
    const r = moveSelection(groups, { groupIdx: 1, noteIdx: 0 }, "down");
    expect(r).toEqual({ groupIdx: 1, noteIdx: 0 });
  });

  it("moves up within a group", () => {
    const r = moveSelection(groups, { groupIdx: 0, noteIdx: 1 }, "up");
    expect(r).toEqual({ groupIdx: 0, noteIdx: 0 });
  });

  it("moves up to previous group last note", () => {
    const r = moveSelection(groups, { groupIdx: 1, noteIdx: 0 }, "up");
    expect(r).toEqual({ groupIdx: 0, noteIdx: 1 });
  });

  it("stays at first note when at start", () => {
    const r = moveSelection(groups, { groupIdx: 0, noteIdx: 0 }, "up");
    expect(r).toEqual({ groupIdx: 0, noteIdx: 0 });
  });

  it("initializes to first note from invalid state", () => {
    const r = moveSelection(groups, { groupIdx: -1, noteIdx: -1 }, "down");
    expect(r).toEqual({ groupIdx: 0, noteIdx: 0 });
  });

  it("handles empty groups", () => {
    expect(moveSelection([], { groupIdx: 0, noteIdx: 0 }, "down")).toEqual({ groupIdx: -1, noteIdx: -1 });
  });
});

describe("flattenNotes", () => {
  it("flattens all notes from groups", () => {
    const groups = [
      { workspaceId: "ws1", workspaceName: "WS1", notes: [mkNote("a", "ws1"), mkNote("b", "ws1")] },
      { workspaceId: "ws2", workspaceName: "WS2", notes: [mkNote("c", "ws2")] },
    ];
    expect(flattenNotes(groups).map((n) => n.id)).toEqual(["a", "b", "c"]);
  });
});

describe("selectedNote", () => {
  const groups = [
    { workspaceId: "ws1", workspaceName: "WS1", notes: [mkNote("a", "ws1"), mkNote("b", "ws1")] },
  ];

  it("returns the selected note", () => {
    expect(selectedNote(groups, { groupIdx: 0, noteIdx: 1 })?.id).toBe("b");
  });

  it("returns null for invalid group", () => {
    expect(selectedNote(groups, { groupIdx: 5, noteIdx: 0 })).toBeNull();
  });

  it("returns null for invalid note", () => {
    expect(selectedNote(groups, { groupIdx: 0, noteIdx: 5 })).toBeNull();
  });

  it("returns null for negative indices", () => {
    expect(selectedNote(groups, { groupIdx: -1, noteIdx: -1 })).toBeNull();
  });
});

describe("kindIcon/kindColor", () => {
  it("returns icon for known kind", () => {
    expect(kindIcon("markdown")).toBe("\u270e");
    expect(kindIcon("sketch")).toBe("\u270f");
  });

  it("returns default icon for unknown kind", () => {
    expect(kindIcon("unknown")).toBe("\u270e");
  });

  it("returns color for known kind", () => {
    expect(kindColor("markdown")).toBe("#e0af68");
  });

  it("returns neutral color for unknown kind", () => {
    expect(kindColor("unknown")).toBe("#6b7280");
  });
});
