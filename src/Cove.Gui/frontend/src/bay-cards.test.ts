import { describe, expect, it } from "vitest";
import {
  BAY_ACCENTS,
  parseCollapsedCardIds,
  serializeCollapsedCardIds,
  toggleCardCollapsed,
  scmChipText,
  bayAccent,
  bayHeadNavigation,
  resolveActiveBayId,
  sortFsEntries,
  joinPath,
  dirBasename,
  mergeFsStatus,
  type BayCardEntry,
} from "./bay-cards";

const ws = (id: string, name = id, projectDir = `/tmp/${id}`): BayCardEntry => ({ id, name, projectDir });

describe("bayAccent", () => {
  it("returns a palette color deterministically", () => {
    const a = bayAccent("ws-abc");
    expect(BAY_ACCENTS).toContain(a);
    expect(bayAccent("ws-abc")).toBe(a);
  });

  it("spreads different ids across the palette", () => {
    const colors = new Set(["a", "b", "c", "d", "e", "f", "g", "h"].map((s) => bayAccent(s)));
    expect(colors.size).toBeGreaterThan(1);
  });
});

describe("resolveActiveBayId", () => {
  const items = [ws("one"), ws("two"), ws("three")];

  it("returns the matching id without reordering anything", () => {
    expect(resolveActiveBayId(items, "two")).toBe("two");
  });

  it("falls back to the first bay for null or unknown ids", () => {
    expect(resolveActiveBayId(items, null)).toBe("one");
    expect(resolveActiveBayId(items, "missing")).toBe("one");
  });

  it("returns null for an empty list", () => {
    expect(resolveActiveBayId([], "x")).toBeNull();
  });
});

describe("bayHeadNavigation", () => {
  it("opens the launcher without switching when the clicked bay is already active", () => {
    expect(bayHeadNavigation("one", "one")).toEqual({ switchRequired: false, showLauncher: true });
  });

  it("switches inactive bays before opening their launcher", () => {
    expect(bayHeadNavigation("one", "two")).toEqual({ switchRequired: true, showLauncher: true });
  });
});

describe("sortFsEntries", () => {
  it("puts directories first, alphabetical case-insensitive, dotfiles last within each group", () => {
    const sorted = sortFsEntries([
      { name: "zeta.ts", isDir: false },
      { name: ".git", isDir: true },
      { name: "src", isDir: true },
      { name: ".env", isDir: false },
      { name: "Alpha.md", isDir: false },
      { name: "Bin", isDir: true },
    ]);
    expect(sorted.map((e) => e.name)).toEqual(["Bin", "src", ".git", "Alpha.md", "zeta.ts", ".env"]);
  });
});

describe("path helpers", () => {
  it("joins paths without doubling slashes", () => {
    expect(joinPath("/a/b", "c")).toBe("/a/b/c");
    expect(joinPath("/a/b/", "c")).toBe("/a/b/c");
  });

  it("takes the last segment as basename ignoring trailing slash", () => {
    expect(dirBasename("/Users/moh/Desktop/Work/Cove")).toBe("Cove");
    expect(dirBasename("/Users/moh/Work/")).toBe("Work");
    expect(dirBasename("")).toBe("");
  });
});

describe("file status decoration", () => {
  const statuses = [
    { path: "src/modified.ts", status: "M" as const },
    { path: "src/added.ts", status: "A" as const },
    { path: "removed/deleted.ts", status: "D" as const },
  ];

  it("decorates existing files and adds deleted files missing from disk", () => {
    expect(mergeFsStatus([
      { name: "modified.ts", isDir: false },
      { name: "added.ts", isDir: false },
    ], "src", statuses)).toEqual([
      { name: "added.ts", isDir: false, status: "A" },
      { name: "modified.ts", isDir: false, status: "M" },
    ]);
  });

  it("synthesizes missing parent directories for deleted files", () => {
    expect(mergeFsStatus([], "", statuses)).toContainEqual({ name: "removed", isDir: true });
    expect(mergeFsStatus([], "removed", statuses)).toEqual([
      { name: "deleted.ts", isDir: false, status: "D" },
    ]);
  });
});

describe("scmChipText", () => {
  it("shows branch with ahead and behind arrows and dirty count", () => {
    expect(scmChipText({ ok: true, branch: "main", ahead: 2, behind: 1, dirty: 3 })).toBe("main ↑2 ↓1 ●3");
  });

  it("omits zero counts", () => {
    expect(scmChipText({ ok: true, branch: "main", ahead: 0, behind: 0, dirty: 0 })).toBe("main");
    expect(scmChipText({ ok: true, branch: "dev", ahead: 1, behind: 0, dirty: 0 })).toBe("dev ↑1");
  });

  it("returns empty for failed summaries", () => {
    expect(scmChipText({ ok: false, error: "not_a_repo" })).toBe("");
    expect(scmChipText({ ok: true })).toBe("");
  });
});

describe("bay card collapse state", () => {
  it("round-trips a set of collapsed ids through json", () => {
    const set = new Set(["ws-a", "ws-b"]);
    expect(parseCollapsedCardIds(serializeCollapsedCardIds(set))).toEqual(set);
  });

  it("returns an empty set for null, garbage, and non-array json", () => {
    expect(parseCollapsedCardIds(null)).toEqual(new Set());
    expect(parseCollapsedCardIds("not json")).toEqual(new Set());
    expect(parseCollapsedCardIds('{"a":1}')).toEqual(new Set());
    expect(parseCollapsedCardIds('[1,2]')).toEqual(new Set());
  });

  it("toggles an id in and out without mutating the input", () => {
    const start = new Set(["ws-a"]);
    const added = toggleCardCollapsed(start, "ws-b");
    expect(added).toEqual(new Set(["ws-a", "ws-b"]));
    const removed = toggleCardCollapsed(added, "ws-a");
    expect(removed).toEqual(new Set(["ws-b"]));
    expect(start).toEqual(new Set(["ws-a"]));
  });
});
