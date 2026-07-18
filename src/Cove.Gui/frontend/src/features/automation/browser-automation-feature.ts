import { EngineEventRouter } from "../../app/engine-event-router";
import { FrontendCommand } from "../../app/frontend-command";
import { LifecycleScope, type ComponentHandle } from "../../app/lifecycle";
import { buildAutomationJs, type AutomationExecEvent } from "../../automation-snapshot";
import type { BrowserPanelAction, BrowserPanelActionResult } from "../../browser-nook";

export interface BrowserAutomationDependencies {
  engineEvents: EngineEventRouter;
  invoke<T>(command: FrontendCommand, args: Record<string, unknown>): Promise<T>;
  invokeBrowserAction(
    nookId: string,
    action: BrowserPanelAction,
  ): Promise<BrowserPanelActionResult>;
}

export interface BrowserAutomationFeature extends ComponentHandle {
  start(): void;
}

export function createBrowserAutomationFeature(
  dependencies: BrowserAutomationDependencies,
): BrowserAutomationFeature {
  const lifecycle = new LifecycleScope();
  let started = false;

  async function execute(event: AutomationExecEvent): Promise<void> {
    if (!event?.requestId) {
      console.warn("browser automation request missing requestId");
      return;
    }
    let resultJson: string;
    try {
      const action: BrowserPanelAction = event.kind === "screenshot"
        ? { kind: "screenshot" }
        : event.kind === "setUserAgent"
          ? { kind: "setUserAgent", userAgent: event.value ?? "" }
          : { kind: "evaluate", code: buildAutomationJs(event) };
      const result = await dependencies.invokeBrowserAction(event.nookId, action);
      if (!result.found) {
        resultJson = JSON.stringify({
          ok: false,
          error: `no live webview for nook ${event.nookId}`,
        });
      } else if (event.kind === "screenshot") {
        resultJson = JSON.stringify({ ok: true, png: result.value });
      } else if (event.kind === "setUserAgent") {
        resultJson = JSON.stringify({ ok: true });
      } else {
        resultJson = typeof result.value === "string" && result.value.length > 0
          ? result.value
          : JSON.stringify({ ok: true });
      }
    } catch (error) {
      resultJson = JSON.stringify({
        ok: false,
        error: (error as Error).message,
      });
    }
    try {
      await dependencies.invoke(FrontendCommand.BrowserAutomationResult, {
        requestId: event.requestId,
        resultJson,
      });
    } catch (error) {
      console.warn("automation result post failed", error);
    }
  }

  function start(): void {
    if (started) return;
    started = true;
    const registration = dependencies.engineEvents.register(
      "browser.automation.exec",
      (payload) => {
        void execute(payload as AutomationExecEvent);
      },
    );
    lifecycle.own(() => registration.dispose());
  }

  return {
    start,
    dispose: () => lifecycle.dispose(),
  };
}
