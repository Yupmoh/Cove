import { invoke, onRyn } from "./invoke";
import { FrontendCommand } from "./app/frontend-command";
import { FrontendEvent } from "./app/frontend-event";
import { LifecycleScope } from "./app/lifecycle";
import { FindBarState, type FindResult } from "./browser-find";
import { NookCrashState, crashReasonText } from "./browser-crash";
import { PermissionPromptQueue, formatPermissionKinds, permissionOrigin, type PermissionRequest } from "./browser-permissions";
import { DownloadShelfState, downloadPercent, formatBytes, joinPath, type DownloadItem } from "./browser-downloads";

export interface BrowserSessionDependencies {
  invoke(command: FrontendCommand, args: unknown): Promise<unknown>;
  observe(event: FrontendEvent, callback: (data: unknown) => void): () => void;
  warn(message: string, context: unknown): void;
}

const browserSessionDependencies: BrowserSessionDependencies = {
  invoke,
  observe: onRyn,
  warn: (message, context) => console.warn(message, context),
};

export class BrowserSession {
  private readonly lifecycle = new LifecycleScope();
  private boundsSync: () => void = () => {};
  private disposal: Promise<void> | null = null;
  private nativeOperations: Promise<void> = Promise.resolve();
  private nativePanelId: number | null = null;
  private readonly permissionTimers = new Map<string, ReturnType<typeof globalThis.setTimeout>>();
  readonly nav: BrowserNavState;
  readonly crashState = new NookCrashState();
  readonly permissions = new PermissionPromptQueue();
  readonly downloads = new DownloadShelfState();
  zoomLevel = 1;
  devToolsOpen = false;
  suspended = false;
  expectedNavigationUrl: string | null = null;

  constructor(
    readonly nookId: string,
    readonly element: HTMLElement,
    initialUrl: string,
    private readonly dependencies: BrowserSessionDependencies = browserSessionDependencies,
  ) {
    this.nav = new BrowserNavState(initialUrl);
    this.lifecycle.own(() => {
      for (const timer of this.permissionTimers.values()) globalThis.clearTimeout(timer);
      this.permissionTimers.clear();
    });
  }

  get isClosed(): boolean {
    return this.lifecycle.isDisposed;
  }

  get webviewId(): number | null {
    return this.nativePanelId;
  }

  setBoundsSync(sync: () => void): void {
    this.boundsSync = sync;
  }

  own(disposer: () => void | Promise<void>): void {
    this.lifecycle.own(disposer);
  }

  ownObserver(observer: ResizeObserver | IntersectionObserver): void {
    this.lifecycle.own(() => observer.disconnect());
  }

  observe(event: FrontendEvent, callback: (data: unknown) => void): void {
    const unsubscribe = this.dependencies.observe(event, (data) => {
      if (!this.isClosed) callback(data);
    });
    this.lifecycle.own(unsubscribe);
  }

  listen(target: EventTarget, event: string, callback: EventListenerOrEventListenerObject): void {
    this.lifecycle.listen(target, event, callback);
  }

  schedulePermissionTimeout(requestId: string, callback: () => void, delayMs = permissionAutoDenyMs): void {
    if (this.isClosed) return;
    this.clearPermissionTimeout(requestId);
    const timer = globalThis.setTimeout(() => {
      this.permissionTimers.delete(requestId);
      if (this.isClosed) return;
      callback();
    }, delayMs);
    this.permissionTimers.set(requestId, timer);
  }

  clearPermissionTimeout(requestId: string): void {
    const timer = this.permissionTimers.get(requestId);
    if (timer === undefined) return;
    globalThis.clearTimeout(timer);
    this.permissionTimers.delete(requestId);
  }

  openPanel(options: Record<string, unknown>): Promise<{ id: number; created: boolean } | null> {
    return this.serializeNativeOperation(async () => {
      if (this.isClosed) return null;
      if (this.webviewId !== null) return { id: this.webviewId, created: false };
      const result = await this.dependencies.invoke(FrontendCommand.WebviewPaneOpen, { options });
      const id = extractWebviewId(result);
      if (id === null) {
        this.dependencies.warn("webviewPane.open returned no usable id", { nookId: this.nookId, result });
        return null;
      }
      if (this.isClosed) {
        await this.closePanel(id);
        return null;
      }
      this.nativePanelId = id;
      return { id, created: true };
    });
  }

  invokePanel<Result>(
    command: FrontendCommand,
    args: Record<string, unknown>,
    includePanelId = true,
  ): Promise<Result | null> {
    return this.invokeOwnedPanel<Result>(command, args, includePanelId).then((result) =>
      result.found ? result.value as Result : null
    );
  }

