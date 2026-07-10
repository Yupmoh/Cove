import { describe, it, expect } from "vitest";
import { BrowserNavState, normalizeUrl, nativeWebviewBounds } from "./browser-pane";

describe("normalizeUrl", () => {
  it("prepends https:// for bare domains", () => {
    expect(normalizeUrl("example.com")).toBe("https://example.com");
  });

  it("leaves URLs with scheme unchanged", () => {
    expect(normalizeUrl("https://example.com")).toBe("https://example.com");
    expect(normalizeUrl("http://localhost:3000")).toBe("http://localhost:3000");
  });

  it("leaves file:// URLs unchanged", () => {
    expect(normalizeUrl("file:///tmp/test.html")).toBe("file:///tmp/test.html");
  });

  it("leaves about: URLs unchanged", () => {
    expect(normalizeUrl("about:blank")).toBe("about:blank");
  });

  it("treats empty as about:blank", () => {
    expect(normalizeUrl("")).toBe("about:blank");
  });

  it("turns search-looking input into a search query", () => {
    expect(normalizeUrl("how to exit vim")).toBe("https://duckduckgo.com/?q=how%20to%20exit%20vim");
    expect(normalizeUrl("coveterminal")).toBe("https://duckduckgo.com/?q=coveterminal");
  });

  it("trims whitespace around bare domains", () => {
    expect(normalizeUrl("  google.com  ")).toBe("https://google.com");
  });
});

describe("BrowserNavState", () => {
  it("starts with initial url and empty forward stack", () => {
    const s = new BrowserNavState("https://example.com");
    expect(s.currentUrl).toBe("https://example.com");
    expect(s.canGoBack).toBe(false);
    expect(s.canGoForward).toBe(false);
  });

  it("navigate pushes url and clears forward stack", () => {
    const s = new BrowserNavState("https://a.com");
    s.navigate("https://b.com");
    expect(s.currentUrl).toBe("https://b.com");
    expect(s.canGoBack).toBe(true);
    expect(s.canGoForward).toBe(false);
  });

  it("back restores previous url and enables forward", () => {
    const s = new BrowserNavState("https://a.com");
    s.navigate("https://b.com");
    s.back();
    expect(s.currentUrl).toBe("https://a.com");
    expect(s.canGoBack).toBe(false);
    expect(s.canGoForward).toBe(true);
  });

  it("forward restores next url after back", () => {
    const s = new BrowserNavState("https://a.com");
    s.navigate("https://b.com");
    s.back();
    s.forward();
    expect(s.currentUrl).toBe("https://b.com");
    expect(s.canGoForward).toBe(false);
    expect(s.canGoBack).toBe(true);
  });

  it("back on initial url is no-op", () => {
    const s = new BrowserNavState("https://a.com");
    s.back();
    expect(s.currentUrl).toBe("https://a.com");
  });

  it("forward with no forward stack is no-op", () => {
    const s = new BrowserNavState("https://a.com");
    s.forward();
    expect(s.currentUrl).toBe("https://a.com");
  });

  it("navigate after back clears forward stack", () => {
    const s = new BrowserNavState("https://a.com");
    s.navigate("https://b.com");
    s.back();
    s.navigate("https://c.com");
    expect(s.currentUrl).toBe("https://c.com");
    expect(s.canGoForward).toBe(false);
    expect(s.canGoBack).toBe(true);
  });

  it("reload returns current url", () => {
    const s = new BrowserNavState("https://a.com");
    s.navigate("https://b.com");
    expect(s.reloadUrl()).toBe("https://b.com");
  });
});

describe("nativeWebviewBounds", () => {
  it("flips css top-left coordinates into bottom-left native bounds", () => {
    const b = nativeWebviewBounds({ x: 298, y: 90, width: 944, height: 790 }, 950);
    expect(b).toEqual({ x: 298, y: 70, width: 944, height: 790 });
  });

  it("rounds fractional rects and enforces a minimum size", () => {
    const b = nativeWebviewBounds({ x: 10.4, y: 20.6, width: 0.2, height: 0.4 }, 500);
    expect(b.x).toBe(10);
    expect(b.width).toBe(1);
    expect(b.height).toBe(1);
    expect(b.y).toBe(479);
  });

  it("clamps the native y at zero when the rect overflows the window", () => {
    const b = nativeWebviewBounds({ x: 0, y: 100, width: 200, height: 600 }, 500);
    expect(b.y).toBe(0);
  });
});
