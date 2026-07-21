import { readFileSync } from "node:fs";
import { Window } from "happy-dom";
import { describe, expect, it, vi } from "vitest";
import { BayCreateFeature, type BayCreateDependencies } from "./bay-create-feature";

const workspaceCreateCss = readFileSync(new URL("../../workspace/workspace-create.css", import.meta.url), "utf8");

function fixture(selectedIcon: string | null = null) {
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
    buildIconGrid: (_selected: string | null, onSelect: (icon: string | null) => void) => {
      if (selectedIcon) onSelect(selectedIcon);
      return document.createElement("div");
    },
    loadBays: vi.fn(async () => {}),
    reload: vi.fn(async () => {}),
    showToast: vi.fn(),
  } as unknown as BayCreateDependencies);
  return { window, root, invoke, feature };
}


describe("BayCreateFeature", () => {
  it("balances all Bay marks into two compact rows", () => {
    expect(workspaceCreateCss).toContain(".wsc-box .ws-icon-grid { grid-template-columns: repeat(7, 32px);");
    expect(workspaceCreateCss).toContain(".wsc-box .ws-icon-cell { width: 32px; height: 32px;");
    expect(workspaceCreateCss).toContain(".wsc-box > .set-body { padding: 14px 20px 0;");
    expect(workspaceCreateCss).toContain(".wsc-actions { display: flex; justify-content: flex-end; gap: 8px; margin: 8px -20px 0; padding: 12px 20px 14px; border-top: 1px solid var(--border);");
  });

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

    const withIcon = fixture("orbit");
    withIcon.feature.open();
    (withIcon.root.querySelector("#wsc-name") as unknown as HTMLInputElement).value = "Marked Bay";
    (withIcon.root.querySelector("#wsc-path") as unknown as HTMLInputElement).value = "/repo";
    (withIcon.root.querySelector("#wsc-create") as unknown as HTMLElement).click();
    await vi.waitFor(() => expect(withIcon.invoke).toHaveBeenCalledWith("cove://commands/bay.set-icon", {
      id: "bay-1",
      kind: "mark",
      value: "orbit",
    }));
    await withIcon.feature.dispose();

    await feature.dispose();
    feature.close();
  });
});
