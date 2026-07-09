import { describe, expect, it, vi } from "vitest";
import { bodyClassForMaterial, coerceMaterial, initBackdrop, isTranslucent, nextToggleMaterial, setBackdropMaterial, TRANSLUCENT_BODY_CLASS, type BackdropDeps } from "./backdrop";

function makeDeps(over: Partial<BackdropDeps> = {}): BackdropDeps {
  return {
    getBackdrop: vi.fn(async () => "blur"),
    setBackdrop: vi.fn(async () => void 0),
    loadPref: vi.fn(async () => null),
    savePref: vi.fn(async () => void 0),
    applyClass: vi.fn(),
    warn: vi.fn(),
    ...over,
  };
}

describe("coerceMaterial", () => {
  it("normalizes known materials, defaulting unknown to none", () => {
    expect(coerceMaterial("blur")).toBe("blur");
    expect(coerceMaterial("Acrylic")).toBe("acrylic");
    expect(coerceMaterial('"mica"')).toBe("mica");
    expect(coerceMaterial("none")).toBe("none");
    expect(coerceMaterial("garbage")).toBe("none");
    expect(coerceMaterial(undefined)).toBe("none");
    expect(coerceMaterial(42)).toBe("none");
  });
});

describe("material to class mapping", () => {
  it("only non-none materials are translucent", () => {
    expect(isTranslucent("none")).toBe(false);
    expect(isTranslucent("blur")).toBe(true);
    expect(bodyClassForMaterial("none")).toBeNull();
    expect(bodyClassForMaterial("mica")).toBe(TRANSLUCENT_BODY_CLASS);
  });

  it("toggles between none and blur", () => {
    expect(nextToggleMaterial("none")).toBe("blur");
    expect(nextToggleMaterial("blur")).toBe("none");
    expect(nextToggleMaterial("mica")).toBe("none");
  });
});

describe("initBackdrop", () => {
  it("always reads getBackdrop and applies from the effective value", async () => {
    const deps = makeDeps({ getBackdrop: vi.fn(async () => "blur") });
    const eff = await initBackdrop(deps);
    expect(eff).toBe("blur");
    expect(deps.getBackdrop).toHaveBeenCalledTimes(1);
    expect(deps.applyClass).toHaveBeenCalledWith(true);
    expect(deps.setBackdrop).not.toHaveBeenCalled();
  });

  it("keeps opaque fallback when the effective material is none despite an open-time request", async () => {
    const deps = makeDeps({ getBackdrop: vi.fn(async () => "none") });
    const eff = await initBackdrop(deps);
    expect(eff).toBe("none");
    expect(deps.applyClass).toHaveBeenCalledWith(false);
  });

  it("replays a persisted preference before reading the effective value", async () => {
    const deps = makeDeps({ loadPref: vi.fn(async () => "none"), getBackdrop: vi.fn(async () => "none") });
    await initBackdrop(deps);
    expect(deps.setBackdrop).toHaveBeenCalledWith("none");
    expect(deps.applyClass).toHaveBeenCalledWith(false);
  });
});

describe("setBackdropMaterial", () => {
  it("persists the requested material but applies the class from the effective value", async () => {
    const deps = makeDeps({ getBackdrop: vi.fn(async () => "none") });
    const eff = await setBackdropMaterial("blur", deps);
    expect(deps.setBackdrop).toHaveBeenCalledWith("blur");
    expect(deps.savePref).toHaveBeenCalledWith("blur");
    expect(eff).toBe("none");
    expect(deps.applyClass).toHaveBeenCalledWith(false);
  });

  it("still applies a class when the request succeeds and is honored", async () => {
    const deps = makeDeps({ getBackdrop: vi.fn(async () => "blur") });
    const eff = await setBackdropMaterial("blur", deps);
    expect(eff).toBe("blur");
    expect(deps.applyClass).toHaveBeenCalledWith(true);
  });

  it("warns and still reconciles when setBackdrop throws", async () => {
    const deps = makeDeps({ setBackdrop: vi.fn(async () => { throw new Error("unsupported"); }), getBackdrop: vi.fn(async () => "none") });
    const eff = await setBackdropMaterial("blur", deps);
    expect(deps.warn).toHaveBeenCalledTimes(1);
    expect(eff).toBe("none");
    expect(deps.applyClass).toHaveBeenCalledWith(false);
  });
});
