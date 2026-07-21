import { Window } from "happy-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { disposeFrontendTransport } from "../../invoke";
import { createOnboardingFeature, type OnboardingFeatureDependencies } from "./onboarding-feature";

interface FixtureAdapter {
  name: string;
  displayName: string;
  status?: string | null;
  installCommand?: string | null;
}

function fixture(adapters: FixtureAdapter[] = []) {
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
    if (command === "app.callEngine") return JSON.stringify({ adapters, themes: [] });
    if (command === "app.adapterList") return JSON.stringify({ adapters });
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
    mapLauncherAdapters: (listed) => listed as FixtureAdapter[],
    launchHarnessShellTask: vi.fn(async () => {}),
    launcherYolo: () => false,
    launcherYoloKey: (adapter) => `launcher.${adapter}`,
  };
  return { window, root, invoke, feature: createOnboardingFeature(dependencies) };
}

async function settleOnboarding(): Promise<void> {
  await Promise.resolve();
  await Promise.resolve();
  await Promise.resolve();
  await Promise.resolve();
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

  it("renders installed tools once and retains eligible missing tools under Install more", async () => {
    const adapters: FixtureAdapter[] = [
      { name: "omp", displayName: "OMP", status: "detected", installCommand: "install omp" },
      { name: "claude-code", displayName: "Claude Code", status: "detected", installCommand: "install claude" },
      { name: "codex", displayName: "Codex", status: "detected", installCommand: "install codex" },
      { name: "pi", displayName: "Pi", status: "detected", installCommand: "install pi" },
      { name: "openclaw", displayName: "OpenClaw", status: "missing", installCommand: "install openclaw" },
      { name: "hermes", displayName: "Hermes", status: "missing", installCommand: "install hermes" },
      { name: "cursor-agent", displayName: "Cursor Agent", status: "missing", installCommand: "install cursor" },
      { name: "opencode", displayName: "opencode", status: "missing", installCommand: "install opencode" },
    ];
    const { root, feature } = fixture(adapters);

    feature.rerun();
    await settleOnboarding();

    const grids = root.querySelectorAll(".ob-adapter-list");
    expect(Array.from(grids[0].querySelectorAll(".ob-adapter-name"), (element) => element.textContent)).toEqual([
      "OMP",
      "Claude Code",
      "Codex",
      "Pi",
    ]);
    expect(Array.from(grids[1].querySelectorAll(".ob-adapter-name"), (element) => element.textContent)).toEqual([
      "Cursor Agent",
      "Hermes",
      "OpenClaw",
      "opencode",
    ]);
    expect(root.querySelectorAll(".ob-adapter-name")).toHaveLength(8);
  });

  it("renders permission rows only for exact detected status", async () => {
    const adapters: FixtureAdapter[] = [
      { name: "omp", displayName: "OMP", status: "detected" },
      { name: "missing", displayName: "Missing", status: "missing", installCommand: "install missing" },
      { name: "broken", displayName: "Broken", status: "broken", installCommand: "install broken" },
      { name: "unknown", displayName: "Unknown", status: "unexpected", installCommand: "install unknown" },
      { name: "null", displayName: "Null", status: null, installCommand: "install null" },
    ];
    const { root, feature } = fixture(adapters);

    feature.rerun();
    await settleOnboarding();
    (root.querySelector(".ob-next") as unknown as HTMLButtonElement).click();
    await settleOnboarding();

    expect(Array.from(root.querySelectorAll(".ob-telemetry-toggle span"), (element) => element.textContent)).toEqual([
      "OMP — bypass permissions (YOLO)",
    ]);
  });
});
