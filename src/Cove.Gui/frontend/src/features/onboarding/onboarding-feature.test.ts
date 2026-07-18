import { Window } from "happy-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { disposeFrontendTransport } from "../../invoke";
import { createOnboardingFeature, type OnboardingFeatureDependencies } from "./onboarding-feature";

function fixture() {
  const window = new Window();
  const root = window.document.createElement("div");
  root.innerHTML = [
    '<div class="ob-title"></div>',
    '<div class="ob-progress-bar"></div>',
    '<div class="ob-body"></div>',
    '<button class="ob-prev"></button>',
    '<button class="ob-next"></button>',
    '<button class="ob-skip"></button>',
  ].join("");
  window.document.body.appendChild(root);
  const invoke = vi.fn(async (command: string, _args?: Record<string, unknown>) => {
    if (command === "app.dictationStatus") return "{}";
    if (command === "app.callEngine") return JSON.stringify({ adapters: [], themes: [] });
    return JSON.stringify({ ok: true });
  });
  Object.assign(window, {
    __ryn: {
      invoke,
      on: vi.fn(),
      off: vi.fn(),
    },
  });
  vi.stubGlobal("window", window);
  vi.stubGlobal("localStorage", window.localStorage);
  const dependencies: OnboardingFeatureDependencies = {
    root: root as unknown as HTMLElement,
    backdrop: {
      getBackdrop: async () => "none",
      setBackdrop: async () => {},
      loadPref: async () => null,
      savePref: async () => {},
      applyClass: vi.fn(),
      warn: vi.fn(),
    },
    getBackdropMaterial: () => "none",
    updateBackdropMaterial: vi.fn(),
    getActiveThemeName: () => null,
    setAgentChimesEnabled: vi.fn(),
    agentChimesEnabled: () => true,
    mapLauncherAdapters: () => [],
    launchHarnessShellTask: vi.fn(async () => {}),
    launcherYolo: () => false,
    launcherYoloKey: (adapter) => `launcher.${adapter}`,
  };
  return { window, root, invoke, feature: createOnboardingFeature(dependencies) };
}

afterEach(async () => {
  await disposeFrontendTransport();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe("OnboardingFeature", () => {
  it("mounts one wizard flow and emits one adapter load per Next click", async () => {
    const { root, invoke, feature } = fixture();
    feature.rerun();

    expect(root.classList.contains("open")).toBe(true);
    expect(root.querySelector(".ob-title")?.textContent).toBe("Detect your tools");
    await Promise.resolve();
    const before = invoke.mock.calls.filter(([command, args]) =>
      command === "app.callEngine" && (args as { uri?: string }).uri === "cove://commands/adapter.tools-list").length;
    (root.querySelector(".ob-next") as unknown as HTMLButtonElement).click();
    await Promise.resolve();
    await Promise.resolve();

    const adapterLoads = invoke.mock.calls.filter(([command, args]) =>
      command === "app.callEngine" && (args as { uri?: string }).uri === "cove://commands/adapter.tools-list");
    expect(adapterLoads).toHaveLength(before + 1);
  });

  it("removes owned listeners and model polling on disposal", async () => {
    vi.useFakeTimers();
    const { root, invoke, feature } = fixture();
    root.classList.add("open");
    await feature.dispose();
    (root.querySelector(".ob-next") as unknown as HTMLButtonElement).click();
    vi.runAllTimers();
    await Promise.resolve();

    expect(invoke).not.toHaveBeenCalled();
    expect(root.classList.contains("open")).toBe(false);
  });
});
