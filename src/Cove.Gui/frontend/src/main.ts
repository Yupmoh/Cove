
import "@xterm/xterm/css/xterm.css";
import { openNote } from "./notepad-nook";
import { closeBrowserWebview, disposeBrowserSessions, invokeBrowserAction, reconcileBrowserBounds } from "./browser-nook";
import { applyMarkdownSettings, resolveMarkdownSettings } from "./markdown-nook";
import { type SidebarMode } from "./sidebar-model";
import { iconSvg } from "./icons";
import { enqueueNookWrite } from "./write-queue";
import { installConsoleCapture } from "./console-capture";
import { disposeFrontendTransport, getFrontendEnginePort, invoke, invokeNative, onRyn } from "./invoke";
import { WorkspaceStore } from "./workspace/workspace-store";
import { WorkspaceController } from "./workspace/workspace-controller";
import { AppShell } from "./shell/app-shell";
import { ToastHost, type ToastAction } from "./shell/toast-host";
import { TitleClusterComponent } from "./shell/title-cluster-component";
import { FindBarFeature } from "./features/find/find-bar-feature";
import { mountApplicationTemplate } from "./app/application-template";

import { createInspectFeature } from "./features/inspect/inspect-feature";
import { createUpdaterFeature } from "./features/updater/updater-feature";
import { EngineEventRouter } from "./app/engine-event-router";
import { LifecycleScope } from "./app/lifecycle";
import { ContextMenuHost } from "./shell/context-menu-host";
import { createPaletteFeature, type PaletteAction } from "./features/palette/palette-feature";
import { createActionCoordinatorFeature } from "./shell/action-coordinator-feature";
import { PerformanceHudFeature } from "./features/performance/performance-hud-feature";
import { createWorkspaceSidebarFeature } from "./features/navigation/workspace-sidebar-feature";
import { createShoreTabsFeature } from "./features/navigation/shore-tabs-feature";
import { createWorkspaceViewFeature } from "./features/workspace/workspace-view-feature";
import { SplitChooserFeature } from "./shell/split-chooser-feature";
import { BayCreateFeature } from "./features/bays/bay-create-feature";
import { createWorkspaceActionsFeature } from "./features/workspace/workspace-actions-feature";
import { createWorkspaceSyncFeature } from "./features/workspace/workspace-sync-feature";
import { createBrowserAutomationFeature } from "./features/automation/browser-automation-feature";
import { createNotificationFeature } from "./features/notifications/notification-feature";
import { createHarnessUpdateFeature } from "./features/updater/harness-update-feature";
import { createTerminalPreferencesFeature } from "./features/settings/terminal-preferences-feature";
import { createAgentStatusFeature } from "./features/navigation/agent-status-feature";
import { createLauncherFeature, mapLauncherAdapters, type AdapterInfo } from "./features/launcher/launcher-feature";
import { createOnboardingFeature, type OnboardingFeature } from "./features/onboarding/onboarding-feature";
import { createSettingsFeature } from "./features/settings/settings-feature";
import type { JsHeapProbe } from "./perf-hud";
import { setupDictation } from "./dictation";
import { ActionRegistry, type CoveAction } from "./app/action-registry";
import { createAppContext } from "./app/app-context";
import { FrontendCommand } from "./app/frontend-command";
import { FrontendEvent } from "./app/frontend-event";
import { createAppearanceFeature } from "./features/appearance/appearance-feature";
import { createBrandFeature } from "./features/branding/brand-feature";

const RYN_MENUBAR_EVENTS_BROKEN = false;

const applicationRoot = document.getElementById("app");
if (!applicationRoot) throw new Error("Missing application root #app");
mountApplicationTemplate(document, applicationRoot);

const brandFeature = createBrandFeature({ document, storage: localStorage });
brandFeature.start();

const enginePort = getFrontendEnginePort();
const workspaceStore = new WorkspaceStore();
const workspaceControllerPort = new WorkspaceController((command, args) => enginePort.invoke(command, args));
const applicationLifecycle = new LifecycleScope();
const applicationEvents = new EngineEventRouter((listener) => enginePort.on(FrontendEvent.EngineEvent, listener));
const applicationActions = new ActionRegistry<CoveAction>();
const appContext = createAppContext({
  document,
  window,
  storage: localStorage,
  engine: enginePort,
  events: applicationEvents,
  actions: applicationActions,
  lifecycle: applicationLifecycle,
  workspace: workspaceStore,
  workspaceController: workspaceControllerPort,
});
const {
  workspace,
  workspaceController,
  lifecycle: appLifecycle,
  events: engineEvents,
  actions,
} = appContext;
const disposeConsoleCapture = installConsoleCapture((level, message) => {
  appContext.engine.native(FrontendCommand.AppFrontendLog, { level, message }).catch(() => {});
});
const terminalPreferences = createTerminalPreferencesFeature({ invoke });
const settings = terminalPreferences.settings;

