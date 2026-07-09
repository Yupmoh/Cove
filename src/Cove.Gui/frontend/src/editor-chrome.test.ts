import { describe, it, expect } from "vitest";
import {
  buildBreadcrumbs,
  toggleWordWrap,
  wordWrapStorageKey,
  minimapStorageKey,
  latestAgentEdit,
  formatAgentEditChip,
} from "./editor-chrome";

describe("buildBreadcrumbs", () => {
  it("splits an absolute path into cumulative segments", () => {
    const segs = buildBreadcrumbs("/Users/moh/src/app.ts");
    expect(segs.map((s) => s.label)).toEqual(["Users", "moh", "src", "app.ts"]);
    expect(segs.map((s) => s.path)).toEqual(["/Users", "/Users/moh", "/Users/moh/src", "/Users/moh/src/app.ts"]);
  });

  it("handles a relative path", () => {
    const segs = buildBreadcrumbs("src/app.ts");
    expect(segs.map((s) => s.path)).toEqual(["src", "src/app.ts"]);
  });

  it("normalizes backslashes", () => {
    const segs = buildBreadcrumbs("src\\a\\b.ts");
    expect(segs.map((s) => s.label)).toEqual(["src", "a", "b.ts"]);
  });

  it("returns empty for empty input", () => {
    expect(buildBreadcrumbs("")).toEqual([]);
  });
});

describe("word-wrap", () => {
  it("toggles between on and off", () => {
    expect(toggleWordWrap("on")).toBe("off");
    expect(toggleWordWrap("off")).toBe("on");
  });

  it("builds stable per-pane storage keys", () => {
    expect(wordWrapStorageKey("p1")).toBe("cove.editor.wordWrap.p1");
    expect(minimapStorageKey("p1")).toBe("cove.editor.minimap.p1");
  });
});

describe("latestAgentEdit", () => {
  it("returns null when there are no entries", () => {
    expect(latestAgentEdit([])).toBeNull();
  });

  it("picks the most recent entry by timestamp", () => {
    const chip = latestAgentEdit([
      { sessionId: "s1", toolUseId: "t1", startLine: 1, endLine: 3, at: "2026-01-01T00:00:00Z" },
      { sessionId: "s2", toolUseId: "t2", startLine: 5, endLine: 5, at: "2026-02-01T00:00:00Z" },
    ]);
    expect(chip).not.toBeNull();
    expect(chip!.toolUseId).toBe("t2");
    expect(chip!.lineRange).toBe("5");
  });

  it("renders a range when start and end differ", () => {
    const chip = latestAgentEdit([{ sessionId: "s", toolUseId: "t", startLine: 2, endLine: 9, at: "2026-01-01T00:00:00Z" }]);
    expect(chip!.lineRange).toBe("2-9");
    expect(formatAgentEditChip(chip!)).toContain("L2-9");
    expect(formatAgentEditChip(chip!)).toContain("t");
  });
});
