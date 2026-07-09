import { describe, it, expect } from "vitest";
import { parseRefSpec, DiffViewMode } from "./diff-view-model";

describe("parseRefSpec", () => {
  it("parses HEAD", () => {
    const r = parseRefSpec("HEAD");
    expect(r.ref).toBe("HEAD");
    expect(r.isWorkingTree).toBe(false);
  });

  it("parses working tree indicator", () => {
    const r = parseRefSpec("WORKING");
    expect(r.ref).toBe("HEAD");
    expect(r.isWorkingTree).toBe(true);
  });

  it("parses branch name", () => {
    const r = parseRefSpec("feature/my-branch");
    expect(r.ref).toBe("feature/my-branch");
    expect(r.isWorkingTree).toBe(false);
  });

  it("parses commit sha", () => {
    const r = parseRefSpec("abc1234");
    expect(r.ref).toBe("abc1234");
    expect(r.isWorkingTree).toBe(false);
  });

  it("defaults to HEAD for empty", () => {
    const r = parseRefSpec("");
    expect(r.ref).toBe("HEAD");
    expect(r.isWorkingTree).toBe(false);
  });
});

describe("DiffViewMode", () => {
  it("has side-by-side and unified values", () => {
    expect(DiffViewMode.SideBySide).toBe("side-by-side");
    expect(DiffViewMode.Unified).toBe("unified");
  });

  it("toggles between modes", () => {
    expect(DiffViewMode.toggle(DiffViewMode.SideBySide)).toBe(DiffViewMode.Unified);
    expect(DiffViewMode.toggle(DiffViewMode.Unified)).toBe(DiffViewMode.SideBySide);
  });
});