const shell = new AppShell(document);
document.body.classList.add(navigator.platform.toUpperCase().includes("MAC") ? "platform-mac" : "platform-other");
const toastComponent = new ToastHost(document);
const contextMenu = new ContextMenuHost(document);
const titleCluster = new TitleClusterComponent(document, iconSvg, runAction);
let onboardingFeature: OnboardingFeature;
const findFeature = new FindBarFeature(document, {
  active: () => {
    const nookId = workspace.focusedNookId;
    const nook = nookId ? workspaceView.nooks.get(nookId) : null;
    return nook && nookId ? { nookId, search: nook.session.search, focus: () => nook.session.term.focus() } : null;
  },
  invoke,
});
const gridEl = shell.grid;
const paletteEl = shell.palette;
const shoresRowEl = shell.shoresRow;
const shoreTabsEl = shell.shoreTabs;
const leftSidebarEl = shell.leftSidebar;
const leftRailEl = shell.leftRail;
const leftContentEl = shell.leftContent;
const leftResizeEl = shell.leftResize;
const palInput = shell.paletteInput;
const palList = shell.paletteList;
const settingsEl = shell.settings;
const setTabsEl = shell.settingsTabs;
const setBodyEl = shell.settingsBody;
const onboardingEl = shell.onboarding;
const launcherEl = shell.launcher;
const launchAgentsEl = shell.launchAgents;
const wsCreateEl = shell.workspaceCreate;
const wscNameEl = shell.workspaceName;
const wscPathEl = shell.workspacePath;
const wscErrorEl = shell.workspaceError;
const perfHudEl = shell.performanceHud;
gridEl.style.display = "flex";

const nookDrag = { nookId: null as string | null };

shell.listen(document, "dragend", () => { nookDrag.nookId = null; workspaceActions.clearDropOverlay(); });

function baseActions(): PaletteAction[] {
  return [
    { kind: "action", label: "New terminal", icon: "+", key: "Cmd T", action: "shore.new" },
    { kind: "action", label: "New browser", icon: "\uD83C\uDF10", action: "tool.browser" },
    { kind: "action", label: "Split right", icon: "\u2502", key: "Cmd D", action: "nook.split-right" },
    { kind: "action", label: "Split down", icon: "\u2500", key: "Cmd Shift D", action: "nook.split-down" },
    { kind: "action", label: "Close nook", icon: "\u00d7", key: "Cmd W", action: "nook.close" },
    { kind: "action", label: "Toggle left sidebar", icon: "\u25e7", key: "Cmd B", action: "view.toggle-sidebar" },
    { kind: "action", label: "Show notepad", icon: "\u270e", action: "view.toggle-notepad" },
    { kind: "action", label: "Show bays", icon: "\u25c9", key: "Cmd Shift A", action: "view.show-bays" },
    { kind: "action", label: "Toggle window backdrop", icon: "\u25d0", action: "view.toggle-backdrop" },
    { kind: "action", label: "Toggle performance HUD", icon: "\ud83d\udcc8", action: "view.toggle-performance" },
    { kind: "action", label: "Increase font size", icon: "+", key: "Cmd =", action: "view.zoom-in" },
    { kind: "action", label: "Decrease font size", icon: "-", key: "Cmd -", action: "view.zoom-out" },
    { kind: "action", label: "Reset font size", icon: "\u21ba", key: "Cmd 0", action: "view.zoom-reset" },
    { kind: "action", label: "Settings", icon: "\u2699", key: "Cmd ,", action: "app.settings" },
    { kind: "action", label: "Inspect UI (report a bug)", icon: "\u2316", action: "tool.inspect" },
  ];
}

function jumpActions(): PaletteAction[] {
  return (workspace.snapshot?.shores ?? []).map((r, i) => ({
    kind: "callback",
    label: `Go to ${r.name}`,
    icon: "\u203a",
    key: i < 9 ? `Cmd ${i + 1}` : undefined,
    run: () => {
      workspace.activeShoreId = r.id;
      const f = workspaceView.firstLeafOf(r);
      if (f) { workspace.focusedNookId = f; workspaceView.render(); workspaceSidebar.render(); workspaceView.focus(f); }
    },
  }));
}

