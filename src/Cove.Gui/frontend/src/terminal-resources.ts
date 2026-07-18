import { CanvasAddon } from "@xterm/addon-canvas";
import { FitAddon } from "@xterm/addon-fit";
import { SearchAddon } from "@xterm/addon-search";
import { SerializeAddon } from "@xterm/addon-serialize";
import { Terminal } from "@xterm/xterm";
import type { TerminalSessionResources, TerminalSettings } from "./terminal-session";

export function createTerminalResources(
  settings: TerminalSettings,
  theme: Record<string, string>,
): TerminalSessionResources {
  return {
    term: new Terminal({
      allowTransparency: true,
      scrollback: settings.scrollback,
      convertEol: false,
      fontFamily: settings.fontFamily || "ui-monospace, SFMono-Regular, Menlo, monospace",
      fontSize: settings.fontSize,
      lineHeight: settings.lineHeight,
      letterSpacing: settings.letterSpacing,
      cursorStyle: settings.cursorStyle,
      cursorBlink: settings.cursorBlink,
      theme,
    }),
    fit: new FitAddon(),
    search: new SearchAddon(),
    serialize: new SerializeAddon(),
    renderer: new CanvasAddon(),
  };
}
