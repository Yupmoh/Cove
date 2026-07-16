import { describe, it, expect } from "vitest";
import { BrowserNavState, normalizeUrl, nativeWebviewBounds, themeBackgroundColor } from "./browser-nook";

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

  it("restores the retained page and history index", () => {
    const s = new BrowserNavState("about:blank");
    s.restore(["https://a.com", "https://b.com", "https://c.com"], 1);
    expect(s.currentUrl).toBe("https://b.com");
    expect(s.canGoBack).toBe(true);
    expect(s.canGoForward).toBe(true);
    s.forward();
    expect(s.currentUrl).toBe("https://c.com");
  });

  it("persists page-driven navigation but ignores expected and duplicate events", () => {
    const s = new BrowserNavState("https://a.com");
    expect(s.webviewNavigated("https://a.com", "https://a.com")).toBe(false);
    expect(s.webviewNavigated("https://b.com", null)).toBe(true);
    expect(s.webviewNavigated("https://b.com", null)).toBe(false);
  });
});

describe("nativeWebviewBounds", () => {
  it("passes top-left css coordinates through per the ryn 0.23 contract", () => {
    const b = nativeWebviewBounds({ x: 298, y: 90, width: 944, height: 790 });
    expect(b).toEqual({ x: 298, y: 90, width: 944, height: 790 });
  });

  it("rounds fractional values and clamps to minimum size", () => {
    const b = nativeWebviewBounds({ x: 10.4, y: 20.6, width: 0.2, height: 0.4 });
    expect(b).toEqual({ x: 10, y: 21, width: 1, height: 1 });
  });

  it("clamps negative y to zero", () => {
    const b = nativeWebviewBounds({ x: 0, y: -5, width: 200, height: 600 });
    expect(b.y).toBe(0);
  });

  it("passes css rects through unscaled per the ryn 0.26 page-zoom contract", () => {
    const b = nativeWebviewBounds({ x: 100, y: 50, width: 60, height: 40 });
    expect(b).toEqual({ x: 100, y: 50, width: 60, height: 40 });
  });
});

describe("themeBackgroundColor", () => {
  it("accepts hex and rgb forms", () => {
    expect(themeBackgroundColor("#1e1e2e")).toBe("#1e1e2e");
    expect(themeBackgroundColor("  #abc  ")).toBe("#abc");
    expect(themeBackgroundColor("#11223344")).toBe("#11223344");
    expect(themeBackgroundColor("rgba(30, 30, 46, 0.5)")).toBe("rgba(30, 30, 46, 0.5)");
    expect(themeBackgroundColor("rgb(30, 30, 46)")).toBe("rgb(30, 30, 46)");
  });

  it("rejects anything ryn would refuse", () => {
    expect(themeBackgroundColor("")).toBeNull();
    expect(themeBackgroundColor("var(--bg)")).toBeNull();
    expect(themeBackgroundColor("#12345")).toBeNull();
    expect(themeBackgroundColor("navy")).toBeNull();
  });
});
