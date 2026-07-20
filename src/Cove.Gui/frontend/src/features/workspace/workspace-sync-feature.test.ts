import { describe, expect, it, vi } from "vitest";
import type { EngineEventPayloads } from "../../app/engine-event-router";
import { createWorkspaceSyncFeature } from "./workspace-sync-feature";

type Handler<K extends keyof EngineEventPayloads> = (payload: EngineEventPayloads[K]) => void;

function createEvents() {
  const handlers = new Map<keyof EngineEventPayloads, Set<(payload: never) => void>>();
  return {
    register<K extends keyof EngineEventPayloads>(channel: K, handler: Handler<K>) {
      const registered = handlers.get(channel) ?? new Set<(payload: never) => void>();
      registered.add(handler as (payload: never) => void);
      handlers.set(channel, registered);
      return {
        dispose: () => {
          registered.delete(handler as (payload: never) => void);
        },
      };
    },
    emit<K extends keyof EngineEventPayloads>(channel: K, payload: EngineEventPayloads[K]) {
      for (const handler of handlers.get(channel) ?? []) handler(payload as never);
    },
  };
}

function deferred() {
  let resolve = (): void => {};
  const promise = new Promise<void>((done) => { resolve = done; });
  return { promise, resolve };
}

async function flush(): Promise<void> {
  await Promise.resolve();
  await Promise.resolve();
}

describe("WorkspaceSyncFeature", () => {
  it("coalesces revision bursts and ignores stale events", async () => {
    const events = createEvents();
    const reload = vi.fn(async () => {});
    const feature = createWorkspaceSyncFeature({ engineEvents: events, reload, warn: vi.fn() });
    feature.start();

    events.emit("workspace.changed", { revision: 1, uri: "cove://commands/nook.spawn" });
    events.emit("workspace.changed", { revision: 3, uri: "cove://commands/layout.mutate" });
    events.emit("workspace.changed", { revision: 2, uri: "cove://commands/nook.rename" });
    await flush();

    expect(reload).toHaveBeenCalledTimes(1);
    await feature.dispose();
  });

  it("runs one trailing reconciliation when a mutation arrives in flight", async () => {
    const events = createEvents();
    const first = deferred();
    const reload = vi.fn()
      .mockImplementationOnce(() => first.promise)
      .mockResolvedValue(undefined);
    const feature = createWorkspaceSyncFeature({ engineEvents: events, reload, warn: vi.fn() });
    feature.start();

    events.emit("workspace.changed", { revision: 1, uri: "cove://commands/layout.mutate" });
    await flush();
    events.emit("workspace.changed", { revision: 2, uri: "cove://commands/layout.mutate" });
    events.emit("workspace.changed", { revision: 3, uri: "cove://commands/nook.rename" });
    first.resolve();
    await flush();

    expect(reload).toHaveBeenCalledTimes(2);
    await feature.dispose();
  });

  it("reconciles after reconnect and recovers after a failed reload", async () => {
    const events = createEvents();
    const warn = vi.fn();
    const reload = vi.fn()
      .mockRejectedValueOnce(new Error("offline"))
      .mockResolvedValue(undefined);
    const feature = createWorkspaceSyncFeature({ engineEvents: events, reload, warn });
    feature.start();

    events.emit("engine.reconnected", undefined);
    await flush();
    events.emit("workspace.changed", { revision: 1, uri: "cove://commands/nook.spawn" });
    await flush();

    expect(reload).toHaveBeenCalledTimes(2);
    expect(warn).toHaveBeenCalledTimes(1);
    await feature.dispose();
  });
});
