import type * as Monaco from "monaco-editor";
import { detectLanguage } from "./monaco-lang";

type MonacoNamespace = typeof Monaco;

export { detectLanguage };

let monacoPromise: Promise<MonacoNamespace> | null = null;

export const MonacoLoader = {
  get isLoaded(): boolean { return monacoPromise !== null; },

  load(): Promise<MonacoNamespace> {
    if (monacoPromise) return monacoPromise;
    monacoPromise = import("monaco-editor").then((mod): MonacoNamespace => {
      const m = mod as MonacoNamespace;
      setupWorkers();
      return m;
    });
    return monacoPromise;
  },

  reset(): void { monacoPromise = null; },
};

function setupWorkers(): void {
  if (window.MonacoEnvironment) return;
  window.MonacoEnvironment = {
    getWorker(_moduleId: string, label: string): Worker {
      return createWorker(label);
    },
  };
}

function createWorker(label: string): Worker {
  switch (label) {
    case "json": return jsonWorker();
    case "css": case "less": case "scss": return cssWorker();
    case "html": return htmlWorker();
    case "typescript": case "javascript": return tsWorker();
    default: return editorWorker();
  }
}

function jsonWorker(): Worker {
  return new Worker(new URL("monaco-editor/esm/vs/language/json/json.worker.js", import.meta.url), { type: "module" });
}

function cssWorker(): Worker {
  return new Worker(new URL("monaco-editor/esm/vs/language/css/css.worker.js", import.meta.url), { type: "module" });
}

function htmlWorker(): Worker {
  return new Worker(new URL("monaco-editor/esm/vs/language/html/html.worker.js", import.meta.url), { type: "module" });
}

function tsWorker(): Worker {
  return new Worker(new URL("monaco-editor/esm/vs/language/typescript/ts.worker.js", import.meta.url), { type: "module" });
}

function editorWorker(): Worker {
  return new Worker(new URL("monaco-editor/esm/vs/editor/editor.worker.js", import.meta.url), { type: "module" });
}
