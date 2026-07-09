import type * as Monaco from "monaco-editor";
import { detectLanguage } from "./monaco-lang";

type MonacoNamespace = typeof Monaco;

export { detectLanguage };

let monacoPromise: Promise<MonacoNamespace> | null = null;

export const MonacoLoader = {
  get isLoaded(): boolean { return monacoPromise !== null; },

  load(): Promise<MonacoNamespace> {
    if (monacoPromise) return monacoPromise;
    monacoPromise = import("monaco-editor").then((mod): MonacoNamespace => mod as MonacoNamespace);
    return monacoPromise;
  },

  reset(): void { monacoPromise = null; },
};
