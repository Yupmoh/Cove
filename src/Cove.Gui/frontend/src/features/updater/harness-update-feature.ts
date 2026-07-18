import { FrontendCommand } from "../../app/frontend-command";
import { LifecycleScope, type ComponentHandle } from "../../app/lifecycle";
import {
  HARNESS_UPDATE_CHECK_INTERVAL_MS,
  HARNESS_UPDATE_DISMISSED_KEY,
  filterToastableUpdates,
  parseDismissed,
  recordDismissal,
  type HarnessUpdate,
} from "../../harness-updates";
import type { ToastAction } from "../../shell/toast-host";

export interface HarnessUpdateDependencies {
  storage: Storage;
  invoke<T>(command: FrontendCommand, args: Record<string, unknown>): Promise<T>;
  launchTask(command: string, displayName: string): Promise<void>;
  showToast(
    title: string,
    body: string,
    actions: ToastAction[],
    timeoutMs: number,
  ): void;
}

export interface HarnessUpdateFeature extends ComponentHandle {
  start(): void;
  check(): Promise<void>;
}

export function createHarnessUpdateFeature(
  dependencies: HarnessUpdateDependencies,
): HarnessUpdateFeature {
  const lifecycle = new LifecycleScope();
  let started = false;

  function readDismissed(): string | null {
    try {
      return dependencies.storage.getItem(HARNESS_UPDATE_DISMISSED_KEY);
    } catch (error) {
      console.warn("harness update dismissals unavailable", error);
      return null;
    }
  }

  function present(update: HarnessUpdate): void {
    const actions: ToastAction[] = [];
    if (update.updateCommand) {
      const command = update.updateCommand;
      actions.push({
        label: "Update",
        primary: true,
        onClick: () => {
          void dependencies.launchTask(command, `Update ${update.displayName}`);
        },
      });
    }
    actions.push({
      label: "Dismiss",
      onClick: () => {
        const next = recordDismissal(parseDismissed(readDismissed()), update);
        try {
          dependencies.storage.setItem(
            HARNESS_UPDATE_DISMISSED_KEY,
            JSON.stringify(next),
          );
        } catch (error) {
          console.warn("harness update dismissal could not be saved", error);
          return;
        }
      },
    });
    dependencies.showToast(
      `${update.displayName} update available`,
      `${update.installedVersion} \u2192 ${update.latestVersion}`,
      actions,
      15000,
    );
  }

  async function check(): Promise<void> {
    if (lifecycle.isDisposed) return;
    let updates: HarnessUpdate[];
    try {
      const result = await dependencies.invoke<{ updates: HarnessUpdate[] }>(
        FrontendCommand.AdapterUpdatesCheck,
        {},
      );
      updates = result.updates ?? [];
    } catch (error) {
      console.warn("harness update check failed", error);
      return;
    }
    if (lifecycle.isDisposed) return;
    const dismissed = parseDismissed(readDismissed());
    for (const update of filterToastableUpdates(updates, dismissed)) {
      if (lifecycle.isDisposed) return;
      present(update);
    }
  }

  function start(): void {
    if (started) return;
    started = true;
    void check();
    lifecycle.interval(() => {
      void check();
    }, HARNESS_UPDATE_CHECK_INTERVAL_MS);
  }

  return {
    start,
    check,
    dispose: () => lifecycle.dispose(),
  };
}
