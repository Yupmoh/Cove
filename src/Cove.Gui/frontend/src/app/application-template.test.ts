import { Window } from "happy-dom";
import { describe, expect, it } from "vitest";
import { AppShell, shellElementIds } from "../shell/app-shell";
import { mountApplicationTemplate } from "./application-template";

describe("mountApplicationTemplate", () => {
  it("mounts every typed shell host before AppShell binds it", () => {
    const window = new Window();
    const root = window.document.createElement("div");
    root.id = "app";
    window.document.body.appendChild(root);

    mountApplicationTemplate(window.document as unknown as Document, root as unknown as HTMLElement);

    expect(root.children).toHaveLength(9);
    expect(Array.from(root.children, (element) => element.id)).toEqual([
      "titlebar",
      "app-row",
      "palette",
      "onboarding",
      "settings",
      "ws-create",
      "findbar",
      "launcher",
      "perf-hud",
    ]);
    for (const id of shellElementIds) expect(window.document.getElementById(id)).not.toBeNull();
    expect(() => new AppShell(window.document as unknown as Document)).not.toThrow();
  });
});
