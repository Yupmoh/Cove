import { describe, it, expect } from "vitest";
import { restoredSummaryText, shouldShowRestoreToast } from "./restore-summary";

describe("restoredSummaryText", () => {
  it("joins restored and fresh with a middot", () => {
    expect(restoredSummaryText(3, 1, 0)).toBe("restored 3 sessions · 1 started fresh");
  });

  it("uses the singular for exactly one restored session", () => {
    expect(restoredSummaryText(1, 0, 0)).toBe("restored 1 session");
  });

  it("returns an empty string when nothing happened", () => {
    expect(restoredSummaryText(0, 0, 0)).toBe("");
  });

  it("reports fresh-only and skipped segments", () => {
    expect(restoredSummaryText(0, 2, 1)).toBe("2 started fresh · 1 skipped");
  });

  it("reports all three segments", () => {
    expect(restoredSummaryText(2, 1, 3)).toBe("restored 2 sessions · 1 started fresh · 3 skipped");
  });
});

describe("shouldShowRestoreToast", () => {
  it("shows a fresh boot summary that has not been shown before", () => {
    expect(shouldShowRestoreToast("2026-07-11T00:00:00Z", null, "restored 1 session")).toBe(true);
  });

  it("does not repeat a summary already shown for this boot", () => {
    expect(shouldShowRestoreToast("2026-07-11T00:00:00Z", "2026-07-11T00:00:00Z", "restored 1 session")).toBe(false);
  });

  it("shows again after a newer boot even if an older boot was shown", () => {
    expect(shouldShowRestoreToast("2026-07-11T09:00:00Z", "2026-07-11T00:00:00Z", "restored 1 session")).toBe(true);
  });

  it("suppresses empty summaries", () => {
    expect(shouldShowRestoreToast("2026-07-11T00:00:00Z", null, "")).toBe(false);
  });

  it("suppresses summaries without a boot stamp", () => {
    expect(shouldShowRestoreToast("", null, "restored 1 session")).toBe(false);
  });
});
