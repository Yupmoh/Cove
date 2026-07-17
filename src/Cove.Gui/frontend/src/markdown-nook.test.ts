import { describe, it, expect, vi } from "vitest";
vi.mock("./monaco-loader", () => ({ MonacoLoader: { load: () => Promise.resolve({}) } }));
import { MarkdownViewMode, toggleViewMode } from "./markdown-view-mode";
import {
  resolveMarkdownSettings,
  markdownEditorCss,
  resolveInitialViewMode,
} from "./markdown-nook";

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

describe("resolveMarkdownSettings", () => {
  it("applies defaults when config is empty", () => {
    const s = resolveMarkdownSettings({});
    expect(s.defaultFont).toBe("");
    expect(s.fontSize).toBe(14);
    expect(s.textAlign).toBe("left");
    expect(s.bookView).toBe(false);
    expect(s.bookViewWidth).toBe("720px");
    expect(s.bookViewMargin).toBe("auto");
    expect(s.defaultViewMode).toBe(MarkdownViewMode.Rte);
  });

  it("parses a full config payload", () => {
    const s = resolveMarkdownSettings({
      "markdown_editor.defaultFont": "JetBrains Mono",
      "markdown_editor.fontSize": "18",
      "markdown_editor.textAlign": "justify",
      "markdown_editor.bookView": "true",
      "markdown_editor.bookViewWidth": "800px",
      "markdown_editor.bookViewMargin": "48px",
      "markdown_editor.defaultViewMode": "source",
    });
    expect(s.defaultFont).toBe("JetBrains Mono");
    expect(s.fontSize).toBe(18);
    expect(s.textAlign).toBe("justify");
    expect(s.bookView).toBe(true);
    expect(s.bookViewWidth).toBe("800px");
    expect(s.bookViewMargin).toBe("48px");
    expect(s.defaultViewMode).toBe(MarkdownViewMode.Source);
  });

  it("clamps font size to a sane range", () => {
    expect(resolveMarkdownSettings({ "markdown_editor.fontSize": "0" }).fontSize).toBe(14);
    expect(resolveMarkdownSettings({ "markdown_editor.fontSize": "-5" }).fontSize).toBe(14);
    expect(resolveMarkdownSettings({ "markdown_editor.fontSize": "999" }).fontSize).toBe(28);
    expect(resolveMarkdownSettings({ "markdown_editor.fontSize": "abc" }).fontSize).toBe(14);
  });

  it("coerces bookView from various truthy/falsy strings", () => {
    expect(resolveMarkdownSettings({ "markdown_editor.bookView": "true" }).bookView).toBe(true);
    expect(resolveMarkdownSettings({ "markdown_editor.bookView": "false" }).bookView).toBe(false);
    expect(resolveMarkdownSettings({ "markdown_editor.bookView": "True" }).bookView).toBe(true);
    expect(resolveMarkdownSettings({ "markdown_editor.bookView": "" }).bookView).toBe(false);
  });

  it("resolves defaultViewMode to source or rte only", () => {
    expect(resolveMarkdownSettings({ "markdown_editor.defaultViewMode": "source" }).defaultViewMode).toBe(MarkdownViewMode.Source);
    expect(resolveMarkdownSettings({ "markdown_editor.defaultViewMode": "rte" }).defaultViewMode).toBe(MarkdownViewMode.Rte);
    expect(resolveMarkdownSettings({ "markdown_editor.defaultViewMode": "bogus" }).defaultViewMode).toBe(MarkdownViewMode.Rte);
    expect(resolveMarkdownSettings({ "markdown_editor.defaultViewMode": "" }).defaultViewMode).toBe(MarkdownViewMode.Rte);
  });

  it("preserves raw text values for font, width, margin, folder", () => {
    const s = resolveMarkdownSettings({
      "markdown_editor.defaultFont": "  Fira Code  ",
      "markdown_editor.bookViewWidth": "",
      "markdown_editor.bookViewMargin": "  ",
    });
    expect(s.defaultFont).toBe("Fira Code");
    expect(s.bookViewWidth).toBe("720px");
    expect(s.bookViewMargin).toBe("auto");
  });
});

describe("markdownEditorCss", () => {
  it("produces base font/size/align css", () => {
    const css = markdownEditorCss(resolveMarkdownSettings({
      "markdown_editor.defaultFont": "Inter",
      "markdown_editor.fontSize": "16",
      "markdown_editor.textAlign": "center",
    }));
    expect(css).toContain("font-family:Inter");
    expect(css).toContain("font-size:16px");
    expect(css).toContain("text-align:center");
  });

  it("uses system font stack when no defaultFont", () => {
    const css = markdownEditorCss(resolveMarkdownSettings({}));
    expect(css).toContain("font-family:ui-sans-serif");
  });

  it("does not add book-view constraints when bookView is off", () => {
    const css = markdownEditorCss(resolveMarkdownSettings({ "markdown_editor.bookView": "false" }));
    expect(css).not.toContain("max-width:");
    expect(css).not.toContain("margin:0 auto");
  });

  it("adds book-view constraints when bookView is on", () => {
    const css = markdownEditorCss(resolveMarkdownSettings({
      "markdown_editor.bookView": "true",
      "markdown_editor.bookViewWidth": "650px",
      "markdown_editor.bookViewMargin": "32px",
    }));
    expect(css).toContain("max-width:650px");
    expect(css).toContain("margin:32px auto");
  });
});

describe("resolveInitialViewMode", () => {
  it("returns source for 'source'", () => {
    expect(resolveInitialViewMode("source")).toBe(MarkdownViewMode.Source);
  });
  it("returns rte for 'rte'", () => {
    expect(resolveInitialViewMode("rte")).toBe(MarkdownViewMode.Rte);
  });
  it("defaults to rte for unknown", () => {
    expect(resolveInitialViewMode("bogus")).toBe(MarkdownViewMode.Rte);
  });
  it("defaults to rte for null", () => {
    expect(resolveInitialViewMode(null)).toBe(MarkdownViewMode.Rte);
  });
});

