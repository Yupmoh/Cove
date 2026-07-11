import { describe, it, expect } from "vitest";
import { diagnosticsToMarkers, lspLanguageForPath, MARKER_SEVERITY } from "./lsp-markers";

describe("diagnosticsToMarkers", () => {
  it("maps all four severities to monaco values", () => {
    const diags = (["error", "warning", "info", "hint"] as const).map((severity, i) => ({
      startLine: i,
      startCol: 0,
      endLine: i,
      endCol: 1,
      severity,
      message: severity,
      code: null,
    }));
    const markers = diagnosticsToMarkers(diags);
    expect(markers.map((m) => m.severity)).toEqual([
      MARKER_SEVERITY.Error,
      MARKER_SEVERITY.Warning,
      MARKER_SEVERITY.Info,
      MARKER_SEVERITY.Hint,
    ]);
  });

  it("maps an unknown severity to Info", () => {
    const markers = diagnosticsToMarkers([
      { startLine: 0, startCol: 0, endLine: 0, endCol: 1, severity: "mystery", message: "m", code: null },
    ]);
    expect(markers[0].severity).toBe(MARKER_SEVERITY.Info);
  });

  it("converts 0-based LSP positions to 1-based monaco positions", () => {
    const markers = diagnosticsToMarkers([
      { startLine: 0, startCol: 6, endLine: 2, endCol: 10, severity: "error", message: "boom", code: "2322" },
    ]);
    expect(markers[0]).toMatchObject({
      startLineNumber: 1,
      startColumn: 7,
      endLineNumber: 3,
      endColumn: 11,
      message: "boom",
      code: "2322",
    });
  });

  it("returns an empty array for no diagnostics", () => {
    expect(diagnosticsToMarkers([])).toEqual([]);
  });
});

describe("lspLanguageForPath", () => {
  it("maps typescript family extensions", () => {
    expect(lspLanguageForPath("/a/b.ts")).toBe("typescript");
    expect(lspLanguageForPath("/a/b.tsx")).toBe("typescriptreact");
    expect(lspLanguageForPath("/a/b.js")).toBe("javascript");
    expect(lspLanguageForPath("/a/b.jsx")).toBe("javascriptreact");
    expect(lspLanguageForPath("/a/b.mts")).toBe("typescript");
    expect(lspLanguageForPath("/a/b.cts")).toBe("typescript");
  });

  it("maps json, css, and html", () => {
    expect(lspLanguageForPath("/a/b.json")).toBe("json");
    expect(lspLanguageForPath("/a/b.css")).toBe("css");
    expect(lspLanguageForPath("/a/b.html")).toBe("html");
    expect(lspLanguageForPath("/a/b.htm")).toBe("html");
  });

  it("returns null for unknown extensions", () => {
    expect(lspLanguageForPath("/a/b.zig")).toBeNull();
    expect(lspLanguageForPath("/a/noext")).toBeNull();
  });
});