const paletteFeature = createPaletteFeature({
  document,
  storage: localStorage,
  root: paletteEl,
  input: palInput,
  list: palList,
  commandActions: baseActions,
  shoreActions: jumpActions,
  nooks: () => [...workspaceView.nooks].map(([id, nook]) => ({
    id,
    title: nook.title,
    focus: () => workspaceView.focus(id),
  })),
  invoke,
  switchBay: (id) => workspaceActions.switchBay(id),
  openTask: (id) => workspaceActions.openTaskInNook(id),
  openFile: (path) => workspaceActions.openFileInEditor(path),
  splitActive: () => workspaceActions.splitActive("row"),
  focusActiveNook: () => {
    if (!workspace.focusedNookId) return;
    workspaceView.nooks.get(workspace.focusedNookId)?.session.term.focus();
  },
  dispatchAction: (action) => actionCoordinator.run(action),
});

appLifecycle.own(onRyn(FrontendEvent.WindowFocused, () => document.body.classList.remove("window-inactive")));
appLifecycle.own(onRyn(FrontendEvent.WindowBlurred, () => document.body.classList.add("window-inactive")));
void invoke<{ version?: string }>(FrontendCommand.SysDaemonStatus, {}).then((s) => {
  if (s?.version) document.getElementById("wordmark-ver")!.textContent = "v" + s.version;
}).catch(() => void 0);

const settingsFeature = createSettingsFeature({
  document,
  storage: localStorage,
  root: settingsEl,
  tabs: setTabsEl,
  body: setBodyEl,
  grid: gridEl,
  invoke,
  invokeNative,
  terminalSettings: settings,
  loadTerminalSettings: () => terminalPreferences.load(),
  applyTerminalSettings: () => workspaceView.applySettings(),
  defaultTerminalTheme: terminalPreferences.defaultTheme,
  themeBackgroundWithOpacity: terminalPreferences.themeBackgroundWithOpacity,
  applyTerminalTheme: (theme) => {
    for (const nook of workspaceView.nooks.values()) nook.session.term.options.theme = theme;
  },
  focusActiveNook: () => {
    if (workspace.focusedNookId) workspaceView.nooks.get(workspace.focusedNookId)?.session.term.focus();
  },
  renderDictationTab: (container) => onboardingFeature.renderDictationTab(container),
  rerunOnboarding: () => onboardingFeature.rerun(),
  renderUpdates: (container) => updaterFeature.renderSettings(container),
  setAgentChimesEnabled: (enabled) => workspaceSidebar.setAgentChimesEnabled(enabled),
  agentChimesEnabled: () => workspaceSidebar.agentChimesEnabled(),
  showToast: showInAppToast,
  launcherProfiles: () => launcherFeature.profiles,
  activeProjectDir: () => workspaceView.activeProjectDir(),
  isPerfHudEnabled: () => performanceHud.enabled,
  togglePerfHud: () => performanceHud.toggle(),
  reloadKeymap: () => actionCoordinator.reloadKeymap(),
  applyMarkdownSettings: (raw) => applyMarkdownSettings(resolveMarkdownSettings(raw)),
});

const launcherFeature = createLauncherFeature({
  document,
  root: launcherEl,
  agentsRoot: launchAgentsEl,
  workspace,
  workspaceController,
  spawnNook: (input) => workspaceView.spawn(input),
  focusNook: (nookId) => workspaceView.focus(nookId),
  focusActiveNook: () => {
    if (!workspace.focusedNookId) return;
    workspaceView.nooks.get(workspace.focusedNookId)?.session.term.focus();
  },
  safeReplaceTarget: (shoreId, placeholderId) => workspaceActions.safeReplaceTarget(shoreId, placeholderId),
  nextShoreName: () => shoreTabsFeature.nextName(),
  activeProjectDir: () => workspaceView.activeProjectDir(),
  renderShore: () => workspaceView.render(),
  launchTileInto: (shoreId, placeholderId, action) => workspaceActions.launchTileInto(shoreId, placeholderId, action),
  resolveLauncherProfileSlug: settingsFeature.resolveLauncherProfileSlug,
  launcherProfileSlugKey: settingsFeature.launcherProfileSlugKey,
  openProfileEditor: settingsFeature.openProfileEditor,
  openContextMenuAt: (event, items, onSelect) => contextMenu.openAt(event, items, onSelect),
  showToast: showInAppToast,
  resumeRecentSession: (adapter, sessionId, cwd, displayName) => workspaceView.resumeRecentSession(adapter, sessionId, cwd, displayName),
  getBrandIndex: () => brandFeature.currentIndex,
  openAdapterSetup: () => onboardingFeature.rerun(),
});

