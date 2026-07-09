import { invoke } from "./invoke";

export function normalizeUrl(input: string): string {
  if (input.length === 0) return "about:blank";
  if (/^[a-z][a-z0-9+.-]*:\/\//i.test(input)) return input;
  if (input.startsWith("about:") || input.startsWith("file:")) return input;
  return "https://" + input;
}

export class BrowserNavState {
  private backStack: string[] = [];
  private forwardStack: string[] = [];
  private current: string;

  constructor(initialUrl: string) {
    this.current = normalizeUrl(initialUrl);
  }

  get currentUrl(): string { return this.current; }
  get canGoBack(): boolean { return this.backStack.length > 0; }
  get canGoForward(): boolean { return this.forwardStack.length > 0; }

  navigate(url: string): void {
    const next = normalizeUrl(url);
    if (next === this.current) return;
    this.backStack.push(this.current);
    this.current = next;
    this.forwardStack = [];
  }

  back(): void {
    if (this.backStack.length === 0) return;
    this.forwardStack.push(this.current);
    this.current = this.backStack.pop()!;
  }

  forward(): void {
    if (this.forwardStack.length === 0) return;
    this.backStack.push(this.current);
    this.current = this.forwardStack.pop()!;
  }

  reloadUrl(): string { return this.current; }
}

interface WebViewPaneOpenResult { id: string }

export async function renderBrowserPane(paneId: string, initialUrl: string): Promise<HTMLElement> {
  const el = document.createElement("div");
  el.className = "browser-pane";
  el.style.cssText = "display:flex;flex-direction:column;height:100%;background:#0d1117;color:#e6edf3;position:relative;overflow:hidden;";

  const chrome = document.createElement("div");
  chrome.style.cssText = "display:flex;align-items:center;gap:4px;padding:6px 8px;border-bottom:1px solid #21262d;background:#161b22;flex-shrink:0;";
  el.appendChild(chrome);

  const backBtn = navButton("\u2190", "Back");
  const fwdBtn = navButton("\u2192", "Forward");
  const reloadBtn = navButton("\u21bb", "Reload");
  backBtn.style.opacity = "0.4";
  fwdBtn.style.opacity = "0.4";
  chrome.appendChild(backBtn);
  chrome.appendChild(fwdBtn);
  chrome.appendChild(reloadBtn);

  const urlBar = document.createElement("input");
  urlBar.type = "text";
  urlBar.placeholder = "Enter URL or search\u2026";
  urlBar.value = initialUrl;
  urlBar.style.cssText = "flex:1;min-width:0;padding:4px 10px;border:1px solid #30363d;border-radius:6px;background:#0d1117;color:#e6edf3;font-size:13px;outline:none;";
  chrome.appendChild(urlBar);

  const securityGlyph = document.createElement("span");
  securityGlyph.style.cssText = "font-size:12px;color:#3fb950;padding:0 4px;flex-shrink:0;";
  securityGlyph.textContent = "";
  chrome.appendChild(securityGlyph);

  const zoomOutBtn = navButton("\u2212", "Zoom out");
  const zoomResetBtn = navButton("100%", "Reset zoom");
  const zoomInBtn = navButton("+", "Zoom in");
  chrome.appendChild(zoomOutBtn);
  chrome.appendChild(zoomResetBtn);
  chrome.appendChild(zoomInBtn);

  const devToolsBtn = navButton("Dev", "DevTools");
  chrome.appendChild(devToolsBtn);

  const titleBar = document.createElement("div");
  titleBar.style.cssText = "padding:2px 10px;font-size:11px;color:#6e7681;background:#161b22;border-bottom:1px solid #21262d;flex-shrink:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;";
  titleBar.textContent = "Loading\u2026";
  el.appendChild(titleBar);

  const contentArea = document.createElement("div");
  contentArea.style.cssText = "flex:1 1 0;min-height:0;position:relative;background:#fff;";
  contentArea.dataset.paneId = paneId;
  el.appendChild(contentArea);

  const loadingBar = document.createElement("div");
  loadingBar.style.cssText = "position:absolute;top:0;left:0;right:0;height:2px;background:#34c2b0;transform-origin:left;transform:scaleX(0);transition:transform 0.2s;z-index:10;";
  el.appendChild(loadingBar);

  const nav = new BrowserNavState(initialUrl);
  let webviewId: string | null = null;
  let zoomLevel = 1.0;
  let devToolsOpen = false;

  const updateChrome = () => {
    urlBar.value = nav.currentUrl;
    backBtn.style.opacity = nav.canGoBack ? "1" : "0.4";
    fwdBtn.style.opacity = nav.canGoForward ? "1" : "0.4";
    if (nav.currentUrl.startsWith("https://")) securityGlyph.textContent = "\uD83D\uDD12";
    else if (nav.currentUrl.startsWith("http://")) securityGlyph.textContent = "\u26A0";
    else securityGlyph.textContent = "";
    zoomResetBtn.textContent = Math.round(zoomLevel * 100) + "%";
  };

  const setLoading = (loading: boolean) => {
    loadingBar.style.transform = loading ? "scaleX(0.7)" : "scaleX(0)";
  };

  const syncBounds = () => {
    if (!webviewId) return;
    const rect = contentArea.getBoundingClientRect();
    if (rect.width < 1 || rect.height < 1) return;
    void invoke("webviewPane.setBounds", { id: webviewId, x: Math.round(rect.x), y: Math.round(rect.y), width: Math.round(rect.width), height: Math.round(rect.height) }).catch(() => void 0);
  };

  const openWebView = async (url: string) => {
    const storagePath = `/tmp/cove-webview-${paneId}`;
    const result = await invoke<WebViewPaneOpenResult>("webviewPane.open", { url, x: 0, y: 0, width: 800, height: 600, storagePath, devTools: false, zoom: zoomLevel });
    webviewId = result.id;
    syncBounds();
    const ro = new ResizeObserver(() => syncBounds());
    ro.observe(contentArea);
  };

  const doNavigate = async (url: string) => {
    nav.navigate(url);
    updateChrome();
    if (webviewId) {
      setLoading(true);
      await invoke("webviewPane.navigate", { id: webviewId, url: nav.currentUrl }).catch(() => void 0);
    } else {
      setLoading(true);
      await openWebView(nav.currentUrl);
    }
    void invoke("cove://commands/browser.navigate", { paneId, url: nav.currentUrl }).catch(() => void 0);
  };

  const doBack = async () => {
    if (!nav.canGoBack) return;
    nav.back();
    updateChrome();
    if (webviewId) await invoke("webviewPane.navigate", { id: webviewId, url: nav.currentUrl }).catch(() => void 0);
    void invoke("cove://commands/browser.back", { paneId }).catch(() => void 0);
  };

  const doForward = async () => {
    if (!nav.canGoForward) return;
    nav.forward();
    updateChrome();
    if (webviewId) await invoke("webviewPane.navigate", { id: webviewId, url: nav.currentUrl }).catch(() => void 0);
    void invoke("cove://commands/browser.forward", { paneId }).catch(() => void 0);
  };

  const doReload = async () => {
    if (webviewId) await invoke("webviewPane.reload", { id: webviewId }).catch(() => void 0);
    void invoke("cove://commands/browser.reload", { paneId }).catch(() => void 0);
  };

  const setZoom = async (level: number) => {
    zoomLevel = Math.max(0.25, Math.min(5.0, level));
    updateChrome();
    if (webviewId) await invoke("webviewPane.setZoom", { id: webviewId, zoom: zoomLevel }).catch(() => void 0);
  };

  const toggleDevTools = async () => {
    devToolsOpen = !devToolsOpen;
    if (webviewId) await invoke("webviewPane.setDevTools", { id: webviewId, devTools: devToolsOpen }).catch(() => void 0);
    devToolsBtn.style.background = devToolsOpen ? "#34c2b0" : "";
  };

  backBtn.addEventListener("click", () => void doBack());
  fwdBtn.addEventListener("click", () => void doForward());
  reloadBtn.addEventListener("click", () => void doReload());
  zoomInBtn.addEventListener("click", () => void setZoom(zoomLevel + 0.1));
  zoomOutBtn.addEventListener("click", () => void setZoom(zoomLevel - 0.1));
  zoomResetBtn.addEventListener("click", () => void setZoom(1.0));
  devToolsBtn.addEventListener("click", () => void toggleDevTools());

  urlBar.addEventListener("keydown", (e) => {
    if (e.key === "Enter") {
      e.preventDefault();
      void doNavigate(urlBar.value);
      urlBar.blur();
    }
  });

  window.__ryn.on("webviewPane.navigated", (data: unknown) => {
    const evt = data as { id: string; url: string };
    if (evt.id !== webviewId) return;
    nav.navigate(evt.url);
    updateChrome();
  });

  window.__ryn.on("webviewPane.titleChanged", (data: unknown) => {
    const evt = data as { id: string; title: string };
    if (evt.id !== webviewId) return;
    titleBar.textContent = evt.title;
  });

  window.__ryn.on("webviewPane.loadStateChanged", (data: unknown) => {
    const evt = data as { id: string; state: string };
    if (evt.id !== webviewId) return;
    setLoading(evt.state === "started");
  });

  window.__ryn.on("webviewPane.faviconChanged", (data: unknown) => {
    const evt = data as { id: string; dataUrl: string };
    if (evt.id !== webviewId) return;
    if (evt.dataUrl) {
      titleBar.style.backgroundImage = `url(${evt.dataUrl})`;
      titleBar.style.backgroundRepeat = "no-repeat";
      titleBar.style.backgroundPosition = "left center";
      titleBar.style.backgroundSize = "12px";
      titleBar.style.paddingLeft = "26px";
    }
  });

  window.__ryn.on("webviewPane.closed", (data: unknown) => {
    const evt = data as { id: string };
    if (evt.id !== webviewId) return;
    webviewId = null;
    contentArea.style.background = "#0d1117";
    titleBar.textContent = "Closed";
  });

  void openWebView(nav.currentUrl).then(() => {
    setLoading(true);
    void invoke("cove://commands/browser.open", { paneId, url: nav.currentUrl }).catch(() => void 0);
  });

  updateChrome();

  return el;
}

function navButton(label: string, title: string): HTMLButtonElement {
  const btn = document.createElement("button");
  btn.textContent = label;
  btn.title = title;
  btn.style.cssText = "min-width:28px;height:28px;border:1px solid #30363d;border-radius:6px;background:transparent;color:#e6edf3;font-size:13px;cursor:pointer;display:flex;align-items:center;justify-content:center;padding:0 6px;flex-shrink:0;";
  btn.addEventListener("mouseenter", () => { btn.style.background = "#21262d"; });
  btn.addEventListener("mouseleave", () => { btn.style.background = "transparent"; });
  return btn;
}
