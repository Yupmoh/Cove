import { Window } from "happy-dom";
import { describe, expect, it, vi } from "vitest";
import { BayCreateFeature, type BayCreateDependencies } from "./bay-create-feature";

function fixture() {
  const window = new Window();
  const document = window.document;
  const root = document.createElement("div");
  root.innerHTML = `
    <button id="wsc-close"></button>
    <input id="wsc-name">
    <input id="wsc-path">
    <div id="wsc-error"></div>
    <div id="wsc-icon-grid"></div>
    <button id="wsc-cancel"></button>
    <button id="wsc-browse"></button>
    <button id="wsc-create"></button>
  `;
  document.body.appendChild(root);
  const invoke = vi.fn(async () => ({ id: "bay-1" }));
  const feature = new BayCreateFeature({
    document,
    root,
    nameInput: root.querySelector("#wsc-name"),
    pathInput: root.querySelector("#wsc-path"),
    error: root.querySelector("#wsc-error"),
    invoke,
    invokeNative: vi.fn(async () => null),
    defaultDirectory: () => "/repo",
    activeProjectDirectory: () => "/active",
    buildIconGrid: () => document.createElement("div"),
    loadBays: vi.fn(async () => {}),
    reload: vi.fn(async () => {}),
    showToast: vi.fn(),
  } as unknown as BayCreateDependencies);
  return { window, root, invoke, feature };
}

describe("BayCreateFeature", () => {
  it("owns dialog validation, creation, and disposal", async () => {
    const { root, invoke, feature } = fixture();
    feature.open();
    expect(root.classList.contains("open")).toBe(true);
    (root.querySelector("#wsc-name") as unknown as HTMLInputElement).value = "Bay";
    (root.querySelector("#wsc-path") as unknown as HTMLInputElement).value = "/repo";
    (root.querySelector("#wsc-create") as unknown as HTMLElement).click();
    await vi.waitFor(() => expect(invoke).toHaveBeenCalledWith("cove://commands/bay.create", {
      name: "Bay",
      projectDir: "/repo",
      collectionId: "",
    }));
    expect(root.classList.contains("open")).toBe(false);

    await feature.dispose();
    feature.close();
  });
});