const splitChooser = new SplitChooserFeature({
  document,
  window,
  adapters: () => launcherFeature.adapters,
  prepare: () => {
    contextMenu.close();
    workspaceView.closeNookMenus();
  },
  select: (direction, kind) => { void workspaceActions.splitActiveWith(direction, kind); },
});

const workspaceView = createWorkspaceViewFeature({
  document,
  window,
  grid: gridEl,
  shoreTabs: shoreTabsEl,
  leftSidebar: leftSidebarEl,
  workspace,
  workspaceController,
  contextMenu,
  findFeature,
  settings,
  nookDrag,
  invoke,
  currentTermTheme: () => terminalPreferences.theme(settingsFeature.activeTheme),
  renderLauncher: (shoreId, placeholderId) => launcherFeature.render(shoreId, placeholderId),
  invalidateLauncherRecents: () => launcherFeature.invalidateRecents(),
  refreshLauncherRecents: () => launcherFeature.refreshRecents(),
  launcherAdapters: () => launcherFeature.adapters,
  launcherYolo: (adapter) => launcherFeature.yolo(adapter),
  renderSidebar: () => workspaceSidebar.render(),
  renderSidebarContent: () => workspaceSidebar.renderContent("left"),
  isSidebarModeVisible: (mode) => workspaceSidebar.isModeVisible(mode as SidebarMode),
  rememberNookTitle: (nookId, title) => workspaceSidebar.rememberNookTitle(nookId, title),
  acknowledgeAgentAttention: (nookId) => workspaceSidebar.acknowledgeAgentAttention(nookId),
  syncAgentNookStateClasses: () => workspaceSidebar.syncAgentNookStateClasses(),
  sidebarBayBoxes: () => workspaceSidebar.bayBoxes,
  sidebarDefaultDirectory: () => workspaceSidebar.defaultDirectory,
  renderShoreTabs: () => shoreTabsFeature.render(),
  shoreTabName: (shore) => shoreTabsFeature.tabName(shore),
  getOverviewVisible: () => shoreTabsFeature.overviewVisible,
  setOverviewVisible: (value) => { shoreTabsFeature.overviewVisible = value; },
  showInAppToast,
  revealNook: (nookId) => workspaceActions.revealNook(nookId),
  runAction,
  openSplitChooser: (event, direction) => splitChooser.open(event, direction),
  closeNookById: (nookId) => workspaceActions.closeNookById(nookId),
  closeFocused: () => workspaceActions.closeFocused(),
  closeOthers: (nookId) => workspaceActions.closeOthers(nookId),
  paintDropOverlay: (host, zone) => workspaceActions.paintDropOverlay(host, zone),
  clearDropOverlay: () => workspaceActions.clearDropOverlay(),
  applyNookMove: (mutation, sourceNookId) => workspaceActions.applyNookMove(mutation, sourceNookId),
  newShore: () => workspaceActions.newShore(),
  openFileInEditor: (path) => workspaceActions.openFileInEditor(path),
});

const shoreTabsFeature = createShoreTabsFeature({
  document,
  window,
  storage: localStorage,
  root: shoreTabsEl,
  row: shoresRowEl,
  workspace,
  workspaceController,
  contextMenu,
  nooks: workspaceView.nooks,
  nookDrag,
  invoke,
  renderShore: () => workspaceView.render(),
  focusNook: (nookId) => workspaceView.focus(nookId),
  clearDropOverlay: () => workspaceActions.clearDropOverlay(),
  moveNookToShore: (nookId, shoreId) => workspaceActions.moveNookToShore(nookId, shoreId),
  newShore: () => workspaceActions.newShore(),
  closeShore: (shoreId) => workspaceActions.closeShore(shoreId),
  firstLeafOf: (shore) => workspaceView.firstLeafOf(shore),
  collectLeafIds: (node) => workspaceView.collectLeafIds(node),
  renderSidebar: () => workspaceSidebar.render(),
  renderSidebarContent: () => workspaceSidebar.renderContent("left"),
  revealBays: () => workspaceSidebar.reveal("bays"),
  shoreLeaves: (shore) => workspaceSidebar.shoreLeaves(shore),
});

const bayCreateFeature = new BayCreateFeature({
  document,
  root: wsCreateEl,
  nameInput: wscNameEl,
  pathInput: wscPathEl,
  error: wscErrorEl,
  invoke,
  invokeNative,
  defaultDirectory: () => workspaceSidebar.defaultDirectory,
  activeProjectDirectory: () => workspaceView.activeProjectDir(),
  buildIconGrid: (selected, onSelect) => workspaceSidebar.buildBayIconGrid(selected, onSelect),
  loadBays: () => workspaceSidebar.loadBayBoxes(),
  showToast: showInAppToast,
});

