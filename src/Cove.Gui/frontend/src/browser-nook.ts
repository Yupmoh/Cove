import { invoke } from "./invoke";
import { FindBarState, type FindResult } from "./browser-find";
import { NookCrashState, crashReasonText } from "./browser-crash";
import { PermissionPromptQueue, formatPermissionKinds, permissionOrigin, type PermissionRequest } from "./browser-permissions";
import { DownloadShelfState, downloadPercent, formatBytes, joinPath, type DownloadItem } from "./browser-downloads";

export const browserWebviewRegistry = new Map<string, number>();

interface BrowserNookInstance { el: HTMLElement; sync: () => void; }

interface BrowserNookDto {
  currentUrl: string;
  history: string[];
  historyIndex: number;
}
const browserNookInstances = new Map<string, BrowserNookInstance>();

export function reconcileBrowserBounds(): void {
  for (const [nookId, instance] of browserNookInstances) {
    const id = browserWebviewRegistry.get(nookId);
    if (id === undefined) continue;
    if (instance.el.isConnected) instance.sync();
    else void invoke("webviewPane.setBounds", { id, x: -20000, y: 0, width: 2, height: 2 }).catch(() => void 0);
  }
}

export let browserDownloadsDir = "";
export function setBrowserDownloadsDir(dir: string): void { browserDownloadsDir = dir; }

const permissionAutoDenyMs = 30000;

