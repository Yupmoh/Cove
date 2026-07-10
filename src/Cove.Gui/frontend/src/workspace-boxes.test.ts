import { describe, it, expect } from "vitest";
import { workspaceInitial, buildWorkspaceBoxes, nextWorkspaceName, type WorkspaceBoxInput } from "./workspace-boxes";

describe("workspaceInitial", () => {
  it("uppercases the first non-space character", () => {
    expect(workspaceInitial("cove")).toBe("C");
    expect(workspaceInitial("  harbor")).toBe("H");
  });
  it("falls back to a placeholder for empty names", () => {
    expect(workspaceInitial("")).toBe("?");
    expect(workspaceInitial("   ")).toBe("?");
  });
});

describe("buildWorkspaceBoxes", () => {
  const items: WorkspaceBoxInput[] = [
    { id: "a", name: "Alpha" },
    { id: "b", name: "beta" },
    { id: "c", name: "" },
  ];
  it("preserves order and derives initials", () => {
    const boxes = buildWorkspaceBoxes(items, "b");
    expect(boxes.map((b) => b.id)).toEqual(["a", "b", "c"]);
    expect(boxes.map((b) => b.initial)).toEqual(["A", "B", "?"]);
  });
  it("marks only the active workspace as active", () => {
    const boxes = buildWorkspaceBoxes(items, "b");
    expect(boxes.map((b) => b.active)).toEqual([false, true, false]);
  });
  it("marks nothing active when the active id is unknown", () => {
    const boxes = buildWorkspaceBoxes(items, null);
    expect(boxes.some((b) => b.active)).toBe(false);
  });
});

describe("nextWorkspaceName", () => {
  it("uses the trimmed input when non-empty", () => {
    expect(nextWorkspaceName("  Harbor  ", "old")).toBe("Harbor");
  });
  it("falls back to the current name for blank input", () => {
    expect(nextWorkspaceName("   ", "old")).toBe("old");
    expect(nextWorkspaceName("", "old")).toBe("old");
  });
});
