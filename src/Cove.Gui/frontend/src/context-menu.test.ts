import { describe, it, expect } from "vitest";
import {
  clampMenuPosition,
  normalizeItems,
  isSelectable,
  firstSelectableIndex,
  moveSelection,
  activeItem,
  type ContextMenuItem,
} from "./context-menu";

const viewport = { width: 1000, height: 800 };

describe("clampMenuPosition", () => {
  it("keeps the anchor position when the menu fits", () => {
    const p = clampMenuPosition({ x: 100, y: 120 }, { width: 200, height: 160 }, viewport);
    expect(p).toEqual({ x: 100, y: 120 });
  });
  it("flips horizontally when the menu overflows the right edge", () => {
    const p = clampMenuPosition({ x: 950, y: 100 }, { width: 200, height: 100 }, viewport);
    expect(p.x).toBe(750);
  });
  it("flips vertically when the menu overflows the bottom edge", () => {
    const p = clampMenuPosition({ x: 100, y: 780 }, { width: 200, height: 200 }, viewport);
    expect(p.y).toBe(580);
  });
  it("clamps into the viewport when even the flipped position overflows", () => {
    const p = clampMenuPosition({ x: 10, y: 10 }, { width: 400, height: 300 }, { width: 320, height: 240 }, 6);
    expect(p.x).toBe(6);
    expect(p.y).toBe(6);
    expect(p.x + 400).toBeGreaterThan(320);
  });
});

describe("normalizeItems", () => {
  it("drops leading and trailing separators and collapses runs", () => {
    const items: ContextMenuItem[] = [
      { id: "sep0", label: "", separator: true },
      { id: "a", label: "A" },
      { id: "sep1", label: "", separator: true },
      { id: "sep2", label: "", separator: true },
      { id: "b", label: "B" },
      { id: "sep3", label: "", separator: true },
    ];
    expect(normalizeItems(items).map((i) => i.id)).toEqual(["a", "sep1", "b"]);
  });
});

describe("isSelectable", () => {
  it("excludes separators and disabled items", () => {
    expect(isSelectable({ id: "a", label: "A" })).toBe(true);
    expect(isSelectable({ id: "s", label: "", separator: true })).toBe(false);
    expect(isSelectable({ id: "d", label: "D", disabled: true })).toBe(false);
  });
});

describe("firstSelectableIndex", () => {
  it("skips leading separators and disabled items", () => {
    const items: ContextMenuItem[] = [
      { id: "s", label: "", separator: true },
      { id: "d", label: "D", disabled: true },
      { id: "a", label: "A" },
    ];
    expect(firstSelectableIndex(items)).toBe(2);
  });
  it("returns -1 when nothing is selectable", () => {
    expect(firstSelectableIndex([{ id: "s", label: "", separator: true }])).toBe(-1);
  });
});

describe("moveSelection", () => {
  const items: ContextMenuItem[] = [
    { id: "a", label: "A" },
    { id: "sep", label: "", separator: true },
    { id: "b", label: "B", disabled: true },
    { id: "c", label: "C" },
  ];
  it("moves down skipping separators and disabled items", () => {
    expect(moveSelection(items, 0, 1)).toBe(3);
  });
  it("wraps around when moving past the end", () => {
    expect(moveSelection(items, 3, 1)).toBe(0);
  });
  it("selects the last selectable item when moving up from no selection", () => {
    expect(moveSelection(items, -1, -1)).toBe(3);
  });
  it("selects the first selectable item when moving down from no selection", () => {
    expect(moveSelection(items, -1, 1)).toBe(0);
  });
  it("returns -1 when nothing can be selected", () => {
    expect(moveSelection([{ id: "s", label: "", separator: true }], 0, 1)).toBe(-1);
  });
});

describe("activeItem", () => {
  const items: ContextMenuItem[] = [
    { id: "a", label: "A" },
    { id: "d", label: "D", disabled: true },
  ];
  it("returns the item when the index is selectable", () => {
    expect(activeItem(items, 0)?.id).toBe("a");
  });
  it("returns null for disabled or out-of-range indices", () => {
    expect(activeItem(items, 1)).toBeNull();
    expect(activeItem(items, 9)).toBeNull();
    expect(activeItem(items, -1)).toBeNull();
  });
});
