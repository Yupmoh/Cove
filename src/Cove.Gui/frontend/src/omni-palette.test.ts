import { describe, it, expect } from "vitest";
import {
  parseQuery,
  fuzzyMatch,
  fuzzyScore,
  filterAndSort,
  MruTracker,
  cycleCategory,
  categoryLabel,
  type PaletteItem,
  type PaletteCategory,
} from "./omni-palette";

describe("parseQuery", () => {
  it("returns all category for empty input", () => {
    expect(parseQuery("")).toEqual({ category: "all", text: "" });
  });
  it("parses > prefix as commands", () => {
    expect(parseQuery(">split")).toEqual({ category: "commands", text: "split" });
  });
  it("parses ~ prefix as bays", () => {
    expect(parseQuery("~myws")).toEqual({ category: "bays", text: "myws" });
  });
  it("parses @ prefix as shores", () => {
    expect(parseQuery("@shore1")).toEqual({ category: "shores", text: "shore1" });
  });
  it("parses $ prefix as nooks", () => {
    expect(parseQuery("$nook")).toEqual({ category: "nooks", text: "nook" });
  });
  it("parses # prefix as tasks", () => {
    expect(parseQuery("#task")).toEqual({ category: "tasks", text: "task" });
  });
  it("parses / prefix as files", () => {
    expect(parseQuery("/readme")).toEqual({ category: "files", text: "readme" });
  });
  it("returns all for non-prefix input", () => {
    expect(parseQuery("split")).toEqual({ category: "all", text: "split" });
  });
  it("trims leading whitespace before checking prefix", () => {
    expect(parseQuery("  >cmd")).toEqual({ category: "commands", text: "cmd" });
  });
});

describe("fuzzyMatch", () => {
  it("matches empty query", () => {
    expect(fuzzyMatch("", "anything")).toBe(true);
  });
  it("matches exact substring", () => {
    expect(fuzzyMatch("split", "Split right")).toBe(true);
  });
  it("matches non-contiguous chars", () => {
    expect(fuzzyMatch("sr", "Split right")).toBe(true);
  });
  it("does not match missing chars", () => {
    expect(fuzzyMatch("xyz", "Split right")).toBe(false);
  });
  it("is case insensitive", () => {
    expect(fuzzyMatch("SPLIT", "split right")).toBe(true);
  });
});

describe("fuzzyScore", () => {
  it("scores exact match highest", () => {
    expect(fuzzyScore("split", "split")).toBe(100);
  });
  it("scores prefix match high", () => {
    expect(fuzzyScore("split", "split right")).toBe(80);
  });
  it("scores non-contiguous lower", () => {
    expect(fuzzyScore("sr", "split right")).toBeLessThan(80);
  });
  it("returns 0 for no match", () => {
    expect(fuzzyScore("xyz", "split")).toBe(0);
  });
  it("returns 1 for empty query", () => {
    expect(fuzzyScore("", "anything")).toBe(1);
  });
});

describe("filterAndSort", () => {
  const items: PaletteItem[] = [
    { id: "1", label: "Split right", category: "commands", icon: "|", run: () => {} },
    { id: "2", label: "Split down", category: "commands", icon: "-", run: () => {} },
    { id: "3", label: "Shore 1", category: "shores", icon: ">", run: () => {} },
    { id: "4", label: "Task A", category: "tasks", icon: "#", run: () => {} },
  ];

  it("filters all categories by text", () => {
    const r = filterAndSort(items, { category: "all", text: "split" });
    expect(r).toHaveLength(2);
    expect(r.map((i) => i.id)).toEqual(["1", "2"]);
  });
  it("filters by category", () => {
    const r = filterAndSort(items, { category: "shores", text: "" });
    expect(r).toHaveLength(1);
    expect(r[0].id).toBe("3");
  });
  it("filters by category and text", () => {
    const r = filterAndSort(items, { category: "commands", text: "down" });
    expect(r).toHaveLength(1);
    expect(r[0].id).toBe("2");
  });
  it("returns empty for no matches", () => {
    expect(filterAndSort(items, { category: "all", text: "xyz" })).toEqual([]);
  });
  it("sorts by fuzzy score descending", () => {
    const r = filterAndSort(items, { category: "all", text: "split" });
    expect(r[0].label).toBe("Split right");
  });
});

describe("MruTracker", () => {
  it("records and sorts by recency", () => {
    const mru = new MruTracker();
    mru.record("a");
    mru.record("b");
    mru.record("c");
    const sorted = mru.sortByIds(["a", "b", "c", "d"]);
    expect(sorted[0]).toBe("c");
    expect(sorted[1]).toBe("b");
    expect(sorted[2]).toBe("a");
    expect(sorted[3]).toBe("d");
  });
  it("re-recording moves to most recent", () => {
    const mru = new MruTracker();
    mru.record("a");
    mru.record("b");
    mru.record("a");
    const sorted = mru.sortByIds(["a", "b"]);
    expect(sorted[0]).toBe("a");
    expect(sorted[1]).toBe("b");
  });
  it("caps at 50 entries", () => {
    const mru = new MruTracker();
    for (let i = 0; i < 60; i++) mru.record(`item-${i}`);
    expect(mru.toList().length).toBe(50);
  });
  it("restores from stored entries", () => {
    const stored = [{ id: "x", timestamp: 1 }, { id: "y", timestamp: 2 }];
    const mru = new MruTracker(stored);
    const sorted = mru.sortByIds(["x", "y"]);
    expect(sorted[0]).toBe("y");
  });
});

describe("cycleCategory", () => {
  it("cycles forward", () => {
    expect(cycleCategory("all", 1)).toBe("commands");
    expect(cycleCategory("commands", 1)).toBe("shores");
  });
  it("cycles backward", () => {
    expect(cycleCategory("commands", -1)).toBe("all");
    expect(cycleCategory("all", -1)).toBe("bays");
  });
  it("wraps around", () => {
    expect(cycleCategory("bays", 1)).toBe("all");
    expect(cycleCategory("all", -1)).toBe("bays");
  });
});

describe("categoryLabel", () => {
  it("returns label for each category", () => {
    expect(categoryLabel("all")).toBe("All");
    expect(categoryLabel("commands")).toBe("Commands");
    expect(categoryLabel("shores")).toBe("Shores");
  });
});
