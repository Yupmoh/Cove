import { Window } from "happy-dom";
import { describe, expect, it, vi } from "vitest";
import { FrontendCommand } from "../../app/frontend-command";
import { createInspectFeature, type InspectFeatureDependencies } from "./inspect-feature";

function fixture() {
  const window = new Window();
  const invoke = vi.fn(async <T>(
    _command: FrontendCommand,
    _args: Record<string, unknown>,
  ) => ({ path: "/tmp/report.json" } as T));
  const dependencies: InspectFeatureDependencies = {
    document: window.document as unknown as Document,
    viewport: () => ({ width: window.innerWidth, height: window.innerHeight }),
    adapters: () => [],
    workspaceNames: () => ({ bay: "Bay", shore: "Shore" }),
    invoke: invoke as InspectFeatureDependencies["invoke"],
    buildAdapterLaunch: vi.fn(async () => ({ command: "", args: [], yolo: false })),
    spawnNook: vi.fn(async () => ({ nookId: "nook-1" })),
    createShore: vi.fn(async () => ({ shoreId: "shore-1" })),
    selectShore: vi.fn(),

    focusNook: vi.fn(),
  };
  return { window, invoke, feature: createInspectFeature(dependencies) };
}

describe("InspectFeature", () => {
  it("owns its overlay and Escape listener", async () => {
    const { window, feature } = fixture();

    feature.start();
    expect(window.document.getElementById("inspect-overlay")).not.toBeNull();
    window.document.dispatchEvent(new window.KeyboardEvent("keydown", { key: "Escape" }));
    expect(window.document.getElementById("inspect-overlay")).toBeNull();

    feature.start();
    await feature.dispose();
    expect(window.document.getElementById("inspect-overlay")).toBeNull();
  });

  it("omits non-HTML element markup from feedback reports", async () => {
    const { window, invoke, feature } = fixture();
    const svg = window.document.createElementNS("http://www.w3.org/2000/svg", "svg");
    window.document.body.appendChild(svg);
    Object.defineProperty(window.document, "elementsFromPoint", {
      configurable: true,
      value: () => [svg],
    });

    feature.start();
    window.document.getElementById("inspect-overlay")?.dispatchEvent(
      new window.MouseEvent("mouseup", { clientX: 10, clientY: 10 }),
    );
    (window.document.querySelector(".inspect-btn") as unknown as HTMLElement).click();

    await vi.waitFor(() => expect(invoke).toHaveBeenCalledOnce());
    const args = invoke.mock.calls[0]?.[1];
    if (typeof args?.json !== "string") throw new Error("feedback JSON was not invoked");
    expect(JSON.parse(args.json).htmlExcerpt).toBe("");
    await feature.dispose();
  });
});
