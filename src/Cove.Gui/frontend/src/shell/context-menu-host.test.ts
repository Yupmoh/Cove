import { Window } from "happy-dom";
import { describe, expect, it, vi } from "vitest";
import { ContextMenuHost } from "./context-menu-host";

describe("ContextMenuHost", () => {
  it("owns selection, Escape, and external listener disposal", async () => {
    const window = new Window();
    const document = window.document as unknown as Document;
    const select = vi.fn();
    const host = new ContextMenuHost(document);

    host.open({ x: 10, y: 12, items: [{ id: "open", label: "Open" }] }, select);
    const menu = document.querySelector(".ctx-menu") as HTMLElement;
    expect(menu).not.toBeNull();
    (menu.querySelector(".ctx-item") as HTMLElement).click();
    expect(select).toHaveBeenCalledWith("open");
    expect(document.querySelector(".ctx-menu")).toBeNull();

    host.open({ x: 10, y: 12, items: [{ id: "open", label: "Open" }] }, select);
    document.dispatchEvent(new window.KeyboardEvent("keydown", { key: "Escape" }) as unknown as Event);
    expect(document.querySelector(".ctx-menu")).toBeNull();

    await host.dispose();
    const sentinel = document.createElement("div");
    sentinel.className = "ctx-menu";
    document.body.appendChild(sentinel);
    document.dispatchEvent(new window.KeyboardEvent("keydown", { key: "Escape" }) as unknown as Event);
    expect(document.querySelector(".ctx-menu")).toBe(sentinel);
  });
});
