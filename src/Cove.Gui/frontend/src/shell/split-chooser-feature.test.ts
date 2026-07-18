import { Window } from "happy-dom";
import { describe, expect, it, vi } from "vitest";
import { SplitChooserFeature, type SplitChooserDependencies } from "./split-chooser-feature";

describe("SplitChooserFeature", () => {
  it("owns chooser dismissal and selection listeners", async () => {
    const window = new Window();
    const select = vi.fn();
    const feature = new SplitChooserFeature({
      document: window.document,
      window,
      adapters: () => [],
      prepare: vi.fn(),
      select,
    } as unknown as SplitChooserDependencies);

    feature.open(new window.MouseEvent("click", { clientX: 20, clientY: 30 }) as unknown as MouseEvent, "row");
    expect(window.document.getElementById("mini-launcher")).not.toBeNull();
    (window.document.querySelector(".ml-tile") as unknown as HTMLElement).click();
    expect(select).toHaveBeenCalledWith("row", "terminal");
    expect(window.document.getElementById("mini-launcher")).toBeNull();

    feature.open(new window.MouseEvent("click") as unknown as MouseEvent, "col");
    await feature.dispose();
    expect(window.document.getElementById("mini-launcher")).toBeNull();
  });
});
