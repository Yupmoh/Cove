import { describe, expect, it, vi } from "vitest";
import { enqueueNookWrite, pendingNookWrites } from "./write-queue";

const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms));

describe("enqueueNookWrite", () => {
  it("delivers same-nook writes in submission order even when an early write resolves slowly", async () => {
    const order: string[] = [];
    const write = async (_p: string, d: string) => {
      await sleep(d === "a" ? 25 : 1);
      order.push(d);
    };

    await Promise.all([
      enqueueNookWrite("p1", "a", write),
      enqueueNookWrite("p1", "b", write),
      enqueueNookWrite("p1", "c", write),
    ]);

    expect(order).toEqual(["a", "b", "c"]);
  });

  it("rejects a failed write while keeping later same-nook writes moving", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => void 0);
    const order: string[] = [];
    const error = new Error("x");
    const write = (_p: string, d: string) => {
      order.push(d);
      return d === "boom" ? Promise.reject(error) : Promise.resolve();
    };

    const failed = enqueueNookWrite("p1", "boom", write);
    const after = enqueueNookWrite("p1", "after", write);

    await expect(failed).rejects.toBe(error);
    await expect(after).resolves.toBeUndefined();
    expect(order).toEqual(["boom", "after"]);
    expect(warn).toHaveBeenCalledTimes(1);
    warn.mockRestore();
  });

  it("tracks independent nooks and clears drained tails", async () => {
    const order: string[] = [];
    const write = async (p: string, d: string) => {
      await sleep(p === "p1" ? 10 : 1);
      order.push(`${p}:${d}`);
    };

    await Promise.all([
      enqueueNookWrite("p1", "a", write),
      enqueueNookWrite("p2", "b", write),
    ]);

    expect(order).toEqual(["p2:b", "p1:a"]);
    await sleep(1);
    expect(pendingNookWrites()).toBe(0);
  });
});