const workspaceSidebar = createWorkspaceSidebarFeature({
  document,
  window,
  storage: localStorage,
  leftRail: leftRailEl,
  leftSidebar: leftSidebarEl,
  leftContent: leftContentEl,
  workspace,
  workspaceController,
  contextMenu,
  launcherFeature,
  nooks: workspaceView.nooks,
  invoke,
  focusNook: (nookId) => workspaceView.focus(nookId),
  revealNook: (nookId) => workspaceActions.revealNook(nookId),
  spawnNook: (input) => workspaceView.spawn(input),
  openFileInEditor: (path) => workspaceActions.openFileInEditor(path),
  openNote,
  showInAppToast,
  switchBay: (bayId, targetShoreId, targetNookId, showLauncher) =>
    workspaceActions.switchBay(bayId, targetShoreId, targetNookId, showLauncher),
  renderShore: () => workspaceView.render(),
  renderShoreTabs: () => shoreTabsFeature.render(),
  openBayLauncher: (bayId) => workspaceActions.openBayLauncher(bayId),
  closeFocused: () => workspaceActions.closeFocused(),
  closeShore: (shoreId) => workspaceActions.closeShore(shoreId),
  disposeNook: (nookId) => workspaceView.disposeNook(nookId),
  firstLeafOf: (shore) => workspaceView.firstLeafOf(shore),
  collectLeafIds: (node) => workspaceView.collectLeafIds(node),
  shoreTabName: (shore) => shoreTabsFeature.tabName(shore),
  reorderShores: (sourceId, targetId) => shoreTabsFeature.reorder(sourceId, targetId),
  newBay: () => bayCreateFeature.open(),
  newShore: () => workspaceActions.newShore(),
  syncTitlebarWorkspaceOffset: () => workspaceView.syncTitlebarWorkspaceOffset(),
  fitAll: () => workspaceView.fitAll(),
});
workspaceSidebar.wireResize(leftResizeEl, "left");

const workspaceActions = createWorkspaceActionsFeature({
  document,
  workspace,
  workspaceController,
  workspaceView,
  shoreTabsFeature,
  workspaceSidebar,
  launcherFeature,
  invoke,
  runAction,
  closeBrowserNook: closeBrowserWebview,
  reconcileBrowserNooks: reconcileBrowserBounds,
});
const workspaceSyncFeature = createWorkspaceSyncFeature({
  engineEvents,
  reload: () => workspaceActions.reload(),
  warn: (message, error) => console.warn(message, error),
});

const appearanceFeature = createAppearanceFeature({
  document,
  storage: localStorage,
  getChrome: () => ({
    leftSidebarHidden: workspaceSidebar.model.leftCollapsed,
    rightSidebarHidden: workspaceSidebar.model.rightCollapsed,
  }),
  setChrome: (leftSidebarHidden, rightSidebarHidden) => {
    workspaceSidebar.setChromeVisibility(leftSidebarHidden, rightSidebarHidden);
  },
  fitWorkspace: () => workspaceView.fitAll(),
  setTitleZoom: (factor) => titleCluster.setZoom(factor),
  setPageZoom: async (factor) => {
    await invokeNative(FrontendCommand.WindowSetPageZoom, { factor });
  },
  syncTitlebar: () => workspaceView.syncTitlebarWorkspaceOffset(),
  reconcileBrowsers: reconcileBrowserBounds,
  getBackdrop: () => invokeNative(FrontendCommand.WindowGetBackdrop, {}),
  setBackdrop: async (material) => {
    await invokeNative(FrontendCommand.WindowSetBackdrop, { material });
  },
  loadConfig: async (key) => {
    try {
      const result = await invoke<{ ok: boolean; value?: string }>(FrontendCommand.AppConfigGet, { key });
      return result.ok ? result.value ?? null : null;
    } catch {
      return null;
    }
  },
  saveConfig: async (key, value) => {
    await invoke(FrontendCommand.AppConfigSet, { key, value })
      .catch((error) => console.warn("backdrop configSet failed", error));
  },
  warn: (message, error) => console.warn(message, error),
});

