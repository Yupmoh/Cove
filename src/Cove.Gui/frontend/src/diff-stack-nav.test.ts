import { describe, it, expect } from "vitest";
import {
  initialCursor,
  nextFile,
  prevFile,
  nextHunk,
  prevHunk,
  resolveDiffStackKey,
} from "./diff-stack-nav";

const files = [
  { filePath: "a.ts", hunkCount: 2 },
  { filePath: "b.ts", hunkCount: 1 },
  { filePath: "c.ts", hunkCount: 3 },
];

describe("file navigation", () => {
  it("advances and clamps at the end", () => {
    let c = initialCursor();
    c = nextFile(c, files);
    expect(c).toEqual({ fileIndex: 1, hunkIndex: 0 });
    c = nextFile(c, files);
    c = nextFile(c, files);
    expect(c.fileIndex).toBe(2);
  });

  it("retreats and clamps at the start", () => {
    let c = { fileIndex: 1, hunkIndex: 0 };
    c = prevFile(c, files);
    expect(c).toEqual({ fileIndex: 0, hunkIndex: 0 });
    c = prevFile(c, files);
    expect(c.fileIndex).toBe(0);
  });

  it("resets hunk index on file change", () => {
    const c = nextFile({ fileIndex: 0, hunkIndex: 5 }, files);
    expect(c.hunkIndex).toBe(0);
  });
});

describe("hunk navigation", () => {
  it("moves within a file then crosses into the next", () => {
    let c = initialCursor();
    c = nextHunk(c, files);
    expect(c).toEqual({ fileIndex: 0, hunkIndex: 1 });
    c = nextHunk(c, files);
    expect(c).toEqual({ fileIndex: 1, hunkIndex: 0 });
  });

  it("moves back across a file boundary to the last hunk", () => {
    const c = prevHunk({ fileIndex: 1, hunkIndex: 0 }, files);
    expect(c).toEqual({ fileIndex: 0, hunkIndex: 1 });
  });

  it("stays put at the very start and end", () => {
    expect(prevHunk({ fileIndex: 0, hunkIndex: 0 }, files)).toEqual({ fileIndex: 0, hunkIndex: 0 });
    expect(nextHunk({ fileIndex: 2, hunkIndex: 2 }, files)).toEqual({ fileIndex: 2, hunkIndex: 2 });
  });
});

describe("resolveDiffStackKey", () => {
  const base = { metaKey: false, ctrlKey: false, altKey: false, shiftKey: false };

  it("maps j/k and arrows to file nav", () => {
    expect(resolveDiffStackKey({ ...base, key: "j" })).toBe("next-file");
    expect(resolveDiffStackKey({ ...base, key: "ArrowDown" })).toBe("next-file");
    expect(resolveDiffStackKey({ ...base, key: "k" })).toBe("prev-file");
    expect(resolveDiffStackKey({ ...base, key: "ArrowUp" })).toBe("prev-file");
  });

  it("maps n/p to hunk nav and Enter/Space to actions", () => {
    expect(resolveDiffStackKey({ ...base, key: "n" })).toBe("next-hunk");
    expect(resolveDiffStackKey({ ...base, key: "p" })).toBe("prev-hunk");
    expect(resolveDiffStackKey({ ...base, key: "Enter" })).toBe("open");
    expect(resolveDiffStackKey({ ...base, key: " " })).toBe("mark-reviewed");
  });

  it("yields reserved chords with modifiers to the global keymap", () => {
    expect(resolveDiffStackKey({ ...base, key: "j", metaKey: true })).toBeNull();
    expect(resolveDiffStackKey({ ...base, key: "n", ctrlKey: true })).toBeNull();
    expect(resolveDiffStackKey({ ...base, key: "x" })).toBeNull();
  });
});
