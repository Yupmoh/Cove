import { describe, expect, it } from "vitest";
import { formatConsoleMessage, stringifyArg, truncate } from "./console-capture";

describe("console capture formatting", () => {
  it("joins mixed arguments into a single space-delimited line", () => {
    expect(formatConsoleMessage(["boot", 3, true])).toBe("boot 3 true");
  });

  it("renders undefined and null as literals", () => {
    expect(stringifyArg(undefined)).toBe("undefined");
    expect(stringifyArg(null)).toBe("null");
  });

  it("serializes plain objects as JSON", () => {
    expect(stringifyArg({ a: 1, b: "x" })).toBe('{"a":1,"b":"x"}');
  });

  it("keeps a string argument verbatim", () => {
    expect(stringifyArg("already text")).toBe("already text");
  });

  it("renders an Error via its stack or name and message", () => {
    const err = new Error("kaboom");
    expect(stringifyArg(err)).toContain("kaboom");
  });

  it("survives circular references without throwing", () => {
    const cyclic: Record<string, unknown> = { name: "loop" };
    cyclic.self = cyclic;
    const rendered = stringifyArg(cyclic);
    expect(rendered).toContain("loop");
    expect(rendered).toContain("[Circular]");
  });

  it("does not truncate short messages", () => {
    expect(truncate("short")).toBe("short");
  });

  it("truncates over-long messages to 2000 chars plus an ellipsis", () => {
    const long = "a".repeat(2500);
    const result = truncate(long);
    expect(result.length).toBe(2001);
    expect(result.endsWith("…")).toBe(true);
  });

  it("truncates the joined message when combined arguments exceed the cap", () => {
    const result = formatConsoleMessage(["x".repeat(1500), "y".repeat(1500)]);
    expect(result.length).toBe(2001);
  });
});
