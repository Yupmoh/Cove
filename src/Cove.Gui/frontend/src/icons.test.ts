import { describe, it, expect } from "vitest";
import { ICONS, iconSvg, iconForNookType, monogram } from "./icons";

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

describe("iconForNookType", () => {
  it("maps engine nook type names including sourceControl", () => {
    expect(iconForNookType("sourceControl")).toBe(ICONS.git);
    expect(iconForNookType("git")).toBe(ICONS.git);
    expect(iconForNookType("tasks-list")).toBe(ICONS.tasks);
    expect(iconForNookType("unknown-nook")).toBe(ICONS.terminal);
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
