import { describe, it, expect } from "vitest";
import {
  shouldShowLauncher,
  buildAdapterTiles,
  buildBuiltinTiles,
  buildLauncherTiles,
  type LauncherAdapter,
  type LauncherBuiltin,
} from "./box-launcher";

describe("shouldShowLauncher", () => {
  it("shows only when the active workspace has no rooms", () => {
    expect(shouldShowLauncher(0)).toBe(true);
    expect(shouldShowLauncher(1)).toBe(false);
    expect(shouldShowLauncher(3)).toBe(false);
    expect(shouldShowLauncher(-1)).toBe(true);
  });
});

describe("buildAdapterTiles", () => {
  const adapters: LauncherAdapter[] = [
    { name: "claude", displayName: "Claude Code", accent: "#cba6f7", binary: "/usr/bin/claude" },
    { name: "codex", displayName: "", accent: "", binary: "" },
  ];
  it("prefers displayName and prefixes the id", () => {
    const tiles = buildAdapterTiles(adapters);
    expect(tiles[0].label).toBe("Claude Code");
    expect(tiles[0].id).toBe("adapter:claude");
    expect(tiles[0].adapterName).toBe("claude");
  });
  it("falls back to the adapter name when displayName is empty", () => {
    expect(buildAdapterTiles(adapters)[1].label).toBe("codex");
  });
  it("disables adapters with no detected binary and adds a calm note", () => {
    const tiles = buildAdapterTiles(adapters);
    expect(tiles[0].disabled).toBe(false);
    expect(tiles[0].note).toBe("");
    expect(tiles[1].disabled).toBe(true);
    expect(tiles[1].note).toBe("not detected");
  });
});

describe("buildBuiltinTiles", () => {
  const builtins: LauncherBuiltin[] = [
    { id: "terminal", label: "Terminal", icon: "▌", action: "room.new" },
    { id: "browser", label: "Browser", icon: "◑", action: "tool.browser" },
  ];
  it("maps builtins to enabled tiles carrying their action", () => {
    const tiles = buildBuiltinTiles(builtins);
    expect(tiles.map((t) => t.id)).toEqual(["builtin:terminal", "builtin:browser"]);
    expect(tiles.every((t) => !t.disabled)).toBe(true);
    expect(tiles[0].action).toBe("room.new");
    expect(tiles[0].kind).toBe("builtin");
  });
});

describe("buildLauncherTiles", () => {
  it("lists detected harnesses first, then built-in pane types, order preserved", () => {
    const adapters: LauncherAdapter[] = [{ name: "claude", displayName: "Claude Code", accent: "", binary: "/x" }];
    const builtins: LauncherBuiltin[] = [{ id: "terminal", label: "Terminal", icon: "▌", action: "room.new" }];
    const tiles = buildLauncherTiles(adapters, builtins);
    expect(tiles.map((t) => t.id)).toEqual(["adapter:claude", "builtin:terminal"]);
  });
});
