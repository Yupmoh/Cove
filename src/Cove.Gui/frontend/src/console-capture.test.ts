import { afterEach, describe, expect, it, vi } from "vitest";
import {
  formatConsoleMessage,
  installConsoleCapture,
  stringifyArg,
  truncate,
} from "./console-capture";

afterEach(() => {
  vi.unstubAllGlobals();
});

function stubWindow(): void {
  const testWindow = new EventTarget() as EventTarget & {
    onerror: OnErrorEventHandler;
  };
  testWindow.onerror = null;
  vi.stubGlobal("window", testWindow);
}

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

describe("console capture lifecycle", () => {
  it("idempotently restores the exact prior console methods and supports reinstall", () => {
    stubWindow();
    const originalWarn = console.warn;
    const originalError = console.error;
    const priorWarn = vi.fn();
    const priorError = vi.fn();
    console.warn = priorWarn;
    console.error = priorError;

    try {
      const forward = vi.fn();
      const dispose = installConsoleCapture(forward);

      expect(console.warn).not.toBe(priorWarn);
      expect(console.error).not.toBe(priorError);
      console.warn("warning", 7);
      console.error("failure");
      expect(priorWarn).toHaveBeenCalledWith("warning", 7);
      expect(priorError).toHaveBeenCalledWith("failure");
      expect(forward).toHaveBeenNthCalledWith(1, "warn", "warning 7");
      expect(forward).toHaveBeenNthCalledWith(2, "error", "failure");

      dispose();
      dispose();
      expect(console.warn).toBe(priorWarn);
      expect(console.error).toBe(priorError);

      const disposeReinstalled = installConsoleCapture(vi.fn());
      expect(console.warn).not.toBe(priorWarn);
      expect(console.error).not.toBe(priorError);
      disposeReinstalled();
      expect(console.warn).toBe(priorWarn);
      expect(console.error).toBe(priorError);
    } finally {
      console.warn = originalWarn;
      console.error = originalError;
    }
  });

  it("does not let a late failed send recurse into replacement capture", async () => {
    stubWindow();
    const originalWarn = console.warn;
    const originalError = console.error;
    const priorWarn = vi.fn();
    const priorError = vi.fn();
    console.warn = priorWarn;
    console.error = priorError;

    let rejectSend!: (reason: Error) => void;
    const pendingSend = new Promise<void>((_resolve, reject) => {
      rejectSend = reject;
    });

    let disposeReplacement: (() => void) | undefined;
    try {
      const firstForward = vi.fn(() => pendingSend);
      const disposeFirst = installConsoleCapture(firstForward);
      console.warn("first owner");
      expect(firstForward).toHaveBeenCalledOnce();

      disposeFirst();
      const replacementForward = vi.fn();
      disposeReplacement = installConsoleCapture(replacementForward);

      rejectSend(new Error("late send failure"));
      await pendingSend.catch(() => {});
      await Promise.resolve();

      expect(firstForward).toHaveBeenCalledOnce();
      expect(replacementForward).not.toHaveBeenCalled();
      expect(priorError).not.toHaveBeenCalled();
    } finally {
      disposeReplacement?.();
      console.warn = originalWarn;
      console.error = originalError;
    }
  });
});
