import { describe, expect, it } from "vitest";
import {
  WORKSPACE_ACCENTS,
  workspaceAccent,
  splitWorkspaceCards,
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

describe("splitWorkspaceCards", () => {
  it("picks the active workspace by id and keeps the rest in order", () => {
    const items = [ws("one"), ws("two"), ws("three")];
    const r = splitWorkspaceCards(items, "two");
    expect(r.active?.id).toBe("two");
    expect(r.others.map((w) => w.id)).toEqual(["one", "three"]);
  });

  it("falls back to the first workspace when activeId is null or unknown", () => {
    const items = [ws("one"), ws("two")];
    expect(splitWorkspaceCards(items, null).active?.id).toBe("one");
    expect(splitWorkspaceCards(items, "missing").active?.id).toBe("one");
  });

  it("returns null active and no others for an empty list", () => {
    const r = splitWorkspaceCards([], "x");
    expect(r.active).toBeNull();
    expect(r.others).toEqual([]);
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
