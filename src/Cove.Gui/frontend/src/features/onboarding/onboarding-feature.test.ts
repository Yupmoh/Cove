import { Window } from "happy-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { FrontendCommand } from "../../app/frontend-command";
import { disposeFrontendTransport } from "../../invoke";
import { createOnboardingFeature, type OnboardingFeatureDependencies } from "./onboarding-feature";
import { mountOnboardingTemplate } from "./onboarding-template";

interface FixtureAdapter {
  name: string;
  displayName: string;
  accent?: string;
  status?: string | null;
  version?: string | null;
  description?: string | null;
  installCommand?: string | null;
}

interface FixtureOptions {
  adapters?: FixtureAdapter[];
  adapterFailures?: number;
  folderResults?: Array<string | null | Error>;
}

function fixture(options: FixtureOptions = {}) {
  const window = new Window();
  const root = mountOnboardingTemplate(window.document as unknown as Document);
  const prior = window.document.createElement("button");
  prior.textContent = "Before onboarding";
  window.document.body.append(prior, root as unknown as typeof prior);
  prior.focus();
  let adapterFailures = options.adapterFailures ?? 0;
  const folderResults = [...(options.folderResults ?? [])];
  const invoke = vi.fn(async (command: string, _args?: Record<string, unknown>) => {
    if (command === "app.dictationStatus") return "{}";
    if (command === FrontendCommand.AppAdapterList) {
      if (adapterFailures > 0) {
        adapterFailures -= 1;
        throw new Error("scan failed");
      }
      return JSON.stringify({ adapters: options.adapters ?? [] });
    }
    if (command === FrontendCommand.DialogOpenFolder) {
      const result = folderResults.shift();
      if (result instanceof Error) throw result;
      return result ?? null;
    }
    return JSON.stringify({ ok: true });
  });
  Object.assign(window, { __ryn: { invoke, on: vi.fn(), off: vi.fn() } });
  vi.stubGlobal("window", window);
  vi.stubGlobal("localStorage", window.localStorage);
  const launchHarnessShellTask = vi.fn(async () => {});
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
    launchHarnessShellTask,
    launcherYolo: () => false,
    launcherYoloKey: (adapter) => `launcher.${adapter}`,
  };
  return { window, root, prior, invoke, launchHarnessShellTask, feature: createOnboardingFeature(dependencies) };
}

async function settleOnboarding(): Promise<void> {
  await Promise.resolve();
  await Promise.resolve();
  await Promise.resolve();
  await Promise.resolve();
}

function click(root: HTMLElement, selector: string): HTMLButtonElement {
  const button = root.querySelector(selector) as HTMLButtonElement;
  button.click();
  return button;
}

