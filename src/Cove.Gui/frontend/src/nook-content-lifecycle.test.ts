import { Window } from "happy-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { FrontendCommand } from "./app/frontend-command";

const invokeMock = vi.fn();
vi.mock("./invoke", () => ({ invoke: (...args: unknown[]) => invokeMock(...args) }));

import { renderHtmlNote } from "./html-note";
import { renderSketchNote } from "./sketch-note";
import { renderKanbanBoard } from "./tasks-kanban";
import { renderDiffReviewNook } from "./diff-review-nook";

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((done) => { resolve = done; });
  return { promise, resolve };
}

describe("resource-owning nook handles", () => {
  beforeEach(() => {
    const testWindow = new Window();
    vi.stubGlobal("window", testWindow);
    vi.stubGlobal("document", testWindow.document);
    vi.stubGlobal("MutationObserver", testWindow.MutationObserver);
    vi.stubGlobal("HTMLCanvasElement", testWindow.HTMLCanvasElement);
    invokeMock.mockReset();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it("balances HTML message listeners across preview replacement and disposal", async () => {
    invokeMock.mockImplementation(async (command: FrontendCommand) => {
      if (command === FrontendCommand.NoteRead) return { content: "<p>note</p>" };
      if (command === FrontendCommand.NoteGetState) return { state: null };
      return undefined;
    });
    const add = vi.spyOn(window, "addEventListener");
    const remove = vi.spyOn(window, "removeEventListener");
    const handle = await renderHtmlNote("bay", "note");
    document.body.appendChild(handle.element);

    const toggle = [...handle.element.querySelectorAll("button")].find((button) => button.textContent === "Source")!;
    toggle.click();
    toggle.click();
    const oldSource = handle.element.querySelector("iframe")?.contentWindow;
    await handle.dispose();
    await handle.dispose();
    window.dispatchEvent(new window.MessageEvent("message", {
      data: { type: "cove:state-snapshot", state: { late: true } },
      source: oldSource ?? null,
    }));
    await Promise.resolve();

    expect(add.mock.calls.filter(([type]) => type === "message")).toHaveLength(2);
    expect(remove.mock.calls.filter(([type]) => type === "message")).toHaveLength(2);
    expect(invokeMock.mock.calls.filter(([command]) => command === FrontendCommand.NoteSaveState)).toHaveLength(0);
  });

  it("removes the sketch resize listener exactly once", async () => {
    const context = {
      fillStyle: "", strokeStyle: "", lineWidth: 0, lineCap: "", font: "",
      fillRect: vi.fn(), save: vi.fn(), scale: vi.fn(), translate: vi.fn(), beginPath: vi.fn(),
      moveTo: vi.fn(), lineTo: vi.fn(), stroke: vi.fn(), restore: vi.fn(), fillText: vi.fn(),
    };
    vi.spyOn(HTMLCanvasElement.prototype, "getContext").mockReturnValue(context as unknown as CanvasRenderingContext2D);
    invokeMock.mockResolvedValue({ content: "{\"elements\":[],\"appState\":{\"zoom\":1,\"scrollX\":0,\"scrollY\":0}}" });
    const add = vi.spyOn(window, "addEventListener");
    const remove = vi.spyOn(window, "removeEventListener");
    const handle = await renderSketchNote("bay", "sketch");
    document.body.appendChild(handle.element);

    await handle.dispose();
    await handle.dispose();
    const renders = context.fillRect.mock.calls.length;
    window.dispatchEvent(new window.Event("resize"));

    expect(context.fillRect).toHaveBeenCalledTimes(renders);
    expect(add.mock.calls.filter(([type]) => type === "resize")).toHaveLength(1);
    expect(remove.mock.calls.filter(([type]) => type === "resize")).toHaveLength(1);
  });

  it("owns the active quick-action document listener and menu", async () => {
    vi.useFakeTimers();
    invokeMock.mockImplementation(async (command: FrontendCommand) => {
      if (command === FrontendCommand.TaskStatusList) return { statuses: [{ id: "todo", bayId: "bay", name: "Todo", color: "red", position: 0, hidden: false }] };
      if (command === FrontendCommand.TaskList) return { cards: [{ id: "card", title: "Task", description: "", taskNumber: 1, bayId: "bay", statusId: "todo", priority: 0, size: 1, assignee: null, source: "", currentPrimaryRunId: null, createdAt: "", updatedAt: "" }] };
      return undefined;
    });
    const add = vi.spyOn(document, "addEventListener");
    const remove = vi.spyOn(document, "removeEventListener");
    const handle = await renderKanbanBoard("bay");
    document.body.appendChild(handle.element);
    const card = handle.element.querySelector<HTMLElement>(".kanban-card")!;

    card.dispatchEvent(new window.MouseEvent("contextmenu", { bubbles: true }));
    await vi.runAllTimersAsync();
    card.dispatchEvent(new window.MouseEvent("contextmenu", { bubbles: true }));
    await vi.runAllTimersAsync();
    await handle.dispose();

    expect(document.querySelector(".quick-actions-menu")).toBeNull();
    expect(add.mock.calls.filter(([type]) => type === "click")).toHaveLength(2);
    expect(remove.mock.calls.filter(([type]) => type === "click")).toHaveLength(2);
  });

  it("stops diff-review polling and ignores an in-flight completion", async () => {
    vi.useFakeTimers();
    const snapshots = deferred<{ snapshots: unknown[] }>();
    invokeMock.mockImplementation((command: FrontendCommand) => {
      if (command === FrontendCommand.SnapshotList) return snapshots.promise;
      if (command === FrontendCommand.ReviewListComments) return Promise.resolve({ comments: [] });
      if (command === FrontendCommand.AttributionFindByRange) return Promise.resolve({ entries: [] });
      return Promise.resolve(undefined);
    });
    const handle = await renderDiffReviewNook("bay");
    document.body.appendChild(handle.element);
    const input = handle.element.querySelector("input")!;
    input.value = "abc";
    handle.element.querySelector("button")!.click();
    expect(invokeMock.mock.calls.filter(([command]) => command === FrontendCommand.SnapshotList)).toHaveLength(1);

    await handle.dispose();
    await vi.advanceTimersByTimeAsync(6000);
    snapshots.resolve({ snapshots: [] });
    await Promise.resolve();

    expect(invokeMock.mock.calls.filter(([command]) => command === FrontendCommand.SnapshotList)).toHaveLength(1);
    expect(handle.element.textContent).not.toContain("No snapshots or comments");
  });
});
