import { Window } from "happy-dom";
import { describe, expect, it, vi } from "vitest";
import { TitleClusterComponent } from "./title-cluster-component";

function fixture(): { window: Window; component: TitleClusterComponent; action: ReturnType<typeof vi.fn> } {
  const window = new Window();
  const cluster = window.document.createElement("div");
  cluster.id = "tb-cluster";
  const right = window.document.createElement("div");
  right.id = "tb-right";
  const wordmark = window.document.createElement("div");
  wordmark.id = "wordmark";
  window.document.body.append(cluster, right, wordmark);
  const action = vi.fn();
  const component = new TitleClusterComponent(
    window.document as unknown as Document,
    () => "<svg></svg>",
    action,
  );
  return { window, component, action };
}

describe("TitleClusterComponent", () => {
  it("renders the wordmark, zoom, tools, and staged update affordance", () => {
    const { window, component } = fixture();

    component.update({ updateStaged: true, zoom: 1.2 });

    expect(window.document.querySelector("#tb-cluster #wordmark")).not.toBeNull();
    expect(window.document.getElementById("tb-zoom-label")?.textContent).toBe("120%");
    expect(window.document.querySelector(".tb-update")).not.toBeNull();
  });

  it("emits one typed action per tool click", () => {
    const { window, component, action } = fixture();
    component.update({ updateStaged: false, zoom: 1 });

    const settings = [...window.document.querySelectorAll(".tb-cluster-btn")]
      .find((element) => element.getAttribute("title") === "Settings") as unknown as HTMLElement;
    settings.click();

    expect(action).toHaveBeenCalledWith("app.settings");
  });
});
