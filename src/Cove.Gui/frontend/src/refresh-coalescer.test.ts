import { describe, it, expect } from "vitest";
import { createCoalescer } from "./refresh-coalescer";

function deferred(): { promise: Promise<void>; resolve: () => void } {
  let resolve!: () => void;
  const promise = new Promise<void>((r) => { resolve = r; });
  return { promise, resolve };
}

describe("createCoalescer", () => {
  it("collapses a burst during an in-flight run into exactly one queued rerun", async () => {
    let runs = 0;
    let gate = deferred();
    const coalesce = createCoalescer(async () => {
      runs += 1;
      await gate.promise;
    });

    const first = coalesce();
    void coalesce();
    void coalesce();
    void coalesce();
    expect(runs).toBe(1);

    const nextGate = deferred();
    const pending = gate;
    gate = nextGate;
    pending.resolve();
    await first;
    await Promise.resolve();
    expect(runs).toBe(2);

    nextGate.resolve();
    await Promise.resolve();
    expect(runs).toBe(2);
  });

  it("runs sequential non-overlapping calls each time", async () => {
    let runs = 0;
    const coalesce = createCoalescer(async () => { runs += 1; });
    await coalesce();
    await coalesce();
    await coalesce();
    expect(runs).toBe(3);
  });

  it("resets in-flight state even if the run throws", async () => {
    let runs = 0;
    const coalesce = createCoalescer(async () => {
      runs += 1;
      throw new Error("boom");
    });
    await expect(coalesce()).rejects.toThrow("boom");
    await expect(coalesce()).rejects.toThrow("boom");
    expect(runs).toBe(2);
  });
});
