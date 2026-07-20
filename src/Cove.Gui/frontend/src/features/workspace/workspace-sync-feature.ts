import type { EngineEventPayloads } from "../../app/engine-event-router";
import { LifecycleScope, type ComponentHandle } from "../../app/lifecycle";

export interface WorkspaceEventRegistrar {
  register<K extends keyof EngineEventPayloads>(
    channel: K,
    handler: (payload: EngineEventPayloads[K]) => void,
  ): ComponentHandle;
}

export interface WorkspaceSyncDependencies {
  engineEvents: WorkspaceEventRegistrar;
  reload(): Promise<unknown>;
  warn(message: string, error: unknown): void;
}

export interface WorkspaceSyncFeature extends ComponentHandle {
  start(): void;
}

export function createWorkspaceSyncFeature(
  dependencies: WorkspaceSyncDependencies,
): WorkspaceSyncFeature {
  const lifecycle = new LifecycleScope();
  let started = false;
  let disposed = false;
  let latestRevision = 0;
  let scheduled = false;
  let running = false;
  let trailing = false;

  async function reconcile(): Promise<void> {
    if (disposed) return;
    scheduled = false;
    running = true;
    do {
      trailing = false;
      try {
        await dependencies.reload();
      } catch (error) {
        dependencies.warn("workspace reconciliation failed", error);
      }
    } while (trailing && !disposed);
    running = false;
  }

  function schedule(): void {
    if (disposed) return;
    if (running) {
      trailing = true;
      return;
    }
    if (scheduled) return;
    scheduled = true;
    queueMicrotask(() => {
      void reconcile();
    });
  }

  function ownRegistration(handle: ComponentHandle): void {
    lifecycle.own(() => handle.dispose());
  }

  function start(): void {
    if (started) return;
    started = true;
    ownRegistration(dependencies.engineEvents.register("workspace.changed", (event) => {
      if (event.revision <= latestRevision) return;
      latestRevision = event.revision;
      schedule();
    }));
    ownRegistration(dependencies.engineEvents.register("engine.reconnected", () => {
      latestRevision = 0;
      schedule();
    }));
  }

  return {
    start,
    async dispose() {
      disposed = true;
      await lifecycle.dispose();
    },
  };
}
