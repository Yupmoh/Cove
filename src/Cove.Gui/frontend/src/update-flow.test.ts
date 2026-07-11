import { describe, it, expect } from "vitest";
import { nextUpdateState, updateButtonLabel, updateAffordanceVisible, type UpdateState } from "./update-flow";

describe("nextUpdateState happy path", () => {
  it("walks idle -> checking -> available -> downloading -> readyToApply -> applying", () => {
    let s: UpdateState = { kind: "idle" };
    s = nextUpdateState(s, { type: "check" });
    expect(s.kind).toBe("checking");
    s = nextUpdateState(s, { type: "checkedAvailable", version: "0.2.0", notes: "https://example/notes" });
    expect(s).toEqual({ kind: "available", version: "0.2.0", notes: "https://example/notes" });
    s = nextUpdateState(s, { type: "download" });
    expect(s.kind).toBe("downloading");
    s = nextUpdateState(s, { type: "downloaded", handle: "abc", version: "0.2.0" });
    expect(s).toEqual({ kind: "readyToApply", handle: "abc", version: "0.2.0" });
    s = nextUpdateState(s, { type: "apply" });
    expect(s.kind).toBe("applying");
  });

  it("resolves a check with no update to upToDate", () => {
    const s = nextUpdateState({ kind: "checking" }, { type: "checkedUpToDate" });
    expect(s.kind).toBe("upToDate");
  });

  it("allows re-checking from idle, upToDate, available and failed", () => {
    expect(nextUpdateState({ kind: "idle" }, { type: "check" }).kind).toBe("checking");
    expect(nextUpdateState({ kind: "upToDate" }, { type: "check" }).kind).toBe("checking");
    expect(nextUpdateState({ kind: "available", version: "1.0.0", notes: null }, { type: "check" }).kind).toBe("checking");
    expect(nextUpdateState({ kind: "failed", message: "x" }, { type: "check" }).kind).toBe("checking");
  });
});

describe("nextUpdateState failure at each step", () => {
  it("collapses a failed check to failed(message)", () => {
    const s = nextUpdateState({ kind: "checking" }, { type: "error", message: "network down" });
    expect(s).toEqual({ kind: "failed", message: "network down" });
  });

  it("collapses a failed download to failed(message)", () => {
    const s = nextUpdateState({ kind: "downloading" }, { type: "error", message: "signature failed" });
    expect(s).toEqual({ kind: "failed", message: "signature failed" });
  });

  it("collapses a failed apply to failed(message)", () => {
    const s = nextUpdateState({ kind: "applying" }, { type: "error", message: "unknown handle" });
    expect(s).toEqual({ kind: "failed", message: "unknown handle" });
  });

  it("retries a failed state back into checking", () => {
    const s = nextUpdateState({ kind: "failed", message: "boom" }, { type: "retry" });
    expect(s.kind).toBe("checking");
  });
});

describe("nextUpdateState guards invalid transitions", () => {
  it("ignores a download event unless an update is available", () => {
    const s: UpdateState = { kind: "idle" };
    expect(nextUpdateState(s, { type: "download" })).toBe(s);
  });

  it("ignores an apply event unless a download is ready", () => {
    const s: UpdateState = { kind: "checking" };
    expect(nextUpdateState(s, { type: "apply" })).toBe(s);
  });
});

describe("updateButtonLabel", () => {
  it("maps every state to its button text", () => {
    expect(updateButtonLabel({ kind: "idle" })).toBe("Check for updates");
    expect(updateButtonLabel({ kind: "checking" })).toBe("Checking…");
    expect(updateButtonLabel({ kind: "upToDate" })).toBe("Up to date");
    expect(updateButtonLabel({ kind: "available", version: "0.2.0", notes: null })).toBe("Update to 0.2.0");
    expect(updateButtonLabel({ kind: "downloading" })).toBe("Downloading…");
    expect(updateButtonLabel({ kind: "readyToApply", handle: "h", version: "0.2.0" })).toBe("Restart to update");
    expect(updateButtonLabel({ kind: "applying" })).toBe("Applying…");
    expect(updateButtonLabel({ kind: "failed", message: "x" })).toBe("Retry update");
  });
});

describe("updateAffordanceVisible", () => {
  it("shows the toolbar affordance only when an update is available or ready", () => {
    expect(updateAffordanceVisible({ kind: "available", version: "0.2.0", notes: null })).toBe(true);
    expect(updateAffordanceVisible({ kind: "readyToApply", handle: "h", version: "0.2.0" })).toBe(true);
    expect(updateAffordanceVisible({ kind: "idle" })).toBe(false);
    expect(updateAffordanceVisible({ kind: "checking" })).toBe(false);
    expect(updateAffordanceVisible({ kind: "downloading" })).toBe(false);
    expect(updateAffordanceVisible({ kind: "applying" })).toBe(false);
    expect(updateAffordanceVisible({ kind: "upToDate" })).toBe(false);
    expect(updateAffordanceVisible({ kind: "failed", message: "x" })).toBe(false);
  });
});