const inspectFeature = createInspectFeature({
  document,
  viewport: () => ({ width: window.innerWidth, height: window.innerHeight }),
  adapters: () => launcherFeature.adapters,
  workspaceNames: () => ({
    bay: workspace.snapshot?.name ?? "",
    shore: workspaceView.activeShore()?.name ?? "",
  }),
  invoke,
  buildAdapterLaunch: launcherFeature.buildAdapterLaunch,
  spawnNook: (input) => workspaceView.spawn(input),
  createShore: (nookId, name) => workspaceController.mutate<{ shoreId: string }>("createShore", {
    newNookId: nookId,
    name,
    shoreId: "",
    targetNookId: "",
    orientation: "",
    nookId: "",
    dir: 0,
    nookType: "terminal",
  }),
  selectShore: (shoreId) => { workspace.activeShoreId = shoreId; },
  focusNook: (nookId) => workspaceView.focus(nookId),
});

shell.listen(window, "resize", () => workspaceView.fitAll());

const performanceHud = new PerformanceHudFeature({
  document,
  root: perfHudEl,
  readHeap: () => (performance as unknown as { memory?: JsHeapProbe }).memory ?? null,
  requestFrame: (callback) => requestAnimationFrame(callback),
  cancelFrame: (handle) => cancelAnimationFrame(handle),
  onToggled: () => {
    if (settingsEl.classList.contains("open") && settingsFeature.activeTab === "diagnostics") {
      settingsFeature.render();
    }
  },
});

function runAction(action: string): void {
  actionCoordinator.run(action);
}

const actionCoordinator = createActionCoordinatorFeature({
  window,
  actions,
  invoke,
  observe: onRyn,
  nativeMenuEventsBroken: RYN_MENUBAR_EVENTS_BROKEN,
  isPaletteOpen: () => paletteFeature.isOpen,
  switchBayByIndex: (index) => workspaceActions.switchBayByIndex(index),
  handlers: [
    ["shore.new", () => workspaceActions.newShore()],
    ["shore.close", () => {
      if (!workspace.activeShoreId) {
        console.warn("shore close requested without an active shore");
        return;
      }
      return workspaceActions.closeShore(workspace.activeShoreId);
    }],
    ["shore.next", () => workspaceActions.nextShore(1)],
    ["shore.prev", () => workspaceActions.nextShore(-1)],
    ["shore.pin", () => shoreTabsFeature.toggleActivePin()],
    ["shore.omni-jump", () => paletteFeature.open()],
    ["nook.close", () => workspaceActions.closeFocused()],
    ["nook.split-right", () => workspaceActions.splitActive("row")],
    ["nook.split-down", () => workspaceActions.splitActive("col")],
    ["nook.focus-next", () => workspaceActions.cycleFocus(1)],
    ["nook.focus-prev", () => workspaceActions.cycleFocus(-1)],
    ["nook.find", () => findFeature.open()],
    ["nook.scroll-top", () => workspaceActions.scrollActiveNook(true)],
    ["nook.scroll-bottom", () => workspaceActions.scrollActiveNook(false)],
    ["nook.maximize", () => workspaceActions.toggleZoom()],
    ["bay.create", () => bayCreateFeature.open()],
    ["view.toggle-sidebar", () => workspaceSidebar.toggleLeft()],
    ["view.toggle-notepad", () => workspaceSidebar.reveal("notepad")],
    ["view.show-bays", () => workspaceSidebar.reveal("bays")],
    ["view.zen-mode", () => appearanceFeature.toggleZen()],
    ["view.zoom-in", () => {
      settings.fontSize = Math.min(24, settings.fontSize + 1);
      workspaceView.applySettings();
      terminalPreferences.persist();
    }],
    ["view.zoom-out", () => {
      settings.fontSize = Math.max(9, settings.fontSize - 1);
      workspaceView.applySettings();
      terminalPreferences.persist();
    }],
    ["view.zoom-reset", () => {
      settings.fontSize = 13;
      workspaceView.applySettings();
      terminalPreferences.persist();
    }],
    ["view.toggle-backdrop", () => appearanceFeature.toggleBackdrop()],
    ["view.toggle-performance", () => performanceHud.toggle()],
    ["tool.inspect", () => inspectFeature.start()],
    ["tool.git", () => workspaceActions.openToolShore("git", "Source Control")],
    ["tool.search", () => workspaceActions.openToolShore("search", "Search")],
    ["tool.tasks", () => workspaceActions.openToolShore("tasks-list", "Tasks")],
    ["tool.library", () => workspaceActions.openToolShore("library", "Library")],
    ["tool.browser", () => workspaceActions.newBrowserShore("https://duckduckgo.com")],
    ["tool.notepad", () => workspaceSidebar.reveal("notepad")],
    ["tool.palette", () => paletteFeature.toggle()],
    ["tool.launcher", () => launcherEl.classList.contains("open")
      ? launcherFeature.close()
      : launcherFeature.open()],
    ["app.settings", () => settingsFeature.open()],
    ["app.zoom-in", () => appearanceFeature.increaseZoom()],
    ["app.zoom-out", () => appearanceFeature.decreaseZoom()],
    ["app.update", () => settingsFeature.open("updates")],
  ],
});

