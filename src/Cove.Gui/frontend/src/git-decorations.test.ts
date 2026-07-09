import { describe, it, expect } from "vitest";
import { parseDiffDecorations, blameForLine, formatBlameHover } from "./git-decorations";

describe("parseDiffDecorations", () => {
  it("marks pure additions as added on new-file lines", () => {
    const patch = ["@@ -0,0 +1,2 @@", "+alpha", "+beta"].join("\n");
    expect(parseDiffDecorations(patch)).toEqual([
      { line: 1, kind: "added" },
      { line: 2, kind: "added" },
    ]);
  });

  it("marks a replaced line as modified", () => {
    const patch = ["@@ -1,1 +1,1 @@", "-old", "+new"].join("\n");
    expect(parseDiffDecorations(patch)).toEqual([{ line: 1, kind: "modified" }]);
  });

  it("marks a pure deletion", () => {
    const patch = ["@@ -1,2 +1,1 @@", " keep", "-gone"].join("\n");
    const decos = parseDiffDecorations(patch);
    expect(decos).toContainEqual({ line: 2, kind: "deleted" });
  });

  it("tracks line numbers across context and additions", () => {
    const patch = ["@@ -1,3 +1,4 @@", " a", " b", "+inserted", " c"].join("\n");
    expect(parseDiffDecorations(patch)).toEqual([{ line: 3, kind: "added" }]);
  });

  it("returns nothing for an empty patch", () => {
    expect(parseDiffDecorations("")).toEqual([]);
  });
});

describe("blame helpers", () => {
  const lines = [
    { line: 1, commit: "abcdef1234567890", author: "Ada", relativeTime: "2 days ago" },
    { line: 2, commit: "deadbeef", author: "Grace", relativeTime: "" },
  ];

  it("finds a blame line by number", () => {
    expect(blameForLine(lines, 1)!.author).toBe("Ada");
    expect(blameForLine(lines, 99)).toBeNull();
  });

  it("formats a hover with a short sha", () => {
    expect(formatBlameHover(lines[0])).toBe("Ada · abcdef12 · 2 days ago");
  });

  it("omits relative time when absent", () => {
    expect(formatBlameHover(lines[1])).toBe("Grace · deadbeef");
  });
});
