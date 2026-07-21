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
import type { EngineEventPayloads } from "../../app/engine-event-router";
import type { ComponentHandle } from "../../app/lifecycle";

function fixture(recentResults: Array<{ sessions: Array<Record<string, unknown>> } | Error> = []) {
  const window = new Window();
  const root = window.document.createElement("div");
  const agents = window.document.createElement("div");
  window.document.body.append(root, agents);
  const handlers = new Map<keyof EngineEventPayloads, Set<(payload: never) => void>>();
  const invoke = vi.fn(async (command: string, args?: Record<string, unknown>) => {
    if (command === "app.adapterList") {
      return JSON.stringify({
        adapters: [{ name: "claude", displayName: "Claude", accent: "#fff", binary: "claude" }],
      });
    }
    if (command === "app.callEngine" && args?.uri === "cove://commands/session.recent") {
      const result = recentResults.shift() ?? { sessions: [] };
      if (result instanceof Error) throw result;
      return JSON.stringify(result);
    }
    return JSON.stringify({});
  });
  Object.assign(window, { __ryn: { invoke, on: vi.fn(), off: vi.fn() } });
  vi.stubGlobal("window", window);
  vi.stubGlobal("localStorage", window.localStorage);
  vi.stubGlobal("requestAnimationFrame", (callback: FrameRequestCallback) => {
    callback(0);
    return 1;
  });
  vi.stubGlobal("cancelAnimationFrame", vi.fn());
  vi.stubGlobal("ResizeObserver", class {
    observe(): void {}
    disconnect(): void {}
  });
  const terminalFocus = vi.fn();
  const engineEvents = {
    register<K extends keyof EngineEventPayloads>(
      channel: K,
      handler: (payload: EngineEventPayloads[K]) => void,
    ): ComponentHandle {
      const channelHandlers = handlers.get(channel) ?? new Set<(payload: never) => void>();
      channelHandlers.add(handler as (payload: never) => void);
      handlers.set(channel, channelHandlers);
      return { dispose: () => { channelHandlers.delete(handler as (payload: never) => void); } };
    },
  };
  const emit = <K extends keyof EngineEventPayloads>(
    channel: K,
    payload: EngineEventPayloads[K],
  ): void => {
    for (const handler of handlers.get(channel) ?? []) handler(payload as never);
  };
  const dependencies: LauncherFeatureDependencies & { engineEvents: typeof engineEvents } = {
    document: window.document as unknown as Document,
    engineEvents,
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
  return {
    root,
    agents,
    invoke,
    terminalFocus,
    emit,
    document: window.document,
    feature: createLauncherFeature(dependencies),
  };
}

afterEach(async () => {
  await disposeFrontendTransport();
  vi.unstubAllGlobals();
  vi.useRealTimers();
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
  it("coalesces recents invalidations and refreshes on reconnect and the mounted fallback", async () => {
    vi.useFakeTimers();
    const { document, emit, feature, invoke } = fixture();
    const launcher = feature.render(null, null);
    document.body.append(launcher as unknown as typeof document.body);
    const coldRefresh = feature.refreshRecents();
    await vi.runAllTicks();
    await coldRefresh;

    const recentCalls = () => invoke.mock.calls.filter(
      ([command, args]) => command === "app.callEngine"
        && (args as Record<string, unknown>)?.uri === "cove://commands/session.recent",
    );
    expect(recentCalls()).toHaveLength(1);

    emit("session.recents.changed", { revision: 1 });
    emit("session.recents.changed", { revision: 2 });
    emit("session.recents.changed", { revision: 3 });
    const burstRefresh = feature.refreshRecents();
    await vi.runAllTicks();
    await burstRefresh;
    expect(recentCalls()).toHaveLength(2);

    feature.invalidateRecents();
    const terminalRefresh = feature.refreshRecents();
    await vi.runAllTicks();
    await terminalRefresh;
    expect(recentCalls()).toHaveLength(3);

    emit("engine.reconnected", undefined);
    const reconnectRefresh = feature.refreshRecents();
    await vi.runAllTicks();
    await reconnectRefresh;
    expect(recentCalls()).toHaveLength(4);

    vi.advanceTimersByTime(30_000);
    await vi.runAllTicks();
    await Promise.resolve();
    await Promise.resolve();
    expect(recentCalls()).toHaveLength(5);
    launcher.remove();
    vi.advanceTimersByTime(30_000);
    await vi.runAllTicks();
    expect(recentCalls()).toHaveLength(5);

    document.body.append(launcher as unknown as typeof document.body);
    await feature.dispose();
    vi.advanceTimersByTime(30_000);
    await vi.runAllTicks();
    expect(recentCalls()).toHaveLength(5);
  });

  it("keeps visible recents when a refresh fails", async () => {
    const { document, emit, feature } = fixture([
      {
        sessions: [{
          adapter: "claude",
          sessionId: "session-1",
          bayId: "bay-1",
          cwd: "/tmp/project",
          startedAt: new Date().toISOString(),
          label: "keep-me",
        }],
      },
      new Error("offline"),
    ]);
    await feature.load();
    const launcher = feature.render(null, null);
    document.body.append(launcher as unknown as typeof document.body);
    await feature.refreshRecents();
    expect(launcher.textContent).toContain("keep-me");

    emit("session.recents.changed", { revision: 1 });
    await feature.refreshRecents();
    expect(launcher.textContent).toContain("keep-me");

    await feature.dispose();
  });

});
