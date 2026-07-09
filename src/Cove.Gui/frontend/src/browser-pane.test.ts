import { describe, it, expect } from "vitest";
import { BrowserNavState, normalizeUrl } from "./browser-pane";

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
