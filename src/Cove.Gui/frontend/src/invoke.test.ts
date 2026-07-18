import { afterEach, describe, expect, it, vi } from "vitest";
import { disposeFrontendTransport, getFrontendEnginePort } from "./invoke";

function bridge(value: string) {
  return {
    invoke: vi.fn(async () => value),
    on: vi.fn(),
    off: vi.fn(),
  };
}

afterEach(async () => {
  await disposeFrontendTransport();
  vi.unstubAllGlobals();
});

describe("getFrontendEnginePort", () => {
  it("keeps a stable facade while resolving a replaced Ryn bridge per call", async () => {
    const first = bridge("first");
    const second = bridge("second");
    const runtimeWindow = { __ryn: first };
    vi.stubGlobal("window", runtimeWindow);
    const engine = getFrontendEnginePort();

    await expect(engine.native("window.center", {})).resolves.toBe("first");
    runtimeWindow.__ryn = second;
    await expect(engine.native("window.center", {})).resolves.toBe("second");
    expect(getFrontendEnginePort()).toBe(engine);
    expect(first.invoke).toHaveBeenCalledOnce();
    expect(second.invoke).toHaveBeenCalledOnce();
  });
});
