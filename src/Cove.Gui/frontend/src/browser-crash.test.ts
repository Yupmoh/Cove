import { describe, expect, it } from "vitest";
import { PaneCrashState, crashReasonText } from "./browser-crash";

describe("crashReasonText", () => {
  it("provides a calm fallback when no reason is given", () => {
    expect(crashReasonText(null)).toContain("stopped responding");
    expect(crashReasonText("")).toContain("stopped responding");
    expect(crashReasonText("   ")).toContain("stopped responding");
  });

  it("embeds a provided reason", () => {
    expect(crashReasonText("oom")).toBe("The page process was terminated (oom).");
  });
});

describe("PaneCrashState", () => {
  it("starts live", () => {
    const s = new PaneCrashState();
    expect(s.lifecycle).toBe("live");
    expect(s.isCrashed).toBe(false);
    expect(s.reason).toBeNull();
  });

  it("transitions to crashed and records the reason", () => {
    const s = new PaneCrashState();
    expect(s.crash("gpu process gone")).toBe(true);
    expect(s.isCrashed).toBe(true);
    expect(s.reason).toBe("gpu process gone");
  });

  it("ignores a second crash while already crashed", () => {
    const s = new PaneCrashState();
    s.crash("first");
    expect(s.crash("second")).toBe(false);
    expect(s.reason).toBe("first");
  });

  it("recovers back to live and clears the reason", () => {
    const s = new PaneCrashState();
    s.crash("boom");
    expect(s.recover()).toBe(true);
    expect(s.lifecycle).toBe("live");
    expect(s.reason).toBeNull();
  });

  it("recover on a live pane is a no-op", () => {
    const s = new PaneCrashState();
    expect(s.recover()).toBe(false);
  });
});
