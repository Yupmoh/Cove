import { FrontendCommand } from "../../app/frontend-command";
import type { ComponentHandle } from "../../app/lifecycle";
import type { TerminalSettings } from "../../terminal-session";
import { xtermThemeFromDto, type ThemeDto } from "../../theme-editor";

const themeBackground = "#1e1e2e";
const defaultTheme = {
  background: themeBackground,
  foreground: "#cdd6f4",
  cursor: "#f5e0dc",
  cursorAccent: themeBackground,
  selectionBackground: "#585b70",
  black: "#45475a",
  red: "#f38ba8",
  green: "#a6e3a1",
  yellow: "#f9e2af",
  blue: "#89b4fa",
  magenta: "#f5c2e7",
  cyan: "#94e2d5",
  white: "#bac2de",
  brightBlack: "#585b70",
  brightRed: "#f38ba8",
  brightGreen: "#a6e3a1",
  brightYellow: "#f9e2af",
  brightBlue: "#89b4fa",
  brightMagenta: "#f5c2e7",
  brightCyan: "#94e2d5",
  brightWhite: "#a6adc8",
};

const defaultSettings: TerminalSettings = {
  fontFamily: "",
  fontSize: 13,
  lineHeight: 1.35,
  letterSpacing: 0,
  cursorStyle: "block",
  cursorBlink: false,
  scrollback: 5000,
  padding: 8,
  backgroundOpacity: 1,
};

export interface TerminalPreferencesDependencies {
  invoke<T>(command: FrontendCommand, args: Record<string, unknown>): Promise<T>;
}

export interface TerminalPreferencesFeature extends ComponentHandle {
  readonly settings: TerminalSettings;
  readonly defaultTheme: Record<string, string>;
  load(): Promise<TerminalSettings>;
  persist(): void;
  theme(activeTheme: ThemeDto | null): Record<string, string>;
  themeBackgroundWithOpacity(opacity: number): string;
}

export function createTerminalPreferencesFeature(
  dependencies: TerminalPreferencesDependencies,
): TerminalPreferencesFeature {
  const settings: TerminalSettings = { ...defaultSettings };

  function clampInt(value: unknown, low: number, high: number, fallback: number): number {
    const parsed = Number(value);
    return Number.isFinite(parsed) && parsed >= low && parsed <= high
      ? Math.trunc(parsed)
      : fallback;
  }

  function clampFloat(value: unknown, low: number, high: number, fallback: number): number {
    const parsed = Number(value);
    return Number.isFinite(parsed) && parsed >= low && parsed <= high
      ? parsed
      : fallback;
  }

  async function read(key: string): Promise<string | null> {
    try {
      const result = await dependencies.invoke<{ ok: boolean; value?: string }>(
        FrontendCommand.AppConfigGet,
        { key },
      );
      return result.ok ? result.value ?? null : null;
    } catch (error) {
      console.warn("terminal setting unavailable", key, error);
      return null;
    }
  }

  async function load(): Promise<TerminalSettings> {
    const fontFamily = await read("terminal.fontFamily")
      ?? defaultSettings.fontFamily;
    const fontSize = clampInt(
      await read("terminal.fontSize"),
      9,
      24,
      defaultSettings.fontSize,
    );
    const lineHeight = clampFloat(
      await read("terminal.lineHeight"),
      1,
      2,
      defaultSettings.lineHeight,
    );
    const letterSpacing = clampFloat(
      await read("terminal.letterSpacing"),
      -5,
      20,
      defaultSettings.letterSpacing,
    );
    const scrollback = clampInt(
      await read("terminal.scrollbackLines"),
      100,
      100000,
      defaultSettings.scrollback,
    );
    const padding = clampInt(
      await read("terminal.padding"),
      0,
      40,
      defaultSettings.padding,
    );
    const backgroundOpacity = clampFloat(
      await read("terminal.backgroundOpacity"),
      0.2,
      1,
      defaultSettings.backgroundOpacity,
    );
    const rawCursorStyle = await read("terminal.cursorStyle");
    const cursorStyle: TerminalSettings["cursorStyle"] =
      rawCursorStyle === "bar" || rawCursorStyle === "underline"
        ? rawCursorStyle
        : "block";
    const cursorBlink = await read("terminal.cursorBlink") === "true";
    Object.assign(settings, {
      fontFamily,
      fontSize,
      lineHeight,
      letterSpacing,
      cursorStyle,
      cursorBlink,
      scrollback,
      padding,
      backgroundOpacity,
    });
    return settings;
  }

  function persist(): void {
    const entries: [string, string][] = [
      ["terminal.fontFamily", settings.fontFamily],
      ["terminal.fontSize", String(settings.fontSize)],
      ["terminal.lineHeight", String(settings.lineHeight)],
      ["terminal.letterSpacing", String(settings.letterSpacing)],
      ["terminal.cursorStyle", settings.cursorStyle],
      ["terminal.cursorBlink", String(settings.cursorBlink)],
      ["terminal.scrollbackLines", String(settings.scrollback)],
      ["terminal.padding", String(settings.padding)],
      ["terminal.backgroundOpacity", String(settings.backgroundOpacity)],
    ];
    for (const [key, value] of entries) {
      void dependencies.invoke(FrontendCommand.AppConfigSet, { key, value }).catch((error) => {
        console.warn("configSet failed", key, error);
      });
    }
  }

  function themeWithOpacity(opacity: number): string {
    const normalized = opacity >= 0 && opacity <= 1 ? opacity : 1;
    const red = parseInt(themeBackground.slice(1, 3), 16);
    const green = parseInt(themeBackground.slice(3, 5), 16);
    const blue = parseInt(themeBackground.slice(5, 7), 16);
    return `rgba(${red}, ${green}, ${blue}, ${normalized})`;
  }

  function theme(activeTheme: ThemeDto | null): Record<string, string> {
    if (activeTheme) {
      return xtermThemeFromDto(activeTheme, settings.backgroundOpacity);
    }
    return {
      ...defaultTheme,
      background: themeWithOpacity(settings.backgroundOpacity),
    };
  }

  return {
    settings,
    defaultTheme,
    load,
    persist,
    theme,
    themeBackgroundWithOpacity: themeWithOpacity,
    dispose() {},
  };
}
