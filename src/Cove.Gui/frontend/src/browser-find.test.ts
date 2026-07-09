import { describe, expect, it } from "vitest";
import { FindBarState, formatFindCounter } from "./browser-find";

describe("formatFindCounter", () => {
  it("shows 0/0 when there are no matches", () => {
    expect(formatFindCounter(0, 0)).toBe("0/0");
    expect(formatFindCounter(-1, 3)).toBe("0/0");
  });

  it("shows active over total when matches exist", () => {
    expect(formatFindCounter(5, 2)).toBe("2/5");
  });
});

describe("FindBarState", () => {
  it("opens and closes", () => {
    const s = new FindBarState();
    expect(s.open).toBe(false);
    s.openBar();
    expect(s.open).toBe(true);
    s.closeBar();
    expect(s.open).toBe(false);
  });

  it("tracks the query and only searches when non-empty", () => {
    const s = new FindBarState();
    expect(s.canSearch).toBe(false);
    s.setQuery("hello");
    expect(s.canSearch).toBe(true);
    expect(s.query).toBe("hello");
  });

  it("applies engine results into the counter", () => {
    const s = new FindBarState();
    s.setQuery("term");
    s.applyResult({ matches: 4, activeIndex: 2 });
    expect(s.matches).toBe(4);
    expect(s.activeIndex).toBe(2);
    expect(s.counter).toBe("2/4");
  });

  it("normalises a zero-match result to activeIndex 0", () => {
    const s = new FindBarState();
    s.applyResult({ matches: 0, activeIndex: 5 });
    expect(s.activeIndex).toBe(0);
    expect(s.counter).toBe("0/0");
  });

  it("resets results when the query is cleared", () => {
    const s = new FindBarState();
    s.setQuery("x");
    s.applyResult({ matches: 3, activeIndex: 1 });
    s.setQuery("");
    expect(s.matches).toBe(0);
    expect(s.activeIndex).toBe(0);
    expect(s.canSearch).toBe(false);
  });

  it("resets results on navigation but keeps the bar open and query", () => {
    const s = new FindBarState();
    s.openBar();
    s.setQuery("term");
    s.applyResult({ matches: 7, activeIndex: 3 });
    s.onNavigate();
    expect(s.matches).toBe(0);
    expect(s.activeIndex).toBe(0);
    expect(s.open).toBe(true);
    expect(s.query).toBe("term");
  });

  it("clears results when closed", () => {
    const s = new FindBarState();
    s.setQuery("term");
    s.applyResult({ matches: 2, activeIndex: 1 });
    s.closeBar();
    expect(s.matches).toBe(0);
    expect(s.activeIndex).toBe(0);
  });

  it("toggles match case", () => {
    const s = new FindBarState();
    expect(s.matchCase).toBe(false);
    s.toggleMatchCase();
    expect(s.matchCase).toBe(true);
  });
});
