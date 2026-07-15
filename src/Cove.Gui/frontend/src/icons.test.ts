import { describe, it, expect } from "vitest";
import { ICONS, adapterIconSvg, fileIcon, iconSvg, iconForNookType, monogram } from "./icons";

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

describe("adapterIconSvg", () => {
  it("uses standalone official marks for every shipped adapter", () => {
    const shipped = ["claude-code", "codex", "omp"];
    for (const adapter of shipped) expect(adapterIconSvg(adapter)).toContain("adapter-icon");
    expect(new Set(shipped.map(adapterIconSvg)).size).toBe(shipped.length);
    expect(adapterIconSvg("claude-code")).toContain("/adapter-icons/claude.png");
    expect(adapterIconSvg("codex")).toContain("/adapter-icons/codex.png");
    expect(adapterIconSvg("omp")).toContain("/adapter-icons/omp.svg");
  });

  it("does not borrow another brand for an unsupported adapter", () => {
    expect(adapterIconSvg("opencode")).toBe(ICONS.agents);
  });
});

describe("fileIcon", () => {
  it("uses language-specific icons and colors", () => {
    expect(fileIcon("Program.cs").kind).toBe("csharp");
    expect(fileIcon("main.ts").kind).toBe("typescript");
    expect(fileIcon("app.js").kind).toBe("javascript");
    expect(fileIcon("README.md").kind).toBe("markdown");
  });

  it("recognizes project and config files before falling back", () => {
    expect(fileIcon("Cove.slnx").kind).toBe("dotnet");
    expect(fileIcon("package.json").kind).toBe("json");
    expect(fileIcon("unknown.bin").kind).toBe("file");
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