let clusterUpdateStaged = false;
function renderTitleCluster(): void {
  titleCluster.update({ updateStaged: clusterUpdateStaged, zoom: appearanceFeature.zoom });
}

const updaterFeature = createUpdaterFeature({
  document,
  invokeNative,
  shouldCheckOnLaunch: async () => {
    try {
      const result = await invoke<{ ok: boolean; value?: string }>(FrontendCommand.AppConfigGet, {
        key: "updates.checkOnLaunch",
      });
      return result.ok && result.value === "true";
    } catch (error) {
      console.warn("update launch preference unavailable", error);
      return false;
    }
  },
  onStagedChanged: (staged) => {
    clusterUpdateStaged = staged;
    renderTitleCluster();
  },
});
const harnessUpdateFeature = createHarnessUpdateFeature({
  storage: localStorage,
  invoke,
  launchTask: (command, displayName) =>
    launcherFeature.launchHarnessShellTask(command, displayName),
  showToast: (title, body, actions, timeoutMs) =>
    showInAppToast(title, body, () => {}, { actions, timeoutMs }),
});

function setupTitleCluster(): void {
  renderTitleCluster();
}

const browserAutomationFeature = createBrowserAutomationFeature({
  engineEvents,
  invoke,
  invokeBrowserAction,
});
const notificationFeature = createNotificationFeature({
  engineEvents,
  observe: onRyn,
  invoke,
  invokeNative,
  reveal: (nookId) => workspaceActions.revealNook(nookId),
  toast: (payload, onClick) => showInAppToast(payload.title, payload.body, onClick),
  warn: (message) => console.warn(message),
});
const agentStatusFeature = createAgentStatusFeature({
  engineEvents,
  storage: localStorage,
  invoke,
  needsInputCount: () => workspaceSidebar.needsInputCount,
  addNeedsInput: (nookId) => workspaceSidebar.addNeedsInput(nookId),
  removeNeedsInput: (nookId) => workspaceSidebar.removeNeedsInput(nookId),
  clearNeedsInput: () => workspaceSidebar.clearNeedsInput(),
  syncAgentNookStateClasses: () => workspaceSidebar.syncAgentNookStateClasses(),
  agentsVisible: () => workspaceSidebar.agentsVisible(),
  renderAgents: () => workspaceSidebar.renderContent("left"),
  refreshAgents: () => workspaceSidebar.refreshAgents(),
  showToast: (title, body) => showInAppToast(title, body, () => {}),
});

onboardingFeature = createOnboardingFeature({
  root: onboardingEl,
  backdrop: appearanceFeature.backdrop,
  getBackdropMaterial: () => appearanceFeature.backdropMaterial,
  updateBackdropMaterial: (material) => appearanceFeature.updateBackdropMaterial(material),
  getActiveThemeName: () => settingsFeature.activeThemeName,
  setAgentChimesEnabled: (enabled) => workspaceSidebar.setAgentChimesEnabled(enabled),
  agentChimesEnabled: () => workspaceSidebar.agentChimesEnabled(),
  mapLauncherAdapters: (adapters) => mapLauncherAdapters(adapters as AdapterInfo[]),
  launchHarnessShellTask: launcherFeature.launchHarnessShellTask,
  launcherYolo: launcherFeature.yolo,
  launcherYoloKey: launcherFeature.yoloKey,
});

