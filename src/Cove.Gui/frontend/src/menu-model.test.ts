import { describe, it, expect } from "vitest";
import { chordToAccelerator, buildAcceleratorMap, menuIA, buildMenu, menuActionIds, menuChordSet } from "./menu-model";

describe("chordToAccelerator", () => {
  it("maps cmd to CmdOrCtrl and uppercases letters", () => {
    expect(chordToAccelerator("cmd+t")).toBe("CmdOrCtrl+T");
  });
  it("orders modifiers Cmd, Ctrl, Alt, Shift regardless of input order", () => {
    expect(chordToAccelerator("shift+cmd+d")).toBe("CmdOrCtrl+Shift+D");
  });
  it("keeps punctuation keys literal", () => {
    expect(chordToAccelerator("cmd+=")).toBe("CmdOrCtrl+=");
    expect(chordToAccelerator("cmd+[")).toBe("CmdOrCtrl+[");
    expect(chordToAccelerator("cmd+shift+`")).toBe("CmdOrCtrl+Shift+`");
  });
  it("maps named keys to title case", () => {
    expect(chordToAccelerator("cmd+shift+up")).toBe("CmdOrCtrl+Shift+Up");
    expect(chordToAccelerator("cmd+enter")).toBe("CmdOrCtrl+Enter");
  });
  it("returns empty string for an empty chord", () => {
    expect(chordToAccelerator("")).toBe("");
  });
});

describe("buildAcceleratorMap", () => {
  it("maps each action to its accelerator", () => {
    const m = buildAcceleratorMap([
      { action: "room.new", chord: "cmd+t" },
      { action: "pane.split-right", chord: "cmd+d" },
    ]);
    expect(m["room.new"]).toBe("CmdOrCtrl+T");
    expect(m["pane.split-right"]).toBe("CmdOrCtrl+D");
  });
});

describe("menuIA", () => {
  it("exposes the top-level menu sections", () => {
    const labels = menuIA().map((s) => s.role ?? s.label);
    expect(labels).toContain("File");
    expect(labels).toContain("View");
    expect(labels).toContain("Pane");
    expect(labels).toContain("appMenu");
    expect(labels).toContain("windowMenu");
  });
});

describe("buildMenu", () => {
  it("injects accelerators sourced from the bindings", () => {
    const menu = buildMenu([{ action: "room.new", chord: "cmd+shift+n" }]);
    const file = menu.find((s) => s.label === "File")!;
    const newRoom = file.items!.find((i) => i.action === "room.new")!;
    expect(newRoom.accelerator).toBe("CmdOrCtrl+Shift+N");
  });
  it("omits an accelerator when the action is unbound", () => {
    const menu = buildMenu([]);
    const file = menu.find((s) => s.label === "File")!;
    const newRoom = file.items!.find((i) => i.action === "room.new")!;
    expect(newRoom.accelerator).toBeUndefined();
  });
  it("re-sourcing after a rebind updates the accelerator", () => {
    const before = buildMenu([{ action: "room.new", chord: "cmd+t" }]);
    const after = buildMenu([{ action: "room.new", chord: "cmd+shift+r" }]);
    const acc = (m: ReturnType<typeof buildMenu>) => m.find((s) => s.label === "File")!.items!.find((i) => i.action === "room.new")!.accelerator;
    expect(acc(before)).toBe("CmdOrCtrl+T");
    expect(acc(after)).toBe("CmdOrCtrl+Shift+R");
  });
});

describe("buildMenu enabled workaround", () => {
  it("marks every actionable leaf as enabled", () => {
    const menu = buildMenu([]);
    const view = menu.find((s) => s.label === "View")!;
    for (const item of view.items!) {
      if (item.separator) continue;
      expect(item.enabled).toBe(true);
    }
  });
  it("does not mark separators as enabled", () => {
    const menu = buildMenu([]);
    const view = menu.find((s) => s.label === "View")!;
    const sep = view.items!.find((i) => i.separator);
    expect(sep).toBeDefined();
    expect(sep!.enabled).toBeUndefined();
  });
  it("leaves role-only top-level sections without an items array", () => {
    const menu = buildMenu([]);
    const appMenu = menu.find((s) => s.role === "appMenu")!;
    expect(appMenu.items).toBeUndefined();
  });
});

describe("menuActionIds", () => {
  it("collects every action referenced by the menu", () => {
    const ids = menuActionIds();
    expect(ids.has("room.new")).toBe(true);
    expect(ids.has("pane.split-right")).toBe(true);
    expect(ids.has("view.zen-mode")).toBe(true);
  });
});

describe("menuChordSet", () => {
  it("collects only the chords of menu-referenced actions", () => {
    const set = menuChordSet([
      { action: "room.new", chord: "cmd+t" },
      { action: "pane.split-right", chord: "cmd+d" },
      { action: "not.in.menu", chord: "cmd+j" },
    ]);
    expect(set.has("cmd+t")).toBe(true);
    expect(set.has("cmd+d")).toBe(true);
    expect(set.has("cmd+j")).toBe(false);
  });
  it("keeps the last chord when an action is bound twice", () => {
    const set = menuChordSet([
      { action: "tool.palette", chord: "cmd+shift+t" },
      { action: "tool.palette", chord: "cmd+k" },
    ]);
    expect(set.has("cmd+k")).toBe(true);
    expect(set.has("cmd+shift+t")).toBe(false);
  });
});
