import { describe, it, expect } from "vitest";
import { MarkdownViewMode, toggleViewMode } from "./markdown-view-mode";

describe("MarkdownViewMode", () => {
  it("has rte and source values", () => {
    expect(MarkdownViewMode.Rte).toBe("rte");
    expect(MarkdownViewMode.Source).toBe("source");
  });
});

describe("toggleViewMode", () => {
  it("toggles rte to source", () => {
    expect(toggleViewMode(MarkdownViewMode.Rte)).toBe(MarkdownViewMode.Source);
  });

  it("toggles source to rte", () => {
    expect(toggleViewMode(MarkdownViewMode.Source)).toBe(MarkdownViewMode.Rte);
  });
  it("defaults to rte for unknown mode", () => {
    expect(toggleViewMode("unknown")).toBe(MarkdownViewMode.Rte);
  });
});
