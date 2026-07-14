import { describe, it, expect } from "vitest";
import { createKeyboardProtocolTracker, shiftEnterSequence } from "./terminal-keyboard";

function enc(s: string): Uint8Array {
  return new TextEncoder().encode(s);
}

describe("createKeyboardProtocolTracker", () => {
  it("defaults to legacy before any negotiation", () => {
    const t = createKeyboardProtocolTracker();
    expect(t.encoding()).toBe("legacy");
  });

  it("detects kitty progressive enhancement push", () => {
    const t = createKeyboardProtocolTracker();
    t.push(enc("\x1b[>1u"));
    expect(t.encoding()).toBe("kitty");
  });

  it("reverts to legacy when kitty flags are popped", () => {
    const t = createKeyboardProtocolTracker();
    t.push(enc("\x1b[>1u"));
    t.push(enc("\x1b[<1u"));
    expect(t.encoding()).toBe("legacy");
  });

  it("pops all pushed levels with a default pop count", () => {
    const t = createKeyboardProtocolTracker();
    t.push(enc("\x1b[>1u"));
    t.push(enc("\x1b[<u"));
    expect(t.encoding()).toBe("legacy");
  });

  it("honours a kitty set with zero flags as disabled", () => {
    const t = createKeyboardProtocolTracker();
    t.push(enc("\x1b[>1u"));
    t.push(enc("\x1b[=0;1u"));
    expect(t.encoding()).toBe("legacy");
  });

  it("detects modifyOtherKeys level two", () => {
    const t = createKeyboardProtocolTracker();
    t.push(enc("\x1b[>4;2m"));
    expect(t.encoding()).toBe("modifyOtherKeys");
  });

  it("treats modifyOtherKeys level zero as disabled", () => {
    const t = createKeyboardProtocolTracker();
    t.push(enc("\x1b[>4;2m"));
    t.push(enc("\x1b[>4;0m"));
    expect(t.encoding()).toBe("legacy");
  });

  it("treats a bare modifyOtherKeys reset as disabled", () => {
    const t = createKeyboardProtocolTracker();
    t.push(enc("\x1b[>4;2m"));
    t.push(enc("\x1b[>4m"));
    expect(t.encoding()).toBe("legacy");
  });

  it("prefers kitty over modifyOtherKeys when both are active", () => {
    const t = createKeyboardProtocolTracker();
    t.push(enc("\x1b[>4;2m"));
    t.push(enc("\x1b[>1u"));
    expect(t.encoding()).toBe("kitty");
  });

  it("tracks a sequence split across chunk boundaries", () => {
    const t = createKeyboardProtocolTracker();
    t.push(enc("\x1b[>"));
    t.push(enc("1"));
    t.push(enc("u"));
    expect(t.encoding()).toBe("kitty");
  });

  it("ignores a terminal query response introduced by question mark", () => {
    const t = createKeyboardProtocolTracker();
    t.push(enc("\x1b[?1u"));
    expect(t.encoding()).toBe("legacy");
  });

  it("ignores ordinary text and colour sequences", () => {
    const t = createKeyboardProtocolTracker();
    t.push(enc("hello \x1b[31mworld\x1b[0m\r\n$ "));
    expect(t.encoding()).toBe("legacy");
  });

  it("still detects kitty when embedded in replayed scrollback", () => {
    const t = createKeyboardProtocolTracker();
    t.push(enc("\x1b[2J\x1b[H\x1b[>13u\x1b[38;5;244mprompt\x1b[0m "));
    expect(t.encoding()).toBe("kitty");
  });

  it("clears state on reset", () => {
    const t = createKeyboardProtocolTracker();
    t.push(enc("\x1b[>1u"));
    t.reset();
    expect(t.encoding()).toBe("legacy");
  });
});

describe("shiftEnterSequence", () => {
  it("encodes kitty CSI-u for shift+enter", () => {
    expect(shiftEnterSequence("kitty")).toBe("\x1b[13;2u");
  });

  it("encodes modifyOtherKeys CSI ~ for shift+enter", () => {
    expect(shiftEnterSequence("modifyOtherKeys")).toBe("\x1b[27;2;13~");
  });

  it("falls back to backslash carriage-return for legacy", () => {
    expect(shiftEnterSequence("legacy")).toBe("\\\r");
  });
});
