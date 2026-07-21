import { Window } from "happy-dom";
import { describe, expect, it, vi } from "vitest";
import { createUpdaterFeature } from "./updater-feature";

describe("UpdaterFeature", () => {
  it("owns updater state and surfaces an available release", async () => {
    const window = new Window();
    const onStagedChanged = vi.fn();
    const feature = createUpdaterFeature({
      document: window.document as unknown as Document,
      invokeNative: vi.fn(async () => JSON.stringify({
        version: "2.0.0",
        releaseUrl: "https://example.test/release",
      })),
      shouldCheckOnLaunch: vi.fn(async () => false),
      onStagedChanged,
    });

    await feature.check();
    const container = window.document.createElement("div");
    window.document.body.appendChild(container);
    feature.renderSettings(container as unknown as HTMLElement);

    expect(onStagedChanged).toHaveBeenLastCalledWith(true);
    expect(container.querySelector("#cove-update-btn")?.textContent).toContain("2.0.0");
    expect((container.querySelector("#cove-update-notes") as unknown as HTMLAnchorElement).href).toBe("https://example.test/release");
    expect(container.querySelectorAll("[style]")).toHaveLength(0);
    expect(container.querySelector(".set-section-header")).not.toBeNull();
    expect(container.querySelector(".set-row label")).not.toBeNull();
    expect(container.querySelector("#cove-update-btn")?.classList.contains("set-action-primary")).toBe(true);
  });

  it("renders and starts idempotently", async () => {
    const window = new Window();
    const invokeNative = vi.fn(async () => "null");
    const shouldCheckOnLaunch = vi.fn(async () => true);
    const feature = createUpdaterFeature({
      document: window.document as unknown as Document,
      invokeNative,
      shouldCheckOnLaunch,
      onStagedChanged: vi.fn(),
    });
    const container = window.document.createElement("div");
    window.document.body.appendChild(container);

    feature.renderSettings(container as unknown as HTMLElement);
    feature.renderSettings(container as unknown as HTMLElement);
    await Promise.all([feature.start(), feature.start()]);

    expect(invokeNative).toHaveBeenCalledTimes(1);
    expect(shouldCheckOnLaunch).toHaveBeenCalledTimes(1);
    expect(container.querySelectorAll("#cove-update-btn")).toHaveLength(1);
    (container.querySelector("#cove-update-btn") as unknown as HTMLButtonElement).click();
    await vi.waitFor(() => expect(invokeNative).toHaveBeenCalledTimes(2));
  });

  it("does not transition update state or rendered UI after disposal", async () => {
    const window = new Window();
    let resolveCheck: ((value: string) => void) | undefined;
    const checkResult = new Promise<string>((resolve) => {
      resolveCheck = resolve;
    });
    const onStagedChanged = vi.fn();
    const feature = createUpdaterFeature({
      document: window.document as unknown as Document,
      invokeNative: vi.fn(() => checkResult),
      shouldCheckOnLaunch: vi.fn(async () => false),
      onStagedChanged,
    });
    const container = window.document.createElement("div");
    window.document.body.appendChild(container);
    feature.renderSettings(container as unknown as HTMLElement);

    const checking = feature.check();
    const status = container.querySelector("#cove-update-status") as unknown as HTMLElement;
    const button = container.querySelector("#cove-update-btn") as unknown as HTMLButtonElement;
    expect(status.textContent).toContain("checking");

    await feature.dispose();
    await feature.dispose();
    button.click();
    resolveCheck?.(JSON.stringify({ version: "2.0.0" }));
    await checking;

    expect(feature.state.kind).toBe("checking");
    expect(onStagedChanged).toHaveBeenCalledTimes(1);
    expect(status.textContent).toContain("checking");
    expect(onStagedChanged).not.toHaveBeenCalledWith(true);
    expect(container.querySelector("#cove-update-btn")).toBeNull();
  });
});
