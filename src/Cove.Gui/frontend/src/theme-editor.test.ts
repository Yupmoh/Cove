import { describe, it, expect } from "vitest";
import {
  DEFAULT_DRAFT,
  DEFAULT_THEME_NAME,
  CATPPUCCIN_MOCHA,
  draftFromTheme,
  themeFromDraft,
  cssVarsFromTheme,
  isCustom,
  isBuiltin,
  canSaveDraft,
  canDelete,
  isValidHex,
  contrastRatio,
  contrastTier,
} from "./theme-editor";

const harbor: import("./theme-editor").ThemeDto = {
  name: "cove-harbor",
  type: "dark",
  terminalBackground: "#0b1622",
  terminalForeground: "#e5e9f0",
  chromeSurface: "#0b1622",
  chromeText: "#e5e9f0",
  chromeAccent: "#4a9eff",
};

describe("draftFromTheme / themeFromDraft round-trip", () => {
  it("converts a theme to a draft and back losslessly", () => {
    const draft = draftFromTheme(harbor);
    expect(draft.name).toBe("cove-harbor");
    const back = themeFromDraft(draft);
    expect(back).toEqual(harbor);
  });
});

describe("cssVarsFromTheme", () => {
  it("maps theme fields to CSS custom properties", () => {
    const vars = cssVarsFromTheme(harbor);
    expect(vars["--bg"]).toBe("#0b1622");
    expect(vars["--panel"]).toBe("#0b1622");
    expect(vars["--fg"]).toBe("#e5e9f0");
    expect(vars["--accent"]).toBe("#4a9eff");
  });
});

describe("catppuccin-mocha builtin", () => {
  it("is the default theme name", () => {
    expect(DEFAULT_THEME_NAME).toBe("catppuccin-mocha");
    expect(CATPPUCCIN_MOCHA.name).toBe("catppuccin-mocha");
    expect(DEFAULT_DRAFT.terminalBackground).toBe(CATPPUCCIN_MOCHA.terminalBackground);
    expect(DEFAULT_DRAFT.chromeAccent).toBe(CATPPUCCIN_MOCHA.chromeAccent);
  });
  it("maps the official Mocha palette onto Cove variables", () => {
    const vars = cssVarsFromTheme(CATPPUCCIN_MOCHA);
    expect(vars["--bg"]).toBe("#1e1e2e");
    expect(vars["--panel"]).toBe("#181825");
    expect(vars["--fg"]).toBe("#cdd6f4");
    expect(vars["--accent"]).toBe("#cba6f7");
  });
  it("round-trips through draft conversion losslessly", () => {
    expect(themeFromDraft(draftFromTheme(CATPPUCCIN_MOCHA))).toEqual(CATPPUCCIN_MOCHA);
  });
  it("text on surface passes AA contrast", () => {
    expect(contrastRatio(CATPPUCCIN_MOCHA.chromeText, CATPPUCCIN_MOCHA.chromeSurface)).toBeGreaterThanOrEqual(4.5);
  });
});

describe("isCustom / isBuiltin", () => {
  it("returns true only when name is in the respective list", () => {
    expect(isCustom("my-theme", ["my-theme", "other"])).toBe(true);
    expect(isCustom("cove-harbor", ["my-theme"])).toBe(false);
    expect(isBuiltin("cove-harbor", ["cove-harbor", "cove-daybreak"])).toBe(true);
    expect(isBuiltin("my-theme", ["cove-harbor"])).toBe(false);
  });
});

describe("isValidHex", () => {
  it("accepts 6-digit hex colors", () => {
    expect(isValidHex("#000000")).toBe(true);
    expect(isValidHex("#ffffff")).toBe(true);
    expect(isValidHex("#FFFFFF")).toBe(true);
    expect(isValidHex("#4a9eff")).toBe(true);
  });
  it("rejects malformed hex", () => {
    expect(isValidHex("#fff")).toBe(false);
    expect(isValidHex("000000")).toBe(false);
    expect(isValidHex("#0000000")).toBe(false);
    expect(isValidHex("#gggggg")).toBe(false);
    expect(isValidHex("")).toBe(false);
  });
});

describe("canSaveDraft", () => {
  it("allows save when name is non-empty and all colors valid", () => {
    expect(canSaveDraft({ ...DEFAULT_DRAFT, name: "my-theme" })).toBe(true);
  });
  it("rejects empty name", () => {
    expect(canSaveDraft({ ...DEFAULT_DRAFT, name: "" })).toBe(false);
    expect(canSaveDraft({ ...DEFAULT_DRAFT, name: "   " })).toBe(false);
  });
  it("rejects invalid color", () => {
    expect(canSaveDraft({ ...DEFAULT_DRAFT, terminalBackground: "#fff" })).toBe(false);
    expect(canSaveDraft({ ...DEFAULT_DRAFT, chromeAccent: "red" })).toBe(false);
  });
});

describe("canDelete", () => {
  it("allows delete only for custom themes", () => {
    expect(canDelete("my-theme", ["my-theme", "other"])).toBe(true);
    expect(canDelete("cove-harbor", ["my-theme"])).toBe(false);
    expect(canDelete("", ["my-theme"])).toBe(false);
  });
});

describe("contrastRatio", () => {
  it("returns 1 for identical colors", () => {
    expect(contrastRatio("#ffffff", "#ffffff")).toBeCloseTo(1, 2);
  });
  it("returns 21 for black-on-white", () => {
    expect(contrastRatio("#000000", "#ffffff")).toBeCloseTo(21, 1);
  });
  it("harbor fg/bg passes AA", () => {
    const r = contrastRatio("#e5e9f0", "#0b1622");
    expect(r).toBeGreaterThanOrEqual(4.5);
  });
});

describe("contrastTier", () => {
  it("classifies ratios into tiers", () => {
    expect(contrastTier(21)).toBe("AAA");
    expect(contrastTier(7)).toBe("AAA");
    expect(contrastTier(6.9)).toBe("AA");
    expect(contrastTier(4.5)).toBe("AA");
    expect(contrastTier(4.4)).toBe("fail");
    expect(contrastTier(1)).toBe("fail");
  });
});
