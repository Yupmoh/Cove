import { describe, it, expect } from "vitest";
import { eventToChord, buildChordMap, resolveDispatch, defaultBindings, type ResolvedBinding } from "./keymap-dispatch";

const bindings: ResolvedBinding[] = [
  { chord: "cmd+t", action: "shore.new", actionType: "app-command" },
  { chord: "cmd+d", action: "nook.split-right", actionType: "app-command" },
  { chord: "cmd+b", action: "view.toggle-sidebar", actionType: "app-command" },
  { chord: "cmd+c", action: "terminal.copy-or-sigint", actionType: "app-command" },
  { chord: "cmd+q", action: "app.quit", actionType: "app-command" },
];

describe("eventToChord", () => {
  it("orders modifiers and lowercases the key", () => {
    expect(eventToChord({ metaKey: true, shiftKey: true, ctrlKey: false, altKey: false, key: "D" })).toBe("cmd+shift+d");
  });
  it("maps space to the space token", () => {
    expect(eventToChord({ metaKey: true, shiftKey: false, ctrlKey: false, altKey: false, key: " " })).toBe("cmd+space");
  });
  it("returns empty when only a modifier is pressed", () => {
    expect(eventToChord({ metaKey: true, shiftKey: false, ctrlKey: false, altKey: false, key: "Meta" })).toBe("");
  });
});

describe("buildChordMap", () => {
  it("indexes bindings by normalized chord", () => {
    const m = buildChordMap(bindings);
    expect(m.get("cmd+t")?.action).toBe("shore.new");
  });
});

describe("resolveDispatch", () => {
  const map = buildChordMap(bindings);
  const menuChords = new Set(["cmd+t"]);
  it("skips reserved chords so the OS handles them", () => {
    expect(resolveDispatch("cmd+q", map, menuChords).kind).toBe("reserved");
  });
  it("marks a chord owned by a menu accelerator so the dispatcher does not double-fire", () => {
    const r = resolveDispatch("cmd+t", map, menuChords);
    expect(r).toEqual({ kind: "menu-owned", action: "shore.new" });
  });
  it("still dispatches a menu action reached by a non-menu chord", () => {
    const withAlias = buildChordMap([...bindings, { chord: "cmd+shift+t", action: "shore.new", actionType: "app-command" }]);
    expect(resolveDispatch("cmd+shift+t", withAlias, menuChords)).toEqual({ kind: "dispatch", action: "shore.new", actionType: "app-command" });
  });
  it("dispatches a non-menu app action", () => {
    const r = resolveDispatch("cmd+d", map, menuChords);
    expect(r).toEqual({ kind: "dispatch", action: "nook.split-right", actionType: "app-command" });
  });
  it("does not globally dispatch terminal-scoped actions", () => {
    expect(resolveDispatch("cmd+c", map, menuChords).kind).toBe("terminal");
  });
  it("returns none for an unbound chord", () => {
    expect(resolveDispatch("cmd+j", map, menuChords).kind).toBe("none");
  });
});

describe("defaultBindings", () => {
  it("covers the spec view, nook, shore, bay and tool chords", () => {
    const actions = new Set(defaultBindings().map((b) => b.action));
    expect(actions.has("view.zen-mode")).toBe(true);
    expect(actions.has("nook.split-down")).toBe(true);
    expect(actions.has("shore.new")).toBe(true);
    expect(actions.has("bay.switch-1")).toBe(true);
    expect(actions.has("tool.git")).toBe(true);
  });
  it("binds zen mode to cmd+shift+backtick", () => {
    const zen = defaultBindings().find((b) => b.action === "view.zen-mode")!;
    expect(zen.chord).toBe("cmd+shift+`");
  });
});
