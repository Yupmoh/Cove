import { Window } from "happy-dom";
import { describe, expect, it, vi } from "vitest";
import { FindBarFeature } from "./find-bar-feature";

function fixture() {
  const window = new Window();
  const bar = window.document.createElement("div");
  bar.id = "findbar";
  const input = window.document.createElement("input");
  input.id = "find-input";
  const next = window.document.createElement("button");
  next.id = "find-next";
  const previous = window.document.createElement("button");
  previous.id = "find-prev";
  const close = window.document.createElement("button");
  close.id = "find-close";
  window.document.body.append(bar, input, next, previous, close);
  const search = {
    findNext: vi.fn(),
    findPrevious: vi.fn(),
    clearDecorations: vi.fn(),
  };
  const focus = vi.fn();
  const invoke = vi.fn(async () => ({ matches: [{ line: 1, text: "match" }] }));
  const feature = new FindBarFeature(window.document as unknown as Document, {
    active: () => ({ nookId: "nook-1", search, focus }),
    invoke,
  });
  return { window, bar, input, next, close, search, focus, invoke, feature };
}

describe("FindBarFeature", () => {
  it("owns find navigation and engine preflight", async () => {
    const { input, next, search, invoke, feature } = fixture();
    feature.open();
    input.value = "needle";
    next.click();
    await Promise.resolve();

    expect(invoke).toHaveBeenCalledWith("app.nookSearch", {
      nookId: "nook-1",
      query: "needle",
      caseSensitive: false,
    });
    expect(search.findNext).toHaveBeenCalledOnce();
  });

  it("clears decorations and restores terminal focus when closed", () => {
    const { bar, close, search, focus, feature } = fixture();
    feature.open();
    close.click();

    expect(bar.classList.contains("open")).toBe(false);
    expect(search.clearDecorations).toHaveBeenCalledOnce();
    expect(focus).toHaveBeenCalledOnce();
  });
});
