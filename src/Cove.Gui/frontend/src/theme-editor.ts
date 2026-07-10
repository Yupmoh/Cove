export interface ThemeDto {
  name: string;
  type: string;
  terminalBackground: string;
  terminalForeground: string;
  chromeSurface: string;
  chromeText: string;
  chromeAccent: string;
}

export interface ThemeEditorState {
  themes: ThemeDto[];
  activeName: string | null;
  draft: ThemeDraft;
  customNames: string[];
}

export interface ThemeDraft {
  name: string;
  type: string;
  terminalBackground: string;
  terminalForeground: string;
  chromeSurface: string;
  chromeText: string;
  chromeAccent: string;
}

export const CATPPUCCIN_MOCHA: ThemeDto = {
  name: "catppuccin-mocha",
  type: "dark",
  terminalBackground: "#1e1e2e",
  terminalForeground: "#cdd6f4",
  chromeSurface: "#181825",
  chromeText: "#cdd6f4",
  chromeAccent: "#cba6f7",
};

export const DEFAULT_THEME_NAME = "catppuccin-mocha";

export const DEFAULT_DRAFT: ThemeDraft = {
  name: "",
  type: "dark",
  terminalBackground: "#1e1e2e",
  terminalForeground: "#cdd6f4",
  chromeSurface: "#181825",
  chromeText: "#cdd6f4",
  chromeAccent: "#cba6f7",
};

export function draftFromTheme(theme: ThemeDto): ThemeDraft {
  return {
    name: theme.name,
    type: theme.type,
    terminalBackground: theme.terminalBackground,
    terminalForeground: theme.terminalForeground,
    chromeSurface: theme.chromeSurface,
    chromeText: theme.chromeText,
    chromeAccent: theme.chromeAccent,
  };
}

export function themeFromDraft(draft: ThemeDraft): ThemeDto {
  return {
    name: draft.name,
    type: draft.type,
    terminalBackground: draft.terminalBackground,
    terminalForeground: draft.terminalForeground,
    chromeSurface: draft.chromeSurface,
    chromeText: draft.chromeText,
    chromeAccent: draft.chromeAccent,
  };
}

export function cssVarsFromTheme(theme: ThemeDto): Record<string, string> {
  return {
    "--bg": theme.terminalBackground,
    "--panel": theme.chromeSurface,
    "--fg": theme.terminalForeground,
    "--accent": theme.chromeAccent,
  };
}

export function isCustom(name: string, customNames: string[]): boolean {
  return customNames.includes(name);
}

export function isBuiltin(name: string, builtinNames: string[]): boolean {
  return builtinNames.includes(name);
}

export function canSaveDraft(draft: ThemeDraft): boolean {
  return draft.name.trim().length > 0 && isValidHex(draft.terminalBackground) && isValidHex(draft.terminalForeground) && isValidHex(draft.chromeSurface) && isValidHex(draft.chromeText) && isValidHex(draft.chromeAccent);
}

export function canDelete(name: string, customNames: string[]): boolean {
  return name.length > 0 && customNames.includes(name);
}

const HEX_RE = /^#[0-9a-fA-F]{6}$/;

export function isValidHex(hex: string): boolean {
  return HEX_RE.test(hex);
}

export function contrastRatio(fg: string, bg: string): number {
  const l1 = relativeLuminance(fg);
  const l2 = relativeLuminance(bg);
  const lighter = Math.max(l1, l2);
  const darker = Math.min(l1, l2);
  return (lighter + 0.05) / (darker + 0.05);
}

function relativeLuminance(hex: string): number {
  if (!isValidHex(hex)) return 0;
  const r = parseInt(hex.slice(1, 3), 16) / 255;
  const g = parseInt(hex.slice(3, 5), 16) / 255;
  const b = parseInt(hex.slice(5, 7), 16) / 255;
  const lin = (c: number): number => c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
  return 0.2126 * lin(r) + 0.7152 * lin(g) + 0.0722 * lin(b);
}

export function contrastTier(ratio: number): "AAA" | "AA" | "fail" {
  if (ratio >= 7) return "AAA";
  if (ratio >= 4.5) return "AA";
  return "fail";
}

export const THEME_COLOR_FIELDS: ReadonlyArray<{ key: keyof ThemeDraft; label: string; desc: string }> = [
  { key: "terminalBackground", label: "Terminal Background", desc: "Base background color" },
  { key: "terminalForeground", label: "Terminal Foreground", desc: "Default text color" },
  { key: "chromeSurface", label: "Chrome Surface", desc: "Panels and sidebars" },
  { key: "chromeText", label: "Chrome Text", desc: "Text on chrome surfaces" },
  { key: "chromeAccent", label: "Chrome Accent", desc: "Highlights and active indicators" },
];
