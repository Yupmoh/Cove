export type RendererKind = "webgl" | "canvas" | "dom";

export function pickRenderer(caps: { webgl2: boolean; canvasAddon: boolean }, override?: RendererKind): RendererKind {
  if (override) return override;
  if (caps.webgl2) return "webgl";
  if (caps.canvasAddon) return "canvas";
  return "dom";
}

export function detectWebgl2(): boolean {
  try {
    const c = document.createElement("canvas");
    return c.getContext("webgl2") != null;
  } catch { return false; }
}
