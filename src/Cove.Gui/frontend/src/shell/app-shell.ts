import { LifecycleScope } from "../app/lifecycle";

export const shellElementIds = [
  "grid",
  "palette",
  "shores-row",
  "shore-tabs",
  "left-sidebar",
  "left-rail",
  "left-content",
  "left-resize",
  "pal-input",
  "pal-list",
  "wordmark-img",
  "settings",
  "set-tabs",
  "set-body",
  "onboarding",
  "findbar",
  "find-input",
  "launcher",
  "launch-agents",
  "ws-create",
  "wsc-name",
  "wsc-path",
  "wsc-error",
  "perf-hud",
] as const;

type ShellElementId = typeof shellElementIds[number];

function requiredElement<T extends HTMLElement>(document: Document, id: ShellElementId, tagName?: string): T {
  const element = document.getElementById(id);
  if (!element) throw new Error(`Missing shell element #${id}`);
  if (tagName && element.tagName !== tagName) throw new Error(`Invalid shell element #${id}`);
  return element as T;
}

export class AppShell {
  private readonly lifecycle = new LifecycleScope();
  readonly grid: HTMLElement;
  readonly palette: HTMLElement;
  readonly shoresRow: HTMLElement;
  readonly shoreTabs: HTMLElement;
  readonly leftSidebar: HTMLElement;
  readonly leftRail: HTMLElement;
  readonly leftContent: HTMLElement;
  readonly leftResize: HTMLElement;
  readonly paletteInput: HTMLInputElement;
  readonly paletteList: HTMLElement;
  readonly wordmarkImage: HTMLImageElement;
  readonly settings: HTMLElement;
  readonly settingsTabs: HTMLElement;
  readonly settingsBody: HTMLElement;
  readonly onboarding: HTMLElement;
  readonly findBar: HTMLElement;
  readonly findInput: HTMLInputElement;
  readonly launcher: HTMLElement;
  readonly launchAgents: HTMLElement;
  readonly workspaceCreate: HTMLElement;
  readonly workspaceName: HTMLInputElement;
  readonly workspacePath: HTMLInputElement;
  readonly workspaceError: HTMLElement;
  readonly performanceHud: HTMLElement;

  constructor(readonly document: Document) {
    this.grid = requiredElement(document, "grid");
    this.palette = requiredElement(document, "palette");
    this.shoresRow = requiredElement(document, "shores-row");
    this.shoreTabs = requiredElement(document, "shore-tabs");
    this.leftSidebar = requiredElement(document, "left-sidebar");
    this.leftRail = requiredElement(document, "left-rail");
    this.leftContent = requiredElement(document, "left-content");
    this.leftResize = requiredElement(document, "left-resize");
    this.paletteInput = requiredElement(document, "pal-input", "INPUT");
    this.paletteList = requiredElement(document, "pal-list");
    this.wordmarkImage = requiredElement(document, "wordmark-img", "IMG");
    this.settings = requiredElement(document, "settings");
    this.settingsTabs = requiredElement(document, "set-tabs");
    this.settingsBody = requiredElement(document, "set-body");
    this.onboarding = requiredElement(document, "onboarding");
    this.findBar = requiredElement(document, "findbar");
    this.findInput = requiredElement(document, "find-input", "INPUT");
    this.launcher = requiredElement(document, "launcher");
    this.launchAgents = requiredElement(document, "launch-agents");
    this.workspaceCreate = requiredElement(document, "ws-create");
    this.workspaceName = requiredElement(document, "wsc-name", "INPUT");
    this.workspacePath = requiredElement(document, "wsc-path", "INPUT");
    this.workspaceError = requiredElement(document, "wsc-error");
    this.performanceHud = requiredElement(document, "perf-hud");
  }

  listen(target: EventTarget, event: string, listener: EventListenerOrEventListenerObject, options?: boolean | AddEventListenerOptions): void {
    this.lifecycle.listen(target, event, listener, options);
  }

  dispose(): Promise<void> {
    return this.lifecycle.dispose();
  }
}
