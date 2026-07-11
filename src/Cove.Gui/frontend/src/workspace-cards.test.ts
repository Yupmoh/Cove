import { describe, expect, it } from "vitest";
import {
  WORKSPACE_ACCENTS,
  parseCollapsedCardIds,
  serializeCollapsedCardIds,
  toggleCardCollapsed,
  scmChipText,
  workspaceAccent,
  resolveActiveWorkspaceId,
  sortFsEntries,
  joinPath,
  dirBasename,
  type WorkspaceCardEntry,
} from "./workspace-cards";

const ws = (id: string, name = id, projectDir = `/tmp/${id}`): WorkspaceCardEntry => ({ id, name, projectDir });

describe("workspaceAccent", () => {
  it("returns a palette color deterministically", () => {
    const a = workspaceAccent("ws-abc");
    expect(WORKSPACE_ACCENTS).toContain(a);
    expect(workspaceAccent("ws-abc")).toBe(a);
  });

  it("spreads different ids across the palette", () => {
    const colors = new Set(["a", "b", "c", "d", "e", "f", "g", "h"].map((s) => workspaceAccent(s)));
    expect(colors.size).toBeGreaterThan(1);
  });
});

describe("resolveActiveWorkspaceId", () => {
  const items = [ws("one"), ws("two"), ws("three")];

  it("returns the matching id without reordering anything", () => {
    expect(resolveActiveWorkspaceId(items, "two")).toBe("two");
  });

  it("falls back to the first workspace for null or unknown ids", () => {
    expect(resolveActiveWorkspaceId(items, null)).toBe("one");
    expect(resolveActiveWorkspaceId(items, "missing")).toBe("one");
  });

  it("returns null for an empty list", () => {
    expect(resolveActiveWorkspaceId([], "x")).toBeNull();
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

describe("workspace card collapse state", () => {
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
