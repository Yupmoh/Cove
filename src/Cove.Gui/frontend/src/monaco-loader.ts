import type * as Monaco from "monaco-editor";
import { detectLanguage } from "./monaco-lang";

type MonacoNamespace = typeof Monaco;

export { detectLanguage };

let monacoPromise: Promise<MonacoNamespace> | null = null;

function cssColor(name: string, fallback: string): string {
  const v = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
  return /^#[0-9a-fA-F]{6}$/.test(v) ? v : fallback;
}

function isLightHex(hex: string): boolean {
  const r = parseInt(hex.slice(1, 3), 16);
  const g = parseInt(hex.slice(3, 5), 16);
  const b = parseInt(hex.slice(5, 7), 16);
  return (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255 > 0.55;
}

export function defineCoveMonacoTheme(monaco: MonacoNamespace): string {
  const bg = cssColor("--panel", "#181825");
  const fg = cssColor("--fg", "#cdd6f4");
  const accent = cssColor("--accent", "#cba6f7");
  monaco.editor.defineTheme("cove", {
    base: isLightHex(bg) ? "vs" : "vs-dark",
    inherit: true,
    rules: [],
    colors: {
      "editor.background": bg,
      "editor.foreground": fg,
      "editorCursor.foreground": accent,
      "editorLineNumber.foreground": cssColor("--muted", "#7f849c"),
      "editor.selectionBackground": accent + "55",
    },
  });
  return "cove";
}

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
