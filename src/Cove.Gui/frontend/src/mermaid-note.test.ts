import { describe, it, expect } from "vitest";
import { _testRenderMermaidSvg } from "./mermaid-note";

describe("Mermaid parser", () => {
  it("parses simple graph TD with two nodes and an edge", () => {
    const svg = _testRenderMermaidSvg(["graph TD", "A[Start] --> B[End]"]);
    expect(svg).toContain("<svg");
    expect(svg).toContain("</svg>");
    expect(svg).toContain("Start");
    expect(svg).toContain("End");
  });

  it("parses diamond decision nodes", () => {
    const svg = _testRenderMermaidSvg(["graph TD", "A --> B{Decision}"]);
    expect(svg).toContain("polygon");
    expect(svg).toContain("Decision");
  });

  it("parses round nodes", () => {
    const svg = _testRenderMermaidSvg(["graph TD", "A(Round) --> B"]);
    expect(svg).toContain("rx=\"18\"");
  });

  it("parses edge labels", () => {
    const svg = _testRenderMermaidSvg(["graph TD", "A --> B", "A -->|yes| C[Yes Path]"]);
    expect(svg).toContain("yes");
  });

  it("produces valid SVG for empty input", () => {
    const svg = _testRenderMermaidSvg([]);
    expect(svg).toContain("<svg");
    expect(svg).toContain("</svg>");
  });
});
