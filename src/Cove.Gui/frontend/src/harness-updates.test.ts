import { describe, it, expect } from "vitest";
import {
  HARNESS_UPDATE_CHECK_INTERVAL_MS,
  filterToastableUpdates,
  parseDismissed,
  recordDismissal,
  type HarnessUpdate,
} from "./harness-updates";

function update(over: Partial<HarnessUpdate> = {}): HarnessUpdate {
  return {
    name: "codex",
    displayName: "Codex",
    installedVersion: "1.0.0",
    latestVersion: "2.0.0",
    updateCommand: "npm install -g @openai/codex@latest",
    ...over,
  };
}

describe("parseDismissed", () => {
  it("returns an empty map for null storage", () => {
    expect(parseDismissed(null)).toEqual({});
  });

  it("returns an empty map for malformed json", () => {
    expect(parseDismissed("{oops")).toEqual({});
  });

  it("returns an empty map for non-object json", () => {
    expect(parseDismissed("[1,2]")).toEqual({});
    expect(parseDismissed('"str"')).toEqual({});
  });

  it("keeps only string values", () => {
    expect(parseDismissed('{"codex":"2.0.0","junk":7}')).toEqual({ codex: "2.0.0" });
  });
});

describe("filterToastableUpdates", () => {
  it("passes updates that were never dismissed", () => {
    expect(filterToastableUpdates([update()], {})).toHaveLength(1);
  });

  it("hides an update whose latest version was dismissed", () => {
    expect(filterToastableUpdates([update()], { codex: "2.0.0" })).toHaveLength(0);
  });

  it("shows again when a newer latest appears after dismissal", () => {
    expect(filterToastableUpdates([update({ latestVersion: "2.1.0" })], { codex: "2.0.0" })).toHaveLength(1);
  });

  it("filters per harness independently", () => {
    const updates = [update(), update({ name: "omp", displayName: "omp" })];
    const shown = filterToastableUpdates(updates, { codex: "2.0.0" });
    expect(shown.map((u) => u.name)).toEqual(["omp"]);
  });
});

describe("recordDismissal", () => {
  it("records the dismissed latest version for the harness", () => {
    expect(recordDismissal({}, update())).toEqual({ codex: "2.0.0" });
  });

  it("overwrites an older dismissal and keeps other entries", () => {
    const next = recordDismissal({ codex: "1.5.0", omp: "3.0.0" }, update());
    expect(next).toEqual({ codex: "2.0.0", omp: "3.0.0" });
  });
});

describe("check interval", () => {
  it("is ten minutes", () => {
    expect(HARNESS_UPDATE_CHECK_INTERVAL_MS).toBe(600_000);
  });
});
