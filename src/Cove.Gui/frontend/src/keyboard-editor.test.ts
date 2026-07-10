import { describe, it, expect } from "vitest";
import {
  categorizeBindings,
  normalizeChord,
  isReservedChord,
  isValidChord,
  chordDisplay,
  canRecordChord,
  type KeybindDto,
} from "./keyboard-editor";

const bindings: KeybindDto[] = [
  { chord: "cmd+t", actionType: "app-command", action: "room.new", description: "New room" },
  { chord: "cmd+w", actionType: "app-command", action: "pane.close", description: "Close pane" },
  { chord: "cmd+shift+b", actionType: "app-command", action: "view.toggle-sidebar", description: "Toggle sidebar" },
  { chord: "cmd+c", actionType: "app-command", action: "terminal.copy-or-sigint", description: "Copy" },
  { chord: "cmd+shift+b", actionType: "app-command", action: "tool.browser", description: "Browser" },
];

describe("categorizeBindings", () => {
  it("groups bindings by action prefix", () => {
    const cats = categorizeBindings(bindings, ["cmd+shift+b"], []);
    expect(cats.length).toBeGreaterThanOrEqual(3);
    const rooms = cats.find((c) => c.name === "Rooms");
    expect(rooms).toBeDefined();
    expect(rooms!.rows.some((r) => r.action === "room.new")).toBe(true);
    const tools = cats.find((c) => c.name === "Tools");
    expect(tools).toBeDefined();
  });
  it("marks conflict rows", () => {
    const cats = categorizeBindings(bindings, ["cmd+shift+b"], []);
    const conflictRows = cats.flatMap((c) => c.rows).filter((r) => r.hasConflict);
    expect(conflictRows.length).toBeGreaterThanOrEqual(1);
  });
  it("marks custom rows from customActions", () => {
    const cats = categorizeBindings(bindings, [], ["room.new"]);
    const roomRow = cats.flatMap((c) => c.rows).find((r) => r.action === "room.new");
    expect(roomRow!.isCustom).toBe(true);
  });
});

describe("normalizeChord", () => {
  it("lowercases and orders modifiers", () => {
    expect(normalizeChord("Shift+Cmd+T")).toBe("cmd+shift+t");
    expect(normalizeChord("CTRL+SHIFT+A")).toBe("ctrl+shift+a");
  });
  it("handles alt and spaces", () => {
    expect(normalizeChord("alt shift x")).toBe("alt+shift+x");
  });
});

describe("isReservedChord", () => {
  it("rejects cmd+q, cmd+tab, ctrl+q", () => {
    expect(isReservedChord("cmd+q")).toBe(true);
    expect(isReservedChord("Cmd+Tab")).toBe(true);
    expect(isReservedChord("ctrl+q")).toBe(true);
  });
  it("allows normal chords", () => {
    expect(isReservedChord("cmd+t")).toBe(false);
    expect(isReservedChord("shift+enter")).toBe(false);
  });
});

describe("isValidChord", () => {
  it("accepts valid chords", () => {
    expect(isValidChord("cmd+t")).toBe(true);
    expect(isValidChord("cmd+shift+`")).toBe(true);
    expect(isValidChord("cmd+[")).toBe(true);
  });
  it("rejects empty", () => {
    expect(isValidChord("")).toBe(false);
    expect(isValidChord("   ")).toBe(false);
  });
});

describe("chordDisplay", () => {
  it("converts modifiers to symbols", () => {
    expect(chordDisplay("cmd+t")).toBe("⌘T");
    expect(chordDisplay("cmd+shift+t")).toBe("⌘⇧T");
    expect(chordDisplay("ctrl+a")).toBe("⌃A");
  });
  it("converts special keys", () => {
    expect(chordDisplay("shift+enter")).toBe("⇧↵");
    expect(chordDisplay("cmd+up")).toBe("⌘↑");
  });
});

describe("canRecordChord", () => {
  it("allows unbound chord", () => {
    const result = canRecordChord("cmd+shift+x", "room.new", bindings);
    expect(result.valid).toBe(true);
    expect(result.conflictAction).toBeNull();
  });
  it("allows re-binding own chord", () => {
    const result = canRecordChord("cmd+t", "room.new", bindings);
    expect(result.valid).toBe(true);
  });
  it("rejects chord bound to different action", () => {
    const result = canRecordChord("cmd+w", "room.new", bindings);
    expect(result.valid).toBe(false);
    expect(result.conflictAction).toBe("pane.close");
  });
  it("rejects reserved chord", () => {
    const result = canRecordChord("cmd+q", "room.new", bindings);
    expect(result.valid).toBe(false);
  });
});
