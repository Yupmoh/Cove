import { describe, expect, it, vi } from "vitest";
import { enqueuePaneWrite, pendingPaneWrites } from "./write-queue";

const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms));

describe("enqueuePaneWrite", () => {
  it("delivers same-pane writes in submission order even when an early write resolves slowly", async () => {
    const order: string[] = [];
    const write = async (_p: string, d: string) => {
      await sleep(d === "a" ? 25 : 1);
      order.push(d);
    };

    await Promise.all([
      enqueuePaneWrite("p1", "a", write),
      enqueuePaneWrite("p1", "b", write),
      enqueuePaneWrite("p1", "c", write),
    ]);

    expect(order).toEqual(["a", "b", "c"]);
  });

  it("keeps ordering after a failed write instead of stalling the queue", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => void 0);
    const order: string[] = [];
    const write = (_p: string, d: string) => {
      order.push(d);
      return d === "boom" ? Promise.reject(new Error("x")) : Promise.resolve();
    };

    await enqueuePaneWrite("p1", "boom", write);
    await enqueuePaneWrite("p1", "after", write);

    expect(order).toEqual(["boom", "after"]);
    expect(warn).toHaveBeenCalledTimes(1);
    warn.mockRestore();
  });

  it("tracks independent panes and clears drained tails", async () => {
    const order: string[] = [];
    const write = async (p: string, d: string) => {
      await sleep(p === "p1" ? 10 : 1);
      order.push(`${p}:${d}`);
    };

    await Promise.all([
      enqueuePaneWrite("p1", "a", write),
      enqueuePaneWrite("p2", "b", write),
    ]);

    expect(order).toEqual(["p2:b", "p1:a"]);
    await sleep(1);
    expect(pendingPaneWrites()).toBe(0);
  });
});
