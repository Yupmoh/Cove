import { describe, expect, it } from "vitest";
import { Terminal } from "@xterm/headless";
import { SerializeAddon } from "@xterm/addon-serialize";
import type { Terminal as BrowserTerminal } from "@xterm/xterm";

function createTerminal(cols: number, rows: number, scrollback: number) {
  const terminal = new Terminal({ cols, rows, scrollback, allowProposedApi: true });
  const serializer = new SerializeAddon();
  serializer.activate(terminal as unknown as BrowserTerminal);
  return { terminal, serializer };
}

function writeTerminal(terminal: Terminal, data: string | Uint8Array): Promise<void> {
  const { promise, resolve } = (Promise as PromiseConstructor & { withResolvers<T>(): { promise: Promise<T>; resolve(value?: T | PromiseLike<T>): void; reject(reason?: unknown): void } }).withResolvers<void>();
  terminal.write(data, resolve);
  return promise;
}

describe("terminal checkpoint restoration", () => {
  it("restores ten thousand lines then replays the exact raw tail", async () => {
    const source = createTerminal(80, 24, 10000);
    const restored = createTerminal(80, 24, 10000);
    try {
      const history = Array.from({ length: 12000 }, (_, index) => `line-${index.toString().padStart(5, "0")}\r\n`).join("");
      await writeTerminal(source.terminal, history);
      const checkpoint = source.serializer.serialize();
      const tail = "\u001b[32mtail-after-checkpoint\u001b[0m\r\n";
      await writeTerminal(source.terminal, tail);

      await writeTerminal(restored.terminal, checkpoint);
      await writeTerminal(restored.terminal, tail);

      expect(restored.serializer.serialize()).toBe(source.serializer.serialize());
      expect(restored.terminal.buffer.active.length).toBe(source.terminal.buffer.active.length);
    } finally {
      source.terminal.dispose();
      restored.terminal.dispose();
    }
  });

  it("restores alternate-screen content and mode supplements before resizing", async () => {
    const source = createTerminal(80, 24, 10000);
    const restored = createTerminal(80, 24, 10000);
    try {
      await writeTerminal(source.terminal, "primary-screen\r\n\u001b[?1049h\u001b[Halternate-screen\u001b[?1003h");
      const checkpoint = source.serializer.serialize();
      const supplement = "\u001b[?1006h\u001b[?1007h\u001b[?2004h";
      await writeTerminal(source.terminal, supplement);

      await writeTerminal(restored.terminal, checkpoint);
      await writeTerminal(restored.terminal, supplement);
      source.terminal.resize(132, 40);
      restored.terminal.resize(132, 40);

      expect(restored.terminal.buffer.active.type).toBe("alternate");
      expect(restored.terminal.buffer.active.getLine(0)?.translateToString(true)).toContain("alternate-screen");
      expect(restored.serializer.serialize()).toBe(source.serializer.serialize());
      expect(restored.terminal.modes.mouseTrackingMode).toBe("any");
      expect(restored.terminal.modes.bracketedPasteMode).toBe(true);
    } finally {
      source.terminal.dispose();
      restored.terminal.dispose();
    }
  });
});
