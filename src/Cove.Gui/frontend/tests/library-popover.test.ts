import { describe, it, expect } from "vitest";
import { _testFuzzyScore } from "../src/library-popover";

describe("fuzzyScore", () => {
  it("returns 0 when query not matched at all", () => {
    expect(_testFuzzyScore("hello", "terminal", "xyz", false)).toBe(0);
  });

  it("scores exact title match", () => {
    expect(_testFuzzyScore("deploy", "terminal", "deploy", false)).toBe(60);
  });

  it("scores partial title match", () => {
    expect(_testFuzzyScore("deployment-log", "terminal", "deploy", false)).toBe(60);
  });

  it("falls through to nookType when title incomplete", () => {
    expect(_testFuzzyScore("d", "deploy", "deploy", false)).toBe(20);
  });

  it("active bay boost dominates", () => {
    const passive = _testFuzzyScore("deploy", "terminal", "deploy", false);
    const active = _testFuzzyScore("deploy", "terminal", "deploy", true);
    expect(active - passive).toBe(50);
    expect(active).toBe(110);
  });

  it("active bay outranks non-active with better title match", () => {
    const betterTitlePassive = _testFuzzyScore("deploy-script", "terminal", "deploy", false);
    const worseTitleActive = _testFuzzyScore("d", "deploy", "deploy", true);
    expect(worseTitleActive).toBeGreaterThan(betterTitlePassive);
  });

  it("returns 0 for empty query", () => {
    expect(_testFuzzyScore("deploy", "terminal", "", false)).toBe(0);
  });
});
