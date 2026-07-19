import { Window } from "happy-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { Mock } from "vitest";
import { FrontendCommand } from "./app/frontend-command";

const invokeMock = vi.fn();
let monacoFake = createMonacoFake();

interface FakeDisposable {
  dispose: Mock;
}

interface FakeModel {
  dispose: Mock;
  getValue: Mock;
  setValue: Mock;
  getLineCount: Mock;
  getOffsetAt: Mock;
  onDidChangeContent: Mock;
  change: FakeDisposable;
  changeCallback: () => void;
}

interface FakeEditor {
  dispose: Mock;
  updateOptions: Mock;
  onDidChangeCursorPosition: Mock;
  onMouseMove: Mock;
  onMouseLeave: Mock;
  onMouseDown: Mock;
  createDecorationsCollection: Mock;
  addCommand: Mock;
  getSelection: Mock;
  getScrollTop: Mock;
  setSelection: Mock;
  setScrollTop: Mock;
  getPosition: Mock;
  subscriptions: FakeDisposable[];
  decorations: { set: Mock; clear: Mock };
}

interface FakeDiffEditor {
  dispose: Mock;
  setModel: Mock;
  getLineChanges: Mock;
  onDidUpdateDiff: Mock;
  getModifiedEditor: Mock;
  update: FakeDisposable;
}

vi.mock("./invoke", () => ({ invoke: (...args: unknown[]) => invokeMock(...args) }));
vi.mock("./monaco-loader", () => ({
  MonacoLoader: { load: () => Promise.resolve(monacoFake) },
  detectLanguage: () => "typescript",
  defineCoveMonacoTheme: () => "cove",
}));

import { renderEditorNook } from "./editor-nook";
import { renderDiffViewerNook } from "./diff-viewer-nook";
import { renderMarkdownNook } from "./markdown-nook";

function disposable() {
  return { dispose: vi.fn() };
}

function createMonacoFake() {
  const models: FakeModel[] = [];
  const editors: FakeEditor[] = [];
  const diffEditors: FakeDiffEditor[] = [];
  const markerCalls: unknown[][] = [];

  function makeModel(value: string): FakeModel {
    let current = value;
    const change = disposable();
    const model: FakeModel = {
      dispose: vi.fn(),
      getValue: vi.fn(() => current),
      setValue: vi.fn((next: string) => { current = next; }),
      getLineCount: vi.fn(() => 1),
      getOffsetAt: vi.fn(() => 0),
      onDidChangeContent: vi.fn((callback: () => void) => { model.changeCallback = callback; return change; }),
      change,
      changeCallback: () => undefined,
    };
    models.push(model);
    return model;
  }

  function makeEditor(): FakeEditor {
    const subscriptions = [disposable(), disposable(), disposable(), disposable()];
    const decorations = { set: vi.fn(), clear: vi.fn() };
    const editor = {
      dispose: vi.fn(),
      updateOptions: vi.fn(),
      onDidChangeCursorPosition: vi.fn(() => subscriptions[0]),
      onMouseMove: vi.fn(() => subscriptions[1]),
      onMouseLeave: vi.fn(() => subscriptions[2]),
      onMouseDown: vi.fn(() => subscriptions[3]),
      createDecorationsCollection: vi.fn(() => decorations),
      addCommand: vi.fn(),
      getSelection: vi.fn(() => null),
      getScrollTop: vi.fn(() => 0),
      setSelection: vi.fn(),
      setScrollTop: vi.fn(),
      getPosition: vi.fn(() => null),
      subscriptions,
      decorations,
    };
    editors.push(editor);
    return editor;
  }

  function makeDiffEditor(): FakeDiffEditor {
    const update = disposable();
    const editor = {
      dispose: vi.fn(),
      setModel: vi.fn(),
      getLineChanges: vi.fn(() => []),
      onDidUpdateDiff: vi.fn(() => update),
      getModifiedEditor: vi.fn(() => ({ getAction: vi.fn(() => ({ run: vi.fn() })) })),
      update,
    };
    diffEditors.push(editor);
    return editor;
  }

  class Range { constructor(..._args: number[]) {} }
  class Selection { constructor(..._args: number[]) {} }

  return {
    models,
    editors,
    diffEditors,
    markerCalls,
    Range,
    Selection,
    KeyMod: { CtrlCmd: 1 },
    KeyCode: { KeyS: 2 },
    editor: {
      MouseTargetType: { GUTTER_LINE_DECORATIONS: 1 },
      createModel: vi.fn((value: string) => makeModel(value)),
      create: vi.fn(() => makeEditor()),
      createDiffEditor: vi.fn(() => makeDiffEditor()),
      setModelMarkers: vi.fn((...args: unknown[]) => markerCalls.push(args)),
      defineTheme: vi.fn(),
      setTheme: vi.fn(),
    },
  };
}

