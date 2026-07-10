import { describe, it, expect } from "vitest";
import {
  partitionPinned,
  togglePin,
  reorderRoom,
  closeAllPreservePinned,
  buildMiniDiagram,
  accentForPaneType,
  glyphForPaneType,
  visibleRoomIds,
  switchWing,
  WingSwitcherState,
  toggleWingSwitcher,
  buildWingModel,
  filterRoomsByWing,
  type TabRoom,
  type MiniDiagramNode,
  type WingModel,
} from "./room-tabs";

describe("glyphForPaneType", () => {
  it("maps known pane types to glyphs and shares one across same-type rooms", () => {
    expect(glyphForPaneType("terminal")).toBe("▌");
    expect(glyphForPaneType("browser")).toBe("◑");
    expect(glyphForPaneType("git")).toBe(glyphForPaneType("source-control"));
  });
  it("falls back to the terminal glyph for unknown types", () => {
    expect(glyphForPaneType("mystery")).toBe("▌");
  });
});

const mkRoom = (id: string, pinned = false): TabRoom => ({ id, name: id, pinned });

describe("partitionPinned", () => {
  it("separates pinned and unpinned rooms", () => {
    const rooms = [mkRoom("a"), mkRoom("b", true), mkRoom("c"), mkRoom("d", true)];
    const r = partitionPinned(rooms);
    expect(r.pinned).toEqual(["b", "d"]);
    expect(r.unpinned).toEqual(["a", "c"]);
  });
  it("handles all pinned", () => {
    const r = partitionPinned([mkRoom("a", true), mkRoom("b", true)]);
    expect(r.pinned).toEqual(["a", "b"]);
    expect(r.unpinned).toEqual([]);
  });
  it("handles none pinned", () => {
    const r = partitionPinned([mkRoom("a"), mkRoom("b")]);
    expect(r.pinned).toEqual([]);
    expect(r.unpinned).toEqual(["a", "b"]);
  });
});

describe("togglePin", () => {
  it("pins an unpinned room", () => {
    expect(togglePin([mkRoom("a"), mkRoom("b")], "a")[0].pinned).toBe(true);
  });
  it("unpins a pinned room", () => {
    expect(togglePin([mkRoom("a", true), mkRoom("b")], "a")[0].pinned).toBe(false);
  });
  it("only affects the target room", () => {
    expect(togglePin([mkRoom("a", true), mkRoom("b", true), mkRoom("c", true)], "b").map((x) => x.pinned)).toEqual([true, false, true]);
  });
});

describe("reorderRoom", () => {
  it("moves room forward", () => {
    expect(reorderRoom([mkRoom("a"), mkRoom("b"), mkRoom("c"), mkRoom("d")], 1, 3).map((x) => x.id)).toEqual(["a", "c", "d", "b"]);
  });
  it("moves room backward", () => {
    expect(reorderRoom([mkRoom("a"), mkRoom("b"), mkRoom("c"), mkRoom("d")], 3, 0).map((x) => x.id)).toEqual(["d", "a", "b", "c"]);
  });
  it("no-ops on same index", () => {
    expect(reorderRoom([mkRoom("a"), mkRoom("b")], 0, 0).map((x) => x.id)).toEqual(["a", "b"]);
  });
  it("no-ops on out-of-bounds", () => {
    expect(reorderRoom([mkRoom("a"), mkRoom("b")], -1, 0).map((x) => x.id)).toEqual(["a", "b"]);
    expect(reorderRoom([mkRoom("a"), mkRoom("b")], 0, 5).map((x) => x.id)).toEqual(["a", "b"]);
  });
});

describe("closeAllPreservePinned", () => {
  it("removes unpinned rooms keeping pinned", () => {
    expect(closeAllPreservePinned([mkRoom("a"), mkRoom("b", true), mkRoom("c"), mkRoom("d", true)]).map((x) => x.id)).toEqual(["b", "d"]);
  });
  it("returns empty when none pinned", () => {
    expect(closeAllPreservePinned([mkRoom("a"), mkRoom("b")])).toEqual([]);
  });
});

describe("accentForPaneType", () => {
  it("returns accent for known pane type", () => {
    expect(accentForPaneType("terminal")).toBe("#4cc2d6");
    expect(accentForPaneType("browser")).toBe("#5b9bd5");
  });
  it("returns neutral for unknown pane type", () => {
    expect(accentForPaneType("unknown")).toBe("#6b7280");
  });
  it("returns neutral for empty", () => {
    expect(accentForPaneType("")).toBe("#6b7280");
  });
});

