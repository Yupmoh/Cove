import {
  BACKDROP_PREF_KEY,
  coerceMaterial,
  initBackdrop,
  nextToggleMaterial,
  setBackdropMaterial,
  type BackdropDeps,
  type BackdropMaterial,
} from "../../backdrop";
import {
  initialZenState,
  toggleZen,
  type ChromeVisibility,
  type ZenState,
} from "../../zen-mode";

export interface AppearanceFeatureDependencies {
  document: Document;
  storage: Storage;
  getChrome(): ChromeVisibility;
  setChrome(leftSidebarHidden: boolean, rightSidebarHidden: boolean): void;
  fitWorkspace(): void;
  setTitleZoom(factor: number): void;
  setPageZoom(factor: number): Promise<void>;
  syncTitlebar(): void;
  reconcileBrowsers(): void;
  getBackdrop(): Promise<unknown>;
  setBackdrop(material: BackdropMaterial): Promise<void>;
  loadConfig(key: string): Promise<string | null>;
  saveConfig(key: string, value: string): Promise<void>;
  warn(message: string, error?: unknown): void;
}

export interface AppearanceFeature {
  readonly zoom: number;
  readonly backdropMaterial: BackdropMaterial;
  readonly backdrop: BackdropDeps;
  applyZoom(): Promise<void>;
  increaseZoom(): Promise<void>;
  decreaseZoom(): Promise<void>;
  toggleZen(): void;
  initializeBackdrop(): Promise<void>;
  toggleBackdrop(): Promise<void>;
  updateBackdropMaterial(material: BackdropMaterial): void;
}

export function createAppearanceFeature(
  dependencies: AppearanceFeatureDependencies,
): AppearanceFeature {
  let zenState: ZenState = initialZenState();
  let material: BackdropMaterial = "none";
  const storedZoom = parseFloat(dependencies.storage.getItem("cove.appZoom") ?? "1");
  let zoom = Number.isFinite(storedZoom) && storedZoom > 0 ? storedZoom : 1;
  const backdrop: BackdropDeps = Object.freeze({
    getBackdrop: dependencies.getBackdrop,
    setBackdrop: dependencies.setBackdrop,
    loadPref: () => dependencies.loadConfig(BACKDROP_PREF_KEY),
    savePref: (next: BackdropMaterial) => dependencies.saveConfig(BACKDROP_PREF_KEY, next),
    applyClass: (translucent: boolean) => {
      dependencies.document.body.classList.toggle("backdrop-translucent", translucent);
    },
    warn: (message: string) => dependencies.warn(message),
  });

  async function applyZoom(): Promise<void> {
    zoom = Math.min(1.5, Math.max(0.7, Math.round(zoom * 10) / 10));
    dependencies.storage.setItem("cove.appZoom", String(zoom));
    dependencies.setTitleZoom(zoom);
    try {
      await dependencies.setPageZoom(zoom);
      dependencies.syncTitlebar();
      dependencies.fitWorkspace();
      dependencies.reconcileBrowsers();
    } catch (error) {
      dependencies.warn("window.setPageZoom failed", error);
    }
  }

  function toggleZenMode(): void {
    const transition = toggleZen(zenState, dependencies.getChrome());
    zenState = transition.state;
    dependencies.document.body.classList.toggle("zen-mode", zenState.active);
    dependencies.setChrome(
      transition.visibility.leftSidebarHidden,
      transition.visibility.rightSidebarHidden,
    );
    dependencies.fitWorkspace();
  }

  async function initializeBackdrop(): Promise<void> {
    try {
      material = coerceMaterial(await initBackdrop(backdrop));
    } catch (error) {
      dependencies.warn("backdrop init failed", error);
    }
  }

  return {
    get zoom() { return zoom; },
    get backdropMaterial() { return material; },
    backdrop,
    applyZoom,
    async increaseZoom() {
      zoom += 0.1;
      await applyZoom();
    },
    async decreaseZoom() {
      zoom -= 0.1;
      await applyZoom();
    },
    toggleZen: toggleZenMode,
    initializeBackdrop,
    async toggleBackdrop() {
      material = coerceMaterial(await setBackdropMaterial(nextToggleMaterial(material), backdrop));
    },
    updateBackdropMaterial(next) {
      material = next;
    },
  };
}