describe("Monaco nook ownership", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    const testWindow = new Window();
    vi.stubGlobal("window", testWindow);
    vi.stubGlobal("document", testWindow.document);
    vi.stubGlobal("localStorage", testWindow.localStorage);
    monacoFake = createMonacoFake();
    invokeMock.mockReset();
    invokeMock.mockImplementation(async (command: FrontendCommand) => {
      if (command === FrontendCommand.EditorOpen) return { content: "const x = 1;", size: 12 };
      if (command === FrontendCommand.AttributionFindByRange) return { entries: [] };
      if (command === FrontendCommand.LspDiagnostics) return { available: true, diagnostics: [] };
      if (command === FrontendCommand.ScmDiff) return { originalContent: "a", modifiedContent: "b", newContent: "" };
      if (command === FrontendCommand.ScmBlame) return { lines: [] };
      if (command === FrontendCommand.EditorGetState) return null;
      if (command === FrontendCommand.ConfigGet) return null;
      return undefined;
    });
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it("cancels editor saves, diagnostics, polling and disposes Monaco resources once", async () => {
    const handle = await renderEditorNook("editor", "/repo/file.ts");
    document.body.appendChild(handle.element);
    monacoFake.models[0].changeCallback();
    const callsBeforeDispose = invokeMock.mock.calls.length;

    await handle.dispose();
    await handle.dispose();
    await vi.advanceTimersByTimeAsync(5000);

    expect(invokeMock.mock.calls.length).toBe(callsBeforeDispose);
    expect(monacoFake.models[0].change.dispose).toHaveBeenCalledTimes(1);
    expect(monacoFake.models[0].dispose).toHaveBeenCalledTimes(1);
    expect(monacoFake.editors[0].dispose).toHaveBeenCalledTimes(1);
    expect(monacoFake.editors[0].decorations.clear).toHaveBeenCalledTimes(1);
    for (const subscription of monacoFake.editors[0].subscriptions) expect(subscription.dispose).toHaveBeenCalledTimes(1);
    expect(vi.getTimerCount()).toBe(0);
  });

  it("disposes every diff editor generation and both models", async () => {
    const handle = await renderDiffViewerNook("diff", "/repo/file.ts", "HEAD");
    document.body.appendChild(handle.element);
    const toggle = handle.element.querySelector("button")!;
    toggle.click();
    toggle.click();

    await handle.dispose();
    await handle.dispose();
    toggle.click();

    expect(monacoFake.diffEditors).toHaveLength(3);
    for (const editor of monacoFake.diffEditors) {
      expect(editor.dispose).toHaveBeenCalledTimes(1);
      expect(editor.update.dispose).toHaveBeenCalledTimes(1);
    }
    expect(monacoFake.models).toHaveLength(2);
    for (const model of monacoFake.models) expect(model.dispose).toHaveBeenCalledTimes(1);
  });

  it("cancels markdown debounce and disposes a lazily acquired editor and model", async () => {
    const handle = await renderMarkdownNook("markdown", "/repo/readme.md");
    document.body.appendChild(handle.element);
    const source = [...handle.element.querySelectorAll("button")].find((button) => button.textContent === "Source")!;
    source.click();
    await Promise.resolve();
    monacoFake.models[0].changeCallback();
    const callsBeforeDispose = invokeMock.mock.calls.length;

    await handle.dispose();
    await vi.advanceTimersByTimeAsync(3000);

    expect(invokeMock.mock.calls.length).toBe(callsBeforeDispose);
    expect(monacoFake.models[0].change.dispose).toHaveBeenCalledTimes(1);
    expect(monacoFake.models[0].dispose).toHaveBeenCalledTimes(1);
    expect(monacoFake.editors[0].dispose).toHaveBeenCalledTimes(1);
    expect(vi.getTimerCount()).toBe(0);
  });
});