export function normalizeUrl(input: string): string {
  const trimmed = input.trim();
  if (trimmed.length === 0) return "about:blank";
  if (/^[a-z][a-z0-9+.-]*:\/\//i.test(trimmed)) return trimmed;
  if (trimmed.startsWith("about:") || trimmed.startsWith("file:")) return trimmed;
  if (/\s/.test(trimmed) || !trimmed.includes(".")) return "https://duckduckgo.com/?q=" + encodeURIComponent(trimmed);
  return "https://" + trimmed;
}

export class BrowserNavState {
  private backStack: string[] = [];
  private forwardStack: string[] = [];
  private current: string;

  constructor(initialUrl: string) {
    this.current = normalizeUrl(initialUrl);
  }

  restore(history: string[], historyIndex: number): void {
    if (history.length === 0) return;
    const retained = history.map(normalizeUrl);
    const activeIndex = Math.max(0, Math.min(historyIndex, retained.length - 1));
    this.backStack = retained.slice(0, activeIndex);
    this.current = retained[activeIndex];
    this.forwardStack = retained.slice(activeIndex + 1).reverse();
  }

  webviewNavigated(url: string, expectedUrl: string | null): boolean {
    const next = normalizeUrl(url);
    const changed = next !== this.current;
    if (changed) this.navigate(next);
    return changed && (expectedUrl === null || next !== normalizeUrl(expectedUrl));
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

export interface CssRect { x: number; y: number; width: number; height: number; }

export function nativeWebviewBounds(rect: CssRect): { x: number; y: number; width: number; height: number } {
  return {
    x: Math.round(rect.x),
    y: Math.max(0, Math.round(rect.y)),
    width: Math.max(1, Math.round(rect.width)),
    height: Math.max(1, Math.round(rect.height)),
  };
}

export function themeBackgroundColor(cssValue: string): string | null {
  const v = cssValue.trim();
  if (/^#([0-9a-f]{3,4}|[0-9a-f]{6}|[0-9a-f]{8})$/i.test(v)) return v;
  if (/^rgba?\(/i.test(v)) return v;
  return null;
}

async function whenLaidOut(el: HTMLElement): Promise<void> {
  for (let i = 0; i < 120; i++) {
    if (el.isConnected) {
      const r = el.getBoundingClientRect();
      if (r.width >= 1 && r.height >= 1) return;
    }
    await new Promise<void>((resolve) => requestAnimationFrame(() => resolve()));
  }
  console.warn("browser nook content area never got a size, opening webview anyway");
}

export function extractWebviewId(openResult: unknown): number | null {
  if (typeof openResult === "number" && Number.isInteger(openResult)) return openResult;
  if (openResult && typeof openResult === "object") {
    const id = (openResult as { id?: unknown }).id;
    if (typeof id === "number" && Number.isInteger(id)) return id;
  }
  return null;
}

export async function closeBrowserWebview(nookId: string): Promise<void> {
  const id = browserWebviewRegistry.get(nookId);
  browserNookInstances.delete(nookId);
  if (id === undefined) return;
  browserWebviewRegistry.delete(nookId);
  await invoke("webviewPane.close", { id }).catch((err) => console.warn("webviewPane.close failed", nookId, err));
}

export async function renderBrowserNook(nookId: string, initialUrl: string, userAgent?: string): Promise<HTMLElement> {
  const cached = browserNookInstances.get(nookId);
  if (cached) {
    requestAnimationFrame(() => requestAnimationFrame(() => reconcileBrowserBounds()));
    return cached.el;
  }
  const el = document.createElement("div");
  el.className = "browser-nook";
  el.tabIndex = 0;
  el.style.cssText = "display:flex;flex-direction:column;height:100%;background:#0d1117;color:#e6edf3;position:relative;overflow:hidden;outline:none;";

  const chrome = document.createElement("div");
  chrome.style.cssText = "display:flex;align-items:center;gap:4px;padding:6px 8px;border-bottom:1px solid #21262d;background:#161b22;flex-shrink:0;";
  el.appendChild(chrome);

  const backBtn = navButton("←", "Back");
  const fwdBtn = navButton("→", "Forward");
  const reloadBtn = navButton("↻", "Reload");
  backBtn.style.opacity = "0.4";
  fwdBtn.style.opacity = "0.4";
  chrome.appendChild(backBtn);
  chrome.appendChild(fwdBtn);
  chrome.appendChild(reloadBtn);

  const urlBar = document.createElement("input");
  urlBar.type = "text";
  urlBar.placeholder = "Enter URL or search…";
  urlBar.value = initialUrl;
  urlBar.style.cssText = "flex:1;min-width:0;padding:4px 10px;border:1px solid #30363d;border-radius:6px;background:#0d1117;color:#e6edf3;font-size:13px;outline:none;";
  chrome.appendChild(urlBar);

  const securityGlyph = document.createElement("span");
  securityGlyph.style.cssText = "font-size:12px;color:#3fb950;padding:0 4px;flex-shrink:0;";
  securityGlyph.textContent = "";
  chrome.appendChild(securityGlyph);

  const findBtn = navButton("⌕", "Find in page");
  chrome.appendChild(findBtn);

  const zoomOutBtn = navButton("−", "Zoom out");
  const zoomResetBtn = navButton("100%", "Reset zoom");
  const zoomInBtn = navButton("+", "Zoom in");
  chrome.appendChild(zoomOutBtn);
  chrome.appendChild(zoomResetBtn);
  chrome.appendChild(zoomInBtn);

  const devToolsBtn = navButton("Dev", "DevTools");
  chrome.appendChild(devToolsBtn);

  const findState = new FindBarState();
  const findBar = document.createElement("div");
  findBar.style.cssText = "display:none;align-items:center;gap:6px;padding:5px 8px;border-bottom:1px solid #21262d;background:#12161c;flex-shrink:0;";
  const findInput = document.createElement("input");
  findInput.type = "text";
  findInput.placeholder = "Find in page…";
  findInput.style.cssText = "flex:1;min-width:0;padding:3px 8px;border:1px solid #30363d;border-radius:6px;background:#0d1117;color:#e6edf3;font-size:12px;outline:none;";
  const findCounter = document.createElement("span");
  findCounter.style.cssText = "font-size:11px;color:#8b949e;min-width:44px;text-align:center;flex-shrink:0;";
  findCounter.textContent = "0/0";
  const findPrevBtn = navButton("↑", "Previous match");
  const findNextBtn = navButton("↓", "Next match");
  const findCaseBtn = navButton("Aa", "Match case");
  const findCloseBtn = navButton("✕", "Close");
  findBar.appendChild(findInput);
  findBar.appendChild(findCounter);
  findBar.appendChild(findCaseBtn);
  findBar.appendChild(findPrevBtn);
  findBar.appendChild(findNextBtn);
  findBar.appendChild(findCloseBtn);
  el.appendChild(findBar);

  const titleBar = document.createElement("div");
  titleBar.style.cssText = "padding:2px 10px;font-size:11px;color:#6e7681;background:#161b22;border-bottom:1px solid #21262d;flex-shrink:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;";
  titleBar.textContent = "Loading…";
  el.appendChild(titleBar);

  const permBar = document.createElement("div");
  permBar.style.cssText = "display:none;align-items:center;gap:10px;padding:8px 12px;background:#1f2733;border-bottom:1px solid #30363d;flex-shrink:0;font-size:12px;";
  el.appendChild(permBar);

  const downloadPromptBar = document.createElement("div");
  downloadPromptBar.style.cssText = "display:none;align-items:center;gap:8px;padding:8px 12px;background:#1f2733;border-bottom:1px solid #30363d;flex-shrink:0;font-size:12px;";
  el.appendChild(downloadPromptBar);

  const contentArea = document.createElement("div");
  contentArea.style.cssText = "flex:1 1 0;min-width:0;min-height:0;position:relative;background:#fff;";
  contentArea.dataset.nookId = nookId;
  el.appendChild(contentArea);

  const crashOverlay = document.createElement("div");
  crashOverlay.style.cssText = "display:none;position:absolute;inset:0;flex-direction:column;align-items:center;justify-content:center;gap:14px;background:#0d1117;color:#e6edf3;text-align:center;padding:24px;z-index:20;";
  contentArea.appendChild(crashOverlay);

  const downloadShelf = document.createElement("div");
  downloadShelf.style.cssText = "display:none;flex-direction:column;gap:2px;max-height:140px;overflow-y:auto;padding:6px 8px;background:#161b22;border-top:1px solid #21262d;flex-shrink:0;";
  el.appendChild(downloadShelf);

  const loadingBar = document.createElement("div");
  loadingBar.style.cssText = "position:absolute;top:0;left:0;right:0;height:2px;background:#34c2b0;transform-origin:left;transform:scaleX(0);transition:transform 0.2s;z-index:10;";
  el.appendChild(loadingBar);

  const nav = new BrowserNavState(initialUrl);
  const crashState = new NookCrashState();
  const permQueue = new PermissionPromptQueue();
  const permTimers = new Map<string, ReturnType<typeof setTimeout>>();
  const downloads = new DownloadShelfState();
  let webviewId: number | null = null;
  let zoomLevel = 1.0;
  let devToolsOpen = false;
  let suspended = false;
  let expectedNavigationUrl: string | null = null;

  const updateChrome = () => {
    urlBar.value = nav.currentUrl;
    backBtn.style.opacity = nav.canGoBack ? "1" : "0.4";
    fwdBtn.style.opacity = nav.canGoForward ? "1" : "0.4";
    if (nav.currentUrl.startsWith("https://")) securityGlyph.textContent = "🔒";
    else if (nav.currentUrl.startsWith("http://")) securityGlyph.textContent = "⚠";
    else securityGlyph.textContent = "";
    zoomResetBtn.textContent = Math.round(zoomLevel * 100) + "%";
  };

  const setLoading = (loading: boolean) => {
    loadingBar.style.transform = loading ? "scaleX(0.7)" : "scaleX(0)";
  };

  const syncBounds = () => {
    if (webviewId === null || crashState.isCrashed) return;
    const rect = contentArea.getBoundingClientRect();
    if (rect.width < 1 || rect.height < 1) return;
    const bounds = nativeWebviewBounds(rect);
    void invoke("webviewPane.setBounds", { id: webviewId, x: bounds.x, y: bounds.y, width: bounds.width, height: bounds.height }).catch(() => void 0);
  };

  const openWebView = async (url: string) => {
    const storagePath = `/tmp/cove-webview-${nookId}`;
    await whenLaidOut(contentArea);
    const bounds = nativeWebviewBounds(contentArea.getBoundingClientRect());
    const openArgs: Record<string, unknown> = {
      url,
      x: bounds.x, y: bounds.y,
      width: bounds.width, height: bounds.height,
      storagePath, devTools: false, zoom: zoomLevel,
    };
    const nookBackground = themeBackgroundColor(getComputedStyle(document.body).getPropertyValue("--bg"));
    if (nookBackground) openArgs.background = nookBackground;
    if (userAgent && userAgent.length > 0) openArgs.userAgent = userAgent;
    const result = await invoke<unknown>("webviewPane.open", { options: openArgs });
    const id = extractWebviewId(result);
    if (id === null) {
      console.error("webviewPane.open returned no usable id, nook stays blank", nookId, result);
      return;
    }
    webviewId = id;
    browserWebviewRegistry.set(nookId, id);
    syncBounds();
    const ro = new ResizeObserver(() => syncBounds());
    ro.observe(contentArea);
    window.addEventListener("resize", syncBounds);
  };

  const doNavigate = async (url: string) => {
    nav.navigate(url);
    expectedNavigationUrl = nav.currentUrl;
    updateChrome();
    if (webviewId !== null) {
      setLoading(true);
      await invoke("webviewPane.navigate", { id: webviewId, url: nav.currentUrl }).catch((err) => { console.warn("webview navigate failed", nav.currentUrl, err); setLoading(false); });
    } else {
      setLoading(true);
      await openWebView(nav.currentUrl).catch((err) => { console.warn("webview open failed", nav.currentUrl, err); setLoading(false); });
    }
    void invoke("cove://commands/browser.navigate", { nookId, url: nav.currentUrl }).catch((err) => console.warn("engine browser.navigate failed", err));
  };

  const doBack = async () => {
    if (!nav.canGoBack) return;
    nav.back();
    expectedNavigationUrl = nav.currentUrl;
    updateChrome();
    if (webviewId !== null) await invoke("webviewPane.navigate", { id: webviewId, url: nav.currentUrl }).catch(() => void 0);
    void invoke("cove://commands/browser.back", { nookId }).catch(() => void 0);
  };

  const doForward = async () => {
    if (!nav.canGoForward) return;
    nav.forward();
    expectedNavigationUrl = nav.currentUrl;
    updateChrome();
    if (webviewId !== null) await invoke("webviewPane.navigate", { id: webviewId, url: nav.currentUrl }).catch(() => void 0);
    void invoke("cove://commands/browser.forward", { nookId }).catch(() => void 0);
  };

  const doReload = async () => {
    if (webviewId !== null) await invoke("webviewPane.reload", { id: webviewId }).catch(() => void 0);
    void invoke("cove://commands/browser.reload", { nookId }).catch(() => void 0);
  };

  const setZoom = async (level: number) => {
    zoomLevel = Math.max(0.25, Math.min(5.0, level));
    updateChrome();
    if (webviewId !== null) await invoke("webviewPane.setZoom", { id: webviewId, factor: zoomLevel }).catch((err) => console.warn("webview setZoom failed", err));
  };

  const toggleDevTools = async () => {
    devToolsOpen = !devToolsOpen;
    if (webviewId !== null) await invoke("webviewPane.setDevTools", { id: webviewId, enabled: devToolsOpen }).catch((err) => console.warn("webview setDevTools failed", err));
    devToolsBtn.style.background = devToolsOpen ? "#34c2b0" : "";
  };

  const renderFind = () => {
    findBar.style.display = findState.open ? "flex" : "none";
    findCounter.textContent = findState.counter;
    findCaseBtn.style.background = findState.matchCase ? "#34c2b0" : "";
  };

  const runFind = async (forward: boolean) => {
    if (webviewId === null) return;
    if (!findState.canSearch) {
      await invoke("webviewPane.findStop", { id: webviewId, clearHighlights: true }).catch(() => void 0);
      findState.applyResult({ matches: 0, activeIndex: 0 });
      renderFind();
      return;
    }
    const result = await invoke<FindResult>("webviewPane.find", { id: webviewId, text: findState.query, forward, matchCase: findState.matchCase }).catch(() => null);
    if (result) findState.applyResult(result);
    renderFind();
  };

  const runFindNext = async (forward: boolean) => {
    if (webviewId === null || !findState.canSearch) return;
    const result = await invoke<FindResult>("webviewPane.findNext", { id: webviewId, forward }).catch(() => null);
    if (result) findState.applyResult(result);
    renderFind();
  };

  const openFind = () => {
    findState.openBar();
    renderFind();
    findInput.value = findState.query;
    findInput.focus();
    findInput.select();
  };

  const closeFind = () => {
    findState.closeBar();
    renderFind();
    if (webviewId !== null) void invoke("webviewPane.findStop", { id: webviewId, clearHighlights: true }).catch(() => void 0);
    el.focus();
  };

  const renderPermission = () => {
    const active = permQueue.active;
    if (!active) { permBar.style.display = "none"; permBar.replaceChildren(); return; }
    permBar.style.display = "flex";
    permBar.replaceChildren();
    const label = document.createElement("span");
    label.style.cssText = "flex:1;min-width:0;overflow:hidden;text-overflow:ellipsis;";
    label.textContent = `${permissionOrigin(active.url)} wants: ${formatPermissionKinds(active.kinds)}`;
    const allow = promptButton("Allow", "#238636");
    const block = promptButton("Block", "#30363d");
    allow.addEventListener("click", () => void resolvePermission(active.requestId, true));
    block.addEventListener("click", () => void resolvePermission(active.requestId, false));
    permBar.appendChild(label);
    permBar.appendChild(block);
    permBar.appendChild(allow);
  };

  const clearPermTimer = (requestId: string) => {
    const t = permTimers.get(requestId);
    if (t) { clearTimeout(t); permTimers.delete(requestId); }
  };

  const resolvePermission = async (requestId: string, grant: boolean) => {
    if (!permQueue.has(requestId)) return;
    clearPermTimer(requestId);
    permQueue.remove(requestId);
    renderPermission();
    await invoke("webviewPane.resolvePermission", { requestId, grant }).catch(() => void 0);
  };

  const dismissPermissionOnTimeout = (requestId: string) => {
    if (!permQueue.has(requestId)) return;
    clearPermTimer(requestId);
    permQueue.remove(requestId);
    renderPermission();
  };

  const renderDownloadPrompt = () => {
    const pending = downloads.prompts[0];
    if (!pending) { downloadPromptBar.style.display = "none"; downloadPromptBar.replaceChildren(); return; }
    downloadPromptBar.style.display = "flex";
    downloadPromptBar.replaceChildren();
    const label = document.createElement("span");
    label.textContent = "Download";
    label.style.cssText = "flex-shrink:0;color:#8b949e;";
    const nameInput = document.createElement("input");
    nameInput.type = "text";
    nameInput.value = pending.suggestedName;
    nameInput.style.cssText = "flex:1;min-width:0;padding:3px 8px;border:1px solid #30363d;border-radius:6px;background:#0d1117;color:#e6edf3;font-size:12px;outline:none;";
    const allow = promptButton("Save", "#238636");
    const deny = promptButton("Cancel", "#30363d");
    allow.addEventListener("click", () => void resolveDownload(pending.downloadId, "allow", nameInput.value.trim() || pending.suggestedName));
    deny.addEventListener("click", () => void resolveDownload(pending.downloadId, "deny", ""));
    downloadPromptBar.appendChild(label);
    downloadPromptBar.appendChild(nameInput);
    downloadPromptBar.appendChild(deny);
    downloadPromptBar.appendChild(allow);
  };

  const resolveDownload = async (downloadId: string, action: "allow" | "deny", filename: string) => {
    const args: Record<string, unknown> = { downloadId, action };
    if (action === "allow") {
      const path = joinPath(browserDownloadsDir, filename);
      downloads.allow(downloadId, browserDownloadsDir.length > 0 ? path : filename);
      if (browserDownloadsDir.length > 0) args.path = path;
    } else {
      downloads.deny(downloadId);
    }
    renderDownloadPrompt();
    renderDownloadShelf();
    await invoke("webviewPane.resolveDownload", args).catch(() => void 0);
  };

  const renderDownloadShelf = () => {
    const items = downloads.shelf;
    if (items.length === 0) { downloadShelf.style.display = "none"; downloadShelf.replaceChildren(); return; }
    downloadShelf.style.display = "flex";
    downloadShelf.replaceChildren();
    for (const item of items) downloadShelf.appendChild(downloadRow(item));
  };

  backBtn.addEventListener("click", () => void doBack());
  fwdBtn.addEventListener("click", () => void doForward());
  reloadBtn.addEventListener("click", () => void doReload());
  zoomInBtn.addEventListener("click", () => void setZoom(zoomLevel + 0.1));
  zoomOutBtn.addEventListener("click", () => void setZoom(zoomLevel - 0.1));
  zoomResetBtn.addEventListener("click", () => void setZoom(1.0));
  devToolsBtn.addEventListener("click", () => void toggleDevTools());
  findBtn.addEventListener("click", () => { if (findState.open) closeFind(); else openFind(); });

  findInput.addEventListener("input", () => { findState.setQuery(findInput.value); void runFind(true); });
  findInput.addEventListener("keydown", (e) => {
    if (e.key === "Enter") { e.preventDefault(); void runFindNext(!e.shiftKey); }
    else if (e.key === "Escape") { e.preventDefault(); closeFind(); }
  });
  findPrevBtn.addEventListener("click", () => void runFindNext(false));
  findNextBtn.addEventListener("click", () => void runFindNext(true));
  findCaseBtn.addEventListener("click", () => { findState.toggleMatchCase(); renderFind(); void runFind(true); });
  findCloseBtn.addEventListener("click", () => closeFind());

  el.addEventListener("keydown", (e) => {
    if ((e.metaKey || e.ctrlKey) && (e.key === "f" || e.key === "F")) {
      e.preventDefault();
      openFind();
    } else if (e.key === "Escape" && findState.open) {
      e.preventDefault();
      closeFind();
    }
  });

  urlBar.addEventListener("keydown", (e) => {
    if (e.key === "Enter") {
      e.preventDefault();
      void doNavigate(urlBar.value);
      urlBar.blur();
    }
  });

  const enterCrash = (reason: string | null) => {
    if (!crashState.crash(reason)) return;
    crashOverlay.replaceChildren();
    const title = document.createElement("div");
    title.style.cssText = "font-size:15px;font-weight:600;";
    title.textContent = "This page crashed";
    const detail = document.createElement("div");
    detail.style.cssText = "font-size:12px;color:#8b949e;max-width:360px;";
    detail.textContent = crashReasonText(reason);
    const reloadCrashBtn = promptButton("Reload", "#238636");
    reloadCrashBtn.addEventListener("click", () => void recoverFromCrash());
    crashOverlay.appendChild(title);
    crashOverlay.appendChild(detail);
    crashOverlay.appendChild(reloadCrashBtn);
    crashOverlay.style.display = "flex";
    setLoading(false);
    titleBar.textContent = "Crashed";
  };

  const recoverFromCrash = async () => {
    if (!crashState.recover()) return;
    crashOverlay.style.display = "none";
    titleBar.textContent = "Loading…";
    setLoading(true);
    if (webviewId !== null) await invoke("webviewPane.reloadFromCrash", { id: webviewId }).catch(() => void 0);
    syncBounds();
  };

  const setSuspended = async (value: boolean) => {
    if (suspended === value || webviewId === null) return;
    suspended = value;
    await invoke("webviewPane.setSuspended", { id: webviewId, suspended: value }).catch(() => void 0);
  };

  const visObserver = new IntersectionObserver((entries) => {
    for (const entry of entries) {
      if (entry.target !== el) continue;
      const visible = entry.isIntersecting && entry.intersectionRatio > 0;
      void setSuspended(!visible);
    }
  }, { threshold: 0 });
  visObserver.observe(el);

  window.__ryn.on("webviewPane.navigated", (data: unknown) => {
    const evt = data as { id: number; url: string };
    if (evt.id !== webviewId) return;
    const persistNavigation = nav.webviewNavigated(evt.url, expectedNavigationUrl);
    expectedNavigationUrl = null;
    updateChrome();
    findState.onNavigate();
    renderFind();
    if (persistNavigation) {
      void invoke("cove://commands/browser.navigate", { nookId, url: nav.currentUrl }).catch((err) => console.warn("engine browser.navigate failed", err));
    }
  });

  window.__ryn.on("webviewPane.titleChanged", (data: unknown) => {
    const evt = data as { id: number; title: string };
    if (evt.id !== webviewId) return;
    if (crashState.isCrashed) return;
    titleBar.textContent = evt.title;
  });

  window.__ryn.on("webviewPane.loadStateChanged", (data: unknown) => {
    const evt = data as { id: number; state: string };
    if (evt.id !== webviewId) return;
    setLoading(evt.state === "started");
  });

  window.__ryn.on("webviewPane.faviconChanged", (data: unknown) => {
    const evt = data as { id: number; dataUrl: string };
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
    const evt = data as { id: number };
    if (evt.id !== webviewId) return;
    webviewId = null;
    contentArea.style.background = "#0d1117";
    titleBar.textContent = "Closed";
  });

  window.__ryn.on("webviewPane.processTerminated", (data: unknown) => {
    const evt = data as { id: number; reason?: string };
    if (evt.id !== webviewId) return;
    enterCrash(evt.reason ?? null);
  });

  window.__ryn.on("webviewPane.permissionRequested", (data: unknown) => {
    const evt = data as { id: number; requestId: string; kinds: string[]; url: string };
    if (evt.id !== webviewId) return;
    const req: PermissionRequest = { requestId: evt.requestId, kinds: evt.kinds ?? [], url: evt.url ?? nav.currentUrl };
    permQueue.add(req);
    permTimers.set(req.requestId, setTimeout(() => dismissPermissionOnTimeout(req.requestId), permissionAutoDenyMs));
    renderPermission();
  });

  window.__ryn.on("webviewPane.downloadRequested", (data: unknown) => {
    const evt = data as { id: number; downloadId: string; url: string; suggestedName: string };
    if (evt.id !== webviewId) return;
    downloads.requested(evt.downloadId, evt.url, evt.suggestedName || "download");
    renderDownloadPrompt();
  });

  window.__ryn.on("webviewPane.downloadProgress", (data: unknown) => {
    const evt = data as { id: number; downloadId: string; receivedBytes: number; totalBytes: number };
    if (evt.id !== webviewId) return;
    downloads.progress(evt.downloadId, evt.receivedBytes ?? 0, evt.totalBytes ?? 0);
    renderDownloadShelf();
  });

  window.__ryn.on("webviewPane.downloadCompleted", (data: unknown) => {
    const evt = data as { id: number; downloadId: string; path?: string };
    if (evt.id !== webviewId) return;
    downloads.completed(evt.downloadId, evt.path);
    renderDownloadShelf();
  });

  window.__ryn.on("webviewPane.downloadFailed", (data: unknown) => {
    const evt = data as { id: number; downloadId: string; reason?: string };
    if (evt.id !== webviewId) return;
    downloads.failed(evt.downloadId, evt.reason ?? "download failed");
    renderDownloadShelf();
  });

  void invoke<BrowserNookDto>("cove://commands/browser.open", { nookId, url: nav.currentUrl })
    .catch(() => null)
    .then((retained) => {
      if (retained) nav.restore(retained.history, retained.historyIndex);
      updateChrome();
      expectedNavigationUrl = nav.currentUrl;
      return openWebView(nav.currentUrl);
    })
    .then(() => setLoading(true))
    .catch((err) => console.warn("webview open failed", nav.currentUrl, err));

  updateChrome();
  renderFind();

  browserNookInstances.set(nookId, { el, sync: syncBounds });
  return el;
}

function downloadRow(item: DownloadItem): HTMLElement {
  const row = document.createElement("div");
  row.style.cssText = "display:flex;align-items:center;gap:8px;padding:4px 6px;border-radius:6px;background:#0d1117;font-size:12px;";
  const name = document.createElement("span");
  name.style.cssText = "flex:1;min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;";
  name.textContent = item.suggestedName;
  const status = document.createElement("span");
  status.style.cssText = "flex-shrink:0;color:#8b949e;";
  const pct = downloadPercent(item);
  if (item.state === "completed") status.textContent = "Done";
  else if (item.state === "failed") { status.textContent = item.error ?? "Failed"; status.style.color = "#f85149"; }
  else if (pct !== null) status.textContent = `${pct}%`;
  else status.textContent = item.receivedBytes > 0 ? formatBytes(item.receivedBytes) : "Downloading…";
  row.appendChild(name);
  row.appendChild(status);
  return row;
}

function promptButton(label: string, background: string): HTMLButtonElement {
  const btn = document.createElement("button");
  btn.textContent = label;
  btn.style.cssText = `height:26px;padding:0 12px;border:1px solid #30363d;border-radius:6px;background:${background};color:#e6edf3;font-size:12px;cursor:pointer;flex-shrink:0;`;
  return btn;
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
