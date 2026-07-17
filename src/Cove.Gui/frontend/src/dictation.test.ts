import { describe, expect, it } from "vitest";
import { classifyDictationTarget, encodeNookText, type FocusDescriptor } from "./dictation";

const focus = (partial: Partial<FocusDescriptor>): FocusDescriptor => ({
  tagName: "DIV",
  inputType: "",
  readOnly: false,
  disabled: false,
  isContentEditable: false,
  className: "",
  ...partial,
});

describe("classifyDictationTarget", () => {
  it("routes a focused text input to editable insertion", () => {
    expect(classifyDictationTarget(focus({ tagName: "INPUT", inputType: "text" }), "nook-1")).toBe("editable");
  });

  it("routes a textarea to editable insertion", () => {
    expect(classifyDictationTarget(focus({ tagName: "TEXTAREA" }), null)).toBe("editable");
  });

  it("routes contenteditable to editable insertion", () => {
    expect(classifyDictationTarget(focus({ isContentEditable: true }), null)).toBe("editable");
  });

  it("skips readonly and disabled inputs in favour of the focused nook", () => {
    expect(classifyDictationTarget(focus({ tagName: "INPUT", inputType: "text", readOnly: true }), "nook-1")).toBe("nook");
    expect(classifyDictationTarget(focus({ tagName: "INPUT", inputType: "text", disabled: true }), "nook-1")).toBe("nook");
  });

  it("routes checkbox and button inputs to the nook, not insertion", () => {
    expect(classifyDictationTarget(focus({ tagName: "INPUT", inputType: "checkbox" }), "nook-1")).toBe("nook");
    expect(classifyDictationTarget(focus({ tagName: "INPUT", inputType: "button" }), "nook-1")).toBe("nook");
  });

  it("falls back to the focused nook for body focus", () => {
    expect(classifyDictationTarget(focus({ tagName: "BODY" }), "nook-9")).toBe("nook");
  });

  it("returns none when nothing is editable and no nook focused", () => {
    expect(classifyDictationTarget(focus({ tagName: "BODY" }), null)).toBe("none");
  });

  it("treats xterm helper textarea as terminal, not editable", () => {
    expect(classifyDictationTarget(focus({ tagName: "TEXTAREA", className: "xterm-helper-textarea" }), "nook-3")).toBe("nook");
  });
});

describe("encodeNookText", () => {
  it("base64-encodes utf-8 text", () => {
    expect(atob(encodeNookText("hi"))).toBe("hi");
  });

  it("survives non-ascii", () => {
    const encoded = encodeNookText("héllo wörld");
    expect(new TextDecoder().decode(Uint8Array.from(atob(encoded), c => c.charCodeAt(0)))).toBe("héllo wörld");
  });
});
