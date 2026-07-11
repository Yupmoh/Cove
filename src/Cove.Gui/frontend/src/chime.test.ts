import { describe, expect, it } from "vitest";
import { chimePlan, detectChimes, chimesEnabledFrom, chimePrefValue, AGENT_CHIMES_STORAGE_KEY, type ChimeKind } from "./chime";
import { mapAgentState } from "./agents-model";

describe("agent chime preference", () => {
  it("defaults to enabled when nothing is stored", () => {
    expect(chimesEnabledFrom(null)).toBe(true);
  });

  it("stays enabled for any value other than the off sentinel", () => {
    expect(chimesEnabledFrom("true")).toBe(true);
    expect(chimesEnabledFrom("")).toBe(true);
  });

  it("is disabled only for the explicit off sentinel", () => {
    expect(chimesEnabledFrom("false")).toBe(false);
  });

  it("round-trips the stored value through the enabled flag", () => {
    expect(chimesEnabledFrom(chimePrefValue(true))).toBe(true);
    expect(chimesEnabledFrom(chimePrefValue(false))).toBe(false);
  });

  it("keys off the shared localStorage slot", () => {
    expect(AGENT_CHIMES_STORAGE_KEY).toBe("cove.sound.agentChimes");
  });
});

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

  it("fires when RAW engine wire statuses are mapped before diffing", () => {
    const rawPrev = [{ nookId: "n1", status: "Working" }, { nookId: "n2", status: "Working" }];
    const rawNext = [{ nookId: "n1", status: "WaitingForInput" }, { nookId: "n2", status: "Stopped" }];
    const prev = new Map(rawPrev.map((c) => [c.nookId, mapAgentState(c.status)]));
    const next = new Map(rawNext.map((c) => [c.nookId, mapAgentState(c.status)]));
    expect(detectChimes(prev, next).sort()).toEqual(["done", "needs-input"]);
  });

  it("stays silent when raw statuses are diffed without mapping (the original bug)", () => {
    const rawPrev = new Map([["n1", "Working"]]);
    const rawNext = new Map([["n1", "WaitingForInput"]]);
    expect(detectChimes(rawPrev, rawNext)).toEqual([]);
  });
});
