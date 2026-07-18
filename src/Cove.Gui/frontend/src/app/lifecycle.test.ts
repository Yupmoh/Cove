import { Window } from "happy-dom";
import { describe, expect, it, vi } from "vitest";
import { LifecycleScope } from "./lifecycle";

describe("LifecycleScope", () => {
  it("removes owned DOM listeners when disposed", async () => {
    const window = new Window();
    const button = window.document.createElement("button");
    const scope = new LifecycleScope();
    const listener = vi.fn();

    scope.listen(button as unknown as EventTarget, "click", listener);
    button.click();
    await scope.dispose();
    button.click();

    expect(listener).toHaveBeenCalledTimes(1);
  });

  it("cancels owned timers before they fire", async () => {
    vi.useFakeTimers();
    const scope = new LifecycleScope();
    const callback = vi.fn();

    scope.timeout(callback, 100);
    scope.interval(callback, 25);
    await scope.dispose();
    await vi.advanceTimersByTimeAsync(200);

    expect(callback).not.toHaveBeenCalled();
    vi.useRealTimers();
  });

  it("disposes resources once in reverse ownership order", async () => {
    const scope = new LifecycleScope();
    const disposed: string[] = [];

    scope.own(() => {
      disposed.push("first");
    });
    scope.own(async () => {
      await Promise.resolve();
      disposed.push("second");
    });

    await scope.dispose();
    await scope.dispose();

    expect(disposed).toEqual(["second", "first"]);
  });

  it("immediately disposes resources acquired after shutdown", async () => {
    const scope = new LifecycleScope();
    await scope.dispose();
    const disposer = vi.fn();

    scope.own(disposer);

    expect(disposer).toHaveBeenCalledOnce();
  });
});