  invokeOwnedPanel<Result>(
    command: FrontendCommand,
    args: Record<string, unknown>,
    includePanelId = true,
  ): Promise<BrowserPanelInvocationResult<Result>> {
    return this.serializeNativeOperation(async () => {
      const id = this.webviewId;
      if (id === null || this.isClosed) return { found: false };
      const callArgs = includePanelId ? { id, ...args } : args;
      const result = await this.dependencies.invoke(command, callArgs);
      if (this.isClosed || this.webviewId !== id) return { found: false };
      return { found: true, value: result as Result };
    });
  }

  panelClosed(id: number): void {
    if (this.webviewId === id) this.nativePanelId = null;
  }

  reconcileBounds(): void {
    if (this.webviewId === null || this.isClosed) return;
    if (this.element.isConnected) {
      this.boundsSync();
      return;
    }
    void this.invokePanel(FrontendCommand.WebviewPaneSetBounds, {
      x: -20000,
      y: 0,
      width: 2,
      height: 2,
    }).catch((error: unknown) => {
      this.dependencies.warn("detached browser bounds update failed", { nookId: this.nookId, error: String(error) });
    });
  }

  dispose(): Promise<void> {
    this.disposal ??= this.disposeOwned();
    return this.disposal;
  }

  private async disposeOwned(): Promise<void> {
    let lifecycleError: unknown = null;
    try {
      await this.lifecycle.dispose();
    } catch (error) {
      lifecycleError = error;
    }
    await this.serializeNativeOperation(async () => {
      const id = this.webviewId;
      this.nativePanelId = null;
      if (id !== null) await this.closePanel(id);
    });
    if (lifecycleError !== null) throw lifecycleError;
  }

  private serializeNativeOperation<Result>(operation: () => Promise<Result>): Promise<Result> {
    const result = this.nativeOperations.then(operation);
    this.nativeOperations = result.then(() => void 0, () => void 0);
    return result;
  }

  private async closePanel(id: number): Promise<void> {
    await this.dependencies.invoke(FrontendCommand.WebviewPaneClose, { id }).catch((error: unknown) => {
      this.dependencies.warn("webviewPane.close failed", { nookId: this.nookId, error: String(error) });
    });
  }
}

interface BrowserNookDto {
  currentUrl: string;
  history: string[];
  historyIndex: number;
}
const browserSessions = new Map<string, BrowserSession>();

export function reconcileBrowserBounds(): void {
  for (const session of browserSessions.values()) session.reconcileBounds();
}

export type BrowserPanelAction =
  | { kind: "screenshot" }
  | { kind: "setUserAgent"; userAgent: string }
  | { kind: "evaluate"; code: string };

export interface BrowserPanelInvocationResult<Result = unknown> {
  found: boolean;
  value?: Result;
}

export type BrowserPanelActionResult = BrowserPanelInvocationResult;

