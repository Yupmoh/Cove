import { describe, expect, it, vi } from "vitest";
import { EngineClient, type EngineBridge } from "./engine-client";

function bridge(): EngineBridge {
  return {
    invoke: vi.fn(async () => JSON.stringify({ ok: true })),
    on: vi.fn(),
    off: vi.fn(),
  };
}

describe("EngineClient", () => {
  it("routes Cove URIs through the engine control bridge", async () => {
    const ryn = bridge();
    const client = new EngineClient(ryn);

    await expect(client.invoke<{ ok: boolean }>("cove://commands/bay.list", { includeClosed: false }))
      .resolves.toEqual({ ok: true });
    expect(ryn.invoke).toHaveBeenCalledWith("app.callEngine", {
      uri: "cove://commands/bay.list",
      argsJson: JSON.stringify({ includeClosed: false }),
    });
  });

  it("routes native commands directly through Ryn", async () => {
    const ryn = bridge();
    const client = new EngineClient(ryn);

    await client.invoke("window.center", {});

    expect(ryn.invoke).toHaveBeenCalledWith("window.center", {});
  });

  it("returns raw native command results without JSON coercion", async () => {
    const ryn = bridge();
    vi.mocked(ryn.invoke).mockResolvedValue("/picked/folder");
    const client = new EngineClient(ryn);

    await expect(client.native<string>("dialog.openFolder", { initialPath: "/" }))
      .resolves.toBe("/picked/folder");
  });

  it("owns subscriptions and removes them during disposal", async () => {
    const ryn = bridge();
    const client = new EngineClient(ryn);
    const listener = vi.fn();

    const unsubscribe = client.on("engine.event", listener);
    expect(ryn.on).toHaveBeenCalledWith("engine.event", listener);

    unsubscribe();
    expect(ryn.off).toHaveBeenCalledWith("engine.event", listener);

    client.on("window.focused", listener);
    await client.dispose();
    expect(ryn.off).toHaveBeenCalledWith("window.focused", listener);
  });

  it("rejects work after disposal", async () => {
    const client = new EngineClient(bridge());
    await client.dispose();

    await expect(client.invoke("window.center", {})).rejects.toThrow("EngineClient is disposed");
    expect(() => client.on("engine.event", () => {})).toThrow("EngineClient is disposed");
  });
});
