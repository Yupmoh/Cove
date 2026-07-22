import { describe, it, expect } from "vitest";
import {
  shouldShowLauncher,
  buildAdapterTiles,
  buildBuiltinTiles,
  buildLauncherTiles,
  isEmptyShoreTree,
  isPlaceholderLeaf,
  launcherPlacement,
  placeableNookForAction,
  resolveLaunchCwd,
  harnessInstallRows,
  type LauncherAdapter,
  type LauncherBuiltin,
  type PlaceholderTreeNode,
} from "./box-launcher";

describe("resolveLaunchCwd", () => {
  it("leaves default cwd resolution to the engine", () => {
    expect(resolveLaunchCwd("", "")).toBe("");
  });
  it("keeps an explicitly selected cwd", () => {
    expect(resolveLaunchCwd("/tmp/here", "")).toBe("/tmp/here");
  });
  it("yields empty so the engine inherits from a sibling nook", () => {
    expect(resolveLaunchCwd("", "nook-123")).toBe("");
  });
  it("preserves explicit intent even when inheritance is requested", () => {
    expect(resolveLaunchCwd("/tmp/here", "nook-123")).toBe("/tmp/here");
  });
  it("treats whitespace-only values as empty", () => {
    expect(resolveLaunchCwd("   ", "  ")).toBe("");
  });
});

