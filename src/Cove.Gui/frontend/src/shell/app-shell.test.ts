import { Window } from "happy-dom";
import { describe, expect, it, vi } from "vitest";
import { AppShell, shellElementIds } from "./app-shell";

function shellDocument(): Document {
  const window = new Window();
  for (const id of shellElementIds) {
    const tag = id === "pal-input" || id === "find-input" || id === "wsc-name" || id === "wsc-path" ? "input" : id === "wordmark-img" ? "img" : "div";
    const element = window.document.createElement(tag);
    element.id = id;
    window.document.body.appendChild(element);
  }
  return window.document as unknown as Document;
}

describe("AppShell", () => {
  it("owns every static application host without replacing existing nodes", () => {
    const document = shellDocument();
    const grid = document.getElementById("grid");

    const shell = new AppShell(document);

    expect(shell.grid).toBe(grid);
    expect(shell.paletteInput).toBeInstanceOf(document.defaultView!.HTMLInputElement);
    expect(shell.wordmarkImage).toBeInstanceOf(document.defaultView!.HTMLImageElement);
  });

  it("fails startup when a required shell host is missing", () => {
    const document = shellDocument();
    document.getElementById("launcher")?.remove();

    expect(() => new AppShell(document)).toThrow("Missing shell element #launcher");
  });

  it("releases persistent global listeners on disposal", async () => {
    const document = shellDocument();
    const listener = vi.fn();
    const shell = new AppShell(document);
    shell.listen(document, "click", listener);

    document.dispatchEvent(new document.defaultView!.Event("click"));
    await shell.dispose();
    document.dispatchEvent(new document.defaultView!.Event("click"));

    expect(listener).toHaveBeenCalledOnce();
  });
});
