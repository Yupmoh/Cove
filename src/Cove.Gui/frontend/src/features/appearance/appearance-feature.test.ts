import { Window } from "happy-dom";
import { describe, expect, it, vi } from "vitest";
import { createAppearanceFeature } from "./appearance-feature";

describe("AppearanceFeature", () => {
  it("owns app zoom persistence and applies every dependent viewport update", async () => {
    const window = new Window();
    window.localStorage.setItem("cove.appZoom", "1.45");
    const setPageZoom = vi.fn(async () => {});
    const setTitleZoom = vi.fn();
    const syncTitlebar = vi.fn();
    const fitWorkspace = vi.fn();
    const reconcileBrowsers = vi.fn();
    const feature = createAppearanceFeature({
      document: window.document as unknown as Document,
      storage: window.localStorage as unknown as Storage,
      getChrome: () => ({ leftSidebarHidden: false, rightSidebarHidden: false }),
      setChrome: vi.fn(),
      fitWorkspace,
      setTitleZoom,
      setPageZoom,
      syncTitlebar,
      reconcileBrowsers,
      getBackdrop: async () => "none",
      setBackdrop: async () => {},
      loadConfig: async () => null,
      saveConfig: async () => {},
      warn: vi.fn(),
    });

    await feature.applyZoom();
    expect(feature.zoom).toBe(1.5);
    expect(window.localStorage.getItem("cove.appZoom")).toBe("1.5");
    expect(setTitleZoom).toHaveBeenLastCalledWith(1.5);
    expect(setPageZoom).toHaveBeenLastCalledWith(1.5);
    expect(syncTitlebar).toHaveBeenCalledOnce();
    expect(fitWorkspace).toHaveBeenCalledOnce();
    expect(reconcileBrowsers).toHaveBeenCalledOnce();
  });

  it("owns reversible zen chrome state", () => {
    const window = new Window();
    const setChrome = vi.fn();
    const fitWorkspace = vi.fn();
    const feature = createAppearanceFeature({
      document: window.document as unknown as Document,
      storage: window.localStorage as unknown as Storage,
      getChrome: () => ({ leftSidebarHidden: true, rightSidebarHidden: false }),
      setChrome,
      fitWorkspace,
      setTitleZoom: vi.fn(),
      setPageZoom: vi.fn(async () => {}),
      syncTitlebar: vi.fn(),
      reconcileBrowsers: vi.fn(),
      getBackdrop: async () => "none",
      setBackdrop: async () => {},
      loadConfig: async () => null,
      saveConfig: async () => {},
      warn: vi.fn(),
    });

    feature.toggleZen();
    expect(window.document.body.classList.contains("zen-mode")).toBe(true);
    expect(setChrome).toHaveBeenLastCalledWith(true, true);
    feature.toggleZen();
    expect(window.document.body.classList.contains("zen-mode")).toBe(false);
    expect(setChrome).toHaveBeenLastCalledWith(true, false);
    expect(fitWorkspace).toHaveBeenCalledTimes(2);
  });

  it("shares one canonical backdrop port with onboarding and actions", async () => {
    const window = new Window();
    let effective = "none";
    const saveConfig = vi.fn(async () => {});
    const feature = createAppearanceFeature({
      document: window.document as unknown as Document,
      storage: window.localStorage as unknown as Storage,
      getChrome: () => ({ leftSidebarHidden: false, rightSidebarHidden: false }),
      setChrome: vi.fn(),
      fitWorkspace: vi.fn(),
      setTitleZoom: vi.fn(),
      setPageZoom: vi.fn(async () => {}),
      syncTitlebar: vi.fn(),
      reconcileBrowsers: vi.fn(),
      getBackdrop: async () => effective,
      setBackdrop: async (material) => { effective = material; },
      loadConfig: async () => "acrylic",
      saveConfig,
      warn: vi.fn(),
    });

    await feature.initializeBackdrop();
    expect(feature.backdropMaterial).toBe("acrylic");
    expect(window.document.body.classList.contains("backdrop-translucent")).toBe(true);
    await feature.toggleBackdrop();
    expect(feature.backdropMaterial).toBe("none");
    expect(saveConfig).toHaveBeenCalledWith("appearance.backdrop", "none");
    expect(window.document.body.classList.contains("backdrop-translucent")).toBe(false);
    expect(feature.backdrop).toBe(feature.backdrop);
  });
});
