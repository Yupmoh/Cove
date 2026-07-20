import { Window } from "happy-dom";
import { describe, expect, it, vi } from "vitest";
import { LifecycleScope } from "../../app/lifecycle";
import { createNotepadFeature, type NotepadFeatureDependencies } from "./notepad-feature";

function fixture(
  notes: Array<{
    id: string;
    bayId: string;
    title: string;
    kind: string;
    updatedAt: string;
  }> = [],
) {
  const window = new Window();
  const container = window.document.createElement("div");
  window.document.body.appendChild(container);
  const invoke = vi.fn(async <T>() => ({ notes } as T));
  const dependencies: NotepadFeatureDependencies = {
    document: window.document as unknown as Document,
    storage: window.localStorage as unknown as Storage,
    invoke: invoke as NotepadFeatureDependencies["invoke"],
    isVisible: () => true,
    rerenderSidebar: vi.fn(),
    sidebarHead: () => window.document.createElement("div") as unknown as HTMLElement,
    spawnNook: vi.fn(async () => ({ nookId: "nook-1" })),
    createShore: vi.fn(async () => ({ shoreId: "shore-1" })),
    selectShore: vi.fn(),

    focusNook: vi.fn(),
    openNote: vi.fn(async () => {}),
  };
  return { container, dependencies, invoke, window, feature: createNotepadFeature(dependencies) };
}

describe("NotepadFeature", () => {
  it("loads and groups notes from the default scope", async () => {
    const { container, invoke, feature } = fixture([{
      id: "note-1",
      bayId: "default",
      title: "Existing",
      kind: "markdown",
      updatedAt: "2026-07-18T00:00:00Z",
    }]);

    feature.render(container as unknown as HTMLElement);
    await Promise.resolve();
    await Promise.resolve();
    container.replaceChildren();
    feature.render(container as unknown as HTMLElement);

    expect(invoke).toHaveBeenCalledWith("cove://commands/note.list", { bayId: "default" });
    expect(container.querySelector(".ns-group-name")?.textContent).toBe("Default");
  });

  it("loads and creates notes without an active bay", async () => {
    const { container, invoke, feature } = fixture();

    feature.render(container as unknown as HTMLElement);
    await Promise.resolve();
    await Promise.resolve();
    (container.querySelector("#ns-empty-create") as unknown as HTMLElement).click();
    await Promise.resolve();
    await Promise.resolve();

    expect(invoke).toHaveBeenCalledWith("cove://commands/note.list", { bayId: "default" });
    expect(invoke).toHaveBeenCalledWith("cove://commands/note.create", {
      title: "Untitled",
      bayId: "default",
      source: "user",
      content: "",
      kind: "markdown",
    });
  });

  it("owns one keyboard action across repeated renders", async () => {
    const { container, dependencies, feature, window } = fixture();

    feature.render(container as unknown as HTMLElement);
    await Promise.resolve();
    await Promise.resolve();
    vi.mocked(dependencies.rerenderSidebar).mockClear();
    const removeFromOriginal = vi.spyOn(container, "removeEventListener");
    const replacement = window.document.createElement("div");
    const removeFromReplacement = vi.spyOn(replacement, "removeEventListener");
    window.document.body.appendChild(replacement);
    feature.render(replacement as unknown as HTMLElement);

    container.dispatchEvent(new window.KeyboardEvent("keydown", { key: "ArrowDown" }));
    replacement.dispatchEvent(new window.KeyboardEvent("keydown", { key: "ArrowDown" }));

    expect(dependencies.rerenderSidebar).toHaveBeenCalledTimes(1);
    expect(removeFromOriginal).toHaveBeenCalledWith("keydown", expect.any(Function));
    await feature.dispose();
    await feature.dispose();
    expect(removeFromReplacement).toHaveBeenCalledTimes(1);
  });

  it("does not retain detached render-node listeners until disposal", async () => {
    const { container, feature } = fixture([{
      id: "note-1",
      bayId: "default",
      title: "Lifecycle",
      kind: "markdown",
      updatedAt: "2026-07-18T00:00:00Z",
    }]);

    feature.render(container as unknown as HTMLElement);
    await Promise.resolve();
    await Promise.resolve();
    container.replaceChildren();
    feature.render(container as unknown as HTMLElement);
    const note = container.querySelector(".ns-note") as unknown as HTMLElement;
    const removeFromDetachedNote = vi.spyOn(note, "removeEventListener");
    container.replaceChildren();
    feature.render(container as unknown as HTMLElement);

    await feature.dispose();

    expect(removeFromDetachedNote).not.toHaveBeenCalled();
  });

  it("disposal cancels an outstanding host-element wait", async () => {
    vi.useFakeTimers();
    const { container, dependencies, feature } = fixture([{
      id: "note-1",
      bayId: "bay-42",
      title: "Lifecycle",
      kind: "markdown",
      updatedAt: "2026-07-18T00:00:00Z",
    }]);

    feature.render(container as unknown as HTMLElement);
    await Promise.resolve();
    await Promise.resolve();
    container.replaceChildren();
    feature.render(container as unknown as HTMLElement);
    (container.querySelector(".ns-note") as unknown as HTMLElement | null)?.click();
    await Promise.resolve();
    await Promise.resolve();
    await Promise.resolve();
    await Promise.resolve();

    expect(vi.getTimerCount()).toBe(1);
    await feature.dispose();
    expect(vi.getTimerCount()).toBe(0);
    const editor = container.ownerDocument.createElement("div");
    editor.className = "notepad-editor";
    container.appendChild(editor);
    await vi.runAllTimersAsync();

    expect(dependencies.openNote).not.toHaveBeenCalled();
    vi.useRealTimers();
  });

  it("releases lifecycle ownership after a host-element wait settles", async () => {
    vi.useFakeTimers();
    const own = vi.spyOn(LifecycleScope.prototype, "own");
    const { container, dependencies, feature } = fixture([{
      id: "note-1",
      bayId: "default",
      title: "Lifecycle",
      kind: "markdown",
      updatedAt: "2026-07-18T00:00:00Z",
    }]);
    const ownedAtCreation = own.mock.calls.length;

    try {
      feature.render(container as unknown as HTMLElement);
      await Promise.resolve();
      await Promise.resolve();
      container.replaceChildren();
      feature.render(container as unknown as HTMLElement);
      (container.querySelector(".ns-note") as unknown as HTMLElement).click();
      await Promise.resolve();
      await Promise.resolve();
      await Promise.resolve();
      await Promise.resolve();
      const editor = container.ownerDocument.createElement("div");
      editor.className = "notepad-editor";
      container.appendChild(editor);
      await vi.advanceTimersByTimeAsync(50);

      expect(dependencies.openNote).toHaveBeenCalledExactlyOnceWith("default", "note-1");
      expect(own).toHaveBeenCalledTimes(ownedAtCreation);
    } finally {
      await feature.dispose();
      own.mockRestore();
      vi.useRealTimers();
    }
  });
});
