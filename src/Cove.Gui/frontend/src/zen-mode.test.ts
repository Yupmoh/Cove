import { describe, it, expect } from "vitest";
import { initialZenState, enterZen, exitZen, toggleZen, type ChromeVisibility } from "./zen-mode";

const shown: ChromeVisibility = { leftSidebarHidden: false, rightSidebarHidden: false };
const hidden: ChromeVisibility = { leftSidebarHidden: true, rightSidebarHidden: true };

describe("initialZenState", () => {
  it("starts inactive with no saved snapshot", () => {
    expect(initialZenState()).toEqual({ active: false, saved: null });
  });
});

describe("enterZen", () => {
  it("saves the current visibility and hides all chrome", () => {
    const r = enterZen(initialZenState(), shown);
    expect(r.state.active).toBe(true);
    expect(r.state.saved).toEqual(shown);
    expect(r.visibility).toEqual(hidden);
  });
  it("preserves partially-hidden chrome in the snapshot", () => {
    const partial: ChromeVisibility = { leftSidebarHidden: true, rightSidebarHidden: false };
    const r = enterZen(initialZenState(), partial);
    expect(r.state.saved).toEqual(partial);
  });
  it("is idempotent when already active", () => {
    const first = enterZen(initialZenState(), shown);
    const second = enterZen(first.state, hidden);
    expect(second.state.saved).toEqual(shown);
  });
});

describe("exitZen", () => {
  it("restores exactly the pre-zen visibility", () => {
    const partial: ChromeVisibility = { leftSidebarHidden: true, rightSidebarHidden: false };
    const entered = enterZen(initialZenState(), partial);
    const exited = exitZen(entered.state);
    expect(exited.state).toEqual({ active: false, saved: null });
    expect(exited.visibility).toEqual(partial);
  });
  it("falls back to all-shown when there is no saved snapshot", () => {
    const exited = exitZen({ active: true, saved: null });
    expect(exited.visibility).toEqual(shown);
  });
});

describe("toggleZen", () => {
  it("enters from inactive then restores on the second toggle", () => {
    const partial: ChromeVisibility = { leftSidebarHidden: false, rightSidebarHidden: true };
    const on = toggleZen(initialZenState(), partial);
    expect(on.state.active).toBe(true);
    expect(on.visibility).toEqual(hidden);
    const off = toggleZen(on.state, on.visibility);
    expect(off.state.active).toBe(false);
    expect(off.visibility).toEqual(partial);
  });
});
