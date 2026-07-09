export type BackdropMaterial = "none" | "blur" | "acrylic" | "mica";

export const TRANSLUCENT_BODY_CLASS = "backdrop-translucent";
export const BACKDROP_PREF_KEY = "appearance.backdrop";

export function coerceMaterial(raw: unknown): BackdropMaterial {
  const s = typeof raw === "string" ? raw.trim().toLowerCase().replace(/^"+|"+$/g, "") : "";
  if (s === "blur" || s === "acrylic" || s === "mica") return s;
  return "none";
}

export function isTranslucent(material: BackdropMaterial): boolean {
  return material !== "none";
}

export function bodyClassForMaterial(material: BackdropMaterial): string | null {
  return isTranslucent(material) ? TRANSLUCENT_BODY_CLASS : null;
}

export function nextToggleMaterial(current: BackdropMaterial): BackdropMaterial {
  return current === "none" ? "blur" : "none";
}

export interface BackdropDeps {
  getBackdrop: () => Promise<unknown>;
  setBackdrop: (material: BackdropMaterial) => Promise<void>;
  loadPref: () => Promise<string | null>;
  savePref: (material: BackdropMaterial) => Promise<void>;
  applyClass: (translucent: boolean) => void;
  warn: (message: string) => void;
}

export async function initBackdrop(deps: BackdropDeps): Promise<BackdropMaterial> {
  const raw = await deps.loadPref();
  if (raw !== null) {
    const pref = coerceMaterial(raw);
    try {
      await deps.setBackdrop(pref);
    } catch (e) {
      deps.warn(`backdrop: setBackdrop(${pref}) failed on init: ${String(e)}`);
    }
  }
  const effective = coerceMaterial(await deps.getBackdrop());
  deps.applyClass(isTranslucent(effective));
  return effective;
}

export async function setBackdropMaterial(material: BackdropMaterial, deps: BackdropDeps): Promise<BackdropMaterial> {
  try {
    await deps.setBackdrop(material);
  } catch (e) {
    deps.warn(`backdrop: setBackdrop(${material}) failed: ${String(e)}`);
  }
  await deps.savePref(material);
  const effective = coerceMaterial(await deps.getBackdrop());
  deps.applyClass(isTranslucent(effective));
  return effective;
}
