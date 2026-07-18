import { Window } from "happy-dom";
import { describe, expect, it, vi } from "vitest";
import { ToastHost } from "./toast-host";

describe("ToastHost", () => {
  it("renders actions and dismisses through one owned host", () => {
    vi.useFakeTimers();
    const window = new Window();
    const action = vi.fn();
    const host = new ToastHost(window.document as unknown as Document);

    host.show("Update ready", "1.0 → 1.1", vi.fn(), {
      actions: [{ label: "Update", primary: true, onClick: action }],
      timeoutMs: 5000,
    });
    const button = window.document.querySelector(".toast-btn") as unknown as HTMLButtonElement | null;
    button?.click();
    vi.advanceTimersByTime(200);

    expect(action).toHaveBeenCalledOnce();
    expect(window.document.querySelector(".toast")).toBeNull();
    vi.useRealTimers();
  });

  it("clears timers and removes owned toasts on disposal", async () => {
    vi.useFakeTimers();
    const window = new Window();
    const click = vi.fn();
    const host = new ToastHost(window.document as unknown as Document);
    host.show("Title", "Body", click);

    await host.dispose();
    vi.runAllTimers();

    expect(click).not.toHaveBeenCalled();
    expect(window.document.getElementById("toast-host")).toBeNull();
    vi.useRealTimers();
  });
});