describe("buildMiniDiagram", () => {
  it("single leaf fills the rect", () => {
    const cells = buildMiniDiagram({ kind: "leaf", paneType: "terminal" }, { x: 0, y: 0, w: 10, h: 10 });
    expect(cells).toHaveLength(1);
    expect(cells[0]).toEqual({ x: 0, y: 0, w: 10, h: 10, accent: "#4cc2d6" });
  });
  it("row split divides horizontally", () => {
    const node: MiniDiagramNode = { kind: "split", orientation: "row", ratio: 0.5, childA: { kind: "leaf", paneType: "terminal" }, childB: { kind: "leaf", paneType: "browser" } };
    const cells = buildMiniDiagram(node, { x: 0, y: 0, w: 10, h: 10 });
    expect(cells).toHaveLength(2);
    expect(cells[0].w + cells[1].w).toBe(10);
    expect(cells[1].x).toBe(cells[0].w);
  });
  it("column split divides vertically", () => {
    const node: MiniDiagramNode = { kind: "split", orientation: "col", ratio: 0.5, childA: { kind: "leaf", paneType: "terminal" }, childB: { kind: "leaf", paneType: "editor" } };
    const cells = buildMiniDiagram(node, { x: 0, y: 0, w: 10, h: 10 });
    expect(cells).toHaveLength(2);
    expect(cells[0].h + cells[1].h).toBe(10);
    expect(cells[1].y).toBe(cells[0].h);
  });
  it("uses numeric orientation 1 for row", () => {
    const node: MiniDiagramNode = { kind: "split", orientation: 1, ratio: 0.5, childA: { kind: "leaf", paneType: "terminal" }, childB: { kind: "leaf", paneType: "browser" } };
    const cells = buildMiniDiagram(node, { x: 0, y: 0, w: 10, h: 10 });
    expect(cells[0].w).toBeLessThan(10);
    expect(cells[1].x).toBeGreaterThan(0);
  });
  it("nested splits subdivide correctly", () => {
    const node: MiniDiagramNode = {
      kind: "split", orientation: "row", ratio: 0.5,
      childA: { kind: "leaf", paneType: "terminal" },
      childB: { kind: "split", orientation: "col", ratio: 0.5, childA: { kind: "leaf", paneType: "browser" }, childB: { kind: "leaf", paneType: "editor" } },
    };
    const cells = buildMiniDiagram(node, { x: 0, y: 0, w: 10, h: 10 });
    expect(cells).toHaveLength(3);
    const browserCell = cells.find((c) => c.accent === "#5b9bd5")!;
    const editorCell = cells.find((c) => c.accent === "#c5c8c6")!;
    expect(browserCell.x).toBe(editorCell.x);
    expect(browserCell.y + browserCell.h).toBe(editorCell.y);
  });
  it("defaults ratio to 0.5 when undefined", () => {
    const node: MiniDiagramNode = { kind: "split", orientation: "row", childA: { kind: "leaf", paneType: "terminal" }, childB: { kind: "leaf", paneType: "browser" } };
    const cells = buildMiniDiagram(node, { x: 0, y: 0, w: 10, h: 10 });
    expect(cells[0].w).toBe(5);
    expect(cells[1].w).toBe(5);
  });
});

describe("wing model", () => {
  const wings: WingModel = {
    wings: [
      { id: "main", name: "Main", roomIds: ["r1", "r2"] },
      { id: "research", name: "Research", roomIds: ["r3"] },
    ],
    activeWingId: "main",
  };
  it("visibleRoomIds returns active wing's rooms", () => {
    expect(visibleRoomIds(wings)).toEqual(["r1", "r2"]);
  });
  it("visibleRoomIds returns empty when no active wing", () => {
    expect(visibleRoomIds({ ...wings, activeWingId: null })).toEqual([]);
  });
  it("switchWing changes active wing", () => {
    expect(visibleRoomIds(switchWing(wings, "research"))).toEqual(["r3"]);
  });
});

describe("buildWingModel", () => {
  it("groups rooms by wing from summaries", () => {
    const wings = [{ id: "main", name: "Main" }, { id: "research", name: "Research" }];
    const rooms = [
      { id: "r1", wingId: "main", pinned: false },
      { id: "r2", wingId: "main", pinned: true },
      { id: "r3", wingId: "research", pinned: false },
    ];
    const model = buildWingModel(wings, rooms, "main");
    expect(model.wings[0].roomIds).toEqual(["r1", "r2"]);
    expect(model.wings[1].roomIds).toEqual(["r3"]);
    expect(model.activeWingId).toBe("main");
  });
  it("defaults activeWingId to first wing when null", () => {
    const model = buildWingModel([{ id: "main", name: "Main" }], [], null);
    expect(model.activeWingId).toBe("main");
  });
  it("handles rooms in wings with no declared wing list entry", () => {
    const model = buildWingModel([{ id: "main", name: "Main" }], [{ id: "r1", wingId: "ghost", pinned: false }], "main");
    expect(model.wings[0].roomIds).toEqual([]);
  });
});

describe("filterRoomsByWing", () => {
  const rooms = [{ id: "r1" }, { id: "r2" }, { id: "r3" }];
  it("returns only rooms whose id is in visibleIds", () => {
    expect(filterRoomsByWing(rooms, ["r1", "r3"])).toEqual([{ id: "r1" }, { id: "r3" }]);
  });
  it("returns empty when visibleIds is empty", () => {
    expect(filterRoomsByWing(rooms, [])).toEqual([]);
  });
  it("preserves order of input rooms", () => {
    expect(filterRoomsByWing(rooms, ["r3", "r1"])).toEqual([{ id: "r1" }, { id: "r3" }]);
  });
});

describe("wing switcher toggle", () => {
  it("toggles collapsed to expanded", () => {
    expect(toggleWingSwitcher(WingSwitcherState.Collapsed)).toBe(WingSwitcherState.Expanded);
  });
  it("toggles expanded to collapsed", () => {
    expect(toggleWingSwitcher(WingSwitcherState.Expanded)).toBe(WingSwitcherState.Collapsed);
  });
});
