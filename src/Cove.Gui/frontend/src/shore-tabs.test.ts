import { describe, it, expect } from "vitest";
import {
  partitionPinned,
  togglePin,
  reorderShore,
  closeAllPreservePinned,
  buildMiniDiagram,
  accentForNookType,
  glyphForNookType,
  visibleShoreIds,
  switchWing,
  WingSwitcherState,
  toggleWingSwitcher,
  buildWingModel,
  filterShoresByWing,
  type TabShore,
  type MiniDiagramNode,
  type WingModel,
} from "./shore-tabs";

describe("glyphForNookType", () => {
  it("maps known nook types to glyphs and shares one across same-type shores", () => {
    expect(glyphForNookType("terminal")).toBe("▌");
    expect(glyphForNookType("browser")).toBe("◑");
    expect(glyphForNookType("git")).toBe(glyphForNookType("source-control"));
  });
  it("falls back to the terminal glyph for unknown types", () => {
    expect(glyphForNookType("mystery")).toBe("▌");
  });
});

const mkShore = (id: string, pinned = false): TabShore => ({ id, name: id, pinned });

describe("partitionPinned", () => {
  it("separates pinned and unpinned shores", () => {
    const shores = [mkShore("a"), mkShore("b", true), mkShore("c"), mkShore("d", true)];
    const r = partitionPinned(shores);
    expect(r.pinned).toEqual(["b", "d"]);
    expect(r.unpinned).toEqual(["a", "c"]);
  });
  it("handles all pinned", () => {
    const r = partitionPinned([mkShore("a", true), mkShore("b", true)]);
    expect(r.pinned).toEqual(["a", "b"]);
    expect(r.unpinned).toEqual([]);
  });
  it("handles none pinned", () => {
    const r = partitionPinned([mkShore("a"), mkShore("b")]);
    expect(r.pinned).toEqual([]);
    expect(r.unpinned).toEqual(["a", "b"]);
  });
});

describe("togglePin", () => {
  it("pins an unpinned shore", () => {
    expect(togglePin([mkShore("a"), mkShore("b")], "a")[0].pinned).toBe(true);
  });
  it("unpins a pinned shore", () => {
    expect(togglePin([mkShore("a", true), mkShore("b")], "a")[0].pinned).toBe(false);
  });
  it("only affects the target shore", () => {
    expect(togglePin([mkShore("a", true), mkShore("b", true), mkShore("c", true)], "b").map((x) => x.pinned)).toEqual([true, false, true]);
  });
});

describe("reorderShore", () => {
  it("moves shore forward", () => {
    expect(reorderShore([mkShore("a"), mkShore("b"), mkShore("c"), mkShore("d")], 1, 3).map((x) => x.id)).toEqual(["a", "c", "d", "b"]);
  });
  it("moves shore backward", () => {
    expect(reorderShore([mkShore("a"), mkShore("b"), mkShore("c"), mkShore("d")], 3, 0).map((x) => x.id)).toEqual(["d", "a", "b", "c"]);
  });
  it("no-ops on same index", () => {
    expect(reorderShore([mkShore("a"), mkShore("b")], 0, 0).map((x) => x.id)).toEqual(["a", "b"]);
  });
  it("no-ops on out-of-bounds", () => {
    expect(reorderShore([mkShore("a"), mkShore("b")], -1, 0).map((x) => x.id)).toEqual(["a", "b"]);
    expect(reorderShore([mkShore("a"), mkShore("b")], 0, 5).map((x) => x.id)).toEqual(["a", "b"]);
  });
});

describe("closeAllPreservePinned", () => {
  it("removes unpinned shores keeping pinned", () => {
    expect(closeAllPreservePinned([mkShore("a"), mkShore("b", true), mkShore("c"), mkShore("d", true)]).map((x) => x.id)).toEqual(["b", "d"]);
  });
  it("returns empty when none pinned", () => {
    expect(closeAllPreservePinned([mkShore("a"), mkShore("b")])).toEqual([]);
  });
});

describe("accentForNookType", () => {
  it("returns accent for known nook type", () => {
    expect(accentForNookType("terminal")).toBe("#4cc2d6");
    expect(accentForNookType("browser")).toBe("#5b9bd5");
  });
  it("returns neutral for unknown nook type", () => {
    expect(accentForNookType("unknown")).toBe("#6b7280");
  });
  it("returns neutral for empty", () => {
    expect(accentForNookType("")).toBe("#6b7280");
  });
});

