import { describe, it, expect } from "vitest";
import { dropZoneFor, moveMutationFor, zoneOverlayRect } from "./nook-dnd";

describe("dropZoneFor", () => {
  it("maps pointer position to edge zones", () => {
    expect(dropZoneFor(10, 100, 400, 200)).toMatchObject({ kind: "split", orientation: "row", dir: -1, edge: "left" });
    expect(dropZoneFor(390, 100, 400, 200)).toMatchObject({ kind: "split", orientation: "row", dir: 1, edge: "right" });
    expect(dropZoneFor(200, 10, 400, 200)).toMatchObject({ kind: "split", orientation: "column", dir: -1, edge: "top" });
    expect(dropZoneFor(200, 190, 400, 200)).toMatchObject({ kind: "split", orientation: "column", dir: 1, edge: "bottom" });
  });

  it("returns center in the middle", () => {
    expect(dropZoneFor(200, 100, 400, 200)).toEqual({ kind: "center" });
  });

  it("picks the nearest edge in corners", () => {
    expect(dropZoneFor(8, 30, 400, 200)).toMatchObject({ edge: "left" });
    expect(dropZoneFor(60, 4, 400, 200)).toMatchObject({ edge: "top" });
  });

  it("degenerate rect falls back to center", () => {
    expect(dropZoneFor(0, 0, 0, 0)).toEqual({ kind: "center" });
  });
});

describe("moveMutationFor", () => {
  it("refuses a center drop so dragging never merges nooks into subtabs", () => {
    expect(moveMutationFor({ kind: "center" }, "a", "b")).toBeNull();
  });

  it("builds moveNook for edges and centerDrop for center", () => {
    const edge = dropZoneFor(10, 100, 400, 200);
    expect(moveMutationFor(edge, "a", "b")).toEqual({ op: "moveNook", nookId: "a", targetNookId: "b", orientation: "row", dir: -1 });
  });

  it("refuses self-drop", () => {
    expect(moveMutationFor({ kind: "center" }, "a", "a")).toBeNull();
  });
});

describe("zoneOverlayRect", () => {
  it("returns half-rects for edges and an inset for center", () => {
    expect(zoneOverlayRect({ kind: "split", orientation: "row", dir: -1, edge: "left" }).width).toBe("50%");
    expect(zoneOverlayRect({ kind: "center" }).left).toBe("25%");
  });
});