afterEach(async () => {
  await disposeFrontendTransport();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe("OnboardingFeature", () => {
  it("feeds disjoint installed and installable sections from one adapter request and lets detected win", async () => {
    const adapters: FixtureAdapter[] = [
      { name: "shared", displayName: "Shared missing", accent: "", status: "missing", installCommand: "install shared" },
      { name: "codex", displayName: "Codex", accent: "", status: "detected", version: "1.2.3", installCommand: "install codex" },
      { name: "shared", displayName: "Shared detected", status: "detected", version: "2.0", installCommand: "install shared" },
      { name: "pi", displayName: "Pi", status: "missing", description: "Minimal coding agent", installCommand: "install pi" },
    ];
    const { root, invoke, feature } = fixture({ adapters });

    feature.rerun();
    await settleOnboarding();

    expect(invoke.mock.calls.filter(([command]) => command === FrontendCommand.AppAdapterList)).toHaveLength(1);
    expect(root.querySelector(".ob-installed")?.textContent).toContain("Installed · 2");
    expect(root.querySelector(".ob-installable")?.textContent).toContain("Install more · 1");
    expect(root.querySelector(".ob-installed")?.textContent).toContain("1.2.3");
    expect(root.querySelector(".ob-installable")?.textContent).toContain("Minimal coding agent");
    expect(Array.from(root.querySelectorAll(".ob-tool-name"), (element) => element.textContent)).toEqual([
      "Codex",
      "Shared detected",
      "Pi",
    ]);
    expect(root.querySelectorAll(".ob-tool-card")).toHaveLength(3);
    expect(root.querySelectorAll(".ob-tool-badge")).toHaveLength(3);
    expect(root.querySelector('.ob-installed-tool[data-adapter="codex"] .adapter-icon')).not.toBeNull();
    expect(root.querySelector('.ob-installed-tool[data-adapter="codex"] .ob-tool-state')?.textContent).toBe("Ready");
    expect(root.querySelector('.ob-installable-tool[data-adapter="pi"] .ob-tool-command')?.textContent).toBe("install pi");
    expect(root.querySelector('.ob-installable-tool[data-adapter="pi"]')?.classList.contains("ob-accent-green")).toBe(true);
  });

  it("renders loading, error with retry, no-installed, and no-installable states distinctly", async () => {
    const { root, invoke, feature } = fixture({
      adapters: [{ name: "codex", displayName: "Codex", status: "detected" }],
      adapterFailures: 1,
    });

    feature.rerun();
    const results = root.querySelector(".ob-scan-results") as HTMLElement;
    expect(results.getAttribute("aria-busy")).toBe("true");
    expect(results.textContent).toContain("Scanning your login shell…");
    expect(root.textContent?.match(/Scanning your login shell…/g)).toHaveLength(1);
    expect(results.querySelector(".ob-loading")).not.toBeNull();
    await settleOnboarding();
    expect(results.getAttribute("aria-busy")).toBe("false");
    expect(results.textContent).toContain("Cove couldn’t scan your tools.");
    expect(root.querySelector(".ob-scan-retry")).toBeInstanceOf(window.HTMLButtonElement);

    click(root, ".ob-scan-retry");
    expect(results.getAttribute("aria-busy")).toBe("true");
    await settleOnboarding();
    expect(invoke.mock.calls.filter(([command]) => command === FrontendCommand.AppAdapterList)).toHaveLength(2);
    expect(results.textContent).toContain("Installed · 1");
    expect(results.textContent).not.toContain("Install more");

    const empty = fixture({ adapters: [] });
    empty.feature.rerun();
    await settleOnboarding();
    expect(empty.root.textContent).toContain("No coding tools detected yet.");
    expect(empty.root.textContent).toContain("Every available tool is already installed or has no automatic installer.");
  });

  it("uses text install actions with accessible names, disables synchronously, completes, and launches one trimmed command", async () => {
    const { root, invoke, launchHarnessShellTask, feature } = fixture({
      adapters: [
        { name: "pi", displayName: "Pi", status: "missing", installCommand: "  install pi  " },
        { name: "broken", displayName: "Broken Tool", status: "broken", installCommand: "  repair tool  " },
      ],
    });
    feature.rerun();
    await settleOnboarding();

    const install = root.querySelector('[aria-label="Install Pi"]') as HTMLButtonElement;
    const reinstall = root.querySelector('[aria-label="Reinstall Broken Tool"]') as HTMLButtonElement;
    expect(install.textContent).toBe("Install");
    expect(reinstall.textContent).toBe("Reinstall");
    install.click();
    expect(install.disabled).toBe(true);
    await settleOnboarding();

    expect(root.classList.contains("open")).toBe(false);
    expect(launchHarnessShellTask).toHaveBeenCalledTimes(1);
    expect(launchHarnessShellTask).toHaveBeenCalledWith("install pi", "Install Pi");
    expect(invoke.mock.calls.filter(([command, args]) =>
      command === FrontendCommand.AppConfigSet && (args as { key?: string }).key === "onboarding.completed")).toHaveLength(1);
  });

  it("retains directory edits and selections across navigation and defers persistence until completion", async () => {
    const { root, invoke, feature } = fixture({ folderResults: [" /work/picked ", null, new Error("denied")] });
    feature.rerun();
    await settleOnboarding();

    const input = root.querySelector("#ob-default-bay-dir") as HTMLInputElement;
    const label = root.querySelector('label[for="ob-default-bay-dir"]');
    expect(label?.textContent).toBe("Default bay directory");
    expect(input.getAttribute("aria-describedby")).toBe("ob-default-bay-help ob-default-bay-error");
    input.value = " /work/manual ";
    input.dispatchEvent(new window.Event("input"));
    expect(invoke.mock.calls.some(([command, args]) =>
      command === FrontendCommand.AppConfigSet && (args as { key?: string }).key === "bays.defaultDir")).toBe(false);

    click(root, ".ob-next");
    await settleOnboarding();
    click(root, ".ob-prev");
    await settleOnboarding();
    expect((root.querySelector("#ob-default-bay-dir") as HTMLInputElement).value).toBe("/work/manual");

    click(root, ".ob-browse");
    await settleOnboarding();
    expect((root.querySelector("#ob-default-bay-dir") as HTMLInputElement).value).toBe("/work/picked");
    click(root, ".ob-browse");
    await settleOnboarding();
    expect((root.querySelector("#ob-default-bay-dir") as HTMLInputElement).value).toBe("/work/picked");
    click(root, ".ob-browse");
    await settleOnboarding();
    expect((root.querySelector("#ob-default-bay-dir") as HTMLInputElement).value).toBe("/work/picked");
    expect(root.querySelector("#ob-default-bay-error")?.textContent).toContain("Couldn’t open the folder picker.");

    for (let step = 0; step < 5; step += 1) {
      click(root, ".ob-next");
      await settleOnboarding();
    }
    expect(invoke.mock.calls.filter(([command, args]) =>
      command === FrontendCommand.AppConfigSet && (args as { key?: string }).key === "bays.defaultDir")).toEqual([
      [FrontendCommand.AppConfigSet, { key: "bays.defaultDir", value: "/work/picked" }],
    ]);
  });

  it("provides dialog semantics, live bindings, focus entry, tab trap, Escape skip, and focus restoration", async () => {
    const { window, root, prior, invoke, feature } = fixture({
      adapters: [{ name: "pi", displayName: "Pi", status: "missing", installCommand: "install pi" }],
    });
    const dialog = root.querySelector(".ob-box") as HTMLElement;
    expect(dialog.getAttribute("role")).toBe("dialog");
    expect(dialog.getAttribute("aria-modal")).toBe("true");
    expect(dialog.getAttribute("aria-labelledby")).toBe("ob-step-title");
    expect(dialog.getAttribute("aria-describedby")).toBe("ob-step-description");
    expect(root.querySelector(".ob-skip")?.tagName).toBe("BUTTON");

    feature.rerun();
    await settleOnboarding();
    expect(root.querySelector(".ob-scan-results")?.getAttribute("aria-live")).toBe("polite");
    expect(window.document.activeElement).toBe(root.querySelector("#ob-step-title"));

    const skip = root.querySelector(".ob-skip") as HTMLButtonElement;
    const next = root.querySelector(".ob-next") as HTMLButtonElement;
    next.focus();
    dialog.dispatchEvent(new window.KeyboardEvent("keydown", { key: "Tab", bubbles: true }) as unknown as Event);
    expect(window.document.activeElement).toBe(skip);
    skip.focus();
    dialog.dispatchEvent(new window.KeyboardEvent("keydown", { key: "Tab", shiftKey: true, bubbles: true }) as unknown as Event);
    expect(window.document.activeElement).toBe(next);

    dialog.dispatchEvent(new window.KeyboardEvent("keydown", { key: "Escape", bubbles: true }) as unknown as Event);
    await settleOnboarding();
    expect(root.classList.contains("open")).toBe(false);
    expect(window.document.activeElement).toBe(prior);
    expect(invoke.mock.calls.some(([command, args]) =>
      command === FrontendCommand.AppConfigSet && (args as { key?: string }).key === "onboarding.completed")).toBe(true);

    feature.rerun();
    await settleOnboarding();
    await feature.dispose();
    expect(window.document.activeElement).toBe(prior);
  });

  it("renders Dictation Settings exclusively with shared setting primitives", async () => {
    const { window, feature } = fixture();
    const container = window.document.createElement("div");
    window.document.body.appendChild(container);

    feature.renderDictationTab(container as unknown as HTMLElement);
    await settleOnboarding();

    expect(container.querySelectorAll("[style]")).toHaveLength(0);
    expect(container.querySelectorAll(".set-section-header")).toHaveLength(2);
    expect(container.querySelectorAll(".set-row")).toHaveLength(3);
    expect(container.querySelectorAll(".ob-dictation-info, .ob-model-status")).toHaveLength(0);
  });

  it("uses a body-only scroll and fixed-footer class contract without inline or diagnostics styling", async () => {
    const { root, feature } = fixture({
      adapters: [{ name: "pi", displayName: "A very long tool name that must not expand the dialog", status: "missing", description: "A long description that needs to wrap inside the available width", installCommand: "install pi" }],
    });
    feature.rerun();
    await settleOnboarding();

    expect(root.querySelector(".ob-box")?.children).toHaveLength(4);
    expect(root.querySelector(".ob-heading")?.children).toHaveLength(2);
    expect(root.querySelector(".ob-kicker")?.textContent).toBe("Getting started");
    expect(root.querySelector(".ob-body.ob-scroll-region")).not.toBeNull();
    expect(root.querySelector(".ob-actions.ob-fixed-footer")).not.toBeNull();
    expect(root.querySelector(".ob-directory-controls")).not.toBeNull();
    expect(root.querySelectorAll('[style]')).toHaveLength(0);
    expect(root.querySelectorAll('[class*="diag"], [class*="perf"]')).toHaveLength(0);
  });
});
