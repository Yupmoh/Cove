import { FrontendCommand } from "../../app/frontend-command";
import { LifecycleScope } from "../../app/lifecycle";

export interface TerminalSearch {
  findNext(query: string, options: unknown): unknown;
  findPrevious(query: string, options: unknown): unknown;
  clearDecorations(): void;
}

export interface FindTarget {
  nookId: string;
  search: TerminalSearch;
  focus(): void;
}

export interface FindBarDependencies {
  active(): FindTarget | null;
  invoke(command: FrontendCommand, args: unknown): Promise<unknown>;
}

const decorations = {
  matchBackground: "#6c5b8e",
  activeMatchBackground: "#cba6f7",
  matchOverviewRuler: "#cba6f7",
  activeMatchColorOverviewRuler: "#cba6f7",
};

export class FindBarFeature {
  private readonly lifecycle = new LifecycleScope();
  private readonly bar: HTMLElement;
  private readonly input: HTMLInputElement;

  constructor(document: Document, private readonly dependencies: FindBarDependencies) {
    const bar = document.getElementById("findbar");
    const input = document.getElementById("find-input");
    const next = document.getElementById("find-next");
    const previous = document.getElementById("find-prev");
    const close = document.getElementById("find-close");
    if (!bar || input?.tagName !== "INPUT" || !next || !previous || !close) {
      throw new Error("Missing terminal find shell");
    }
    this.bar = bar;
    this.input = input as HTMLInputElement;
    this.lifecycle.listen(input, "input", () => void this.find(1));
    this.lifecycle.listen(input, "keydown", (event) => {
      const keyboard = event as KeyboardEvent;
      if (keyboard.key === "Escape") {
        keyboard.preventDefault();
        this.close();
      } else if (keyboard.key === "Enter") {
        keyboard.preventDefault();
        void this.find(keyboard.shiftKey ? -1 : 1);
      }
    });
    this.lifecycle.listen(next, "click", () => void this.find(1));
    this.lifecycle.listen(previous, "click", () => void this.find(-1));
    this.lifecycle.listen(close, "click", () => this.close());
  }

  open(): void {
    this.bar.classList.add("open");
    this.input.focus();
    this.input.select();
  }

  close(): void {
    this.bar.classList.remove("open");
    const target = this.dependencies.active();
    target?.search.clearDecorations();
    target?.focus();
  }

  dispose(): Promise<void> {
    return this.lifecycle.dispose();
  }

  private async find(direction: number): Promise<void> {
    const target = this.dependencies.active();
    const query = this.input.value;
    if (!target || !query) return;
    try {
      const result = await this.dependencies.invoke(FrontendCommand.AppNookSearch, {
        nookId: target.nookId,
        query,
        caseSensitive: false,
      }) as { matches: { line: number; text: string }[] };
      if (result.matches.length === 0) {
        target.search.clearDecorations();
        return;
      }
    } catch {
      void 0;
    }
    const options = { caseSensitive: false, decorations };
    if (direction >= 0) target.search.findNext(query, options);
    else target.search.findPrevious(query, options);
  }
}
