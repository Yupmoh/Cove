import { describe, it, expect } from "vitest";
import { BAY_ICON_CHOICES, bayGlyph } from "./bay-icons";

describe("bay-icons", () => {
  it("offers a curated set of emoji choices", () => {
    expect(BAY_ICON_CHOICES.length).toBe(16);
    expect(BAY_ICON_CHOICES).toContain("🚀");
    expect(new Set(BAY_ICON_CHOICES).size).toBe(BAY_ICON_CHOICES.length);
  });

  it("returns the emoji for an emoji icon", () => {
    expect(bayGlyph({ kind: "emoji", value: "🌊" })).toBe("🌊");
  });

  it("returns null for null or undefined", () => {
    expect(bayGlyph(null)).toBeNull();
    expect(bayGlyph(undefined)).toBeNull();
  });

  it("returns null for an unknown kind", () => {
    expect(bayGlyph({ kind: "glyph", value: "x" })).toBeNull();
  });

  it("returns null for an emoji icon with an empty value", () => {
    expect(bayGlyph({ kind: "emoji", value: "" })).toBeNull();
  });

  it("round-trips an icon carried on a bay.list summary", () => {
    const summary = { iconKind: "emoji", iconValue: "🚀" } as { iconKind?: string | null; iconValue?: string | null };
    const icon = summary.iconKind ? { kind: summary.iconKind, value: summary.iconValue ?? "" } : null;
    expect(bayGlyph(icon)).toBe("🚀");
  });

  it("renders no glyph for a bay.list summary without an icon", () => {
    const summary = {} as { iconKind?: string | null; iconValue?: string | null };
    const icon = summary.iconKind ? { kind: summary.iconKind, value: summary.iconValue ?? "" } : null;
    expect(bayGlyph(icon)).toBeNull();
  });
});