describe("buildMiniDiagram", () => {
  it("single leaf fills the rect", () => {
    const cells = buildMiniDiagram({ kind: "leaf", nookType: "terminal" }, { x: 0, y: 0, w: 10, h: 10 });
    expect(cells).toHaveLength(1);
    expect(cells[0]).toEqual({ x: 0, y: 0, w: 10, h: 10, accent: "#4cc2d6" });
  });
  it("row split divides horizontally", () => {
    const node: MiniDiagramNode = { kind: "split", orientation: "row", ratio: 0.5, childA: { kind: "leaf", nookType: "terminal" }, childB: { kind: "leaf", nookType: "browser" } };
    const cells = buildMiniDiagram(node, { x: 0, y: 0, w: 10, h: 10 });
    expect(cells).toHaveLength(2);
    expect(cells[0].w + cells[1].w).toBe(10);
    expect(cells[1].x).toBe(cells[0].w);
  });
  it("column split divides vertically", () => {
    const node: MiniDiagramNode = { kind: "split", orientation: "col", ratio: 0.5, childA: { kind: "leaf", nookType: "terminal" }, childB: { kind: "leaf", nookType: "editor" } };
    const cells = buildMiniDiagram(node, { x: 0, y: 0, w: 10, h: 10 });
    expect(cells).toHaveLength(2);
    expect(cells[0].h + cells[1].h).toBe(10);
    expect(cells[1].y).toBe(cells[0].h);
  });
  it("uses numeric orientation 1 for row", () => {
    const node: MiniDiagramNode = { kind: "split", orientation: 1, ratio: 0.5, childA: { kind: "leaf", nookType: "terminal" }, childB: { kind: "leaf", nookType: "browser" } };
    const cells = buildMiniDiagram(node, { x: 0, y: 0, w: 10, h: 10 });
    expect(cells[0].w).toBeLessThan(10);
    expect(cells[1].x).toBeGreaterThan(0);
  });
  it("nested splits subdivide correctly", () => {
    const node: MiniDiagramNode = {
      kind: "split", orientation: "row", ratio: 0.5,
      childA: { kind: "leaf", nookType: "terminal" },
      childB: { kind: "split", orientation: "col", ratio: 0.5, childA: { kind: "leaf", nookType: "browser" }, childB: { kind: "leaf", nookType: "editor" } },
    };
    const cells = buildMiniDiagram(node, { x: 0, y: 0, w: 10, h: 10 });
    expect(cells).toHaveLength(3);
    const browserCell = cells.find((c) => c.accent === "#5b9bd5")!;
    const editorCell = cells.find((c) => c.accent === "#c5c8c6")!;
    expect(browserCell.x).toBe(editorCell.x);
    expect(browserCell.y + browserCell.h).toBe(editorCell.y);
  });
  it("defaults ratio to 0.5 when undefined", () => {
    const node: MiniDiagramNode = { kind: "split", orientation: "row", childA: { kind: "leaf", nookType: "terminal" }, childB: { kind: "leaf", nookType: "browser" } };
    const cells = buildMiniDiagram(node, { x: 0, y: 0, w: 10, h: 10 });
    expect(cells[0].w).toBe(5);
    expect(cells[1].w).toBe(5);
  });
});

describe("wing model", () => {
  const wings: WingModel = {
    wings: [
      { id: "main", name: "Main", shoreIds: ["r1", "r2"] },
      { id: "research", name: "Research", shoreIds: ["r3"] },
    ],
    activeWingId: "main",
  };
  it("visibleShoreIds returns active wing's shores", () => {
    expect(visibleShoreIds(wings)).toEqual(["r1", "r2"]);
  });
  it("visibleShoreIds returns empty when no active wing", () => {
    expect(visibleShoreIds({ ...wings, activeWingId: null })).toEqual([]);
  });
  it("switchWing changes active wing", () => {
    expect(visibleShoreIds(switchWing(wings, "research"))).toEqual(["r3"]);
  });
});

describe("buildWingModel", () => {
  it("groups shores by wing from summaries", () => {
    const wings = [{ id: "main", name: "Main" }, { id: "research", name: "Research" }];
    const shores = [
      { id: "r1", wingId: "main", pinned: false },
      { id: "r2", wingId: "main", pinned: true },
      { id: "r3", wingId: "research", pinned: false },
    ];
    const model = buildWingModel(wings, shores, "main");
    expect(model.wings[0].shoreIds).toEqual(["r1", "r2"]);
    expect(model.wings[1].shoreIds).toEqual(["r3"]);
    expect(model.activeWingId).toBe("main");
  });
  it("defaults activeWingId to first wing when null", () => {
    const model = buildWingModel([{ id: "main", name: "Main" }], [], null);
    expect(model.activeWingId).toBe("main");
  });
  it("handles shores in wings with no declared wing list entry", () => {
    const model = buildWingModel([{ id: "main", name: "Main" }], [{ id: "r1", wingId: "ghost", pinned: false }], "main");
    expect(model.wings[0].shoreIds).toEqual([]);
  });
});

describe("filterShoresByWing", () => {
  const shores = [{ id: "r1" }, { id: "r2" }, { id: "r3" }];
  it("returns only shores whose id is in visibleIds", () => {
    expect(filterShoresByWing(shores, ["r1", "r3"])).toEqual([{ id: "r1" }, { id: "r3" }]);
  });
  it("returns empty when visibleIds is empty", () => {
    expect(filterShoresByWing(shores, [])).toEqual([]);
  });
  it("preserves order of input shores", () => {
    expect(filterShoresByWing(shores, ["r3", "r1"])).toEqual([{ id: "r1" }, { id: "r3" }]);
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
