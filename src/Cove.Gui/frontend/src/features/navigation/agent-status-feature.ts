import { EngineEventRouter } from "../../app/engine-event-router";
import { FrontendCommand } from "../../app/frontend-command";
import { LifecycleScope, type ComponentHandle } from "../../app/lifecycle";
import { restoredSummaryText, shouldShowRestoreToast } from "../../restore-summary";

const restoreShownKey = "cove.restore.lastShownBoot";

export interface AgentStatusDependencies {
  engineEvents: EngineEventRouter;
  storage: Storage;
  invoke<T>(command: FrontendCommand, args: Record<string, unknown> | number): Promise<T>;
  needsInputCount(): number;
  addNeedsInput(nookId: string): void;
  removeNeedsInput(nookId: string): void;
  clearNeedsInput(): void;
  syncAgentNookStateClasses(): void;
  agentsVisible(): boolean;
  renderAgents(): void;
  refreshAgents(): Promise<void>;
  showToast(title: string, body: string): void;
}

export interface AgentStatusFeature extends ComponentHandle {
  start(): void;
  maybeShowRestoreToast(): Promise<void>;
}

export function createAgentStatusFeature(
  dependencies: AgentStatusDependencies,
): AgentStatusFeature {
  const lifecycle = new LifecycleScope();
  let started = false;

  function syncNeedsInput(): void {
    const count = dependencies.needsInputCount();
    const update = count === 0
      ? dependencies.invoke(FrontendCommand.BadgeClear, {})
      : dependencies.invoke(FrontendCommand.BadgeSetCount, count);
    void update.catch((error) => {
      console.warn("dock badge synchronization failed", { count, error });
    });
    dependencies.syncAgentNookStateClasses();
    if (dependencies.agentsVisible()) dependencies.renderAgents();
  }

  function presentRestoreToast(
    restored: number,
    fresh: number,
    skipped: number,
    bootedAt: string,
  ): void {
    const text = restoredSummaryText(restored, fresh, skipped);
    let lastShown: string | null = null;
    try {
      lastShown = dependencies.storage.getItem(restoreShownKey);
    } catch (error) {
      console.warn("restore toast state unavailable", error);
    }
    if (!shouldShowRestoreToast(bootedAt, lastShown, text)) return;
    try {
      dependencies.storage.setItem(restoreShownKey, bootedAt);
    } catch (error) {
      console.warn("restore toast state could not be saved", error);
    }
    dependencies.showToast("Sessions restored", text);
  }

  async function maybeShowRestoreToast(): Promise<void> {
    try {
      const result = await dependencies.invoke<{
        present?: boolean;
        restored?: number;
        fresh?: number;
        skipped?: number;
        bootedAt?: string;
      }>(FrontendCommand.RestoreSummaryGet, {});
      if (!result?.present) return;
      presentRestoreToast(
        result.restored ?? 0,
        result.fresh ?? 0,
        result.skipped ?? 0,
        result.bootedAt ?? "",
      );
    } catch (error) {
      console.warn("restore summary pull failed", error);
    }
  }

  function ownRegistration(handle: ComponentHandle): void {
    lifecycle.own(() => handle.dispose());
  }

  function start(): void {
    if (started) return;
    started = true;
    ownRegistration(dependencies.engineEvents.register("dock.badge", (event) => {
      if (!event?.nookId) {
        console.warn("dock.badge missing nook identifier");
        return;
      }
      dependencies.addNeedsInput(event.nookId);
      syncNeedsInput();
    }));
    ownRegistration(dependencies.engineEvents.register("needs-input.clear", (event) => {
      if (!event?.nookId) {
        console.warn("needs-input.clear missing nook identifier");
        return;
      }
      dependencies.removeNeedsInput(event.nookId);
      syncNeedsInput();
    }));
    ownRegistration(dependencies.engineEvents.register("dock.badge.clear", () => {
      dependencies.clearNeedsInput();
      syncNeedsInput();
    }));
    ownRegistration(dependencies.engineEvents.register("state.changed", () => {
      if (dependencies.agentsVisible()) void dependencies.refreshAgents();
    }));
    ownRegistration(dependencies.engineEvents.register("agent.changed", () => {
      if (dependencies.agentsVisible()) void dependencies.refreshAgents();
    }));
    ownRegistration(dependencies.engineEvents.register("engine.reconnected", () => {
      void dependencies.refreshAgents();
    }));
    ownRegistration(dependencies.engineEvents.register("restore.summary", (event) => {
      presentRestoreToast(
        event.restored ?? 0,
        event.fresh ?? 0,
        event.skipped ?? 0,
        event.bootedAt ?? "",
      );
    }));
  }

  return {
    start,
    maybeShowRestoreToast,
    dispose: () => lifecycle.dispose(),
  };
}