describe("shouldShowLauncher", () => {
  it("shows only when the active bay has no shores", () => {
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
  it("carries a trimmed detected version onto the tile, defaulting to empty", () => {
    const withVersion: LauncherAdapter[] = [
      { name: "claude", displayName: "Claude Code", accent: "", binary: "/x", version: "  1.2.3  " },
    ];
    expect(buildAdapterTiles(withVersion)[0].version).toBe("1.2.3");
    expect(buildAdapterTiles(adapters)[0].version).toBe("");
  });
});

describe("buildBuiltinTiles", () => {
  const builtins: LauncherBuiltin[] = [
    { id: "terminal", label: "Terminal", icon: "▌", action: "shore.new" },
    { id: "browser", label: "Browser", icon: "◑", action: "tool.browser" },
  ];
  it("maps builtins to enabled tiles carrying their action", () => {
    const tiles = buildBuiltinTiles(builtins);
    expect(tiles.map((t) => t.id)).toEqual(["builtin:terminal", "builtin:browser"]);
    expect(tiles.every((t) => !t.disabled)).toBe(true);
    expect(tiles[0].action).toBe("shore.new");
    expect(tiles[0].kind).toBe("builtin");
    expect(tiles[0].version).toBe("");
  });
});

describe("buildLauncherTiles", () => {
  it("lists detected harnesses first, then built-in nook types, order preserved", () => {
    const adapters: LauncherAdapter[] = [{ name: "claude", displayName: "Claude Code", accent: "", binary: "/x" }];
    const builtins: LauncherBuiltin[] = [{ id: "terminal", label: "Terminal", icon: "▌", action: "shore.new" }];
    const tiles = buildLauncherTiles(adapters, builtins);
    expect(tiles.map((t) => t.id)).toEqual(["adapter:claude", "builtin:terminal"]);
  });
});

describe("isEmptyShoreTree", () => {
  it("treats a lone empty-typed leaf as empty", () => {
    expect(isEmptyShoreTree({ kind: "leaf", subtabs: [{ nookType: "empty" }] })).toBe(true);
    expect(isEmptyShoreTree({ kind: "leaf", subtabs: [] })).toBe(true);
  });
  it("treats a leaf with a real nook as non-empty", () => {
    expect(isEmptyShoreTree({ kind: "leaf", subtabs: [{ nookType: "terminal" }] })).toBe(false);
  });
  it("treats a split as non-empty", () => {
    expect(isEmptyShoreTree({ kind: "split" })).toBe(false);
    expect(isEmptyShoreTree(null)).toBe(false);
  });
});

describe("launcherPlacement", () => {
  it("replaces into an empty shore, otherwise creates a shore", () => {
    expect(launcherPlacement(true)).toBe("replace");
    expect(launcherPlacement(false)).toBe("create");
  });
});

describe("placeableNookForAction", () => {
  it("maps terminal and browser and tool tiles to nook types and shore names", () => {
    expect(placeableNookForAction("shore.new")).toEqual({ nookType: "terminal", kind: "terminal", shoreName: "Shore" });
    expect(placeableNookForAction("tool.browser")).toEqual({ nookType: "browser", kind: "browser", shoreName: "Browser" });
    expect(placeableNookForAction("tool.git")).toEqual({ nookType: "git", kind: "tool", shoreName: "Source Control" });
    expect(placeableNookForAction("tool.search")).toEqual({ nookType: "search", kind: "tool", shoreName: "Search" });
    expect(placeableNookForAction("tool.tasks")).toEqual({ nookType: "tasks-list", kind: "tool", shoreName: "Tasks" });
    expect(placeableNookForAction("tool.notepad")).toBeNull();
  });
});

describe("isPlaceholderLeaf", () => {
  const emptyLeaf: PlaceholderTreeNode = { kind: "leaf", nookId: "empty-1", subtabs: [{ nookType: "empty" }] };
  const liveLeaf: PlaceholderTreeNode = { kind: "leaf", nookId: "nook-live", subtabs: [{ nookType: "terminal" }] };

  it("treats an empty-typed leaf as a placeholder", () => {
    expect(isPlaceholderLeaf(emptyLeaf, "empty-1")).toBe(true);
  });

  it("treats a leaf with no subtabs as a placeholder", () => {
    expect(isPlaceholderLeaf({ kind: "leaf", nookId: "bare" }, "bare")).toBe(true);
  });

  it("does not treat a live terminal leaf as a placeholder", () => {
    expect(isPlaceholderLeaf(liveLeaf, "nook-live")).toBe(false);
  });

  it("finds a placeholder leaf nested inside splits", () => {
    const tree: PlaceholderTreeNode = { kind: "split", childA: liveLeaf, childB: emptyLeaf };
    expect(isPlaceholderLeaf(tree, "empty-1")).toBe(true);
    expect(isPlaceholderLeaf(tree, "nook-live")).toBe(false);
  });

  it("returns false when the id is absent", () => {
    expect(isPlaceholderLeaf(emptyLeaf, "missing")).toBe(false);
    expect(isPlaceholderLeaf(null, "empty-1")).toBe(false);
  });
});

describe("harnessInstallRows", () => {
  const adapter = (over: Partial<LauncherAdapter>): LauncherAdapter => ({
    name: "x",
    displayName: "X",
    accent: "#fff",
    binary: "x",
    ...over,
  });

  it("lists only non-detected adapters that have an install command", () => {
    const rows = harnessInstallRows([
      adapter({ name: "a", status: "detected", installCommand: "brew install a" }),
      adapter({ name: "b", status: "missing", installCommand: "brew install b" }),
      adapter({ name: "c", status: "missing", installCommand: "  " }),
    ]);
    expect(rows.map((r) => r.name)).toEqual(["b"]);
  });

  it("sorts by display label and falls back to name", () => {
    const rows = harnessInstallRows([
      adapter({ name: "zeta", displayName: "", status: "missing", installCommand: "z" }),
      adapter({ name: "b", displayName: "Alpha", status: "missing", installCommand: "a" }),
    ]);
    expect(rows.map((r) => r.label)).toEqual(["Alpha", "zeta"]);
  });

  it("carries description, trimmed command, accent, and the canonical adapter icon", () => {
    const rows = harnessInstallRows([
      adapter({ name: "pi", status: "missing", installCommand: " curl -fsSL x | sh ", description: "Minimal agent.", accent: "#abc", iconSvg: '<svg data-adapter-icon="pi"></svg>' }),
    ]);
    expect(rows[0]).toEqual({ name: "pi", label: "X", description: "Minimal agent.", command: "curl -fsSL x | sh", accent: "#abc", iconSvg: '<svg data-adapter-icon="pi"></svg>' });
  });

  it("defaults missing descriptions to empty", () => {
    const rows = harnessInstallRows([adapter({ name: "pi", status: "missing", installCommand: "i" })]);
    expect(rows[0].description).toBe("");
  });
});
