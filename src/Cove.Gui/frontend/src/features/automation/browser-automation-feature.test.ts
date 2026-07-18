import { describe, expect, it, vi } from "vitest";
import { EngineEventRouter } from "../../app/engine-event-router";
import { createBrowserAutomationFeature } from "./browser-automation-feature";

describe("BrowserAutomationFeature", () => {
  it("owns engine routing and returns a structured missing-webview result", async () => {
    let emit: (data: unknown) => void = () => {};
    const router = new EngineEventRouter((listener) => {
      emit = listener;
      return () => { emit = () => {}; };
    });
    const invokeCall = vi.fn();
    async function invoke<T>(
      command: string,
      args: Record<string, unknown>,
    ): Promise<T> {
      invokeCall(command, args);
      return {} as T;
    }
    const feature = createBrowserAutomationFeature({
      engineEvents: router,
      invoke,
      invokeBrowserAction: vi.fn(async () => ({ found: false })),
    });

    feature.start();
    router.start();
    emit({
      channel: "browser.automation.exec",
      payload: { requestId: "request-1", nookId: "missing", kind: "screenshot" },
    });
    await Promise.resolve();
    await Promise.resolve();

    expect(invokeCall).toHaveBeenCalledWith(
      "cove://commands/browser.automation.result",
      {
        requestId: "request-1",
        resultJson: JSON.stringify({
          ok: false,
          error: "no live webview for nook missing",
        }),
      },
    );

    await feature.dispose();
    await router.dispose();
  });

  it("routes native panel actions through the scoped browser owner", async () => {
    let emit: (data: unknown) => void = () => {};
    const router = new EngineEventRouter((listener) => {
      emit = listener;
      return () => { emit = () => {}; };
    });
    const invokeCall = vi.fn();
    async function invoke<T>(
      command: string,
      args: Record<string, unknown>,
    ): Promise<T> {
      invokeCall(command, args);
      return {} as T;
    }
    const invokeBrowserAction = vi.fn(async () => ({ found: true, value: "png-data" }));
    const feature = createBrowserAutomationFeature({
      engineEvents: router,
      invoke,
      invokeBrowserAction,
    });

    feature.start();
    router.start();
    emit({
      channel: "browser.automation.exec",
      payload: { requestId: "request-2", nookId: "browser-1", kind: "screenshot" },
    });
    await vi.waitFor(() => expect(invokeCall).toHaveBeenCalledWith(
      "cove://commands/browser.automation.result",
      {
        requestId: "request-2",
        resultJson: JSON.stringify({ ok: true, png: "png-data" }),
      },
    ));

    expect(invokeBrowserAction).toHaveBeenCalledExactlyOnceWith(
      "browser-1",
      { kind: "screenshot" },
    );
    expect(invokeCall).not.toHaveBeenCalledWith(
      "webviewPane.screenshot",
      expect.anything(),
    );
  });
});