export async function invokeBrowserAction(
  nookId: string,
  action: BrowserPanelAction,
): Promise<BrowserPanelActionResult> {
  const session = browserSessions.get(nookId);
  if (!session || session.isClosed) return { found: false };
  let result: BrowserPanelActionResult;
  if (action.kind === "screenshot") {
    result = await session.invokeOwnedPanel(FrontendCommand.WebviewPaneScreenshot, {});
  } else if (action.kind === "setUserAgent") {
    result = await session.invokeOwnedPanel(FrontendCommand.WebviewPaneSetUserAgent, {
      userAgent: action.userAgent,
    });
  } else {
    result = await session.invokeOwnedPanel(FrontendCommand.WebviewPaneEval, {
      code: action.code,
    });
  }
  return result;
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

async function whenLaidOut(el: HTMLElement, cancelled: () => boolean): Promise<boolean> {
  for (let i = 0; i < 120; i++) {
    if (cancelled()) return false;
    if (el.isConnected) {
      const r = el.getBoundingClientRect();
      if (r.width >= 1 && r.height >= 1) return true;
    }
    await new Promise<void>((resolve) => requestAnimationFrame(() => resolve()));
  }
  if (cancelled()) return false;
  console.warn("browser nook content area never got a size, opening webview anyway");
  return true;
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
  const session = browserSessions.get(nookId);
  browserSessions.delete(nookId);
  await session?.dispose();
}

export async function disposeBrowserSessions(): Promise<void> {
  const sessions = [...browserSessions.values()];
  browserSessions.clear();
  await Promise.all(sessions.map((session) => session.dispose()));
}

export async function renderBrowserNook(nookId: string, initialUrl: string, userAgent?: string): Promise<HTMLElement> {
  const cached = browserSessions.get(nookId);
  if (cached) {
    requestAnimationFrame(() => requestAnimationFrame(() => reconcileBrowserBounds()));
    return cached.element;
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

  const session = new BrowserSession(nookId, el, initialUrl);
  const nav = session.nav;
  const crashState = session.crashState;
  const permQueue = session.permissions;
  const downloads = session.downloads;

  const updateChrome = () => {
    urlBar.value = nav.currentUrl;
    backBtn.style.opacity = nav.canGoBack ? "1" : "0.4";
    fwdBtn.style.opacity = nav.canGoForward ? "1" : "0.4";
    if (nav.currentUrl.startsWith("https://")) securityGlyph.textContent = "🔒";
    else if (nav.currentUrl.startsWith("http://")) securityGlyph.textContent = "⚠";
    else securityGlyph.textContent = "";
    zoomResetBtn.textContent = Math.round(session.zoomLevel * 100) + "%";
  };

  const setLoading = (loading: boolean) => {
    loadingBar.style.transform = loading ? "scaleX(0.7)" : "scaleX(0)";
  };

  const syncBounds = () => {
    if (session.isClosed || session.webviewId === null || crashState.isCrashed) return;
    const rect = contentArea.getBoundingClientRect();
    if (rect.width < 1 || rect.height < 1) return;
    const bounds = nativeWebviewBounds(rect);
    void session.invokePanel(FrontendCommand.WebviewPaneSetBounds, {
      x: bounds.x,
      y: bounds.y,
      width: bounds.width,
      height: bounds.height,
    }).catch(() => void 0);
  };
  session.setBoundsSync(syncBounds);
  const onBridge = (event: FrontendEvent, callback: (data: unknown) => void): void => session.observe(event, callback);
  let boundsOwnershipInstalled = false;
  const openWebView = async (url: string) => {
    const storagePath = `/tmp/cove-webview-${nookId}`;
    if (!await whenLaidOut(contentArea, () => session.isClosed)) return null;
    const bounds = nativeWebviewBounds(contentArea.getBoundingClientRect());
    const openArgs: Record<string, unknown> = {
      url,
      x: bounds.x, y: bounds.y,
      width: bounds.width, height: bounds.height,
      storagePath, devTools: false, zoom: session.zoomLevel,
    };
    const nookBackground = themeBackgroundColor(getComputedStyle(document.body).getPropertyValue("--bg"));
    if (nookBackground) openArgs.background = nookBackground;
    if (userAgent && userAgent.length > 0) openArgs.userAgent = userAgent;
    const panel = await session.openPanel(openArgs);
    if (panel === null) return null;
    syncBounds();
    if (!boundsOwnershipInstalled) {
      boundsOwnershipInstalled = true;
      const resizeObserver = new ResizeObserver(() => syncBounds());
      resizeObserver.observe(contentArea);
      session.ownObserver(resizeObserver);
      session.listen(window, "resize", syncBounds);
    }
    return panel;
  };

  const doNavigate = async (url: string) => {
    nav.navigate(url);
    session.expectedNavigationUrl = nav.currentUrl;
    updateChrome();
    const targetUrl = nav.currentUrl;
    setLoading(true);
    if (session.webviewId !== null) {
      await session.invokePanel(FrontendCommand.WebviewPaneNavigate, { url: targetUrl }).catch((err) => {
        console.warn("webview navigate failed", targetUrl, err);
        if (!session.isClosed) setLoading(false);
      });
    } else {
      await openWebView(targetUrl).then(async (panel) => {
        if (panel && !panel.created) await session.invokePanel(FrontendCommand.WebviewPaneNavigate, { url: targetUrl });
      }).catch((err) => {
        console.warn("webview open failed", targetUrl, err);
        if (!session.isClosed) setLoading(false);
      });
    }
    if (session.isClosed) return;
    void invoke(FrontendCommand.BrowserNavigate, { nookId, url: nav.currentUrl }).catch((err) => console.warn("engine browser.navigate failed", err));
  };

  const doBack = async () => {
    if (!nav.canGoBack) return;
    nav.back();
    session.expectedNavigationUrl = nav.currentUrl;
    updateChrome();
    await session.invokePanel(FrontendCommand.WebviewPaneNavigate, { url: nav.currentUrl }).catch(() => void 0);
    if (session.isClosed) return;
    void invoke(FrontendCommand.BrowserBack, { nookId }).catch(() => void 0);
  };

  const doForward = async () => {
    if (!nav.canGoForward) return;
    nav.forward();
    session.expectedNavigationUrl = nav.currentUrl;
    updateChrome();
    await session.invokePanel(FrontendCommand.WebviewPaneNavigate, { url: nav.currentUrl }).catch(() => void 0);
    if (session.isClosed) return;
    void invoke(FrontendCommand.BrowserForward, { nookId }).catch(() => void 0);
  };

  const doReload = async () => {
    await session.invokePanel(FrontendCommand.WebviewPaneReload, {}).catch(() => void 0);
    if (session.isClosed) return;
    void invoke(FrontendCommand.BrowserReload, { nookId }).catch(() => void 0);
  };

  const setZoom = async (level: number) => {
    session.zoomLevel = Math.max(0.25, Math.min(5.0, level));
    updateChrome();
    await session.invokePanel(FrontendCommand.WebviewPaneSetZoom, { factor: session.zoomLevel }).catch((err) => console.warn("webview setZoom failed", err));
  };

  const toggleDevTools = async () => {
    session.devToolsOpen = !session.devToolsOpen;
    await session.invokePanel(FrontendCommand.WebviewPaneSetDevTools, { enabled: session.devToolsOpen }).catch((err) => console.warn("webview setDevTools failed", err));
    if (session.isClosed) return;
    devToolsBtn.style.background = session.devToolsOpen ? "#34c2b0" : "";
  };

  const renderFind = () => {
    findBar.style.display = findState.open ? "flex" : "none";
    findCounter.textContent = findState.counter;
    findCaseBtn.style.background = findState.matchCase ? "#34c2b0" : "";
  };

  const runFind = async (forward: boolean) => {
    if (session.webviewId === null) return;
    if (!findState.canSearch) {
      await session.invokePanel(FrontendCommand.WebviewPaneFindStop, { clearHighlights: true }).catch(() => void 0);
      if (session.isClosed) return;
      findState.applyResult({ matches: 0, activeIndex: 0 });
      renderFind();
      return;
    }
    const result = await session.invokePanel<FindResult>(FrontendCommand.WebviewPaneFind, {
      text: findState.query,
      forward,
      matchCase: findState.matchCase,
    }).catch(() => null);
    if (session.isClosed) return;
    if (result) findState.applyResult(result);
    renderFind();
  };

  const runFindNext = async (forward: boolean) => {
    if (session.webviewId === null || !findState.canSearch) return;
    const result = await session.invokePanel<FindResult>(FrontendCommand.WebviewPaneFindNext, { forward }).catch(() => null);
    if (session.isClosed) return;
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
    void session.invokePanel(FrontendCommand.WebviewPaneFindStop, { clearHighlights: true }).catch(() => void 0);
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
    session.clearPermissionTimeout(requestId);
  };

  const resolvePermission = async (requestId: string, grant: boolean) => {
    if (!permQueue.has(requestId)) return;
    clearPermTimer(requestId);
    permQueue.remove(requestId);
    renderPermission();
    await session.invokePanel(FrontendCommand.WebviewPaneResolvePermission, { requestId, grant }, false).catch(() => void 0);
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
    await session.invokePanel(FrontendCommand.WebviewPaneResolveDownload, args, false).catch(() => void 0);
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
  zoomInBtn.addEventListener("click", () => void setZoom(session.zoomLevel + 0.1));
  zoomOutBtn.addEventListener("click", () => void setZoom(session.zoomLevel - 0.1));
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
    await session.invokePanel(FrontendCommand.WebviewPaneReloadFromCrash, {}).catch(() => void 0);
    syncBounds();
  };

  const setSuspended = async (value: boolean) => {
    if (session.isClosed || session.suspended === value || session.webviewId === null) return;
    session.suspended = value;
    await session.invokePanel(FrontendCommand.WebviewPaneSetSuspended, { suspended: value }).catch(() => void 0);
  };

  const visObserver = new IntersectionObserver((entries) => {
    for (const entry of entries) {
      if (entry.target !== el) continue;
      const visible = entry.isIntersecting && entry.intersectionRatio > 0;
      void setSuspended(!visible);
    }
  }, { threshold: 0 });
  visObserver.observe(el);
  session.ownObserver(visObserver);

  onBridge(FrontendEvent.WebviewPaneNavigated, (data: unknown) => {
    const evt = data as { id: number; url: string };
    if (evt.id !== session.webviewId) return;
    const persistNavigation = nav.webviewNavigated(evt.url, session.expectedNavigationUrl);
    session.expectedNavigationUrl = null;
    updateChrome();
    findState.onNavigate();
    renderFind();
    if (persistNavigation) {
      void invoke(FrontendCommand.BrowserNavigate, { nookId, url: nav.currentUrl }).catch((err) => console.warn("engine browser.navigate failed", err));
    }
  });

  onBridge(FrontendEvent.WebviewPaneTitleChanged, (data: unknown) => {
    const evt = data as { id: number; title: string };
    if (evt.id !== session.webviewId) return;
    if (crashState.isCrashed) return;
    titleBar.textContent = evt.title;
  });

  onBridge(FrontendEvent.WebviewPaneLoadStateChanged, (data: unknown) => {
    const evt = data as { id: number; state: string };
    if (evt.id !== session.webviewId) return;
    setLoading(evt.state === "started");
  });

  onBridge(FrontendEvent.WebviewPaneFaviconChanged, (data: unknown) => {
    const evt = data as { id: number; dataUrl: string };
    if (evt.id !== session.webviewId) return;
    if (evt.dataUrl) {
      titleBar.style.backgroundImage = `url(${evt.dataUrl})`;
      titleBar.style.backgroundRepeat = "no-repeat";
      titleBar.style.backgroundPosition = "left center";
      titleBar.style.backgroundSize = "12px";
      titleBar.style.paddingLeft = "26px";
    }
  });

  onBridge(FrontendEvent.WebviewPaneClosed, (data: unknown) => {
    const evt = data as { id: number };
    if (evt.id !== session.webviewId) return;
    session.panelClosed(evt.id);
    contentArea.style.background = "#0d1117";
    titleBar.textContent = "Closed";
  });

  onBridge(FrontendEvent.WebviewPaneProcessTerminated, (data: unknown) => {
    const evt = data as { id: number; reason?: string };
    if (evt.id !== session.webviewId) return;
    enterCrash(evt.reason ?? null);
  });

  onBridge(FrontendEvent.WebviewPanePermissionRequested, (data: unknown) => {
    const evt = data as { id: number; requestId: string; kinds: string[]; url: string };
    if (evt.id !== session.webviewId) return;
    const req: PermissionRequest = { requestId: evt.requestId, kinds: evt.kinds ?? [], url: evt.url ?? nav.currentUrl };
    permQueue.add(req);
    session.schedulePermissionTimeout(req.requestId, () => dismissPermissionOnTimeout(req.requestId));
    renderPermission();
  });

  onBridge(FrontendEvent.WebviewPaneDownloadRequested, (data: unknown) => {
    const evt = data as { id: number; downloadId: string; url: string; suggestedName: string };
    if (evt.id !== session.webviewId) return;
    downloads.requested(evt.downloadId, evt.url, evt.suggestedName || "download");
    renderDownloadPrompt();
  });

  onBridge(FrontendEvent.WebviewPaneDownloadProgress, (data: unknown) => {
    const evt = data as { id: number; downloadId: string; receivedBytes: number; totalBytes: number };
    if (evt.id !== session.webviewId) return;
    downloads.progress(evt.downloadId, evt.receivedBytes ?? 0, evt.totalBytes ?? 0);
    renderDownloadShelf();
  });

  onBridge(FrontendEvent.WebviewPaneDownloadCompleted, (data: unknown) => {
    const evt = data as { id: number; downloadId: string; path?: string };
    if (evt.id !== session.webviewId) return;
    downloads.completed(evt.downloadId, evt.path);
    renderDownloadShelf();
  });

  onBridge(FrontendEvent.WebviewPaneDownloadFailed, (data: unknown) => {
    const evt = data as { id: number; downloadId: string; reason?: string };
    if (evt.id !== session.webviewId) return;
    downloads.failed(evt.downloadId, evt.reason ?? "download failed");
    renderDownloadShelf();
  });

  void invoke<BrowserNookDto>(FrontendCommand.BrowserOpen, { nookId, url: nav.currentUrl })
    .catch(() => null)
    .then((retained) => {
      if (session.isClosed) return null;
      if (retained) nav.restore(retained.history, retained.historyIndex);
      updateChrome();
      session.expectedNavigationUrl = nav.currentUrl;
      return openWebView(nav.currentUrl);
    })
    .then(() => {
      if (!session.isClosed) setLoading(true);
    })
    .catch((err) => console.warn("webview open failed", nav.currentUrl, err));

  updateChrome();
  renderFind();

  browserSessions.set(nookId, session);
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
