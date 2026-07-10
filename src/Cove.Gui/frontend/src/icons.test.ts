import { describe, it, expect } from "vitest";
import { ICONS, iconSvg, iconForPaneType, monogram } from "./icons";

describe("iconSvg", () => {
  it("returns inline svg markup for known names", () => {
    for (const name of Object.keys(ICONS)) {
      expect(iconSvg(name)).toContain("<svg");
      expect(iconSvg(name)).toContain("currentColor");
    }
  });

  it("falls back to the terminal icon for unknown names", () => {
    expect(iconSvg("nope")).toBe(ICONS.terminal);
  });
});

describe("iconForPaneType", () => {
  it("maps engine pane type names including sourceControl", () => {
    expect(iconForPaneType("sourceControl")).toBe(ICONS.git);
    expect(iconForPaneType("git")).toBe(ICONS.git);
    expect(iconForPaneType("tasks-list")).toBe(ICONS.tasks);
    expect(iconForPaneType("unknown-pane")).toBe(ICONS.terminal);
  });
});

describe("monogram", () => {
  it("builds two-letter monograms from adapter labels", () => {
    expect(monogram("Claude Code")).toBe("CC");
    expect(monogram("Codex")).toBe("Co");
    expect(monogram("omp")).toBe("Om");
    expect(monogram("x")).toBe("X");
    expect(monogram("  ")).toBe("?");
  });
});
