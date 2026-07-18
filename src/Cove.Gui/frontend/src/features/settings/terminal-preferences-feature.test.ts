import { describe, expect, it, vi } from "vitest";
import { createTerminalPreferencesFeature } from "./terminal-preferences-feature";

describe("TerminalPreferencesFeature", () => {
  it("owns validated loading, mutable settings, and persistence", async () => {
    const values: Record<string, string> = {
      "terminal.fontFamily": "Berkeley Mono",
      "terminal.fontSize": "99",
      "terminal.lineHeight": "1.5",
      "terminal.cursorStyle": "bar",
      "terminal.cursorBlink": "true",
      "terminal.backgroundOpacity": "0.75",
    };
    const writes = vi.fn();
    async function invoke<T>(
      command: string,
      args: Record<string, unknown>,
    ): Promise<T> {
      if (command === "app.configGet") {
        const value = values[String(args.key)];
        return { ok: value !== undefined, value } as T;
      }
      writes(command, args);
      return {} as T;
    }
    const feature = createTerminalPreferencesFeature({ invoke });

    const settings = await feature.load();
    expect(settings.fontFamily).toBe("Berkeley Mono");
    expect(settings.fontSize).toBe(13);
    expect(settings.lineHeight).toBe(1.5);
    expect(settings.cursorStyle).toBe("bar");
    expect(feature.theme(null).background).toBe("rgba(30, 30, 46, 0.75)");

    settings.fontSize = 16;
    feature.persist();
    await Promise.resolve();
    expect(writes).toHaveBeenCalledWith("app.configSet", {
      key: "terminal.fontSize",
      value: "16",
    });
    expect(writes).toHaveBeenCalledTimes(9);

    feature.dispose();
  });
});
