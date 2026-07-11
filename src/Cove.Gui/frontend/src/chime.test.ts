import { describe, expect, it } from "vitest";
import { chimePlan, detectChimes, type ChimeKind } from "./chime";

describe("chimePlan", () => {
  it("gives done a rising two-note plan", () => {
    const plan = chimePlan("done");
    expect(plan.length).toBe(2);
    expect(plan[1].freq).toBeGreaterThan(plan[0].freq);
  });

  it("gives needs-input a repeated attention note", () => {
    const plan = chimePlan("needs-input");
    expect(plan.length).toBe(2);
    expect(plan[0].freq).toBe(plan[1].freq);
    expect(plan[1].start).toBeGreaterThan(plan[0].start);
  });

  it("keeps every note gentle", () => {
    for (const kind of ["done", "needs-input"] as ChimeKind[]) {
      for (const n of chimePlan(kind)) {
        expect(n.gain).toBeLessThanOrEqual(0.12);
        expect(n.dur).toBeLessThanOrEqual(0.5);
      }
    }
  });
});

describe("detectChimes", () => {
  it("chimes when an agent transitions into needs-input or done", () => {
    const prev = new Map([["n1", "running"], ["n2", "running"]]);
    const next = new Map([["n1", "needs-input"], ["n2", "done"]]);
    expect(detectChimes(prev, next).sort()).toEqual(["done", "needs-input"]);
  });

  it("stays silent for unchanged states and non-alert transitions", () => {
    const prev = new Map([["n1", "needs-input"], ["n2", "idle"]]);
    const next = new Map([["n1", "needs-input"], ["n2", "running"]]);
    expect(detectChimes(prev, next)).toEqual([]);
  });

  it("stays silent on first load when nothing was tracked", () => {
    const next = new Map([["n1", "needs-input"]]);
    expect(detectChimes(new Map(), next)).toEqual([]);
  });

  it("chimes for a new agent appearing already needing input after first load", () => {
    const prev = new Map([["n1", "running"]]);
    const next = new Map([["n1", "running"], ["n2", "needs-input"]]);
    expect(detectChimes(prev, next)).toEqual(["needs-input"]);
  });
});