function registerApplicationOwnership(): void {
  appLifecycle.own(disposeConsoleCapture);
  appLifecycle.own(disposeFrontendTransport);
  appLifecycle.own(() => shell.dispose());
  appLifecycle.own(() => toastComponent.dispose());
  appLifecycle.own(() => contextMenu.dispose());
  appLifecycle.own(() => paletteFeature.dispose());
  appLifecycle.own(() => actionCoordinator.dispose());
  appLifecycle.own(() => performanceHud.dispose());
  appLifecycle.own(() => workspaceSidebar.dispose());
  appLifecycle.own(() => shoreTabsFeature.dispose());
  appLifecycle.own(() => workspaceView.dispose());
  appLifecycle.own(() => workspaceActions.dispose());
  appLifecycle.own(() => splitChooser.dispose());
  appLifecycle.own(() => bayCreateFeature.dispose());
  appLifecycle.own(() => titleCluster.dispose());
  appLifecycle.own(() => brandFeature.dispose());
  appLifecycle.own(() => findFeature.dispose());
  appLifecycle.own(() => settingsFeature.dispose());
  appLifecycle.own(() => terminalPreferences.dispose());
  appLifecycle.own(() => launcherFeature.dispose());
  appLifecycle.own(() => inspectFeature.dispose());
  appLifecycle.own(() => onboardingFeature.dispose());
  appLifecycle.own(() => updaterFeature.dispose());
  appLifecycle.own(() => notificationFeature.dispose());
  appLifecycle.own(() => browserAutomationFeature.dispose());
  appLifecycle.own(() => harnessUpdateFeature.dispose());
  appLifecycle.own(() => agentStatusFeature.dispose());
  appLifecycle.own(() => workspaceSyncFeature.dispose());
  appLifecycle.own(() => engineEvents.dispose());
  appLifecycle.own(disposeBrowserSessions);
  appLifecycle.own(() => {
    contextMenu.close();
    splitChooser.close();
    workspaceActions.clearDropOverlay();
    document.getElementById("nook-menu")?.remove();
  });
  appLifecycle.listen(window, "beforeunload", () => {
    void shutdownApplication();
  });
}

async function shutdownApplication(): Promise<void> {
  try {
    await appLifecycle.dispose();
  } catch (error) {
    console.warn("application disposal failed", error);
  }
}

registerApplicationOwnership();

function showInAppToast(title: string, body: string, onClick: () => void, opts?: { actions?: ToastAction[]; timeoutMs?: number }): void {
  toastComponent.show(title, body, onClick, opts);
}

function setupConfigurationEvents(): void {
  engineEvents.register("config.changed", (payload) => {
    const key = payload?.key;
    if (key) {
      if (key.startsWith("appearance.")) { void settingsFeature.applyAppearance(key); }
      if (key.startsWith("terminal.")) { void terminalPreferences.load().then(() => { workspaceView.applySettings(); }); }
      if (settingsEl.classList.contains("open")) { settingsFeature.render(); }
    }
  });
}

engineEvents.start(() => {
  setupConfigurationEvents();
  browserAutomationFeature.start();
  agentStatusFeature.start();
  workspaceSyncFeature.start();
  notificationFeature.start();
  engineEvents.register("dictation.model", (payload) => {
    if (payload?.error) onboardingFeature.setDictationModelError(payload.error);
    else if (payload?.ready) onboardingFeature.setDictationModelError(null);
  });
});

async function startApplication(): Promise<void> {
  await terminalPreferences.load();
  workspaceView.applySettings();
  await settingsFeature.start();
  void settingsFeature.applyAppearance(null);
  try {
    const bayRes = await invoke<{ ok: boolean; value?: string }>(FrontendCommand.AppConfigGet, { key: "bays.defaultDir" });
    workspaceSidebar.setDefaultDirectory(bayRes.ok ? (bayRes.value ?? "") : "");
  } catch { workspaceSidebar.setDefaultDirectory(""); }
  void updaterFeature.start().catch((error) => {
    console.warn("boot update check failed", error);
  });
  harnessUpdateFeature.start();
  await workspaceSidebar.loadModel();
  workspaceSidebar.applyModel();
  actionCoordinator.start();
  void actionCoordinator.reloadKeymap();
  setupTitleCluster();
  void appearanceFeature.applyZoom();
  try { await invokeNative(FrontendCommand.WindowCenter, {}); } catch (err) { console.warn("window center failed", err); }
  const dictationFeature = setupDictation({
    invoke: (cmd, args) => invokeNative(cmd, args ?? {}),
    events: engineEvents,
    getFocusedNookId: () => workspace.focusedNookId,
    writeNook: (nookId, dataBase64) => enqueueNookWrite(nookId, dataBase64, (id, b64) => invoke(FrontendCommand.AppNookWrite, { nookId: id, dataBase64: b64 }).then(() => undefined)),
  });
  appLifecycle.own(() => dictationFeature.dispose());
  void appearanceFeature.initializeBackdrop();
  void workspaceSidebar.loadBayBoxes();
  void launcherFeature.load();
  await workspaceActions.reload();
  await shoreTabsFeature.loadWings();
  shoreTabsFeature.render();
  workspaceSidebar.startAgentPolling();
  void agentStatusFeature.maybeShowRestoreToast();
  void onboardingFeature.maybeShow();
}

void startApplication().catch(async (error) => {
  console.warn("application startup failed", error);
  await shutdownApplication();
});
