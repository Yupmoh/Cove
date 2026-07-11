import { describe, it, expect } from "vitest";
import { bayInitial, buildBayBoxes, nextBayName, type BayBoxInput } from "./bay-boxes";

describe("bayInitial", () => {
  it("uppercases the first non-space character", () => {
    expect(bayInitial("cove")).toBe("C");
    expect(bayInitial("  harbor")).toBe("H");
  });
  it("falls back to a placeholder for empty names", () => {
    expect(bayInitial("")).toBe("?");
    expect(bayInitial("   ")).toBe("?");
  });
});

describe("buildBayBoxes", () => {
  const items: BayBoxInput[] = [
    { id: "a", name: "Alpha" },
    { id: "b", name: "beta" },
    { id: "c", name: "" },
  ];
  it("preserves order and derives initials", () => {
    const boxes = buildBayBoxes(items, "b");
    expect(boxes.map((b) => b.id)).toEqual(["a", "b", "c"]);
    expect(boxes.map((b) => b.initial)).toEqual(["A", "B", "?"]);
  });
  it("marks only the active bay as active", () => {
    const boxes = buildBayBoxes(items, "b");
    expect(boxes.map((b) => b.active)).toEqual([false, true, false]);
  });
  it("marks nothing active when the active id is unknown", () => {
    const boxes = buildBayBoxes(items, null);
    expect(boxes.some((b) => b.active)).toBe(false);
  });
});

describe("nextBayName", () => {
  it("uses the trimmed input when non-empty", () => {
    expect(nextBayName("  Harbor  ", "old")).toBe("Harbor");
  });
  it("falls back to the current name for blank input", () => {
    expect(nextBayName("   ", "old")).toBe("old");
    expect(nextBayName("", "old")).toBe("old");
  });
});
