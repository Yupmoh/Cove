import { describe, it, expect } from "vitest";
import { BAY_ICON_CHOICES, bayMark } from "./bay-icons";

describe("bay-icons", () => {
  it("offers unique abstract line-art choices", () => {
    expect(BAY_ICON_CHOICES.length).toBeGreaterThanOrEqual(8);
    expect(new Set(BAY_ICON_CHOICES.map((choice) => choice.id)).size).toBe(BAY_ICON_CHOICES.length);
    for (const choice of BAY_ICON_CHOICES) {
      expect(choice.svg).toContain("<svg");
      expect(choice.svg).toContain("currentColor");
    }
  });

  it("resolves a persisted abstract mark", () => {
    expect(bayMark({ kind: "mark", value: "orbit" })?.id).toBe("orbit");
  });

  it("maps legacy emoji icons to abstract marks without rendering emoji", () => {
    const mark = bayMark({ kind: "emoji", value: "🚀" });
    expect(mark).not.toBeNull();
    expect(mark?.svg).toContain("<svg");
    expect(mark?.svg).not.toContain("🚀");
  });

  it("returns null for null, empty, or unknown icons", () => {
    expect(bayMark(null)).toBeNull();
    expect(bayMark(undefined)).toBeNull();
    expect(bayMark({ kind: "mark", value: "" })).toBeNull();
    expect(bayMark({ kind: "glyph", value: "x" })).toBeNull();
  });

  it("round-trips a mark carried on a bay.list summary", () => {
    const summary = { iconKind: "mark", iconValue: "prism" } as { iconKind?: string | null; iconValue?: string | null };
    const icon = summary.iconKind ? { kind: summary.iconKind, value: summary.iconValue ?? "" } : null;
    expect(bayMark(icon)?.id).toBe("prism");
  });
});
