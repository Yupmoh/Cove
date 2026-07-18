import { Window } from "happy-dom";
import { describe, expect, it, vi } from "vitest";
import {
  HARNESS_UPDATE_DISMISSED_KEY,
  type HarnessUpdate,
} from "../../harness-updates";
import type { ToastAction } from "../../shell/toast-host";
import {
  createHarnessUpdateFeature,
  type HarnessUpdateDependencies,
} from "./harness-update-feature";

describe("HarnessUpdateFeature", () => {
  it("owns update checks and dismissal persistence", async () => {
    const window = new Window();
    const showToast = vi.fn();
    async function invoke<T>(): Promise<T> {
      return {
        updates: [{
          name: "codex",
          displayName: "Codex",
          installedVersion: "1.0.0",
          latestVersion: "2.0.0",
          updateCommand: "update codex",
        }],
      } as T;
    }
    const feature = createHarnessUpdateFeature({
      storage: window.localStorage as unknown as Storage,
      invoke,
      launchTask: vi.fn(async () => {}),
      showToast,
    });

    await feature.check();
    expect(showToast).toHaveBeenCalledOnce();
    const actions = showToast.mock.calls[0][2] as ToastAction[];
    actions.find((action) => action.label === "Dismiss")?.onClick();
    expect(window.localStorage.getItem(HARNESS_UPDATE_DISMISSED_KEY))
      .toBe('{"codex":"2.0.0"}');

    await feature.dispose();
  });

  it("does not present an in-flight check after disposal", async () => {
    const window = new Window();
    const showToast = vi.fn();
    let resolveCheck = (_value: { updates: HarnessUpdate[] }): void => {};
    const invoke: HarnessUpdateDependencies["invoke"] = <T>() => new Promise<T>((resolve) => {
      resolveCheck = resolve as (value: { updates: HarnessUpdate[] }) => void;
    });
    const feature = createHarnessUpdateFeature({
      storage: window.localStorage as unknown as Storage,
      invoke,
      launchTask: vi.fn(async () => {}),
      showToast,
    });

    const check = feature.check();
    await feature.dispose();
    resolveCheck({
      updates: [{
        name: "codex",
        displayName: "Codex",
        installedVersion: "1.0.0",
        latestVersion: "2.0.0",
        updateCommand: "update codex",
      }],
    });
    await check;

    expect(showToast).not.toHaveBeenCalled();
  });
});
