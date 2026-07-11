export type DropZone =
  | { kind: "split"; orientation: "row" | "column"; dir: 1 | -1; edge: "left" | "right" | "top" | "bottom" }
  | { kind: "center" };

export const EDGE_BAND = 0.3;

export function dropZoneFor(x: number, y: number, width: number, height: number): DropZone {
  if (width <= 0 || height <= 0) return { kind: "center" };
  const rx = Math.min(1, Math.max(0, x / width));
  const ry = Math.min(1, Math.max(0, y / height));
  const dists: { edge: "left" | "right" | "top" | "bottom"; d: number }[] = [
    { edge: "left", d: rx },
    { edge: "right", d: 1 - rx },
    { edge: "top", d: ry },
    { edge: "bottom", d: 1 - ry },
  ];
  dists.sort((a, b) => a.d - b.d);
  const nearest = dists[0];
  if (nearest.d >= EDGE_BAND) return { kind: "center" };
  switch (nearest.edge) {
    case "left":
      return { kind: "split", orientation: "row", dir: -1, edge: "left" };
    case "right":
      return { kind: "split", orientation: "row", dir: 1, edge: "right" };
    case "top":
      return { kind: "split", orientation: "column", dir: -1, edge: "top" };
    case "bottom":
      return { kind: "split", orientation: "column", dir: 1, edge: "bottom" };
  }
}

export interface MoveMutation {
  op: string;
  nookId: string;
  targetNookId: string;
  orientation: string;
  dir: number;
}

export function moveMutationFor(zone: DropZone, sourceNookId: string, targetNookId: string): MoveMutation | null {
  if (sourceNookId === targetNookId) return null;
  if (zone.kind === "center") return null;
  return { op: "moveNook", nookId: sourceNookId, targetNookId, orientation: zone.orientation, dir: zone.dir };
}

export function zoneOverlayRect(zone: DropZone): { left: string; top: string; width: string; height: string } {
  if (zone.kind === "center") return { left: "25%", top: "25%", width: "50%", height: "50%" };
  switch (zone.edge) {
    case "left":
      return { left: "0", top: "0", width: "50%", height: "100%" };
    case "right":
      return { left: "50%", top: "0", width: "50%", height: "100%" };
    case "top":
      return { left: "0", top: "0", width: "100%", height: "50%" };
    case "bottom":
      return { left: "0", top: "50%", width: "100%", height: "50%" };
  }
}
