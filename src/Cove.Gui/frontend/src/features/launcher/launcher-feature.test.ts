import { Window } from "happy-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { disposeFrontendTransport } from "../../invoke";
import { WorkspaceController } from "../../workspace/workspace-controller";
import { WorkspaceStore } from "../../workspace/workspace-store";
import {
  createLauncherFeature,
  mapLauncherAdapters,
  type LauncherFeatureDependencies,
} from "./launcher-feature";

function fixture() {
  const window = new Window();
  const root = window.document.createElement("div");
  const agents = window.document.createElement("div");
  window.document.body.append(root, agents);
  const invoke = vi.fn(async (command: string) => {
    if (command === "app.adapterList") {
      return JSON.stringify({
        adapters: [{ name: "claude", displayName: "Claude", accent: "#fff", binary: "claude" }],
      });
    }
    return JSON.stringify({});
  });
  Object.assign(window, { __ryn: { invoke, on: vi.fn(), off: vi.fn() } });
  vi.stubGlobal("window", window);
  vi.stubGlobal("localStorage", window.localStorage);
  vi.stubGlobal("ResizeObserver", class {
    observe(): void {}
    disconnect(): void {}
  });
  const terminalFocus = vi.fn();
  const dependencies: LauncherFeatureDependencies = {
    document: window.document as unknown as Document,
    root: root as unknown as HTMLElement,
    agentsRoot: agents as unknown as HTMLElement,
    workspace: new WorkspaceStore(),
    workspaceController: new WorkspaceController(async <T>() => ({} as T)),
    spawnNook: vi.fn(async () => ({ nookId: "nook-1" })),

    focusNook: vi.fn(),
    focusActiveNook: terminalFocus,
    safeReplaceTarget: () => null,
    nextShoreName: () => "Shore 1",
    activeProjectDir: () => "/tmp/project",
    renderShore: vi.fn(),
    launchTileInto: vi.fn(async () => {}),
    resolveLauncherProfileSlug: vi.fn(async () => "default"),
    launcherProfileSlugKey: (adapter) => `profile.${adapter}`,
    openProfileEditor: vi.fn(async () => {}),

    openContextMenuAt: vi.fn(),
    showToast: vi.fn(),
    resumeRecentSession: vi.fn(async () => {}),
    getBrandIndex: () => 0,
    openAdapterSetup: vi.fn(),
  };
  return { root, agents, invoke, terminalFocus, feature: createLauncherFeature(dependencies) };
}

afterEach(async () => {
  await disposeFrontendTransport();
  vi.unstubAllGlobals();
});

describe("LauncherFeature", () => {
  it("maps engine adapters into launcher tiles without retaining transport fields", () => {
    expect(mapLauncherAdapters([{
      name: "claude",
      displayName: "Claude",
      accent: "#fff",
      binary: "/bin/claude",
      binaryPath: "/resolved/claude",
    }])).toEqual([{
      name: "claude",
      displayName: "Claude",
      accent: "#fff",
      binary: "/bin/claude",
      version: "",
      status: "",
      updateCommand: "",
      installCommand: "",
      uninstallCommand: "",
      description: "",
    }]);
  });

  it("owns launcher open, close, and persistent listeners", async () => {
    const { root, agents, terminalFocus, feature } = fixture();

    feature.open();
    await Promise.resolve();
    await Promise.resolve();

    expect(root.classList.contains("open")).toBe(true);
    expect(agents.querySelectorAll(".launch-tile")).toHaveLength(1);
    feature.close();
    expect(terminalFocus).toHaveBeenCalledOnce();

    feature.open();
    root.dispatchEvent(new root.ownerDocument.defaultView!.MouseEvent("mousedown", { bubbles: true }));
    expect(root.classList.contains("open")).toBe(false);

    root.classList.add("open");
    await feature.dispose();
    root.classList.add("open");
    root.dispatchEvent(new root.ownerDocument.defaultView!.MouseEvent("mousedown", { bubbles: true }));
    expect(root.classList.contains("open")).toBe(true);
  });
});
