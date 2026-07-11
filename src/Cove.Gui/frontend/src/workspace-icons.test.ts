import { describe, it, expect } from "vitest";
import { WORKSPACE_ICON_CHOICES, workspaceGlyph } from "./workspace-icons";

describe("workspace-icons", () => {
  it("offers a curated set of emoji choices", () => {
    expect(WORKSPACE_ICON_CHOICES.length).toBe(16);
    expect(WORKSPACE_ICON_CHOICES).toContain("🚀");
    expect(new Set(WORKSPACE_ICON_CHOICES).size).toBe(WORKSPACE_ICON_CHOICES.length);
  });

  it("returns the emoji for an emoji icon", () => {
    expect(workspaceGlyph({ kind: "emoji", value: "🌊" })).toBe("🌊");
  });

  it("returns null for null or undefined", () => {
    expect(workspaceGlyph(null)).toBeNull();
    expect(workspaceGlyph(undefined)).toBeNull();
  });

  it("returns null for an unknown kind", () => {
    expect(workspaceGlyph({ kind: "glyph", value: "x" })).toBeNull();
  });

  it("returns null for an emoji icon with an empty value", () => {
    expect(workspaceGlyph({ kind: "emoji", value: "" })).toBeNull();
  });
});
