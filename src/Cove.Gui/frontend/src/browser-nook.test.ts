import { describe, it, expect, vi } from "vitest";
import { BrowserNavState, BrowserSession, normalizeUrl, nativeWebviewBounds, themeBackgroundColor } from "./browser-nook";

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

describe("BrowserSession", () => {
  it("serializes overlapping panel open, navigation, reload, and crash recovery", async () => {
    let completeOpen!: (value: unknown) => void;
    const openResult = new Promise<unknown>((resolve) => {
      completeOpen = resolve;
    });
    const invoke = vi.fn((command: string) => command === "webviewPane.open" ? openResult : Promise.resolve({}));
    const session = new BrowserSession(
      "browser-1",
      { isConnected: true } as HTMLElement,
      "https://example.com",
      {
        invoke,
        observe: vi.fn(() => vi.fn()),
        warn: vi.fn(),
      },
    );

    const initialOpen = session.openPanel({ url: "https://example.com" });
    const navigationOpen = session.openPanel({ url: "https://example.com/next" });
    const navigation = session.invokePanel("webviewPane.navigate", { url: "https://example.com/next" });
    const reload = session.invokePanel("webviewPane.reload", {});
    const crashRecovery = session.invokePanel("webviewPane.reloadFromCrash", {});
    await Promise.resolve();

    expect(invoke).toHaveBeenCalledTimes(1);
    expect(invoke).toHaveBeenCalledWith("webviewPane.open", {
      options: { url: "https://example.com" },
    });

    completeOpen({ id: 42 });
    await Promise.all([initialOpen, navigationOpen, navigation, reload, crashRecovery]);

    expect(invoke.mock.calls.filter(([command]) => command === "webviewPane.open")).toHaveLength(1);
    expect(invoke).toHaveBeenCalledWith("webviewPane.navigate", {
      id: 42,
      url: "https://example.com/next",
    });
    expect(invoke).toHaveBeenCalledWith("webviewPane.reload", { id: 42 });
    expect(invoke).toHaveBeenCalledWith("webviewPane.reloadFromCrash", { id: 42 });
    expect(session.webviewId).toBe(42);
  });

  it("closes an in-flight panel open exactly once when disposal wins the race", async () => {
    let completeOpen!: (value: unknown) => void;
    const openResult = new Promise<unknown>((resolve) => {
      completeOpen = resolve;
    });
    const invoke = vi.fn((command: string) => command === "webviewPane.open" ? openResult : Promise.resolve({}));
    const session = new BrowserSession(
      "browser-1",
      { isConnected: true } as HTMLElement,
      "https://example.com",
      {
        invoke,
        observe: vi.fn(() => vi.fn()),
        warn: vi.fn(),
      },
    );

    const opening = session.openPanel({ url: "https://example.com" });
    await Promise.resolve();
    const firstDisposal = session.dispose();
    const secondDisposal = session.dispose();
    completeOpen({ id: 42 });

    await Promise.all([opening, firstDisposal, secondDisposal]);

    expect(invoke.mock.calls.filter(([command]) => command === "webviewPane.open")).toHaveLength(1);
    expect(invoke.mock.calls.filter(([command]) => command === "webviewPane.close")).toEqual([
      ["webviewPane.close", { id: 42 }],
    ]);
    expect(session.webviewId).toBeNull();
  });

  it("owns navigation, prompts, downloads, and permission timers", async () => {
    vi.useFakeTimers();
    const expired = vi.fn();
    const session = new BrowserSession(
      "browser-1",
      { isConnected: true } as HTMLElement,
      "https://example.com",
      {
        invoke: vi.fn(async () => ({})),
        observe: vi.fn(() => vi.fn()),
        warn: vi.fn(),
      },
    );
    session.permissions.add({ requestId: "request-1", kinds: ["camera"], url: "https://example.com" });
    session.downloads.requested("download-1", "https://example.com/file", "file");
    session.schedulePermissionTimeout("request-1", expired, 100);

    await session.dispose();
    vi.advanceTimersByTime(100);

    expect(session.nav.currentUrl).toBe("https://example.com");
    expect(session.permissions.active?.requestId).toBe("request-1");
    expect(session.downloads.prompts[0]?.downloadId).toBe("download-1");
    expect(expired).not.toHaveBeenCalled();
    vi.useRealTimers();
  });

  it("owns the native handle, subscriptions, observers, and disposal", async () => {
    const invoke = vi.fn(async (command: string) => command === "webviewPane.open" ? { id: 42 } : {});
    const unsubscribe = vi.fn();
    const observe = vi.fn(() => unsubscribe);
    const observer = { disconnect: vi.fn() };
    const session = new BrowserSession(
      "browser-1",
      { isConnected: true } as HTMLElement,
      "about:blank",
      {
        invoke,
        observe,
        warn: vi.fn(),
      },
    );
    await session.openPanel({ url: "about:blank" });
    session.ownObserver(observer as unknown as ResizeObserver);
    session.observe("webviewPane.navigated", vi.fn());

    await session.dispose();
    await session.dispose();

    expect(unsubscribe).toHaveBeenCalledOnce();
    expect(observer.disconnect).toHaveBeenCalledOnce();
    expect(invoke).toHaveBeenCalledWith("webviewPane.close", { id: 42 });
    expect(session.webviewId).toBeNull();
  });

  it("distinguishes a live panel returning null from a missing panel", async () => {
    const invoke = vi.fn(async (command: string) => {
      if (command === "webviewPane.open") return { id: 42 };
      return null;
    });
    const session = new BrowserSession(
      "browser-1",
      { isConnected: true } as HTMLElement,
      "about:blank",
      {
        invoke,
        observe: vi.fn(() => vi.fn()),
        warn: vi.fn(),
      },
    );
    await session.openPanel({ url: "about:blank" });

    await expect(session.invokeOwnedPanel("webviewPane.eval", {
      code: "null",
    })).resolves.toEqual({ found: true, value: null });
  });

  it("reconciles visible and detached native bounds through one owner", async () => {
    const invoke = vi.fn(async (command: string) => command === "webviewPane.open" ? { id: 7 } : {});
    const sync = vi.fn();
    const session = new BrowserSession(
      "browser-1",
      { isConnected: false } as HTMLElement,
      "about:blank",
      {
        invoke,
        observe: vi.fn(() => vi.fn()),
        warn: vi.fn(),
      },
    );
    await session.openPanel({ url: "about:blank" });
    session.setBoundsSync(sync);

    session.reconcileBounds();
    await Promise.resolve();

    expect(sync).not.toHaveBeenCalled();
    expect(invoke).toHaveBeenCalledWith("webviewPane.setBounds", {
      id: 7,
      x: -20000,
      y: 0,
      width: 2,
      height: 2,
    });
  });
});
