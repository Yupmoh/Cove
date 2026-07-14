export type RendererKind = "webgl" | "canvas" | "dom";

export function pickRenderer(
  caps: { webgl2: boolean; canvasAddon: boolean },
  override?: RendererKind,
  visibleTerminals = 1,
): RendererKind {
  if (override === "dom") return "dom";
  if (override === "canvas") return caps.canvasAddon ? "canvas" : "dom";
  if (override === "webgl" && caps.webgl2 && visibleTerminals <= 8) return "webgl";
  if (caps.canvasAddon) return "canvas";
  return "dom";
}

export function detectWebgl2(): boolean {
  try {
    const c = document.createElement("canvas");
    return c.getContext("webgl2") != null;
  } catch { return false; }
}
