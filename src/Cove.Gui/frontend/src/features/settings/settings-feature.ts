import { FrontendCommand } from "../../app/frontend-command";
import { LifecycleScope, type ComponentHandle } from "../../app/lifecycle";
import { createSurfaceMotion } from "../../app/surface-motion";
import { orderSettingsTabs, settingsTabMetadata, resolveActiveSettingsTab } from "../../settings-tabs";
import { adapterIconSvg, iconSvg } from "../../icons";
import { adapterStatusMeta, toolsSubtitle, retentionChipVisible, retentionChipLabel, projectToolsAdapters, type ToolsAdapter } from "../../tools-tab";
import {
  deriveProfileSlug, isValidProfileSlug, profilePickerLabel, selectedLauncherProfile, launcherProfileChoices, envMapFromRows,
  type LaunchProfileListItem, type LaunchProfileDetail, type CreateProfileInput, type UpdateProfileInput,
} from "../../profiles";
import { DEFAULT_DRAFT, draftFromTheme, themeFromDraft, cssVarsFromTheme, xtermThemeFromDto, canSaveDraft, canDelete, isValidHex, contrastRatio, contrastTier, THEME_COLOR_FIELDS, type ThemeDto, type ThemeDraft } from "../../theme-editor";
import { categorizeBindings, isReservedChord, isValidChord, chordDisplay, canRecordChord, normalizeChord as normalizeChordStr, type KeybindDto } from "../../keyboard-editor";
import { playChime } from "../../chime";
import { parseSnapshotExport, snapshotRows, summarizeSnapshots, formatBytes as formatSnapshotBytes, type DiagnosticsSnapshot } from "../../diagnostics-snapshot";
import { initialPerfBundlesState, applyBundleList, beginCreate, finishCreate, surfaceError, requestDelete, cancelDelete, bundleRows, PERF_BUNDLES_EMPTY_TEXT, type PerfBundlesState, type PerfBundleListResult, type PerfBundleDto } from "../../perf-bundles";
import type { TerminalSettings } from "../../terminal-session";

export interface SettingsFeatureDependencies {
  document: Document;
  storage: Storage;
  root: HTMLElement;
  tabs: HTMLElement;
  body: HTMLElement;
  grid: HTMLElement;
  invoke<T>(command: FrontendCommand, args: unknown): Promise<T>;
  invokeNative<T>(command: FrontendCommand, args: Record<string, unknown>): Promise<T>;
  terminalSettings: TerminalSettings;
  loadTerminalSettings(): Promise<Partial<TerminalSettings>>;
  applyTerminalSettings(): void;
  defaultTerminalTheme: Record<string, string>;
  themeBackgroundWithOpacity(opacity: number): string;
  applyTerminalTheme(theme: Record<string, string>): void;
  focusActiveNook(): void;
  renderDictationTab(container: HTMLElement): void;
  rerunOnboarding(): void;
  renderUpdates(container: HTMLElement): void;
  setAgentChimesEnabled(enabled: boolean): void;
  agentChimesEnabled(): boolean;
  showToast(title: string, body: string, onClick: () => void): void;
  launcherProfiles(): ReadonlyMap<string, LaunchProfileListItem[]>;
  activeProjectDir(): string | null;
  isPerfHudEnabled(): boolean;
  togglePerfHud(): void;
  reloadKeymap(): Promise<void>;
  applyMarkdownSettings(raw: Record<string, string>): void | Promise<void>;
}

export interface SettingsFeature extends ComponentHandle {
  readonly activeTheme: ThemeDto | null;
  readonly activeThemeName: string | null;
  readonly activeTab: string | null;
  open(tab?: string): void;
  close(): void;
  render(): void;
  start(): Promise<void>;
  applyAppearance(changedKey: string | null): Promise<void>;
  resolveLauncherProfileSlug(adapter: string): Promise<string>;
  launcherProfileSlugKey(adapter: string): string;
  openProfileEditor(adapter: { name: string; binary: string }, slug: string | null, onSaved: (savedSlug: string) => void | Promise<void>): void;
}

export function createSettingsFeature(dependencies: SettingsFeatureDependencies): SettingsFeature {
  const lifecycle = new LifecycleScope();
  const document = dependencies.document;
  const storage = dependencies.storage;
  const settingsEl = dependencies.root;
  const surfaceMotion = createSurfaceMotion(settingsEl);
  const setTabsEl = dependencies.tabs;
  const setBodyEl = dependencies.body;
  const gridEl = dependencies.grid;
  const settings = dependencies.terminalSettings;
  const invoke = dependencies.invoke;
  const invokeNative = dependencies.invokeNative;
  const loadSettings = dependencies.loadTerminalSettings;
  const applySettings = dependencies.applyTerminalSettings;
  const themeBackgroundWithOpacity = dependencies.themeBackgroundWithOpacity;
  const showInAppToast = dependencies.showToast;
  const setAgentChimesEnabled = dependencies.setAgentChimesEnabled;
  const agentChimesEnabled = dependencies.agentChimesEnabled;
  const activeProjectDir = dependencies.activeProjectDir;
  const reloadKeymap = dependencies.reloadKeymap;
  const onboardingFeature = { renderDictationTab: dependencies.renderDictationTab, rerun: dependencies.rerunOnboarding };
  const updaterFeature = { renderSettings: dependencies.renderUpdates };
  const launcherFeature = { get profiles() { return dependencies.launcherProfiles(); } };
  const perfHudState = { get enabled() { return dependencies.isPerfHudEnabled(); } };
  const doTogglePerfHud = dependencies.togglePerfHud;
  const ownedOverlays = new Set<HTMLElement>();
  let activeTheme: ThemeDto | null = null;

interface ConfigSchemaEntry { key: string; label: string; tab: string; control: string; description: string | null; type: string; options: string[] | null; }

let configSchema: ConfigSchemaEntry[] = [];

let activeSettingsTab: string | null = null;
let renderGeneration = 0;
let previousFocus: HTMLElement | null = null;
let lastToolsAdapters: ToolsAdapter[] | null = null;

async function loadConfigSchema(): Promise<void> {
  try {
    const res = await invoke<{ entries: ConfigSchemaEntry[] }>(FrontendCommand.ConfigSchema, {});
    configSchema = res.entries ?? [];
  } catch {
    configSchema = [];
  }
}

function openSettings(tab?: string): void {
  const focused = document.activeElement;
  previousFocus = focused && focused !== document.body && "focus" in focused ? focused as HTMLElement : null;
  if (tab) activeSettingsTab = tab;
  if (configSchema.length === 0) {
    void loadConfigSchema().then(() => renderSettings());
  } else {
    renderSettings();
  }
  surfaceMotion.open();
  settingsEl.focus();
}

function closeSettings(): void {
  ++renderGeneration;
  cancelKeybindRecording();
  surfaceMotion.close();
  if (previousFocus?.isConnected) previousFocus.focus();
  else dependencies.focusActiveNook();
}

function isRealSetting(e: ConfigSchemaEntry): boolean {
  return e.control !== "section" && e.type !== "object";
}

function renderSettings(): void {
  const generation = ++renderGeneration;
  const schemaTabs = [...new Set(configSchema.filter(isRealSetting).map((e) => e.tab))].sort();
  const tabs = orderSettingsTabs(schemaTabs);
  if (tabs.length === 0) {
    setTabsEl.innerHTML = "";
    setBodyEl.innerHTML = '<div class="set-page"><div class="set-page-scroll"><div class="set-empty">No settings available</div></div><div class="set-page-footer"></div></div>';
    return;
  }
  activeSettingsTab = resolveActiveSettingsTab(tabs, activeSettingsTab);

  setTabsEl.innerHTML = "";
  setTabsEl.setAttribute("role", "tablist");
  setTabsEl.setAttribute("aria-label", "Settings categories");
  let currentGroup = "";
  for (const tab of tabs) {
    const metadata = settingsTabMetadata(tab);
    if (metadata.group !== currentGroup) {
      currentGroup = metadata.group;
      const group = document.createElement("div");
      group.className = "set-nav-group-label";
      group.textContent = currentGroup;
      setTabsEl.appendChild(group);
    }
    const el = document.createElement("button");
    el.type = "button";
    el.className = "set-nav-item" + (tab === activeSettingsTab ? " active" : "");
    el.id = `set-tab-${tab}`;
    el.setAttribute("role", "tab");
    el.setAttribute("aria-selected", String(tab === activeSettingsTab));
    el.setAttribute("aria-controls", `set-panel-${tab}`);
    el.tabIndex = tab === activeSettingsTab ? 0 : -1;
    const tabIcon = document.createElement("span");
    tabIcon.className = "set-nav-icon";
    tabIcon.innerHTML = iconSvg(metadata.icon);
    const label = document.createElement("span");
    label.className = "set-nav-label";
    label.textContent = metadata.label;
    el.appendChild(tabIcon);
    el.appendChild(label);
    el.addEventListener("click", () => { activeSettingsTab = tab; renderSettings(); });
    el.addEventListener("keydown", (event) => navigateSettingsTabs(event as KeyboardEvent, tabs, tab));
    setTabsEl.appendChild(el);
  }

  setBodyEl.innerHTML = "";
  if (!activeSettingsTab) return;
  const metadata = settingsTabMetadata(activeSettingsTab);
  const page = document.createElement("section");
  page.className = "set-page";
  page.id = `set-panel-${activeSettingsTab}`;
  page.setAttribute("role", "tabpanel");
  page.setAttribute("aria-labelledby", `set-tab-${activeSettingsTab}`);
  const header = document.createElement("header");
  header.className = "set-page-header";
  const pageIcon = document.createElement("span");
  pageIcon.className = "set-page-icon";
  pageIcon.innerHTML = iconSvg(metadata.icon);
  const identity = document.createElement("div");
  identity.className = "set-page-identity";
  const heading = document.createElement("h2");
  heading.textContent = metadata.label;
  const description = document.createElement("p");
  description.textContent = activeSettingsTab === "tools"
    ? "Manage coding harnesses, launch profiles, and installation status."
    : metadata.description;
  identity.append(heading, description);
  header.append(pageIcon, identity);
  const scroll = document.createElement("div");
  scroll.className = "set-page-scroll";
  const content = document.createElement("div");
  content.className = "set-page-content";
  scroll.appendChild(content);
  const footer = document.createElement("footer");
  footer.className = "set-page-footer";
  page.append(header, scroll, footer);
  setBodyEl.appendChild(page);
  renderAutoApplyFooter(footer);
  if (activeSettingsTab === "theme") {
    renderThemeEditor(content, footer, generation, activeSettingsTab);
    return;
  }
  if (activeSettingsTab === "keyboard") {
    renderKeyboardEditor(content, generation, activeSettingsTab);
    return;
  }
  if (activeSettingsTab === "tools") {
    void renderToolsTab(content, generation);
    return;
  }
  if (activeSettingsTab === "dictation") {
    onboardingFeature.renderDictationTab(content);
    groupSettingsContent(content);
    return;
  }
  const entries = configSchema.filter((e) => e.tab === activeSettingsTab && (e.control === "section" || isRealSetting(e)));
  for (const entry of entries) {
    if (entry.control === "section") {
      const header = document.createElement("div");
      header.className = "set-section-header";
      const sectionTitle = document.createElement("span");
      sectionTitle.textContent = entry.label;
      header.appendChild(sectionTitle);
      if (entry.description) {
        const sectionDescription = document.createElement("span");
        sectionDescription.className = "set-section-description";
        sectionDescription.textContent = entry.description;
        header.appendChild(sectionDescription);
      }
      content.appendChild(header);
      continue;
    }
    const row = document.createElement("div");
    row.className = "set-row";
    const label = document.createElement("label");
    const controlId = `set-control-${entry.key.replace(/[^a-zA-Z0-9_-]/g, "-")}`;
    label.htmlFor = controlId;
    const labelText = document.createElement("span");
    labelText.textContent = entry.label;
    label.appendChild(labelText);
    if (entry.description) {
      const desc = document.createElement("span");
      desc.className = "set-desc";
      desc.textContent = entry.description;
      label.appendChild(desc);
    }
    row.appendChild(label);

    void loadSettingValue(entry, row, controlId, generation, activeSettingsTab);
    content.appendChild(row);
  }
  if (activeSettingsTab === "diagnostics") renderDiagnosticsExtras(content);
  if (activeSettingsTab === "updates") updaterFeature.renderSettings(content);
  if (activeSettingsTab === "audio") renderAudioExtras(content);
  groupSettingsContent(content);
}

function navigateSettingsTabs(event: KeyboardEvent, tabs: string[], tab: string): void {
  const keys = ["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "Home", "End"];
  if (!keys.includes(event.key)) {
    if ((event.key === "Enter" || event.key === " ") && activeSettingsTab !== tab) { event.preventDefault(); activeSettingsTab = tab; renderSettings(); }
    return;
  }
  event.preventDefault();
  const current = tabs.indexOf(tab);
  const next = event.key === "Home" ? 0 : event.key === "End" ? tabs.length - 1 : (current + (event.key === "ArrowUp" || event.key === "ArrowLeft" ? -1 : 1) + tabs.length) % tabs.length;
  for (const button of setTabsEl.querySelectorAll<HTMLElement>('[role="tab"]')) button.tabIndex = -1;
  const target = setTabsEl.querySelector<HTMLElement>(`#set-tab-${tabs[next]}`);
  if (target) { target.tabIndex = 0; target.focus(); target.scrollIntoView({ block: "nearest", inline: "nearest" }); }
}

function renderAutoApplyFooter(footer: HTMLElement): void {
  const status = document.createElement("span");
  status.className = "set-footer-status";
  status.textContent = "Changes apply immediately";
  const done = document.createElement("button");
  done.type = "button";
  done.className = "set-action set-action-primary";
  done.textContent = "Done";
  done.addEventListener("click", closeSettings);
  footer.replaceChildren(status, done);
}

function groupSettingsContent(content: HTMLElement): void {
  const nodes = [...content.children];
  if (nodes.length === 0) return;
  content.replaceChildren();
  let group: HTMLElement | null = null;
  for (const node of nodes) {
    if (node.classList.contains("set-section-header") || !group) {
      group = document.createElement("section");
      group.className = "set-group";
      const title = node.classList.contains("set-section-header") ? node : document.createElement("div");
      if (!node.classList.contains("set-section-header")) { title.className = "set-section-header"; title.textContent = "General"; }
      group.appendChild(title);
      content.appendChild(group);
      if (node.classList.contains("set-section-header")) continue;
    }
    group.appendChild(node);
  }
}

function renderAudioExtras(container: HTMLElement): void {
  const header = document.createElement("div");
  header.className = "tools-profiles-header";
  header.className = "set-section-header";
  header.textContent = "Sound";
  container.appendChild(header);

  const row = document.createElement("div");
  row.className = "set-row";
  const label = document.createElement("label");
  const labelText = document.createElement("span");
  labelText.textContent = "Agent chimes";
  const desc = document.createElement("span");
  desc.className = "set-desc";
  desc.textContent = "Play a soft tone when an agent finishes or needs input. On by default.";
  label.appendChild(labelText);
  label.appendChild(desc);

  const controls = document.createElement("div");
  const preview = document.createElement("button");
  preview.className = "set-utility-btn";
  preview.textContent = "Preview";
  preview.addEventListener("click", () => playChime("done"));
  const toggle = document.createElement("button");
  const paint = (): void => {
    const on = agentChimesEnabled();
    toggle.className = "set-utility-toggle" + (on ? " on" : "");
    toggle.textContent = on ? "On" : "Off";
    preview.disabled = !on;
  };
  toggle.addEventListener("click", () => {
    const next = !agentChimesEnabled();
    setAgentChimesEnabled(next);
    if (next) playChime("done");
    paint();
  });
  paint();
  controls.appendChild(preview);
  controls.appendChild(toggle);

  row.appendChild(label);
  row.appendChild(controls);
  container.appendChild(row);
}

interface ToolsListResponse { adapters: ToolsAdapter[]; }

async function renderToolsTab(container: HTMLElement, generation = renderGeneration): Promise<void> {
  container.innerHTML = "";

  const actions = document.createElement("div");
  actions.className = "tools-actions";
  const rescanBtn = document.createElement("button");
  rescanBtn.type = "button";
  rescanBtn.className = "set-action set-action-secondary";
  rescanBtn.textContent = "Rescan";
  rescanBtn.addEventListener("click", () => void doRescanAdapters(container, rescanBtn, generation));
  const addBtn = document.createElement("button");
  addBtn.type = "button";
  addBtn.className = "set-action set-action-secondary";
  addBtn.textContent = "Add adapter from folder…";
  addBtn.addEventListener("click", () => void doAddAdapterFromFolder(container, generation));
  const wizardBtn = document.createElement("button");
  wizardBtn.type = "button";
  wizardBtn.className = "set-action set-action-secondary";
  wizardBtn.textContent = "Re-run setup wizard";
  wizardBtn.addEventListener("click", () => onboardingFeature.rerun());
  actions.appendChild(rescanBtn);
  actions.appendChild(addBtn);
  actions.appendChild(wizardBtn);
  container.appendChild(actions);

  const state = document.createElement("div");
  state.className = lastToolsAdapters ? "tools-state tools-state-refreshing" : "tools-state tools-state-loading";
  state.setAttribute("role", "status");
  state.textContent = lastToolsAdapters ? "Refreshing tools…" : "Loading tools…";
  container.appendChild(state);
  if (lastToolsAdapters) renderToolsGroups(container, lastToolsAdapters, generation);

  try {
    const result = await invoke<ToolsListResponse>(FrontendCommand.AdapterToolsList, {});
    if (!isCurrentToolsDestination(container, generation)) return;
    lastToolsAdapters = result.adapters ?? [];
    container.querySelector(".tools-state")?.remove();
    for (const group of container.querySelectorAll(".tools-section")) group.remove();
    renderToolsGroups(container, lastToolsAdapters, generation);
  } catch (err) {
    console.warn("adapter.tools-list failed for tools tab", err);
    if (!isCurrentToolsDestination(container, generation)) return;
    state.className = "tools-state tools-state-error";
    state.textContent = "Tools could not be refreshed.";
    const retry = document.createElement("button");
    retry.type = "button";
    retry.className = "set-action set-action-secondary";
    retry.textContent = "Retry";
    retry.addEventListener("click", () => void renderToolsTab(container));
    state.appendChild(retry);
  }
}

function isCurrentToolsDestination(container: HTMLElement, generation: number): boolean {
  return generation === renderGeneration && container.isConnected && activeSettingsTab === "tools" && container.closest("#set-panel-tools") !== null;
}

function renderToolsGroups(container: HTMLElement, adapters: ToolsAdapter[], generation: number): void {
  const groups = projectToolsAdapters(adapters);
  buildToolsGroup(container, "installed", "Installed", "Ready to launch or needing attention.", groups.installed, true, generation);
  buildToolsGroup(container, "available", "Available to install", "Catalog tools with an existing installation path.", groups.available, false, generation);
  if (groups.unavailable.length > 0) buildToolsGroup(container, "unavailable", "Unavailable", "Catalog tools without an installation action on this system.", groups.unavailable, false, generation);
}

function buildToolsGroup(container: HTMLElement, key: string, title: string, helper: string, adapters: ToolsAdapter[], installed: boolean, generation: number): void {
  const section = document.createElement("section");
  section.className = `set-group tools-section tools-section-${key}`;
  const heading = document.createElement("div");
  heading.className = "set-section-header";
  const titleEl = document.createElement("h3");
  titleEl.className = "tools-section-title";
  titleEl.id = `tools-section-${key}`;
  titleEl.textContent = `${title} · ${adapters.length}`;
  const helperEl = document.createElement("span");
  helperEl.className = "set-section-description";
  helperEl.textContent = helper;
  heading.append(titleEl, helperEl);
  section.appendChild(heading);
  const list = document.createElement("div");
  list.className = "tools-list";
  list.setAttribute("role", "list");
  list.setAttribute("aria-labelledby", titleEl.id);
  if (adapters.length === 0) {
    const empty = document.createElement("div");
    empty.className = "tools-empty";
    empty.textContent = installed ? "No installed tools" : "No tools available to install";
    list.appendChild(empty);
  } else {
    for (const adapter of adapters) list.appendChild(buildToolsCard(adapter, container, installed, generation));
  }
  section.appendChild(list);
  container.appendChild(section);
}

function buildToolsCard(a: ToolsAdapter, container: HTMLElement, installed: boolean, generation: number): HTMLElement {
  const card = document.createElement("div");
  card.className = "tools-card";
  card.setAttribute("role", "article");
  card.style.setProperty("--adapter-accent", a.accent || "var(--accent)");

  const icon = document.createElement("span");
  icon.className = "tools-icon";
  icon.innerHTML = a.iconSvg || adapterIconSvg(a.name);
  const iconGraphic = icon.querySelector("svg, img, .adapter-icon-mask");
  if (iconGraphic) iconGraphic.setAttribute("aria-hidden", "true");
  card.appendChild(icon);

  const body = document.createElement("div");
  body.className = "tools-body";

  const titleRow = document.createElement("div");
  titleRow.className = "tools-title-row";
  const name = document.createElement("span");
  name.className = "tools-name";
  name.textContent = a.displayName || a.name;
  name.title = name.textContent;
  titleRow.appendChild(name);

  const meta = adapterStatusMeta(a.status);
  const status = document.createElement("span");
  status.className = "tools-status";
  status.dataset.status = meta.label;
  const dot = document.createElement("span");
  dot.className = "tools-dot";
  const statusLabel = document.createElement("span");
  statusLabel.textContent = meta.label;
  status.appendChild(dot);
  status.appendChild(statusLabel);
  titleRow.appendChild(status);
  body.appendChild(titleRow);

  const subtitle = document.createElement("div");
  subtitle.className = "tools-subtitle";
  subtitle.textContent = toolsSubtitle(a.status, a.version, a.binaryPath, a.installHint);
  subtitle.title = subtitle.textContent;
  body.appendChild(subtitle);

  const manifestRow = document.createElement("div");
  manifestRow.className = "tools-manifest";
  const manifestName = document.createElement("span");
  manifestName.textContent = a.bundled ? `${a.name} · bundled` : a.name;
  manifestRow.appendChild(manifestName);
  if (a.removable) {
    const removeBtn = document.createElement("button");
    removeBtn.className = "set-utility-btn";
    removeBtn.type = "button";
    removeBtn.textContent = "Remove";
    removeBtn.setAttribute("aria-label", `Remove ${a.displayName || a.name}`);
    removeBtn.addEventListener("click", () => openRemoveAdapterDialog(a, container, generation));
    manifestRow.appendChild(removeBtn);
  }
  body.appendChild(manifestRow);

  if (retentionChipVisible(a.retention)) body.appendChild(buildRetentionChip(a, container, generation));

  if (installed) void buildProfilesSection(a, container, generation).then((el) => {
    if (isCurrentToolsDestination(container, generation)) body.appendChild(el);
  });

  card.appendChild(body);
  return card;
}

function isCurrentPageDestination(container: HTMLElement, tab: string): boolean {
  return container.isConnected && activeSettingsTab === tab && container.closest(`#set-panel-${tab}`) !== null;
}

const launcherProfileSlugKey = (adapter: string) => `cove:launcher-profile:${adapter}`;

interface ProfileListResult { profiles: LaunchProfileListItem[] }

async function resolveLauncherProfileSlug(adapter: string): Promise<string> {
  const stored = storage.getItem(launcherProfileSlugKey(adapter));
  const cached = launcherFeature.profiles.get(adapter);
  if (cached) return selectedLauncherProfile(launcherProfileChoices(adapter, cached), stored)?.slug ?? "default";
  try {
    const result = await invoke<ProfileListResult>(FrontendCommand.LaunchProfileList, { adapter });
    return selectedLauncherProfile(launcherProfileChoices(adapter, result.profiles ?? []), stored)?.slug ?? "default";
  } catch (err) {
    console.warn("launch-profile.list failed", adapter, err);
    return "default";
  }
}

async function buildProfilesSection(a: ToolsAdapter, container: HTMLElement, generation: number): Promise<HTMLElement> {
  const section = document.createElement("div");
  section.className = "tools-profiles";

  const header = document.createElement("div");
  const label = document.createElement("span");
  label.className = "tools-subtitle";
  label.textContent = "Launch profiles";
  header.appendChild(label);

  const newBtn = document.createElement("button");
  newBtn.type = "button";
  newBtn.className = "set-utility-btn";
  newBtn.textContent = "New profile";
  newBtn.setAttribute("aria-label", `New profile for ${a.displayName || a.name}`);
  newBtn.addEventListener("click", () => openProfileEditor(a, null, () => {
    if (isCurrentToolsDestination(container, generation)) return renderToolsTab(container);
  }));
  header.appendChild(newBtn);
  section.appendChild(header);

  const listEl = document.createElement("div");
  listEl.className = "tools-profiles-list";
  section.appendChild(listEl);

  try {
    const result = await invoke<ProfileListResult>(FrontendCommand.LaunchProfileList, { adapter: a.name });
    if (!isCurrentToolsDestination(container, generation)) return section;
    renderProfileList(a, result.profiles ?? [], listEl, container, generation);
  } catch (err) {
    console.warn("launch-profile.list failed", a.name, err);
    const note = document.createElement("div");
    note.className = "tools-subtitle";
    note.textContent = "profiles unavailable";
    listEl.appendChild(note);
  }
  return section;
}

function renderProfileList(
  a: ToolsAdapter,
  profiles: LaunchProfileListItem[],
  listEl: HTMLElement,
  container: HTMLElement,
  generation: number,
): void {
  listEl.innerHTML = "";
  if (profiles.length === 0) {
    const empty = document.createElement("div");
    empty.className = "tools-subtitle";
    empty.textContent = "no profiles — using defaults";
    listEl.appendChild(empty);
    return;
  }
  for (const p of profiles) {
    const row = document.createElement("div");
    row.className = "tools-profile-row";
    const radio = document.createElement("input");
    radio.type = "radio";
    radio.name = `profile-radio-${a.name}`;
    radio.setAttribute("aria-label", `Select ${p.name} profile for ${a.displayName || a.name}`);
    const storedSlug = storage.getItem(launcherProfileSlugKey(a.name));
    radio.checked = storedSlug ? storedSlug === p.slug : p.isDefault;
    radio.addEventListener("change", () => {
      storage.setItem(launcherProfileSlugKey(a.name), p.slug);
      void invoke(FrontendCommand.LaunchProfileSetDefault, { adapter: a.name, slug: p.slug }).catch((err) => console.warn("launch-profile.set-default failed", a.name, p.slug, err));
    });
    row.appendChild(radio);
    const name = document.createElement("span");
    name.className = "tools-profile-name";
    name.textContent = profilePickerLabel(p);
    row.appendChild(name);
    const editBtn = document.createElement("button");
    editBtn.type = "button";
    editBtn.className = "set-utility-btn";
    editBtn.textContent = "Edit";
    editBtn.setAttribute("aria-label", `Edit ${p.name} profile for ${a.displayName || a.name}`);
    editBtn.addEventListener("click", () => openProfileEditor(a, p.slug, () => {
      if (isCurrentToolsDestination(container, generation)) return renderToolsTab(container);
    }));
    row.appendChild(editBtn);
    if (profiles.length > 1) {
      const delBtn = document.createElement("button");
      delBtn.type = "button";
      delBtn.className = "set-utility-btn set-action-destructive";
      delBtn.textContent = "Delete";
      delBtn.setAttribute("aria-label", `Delete ${p.name} profile for ${a.displayName || a.name}`);
      delBtn.addEventListener("click", async () => {
        await invoke(FrontendCommand.LaunchProfileDelete, { adapter: a.name, slug: p.slug });
        if (!isCurrentToolsDestination(container, generation)) return;
        await renderToolsTab(container);
      });
      row.appendChild(delBtn);
    }
    listEl.appendChild(row);
  }
}

function openProfileEditor(
  a: { name: string; binary: string },
  slug: string | null,
  onSaved: (savedSlug: string) => void | Promise<void>,
): void {
  const overlay = document.createElement("div");
  overlay.className = "settings-dialog-overlay";
  const dialog = document.createElement("div");
  dialog.className = "settings-dialog";
  dialog.addEventListener("click", (e) => e.stopPropagation());

  const title = document.createElement("div");
  title.className = "settings-dialog-title";
  title.textContent = slug ? `Edit profile · ${slug}` : `New profile · ${a.name}`;
  dialog.appendChild(title);

  const nameInput = textRow(dialog, "Profile name", "", "e.g. Claude Code (ccx)",
    "What this profile is called in the launcher");
  const slugInput = textRow(dialog, "Profile ID", slug ?? "", "auto-generated from the name",
    "Lowercase identifier used by the CLI and config; leave empty to generate it from the name");
  slugInput.disabled = slug !== null;
  const commandInput = textRow(dialog, "Command", "", `defaults to ${a.binary}`,
    `Program to launch instead of ${a.binary} — a name on PATH or a full path like ~/bin/my-wrapper`);
  const modelInput = textRow(dialog, "Model", "", "e.g. opus, sonnet, default",
    "Passed to the CLI as its model flag; empty uses the tool's own default");
  const effortInput = textRow(dialog, "Reasoning effort", "", "e.g. low, medium, high, max",
    "Passed to the CLI when set; empty uses the tool's own default");
  const agentInput = textRow(dialog, "Agent", "", "optional",
    "ID of a saved agent definition to launch with this profile");

  const envLabel = document.createElement("div");
  envLabel.className = "tools-subtitle";
  envLabel.textContent = "Environment variables (KEY=VALUE, one per line)";
  dialog.appendChild(envLabel);
  const envArea = document.createElement("textarea");
  envArea.className = "settings-dialog-textarea";
  envArea.rows = 4;
  dialog.appendChild(envArea);

  const argsLabel = document.createElement("div");
  argsLabel.className = "tools-subtitle";
  argsLabel.textContent = "Extra CLI args (one per line)";
  dialog.appendChild(argsLabel);
  const argsArea = document.createElement("textarea");
  argsArea.className = "settings-dialog-textarea";
  argsArea.rows = 3;
  dialog.appendChild(argsArea);

  const defaultCb = document.createElement("input");
  defaultCb.type = "checkbox";
  const defaultLabel = document.createElement("label");
  defaultLabel.className = "settings-dialog-check";
  defaultLabel.appendChild(defaultCb);
  const defaultText = document.createElement("span");
  defaultText.textContent = "Set as default profile";
  defaultLabel.appendChild(defaultText);
  dialog.appendChild(defaultLabel);

  const errorEl = document.createElement("div");
  errorEl.className = "settings-dialog-error";
  dialog.appendChild(errorEl);

  const buttonRow = document.createElement("div");
  buttonRow.className = "settings-dialog-actions";
  const cancelBtn = document.createElement("button");
  cancelBtn.className = "set-utility-btn";
  cancelBtn.textContent = "Cancel";
  cancelBtn.addEventListener("click", () => overlay.remove());
  const saveBtn = document.createElement("button");
  saveBtn.className = "set-utility-btn";
  saveBtn.textContent = slug ? "Save" : "Create";
  saveBtn.addEventListener("click", async () => {
    errorEl.textContent = "";
    const newSlug = deriveProfileSlug(slugInput.value) || deriveProfileSlug(nameInput.value);
    if (!slug && !isValidProfileSlug(newSlug)) {
      errorEl.textContent = "enter a profile name, or an ID using lowercase letters, digits, and dashes";
      return;
    }
    const envRows: Array<{ key: string; value: string }> = [];
    for (const line of envArea.value.split("\n")) {
      const trimmed = line.trim();
      if (!trimmed) continue;
      const eq = trimmed.indexOf("=");
      if (eq < 0) { errorEl.textContent = `env line "${trimmed}" must be KEY=VALUE`; return; }
      envRows.push({ key: trimmed.slice(0, eq).trim(), value: trimmed.slice(eq + 1).trim() });
    }
    const env = envMapFromRows(envRows);
    const extraArgLines = argsArea.value.split("\n").map((s) => s.trim()).filter((s) => s.length > 0);
    const commandValue = commandInput.value.trim();
    const cliArgs = commandValue
      ? [commandValue, ...extraArgLines]
      : extraArgLines.length > 0
        ? [a.binary, ...extraArgLines]
        : [];
    const base = {
      adapter: a.name,
      slug: slug ?? newSlug,
      name: nameInput.value.trim() || (slug ?? newSlug),
      model: modelInput.value.trim() || null,
      effort: effortInput.value.trim() || null,
      cliArgs,
      env,
      agent: agentInput.value.trim() || null,
      isDefault: defaultCb.checked,
    };
    try {
      if (slug) {
        const update: UpdateProfileInput = base;
        await invoke(FrontendCommand.LaunchProfileUpdate, update);
      } else {
        const create: CreateProfileInput = base;
        await invoke(FrontendCommand.LaunchProfileCreate, create);
      }
      overlay.remove();
      await onSaved(base.slug);
    } catch (err) {
      errorEl.textContent = String(err);
    }
  });
  buttonRow.appendChild(cancelBtn);
  buttonRow.appendChild(saveBtn);
  dialog.appendChild(buttonRow);

  overlay.appendChild(dialog);
  overlay.addEventListener("click", () => overlay.remove());
  ownedOverlays.add(overlay);
  document.body.appendChild(overlay);

  if (slug) {
    void loadProfileIntoEditor(a.name, slug, { nameInput, commandInput, modelInput, effortInput, agentInput, envArea, argsArea, defaultCb });
  } else {
    defaultCb.checked = true;
  }
}

interface EditorFields {
  nameInput: HTMLInputElement;
  commandInput: HTMLInputElement;
  modelInput: HTMLInputElement;
  effortInput: HTMLInputElement;
  agentInput: HTMLInputElement;
  envArea: HTMLTextAreaElement;
  argsArea: HTMLTextAreaElement;
  defaultCb: HTMLInputElement;
}

async function loadProfileIntoEditor(adapter: string, slug: string, f: EditorFields): Promise<void> {
  try {
    const detail = await invoke<LaunchProfileDetail>(FrontendCommand.LaunchProfileGet, { adapter, slug });
    f.nameInput.value = detail.name;
    f.modelInput.value = detail.model ?? "";
    f.effortInput.value = detail.effort ?? "";
    f.agentInput.value = detail.agent ?? "";
    f.envArea.value = Object.entries(detail.env).map(([k, v]) => `${k}=${v}`).join("\n");
    const cliArgs = detail.cliArgs ?? [];
    f.commandInput.value = cliArgs[0] ?? "";
    f.argsArea.value = cliArgs.slice(1).join("\n");
    f.defaultCb.checked = detail.isDefault;
  } catch (err) {
    console.warn("launch-profile.get failed", adapter, slug, err);
  }
}

function textRow(parent: HTMLElement, label: string, value: string, placeholder: string, hint?: string): HTMLInputElement {
  const row = document.createElement("div");
  row.className = "settings-dialog-field";
  const lab = document.createElement("label");
  lab.className = "tools-subtitle";
  lab.textContent = label;
  row.appendChild(lab);
  if (hint) {
    const hintEl = document.createElement("div");
    hintEl.className = "settings-dialog-hint";
    hintEl.textContent = hint;
    row.appendChild(hintEl);
  }
  const input = document.createElement("input");
  input.className = "settings-dialog-input";
  input.type = "text";
  input.value = value;
  input.placeholder = placeholder;
  row.appendChild(input);
  parent.appendChild(row);
  return input;
}

function buildRetentionChip(a: ToolsAdapter, container: HTMLElement, generation: number): HTMLElement {
  const chip = document.createElement("div");
  chip.className = "tools-retention";
  const label = document.createElement("span");
  label.className = "set-desc";
  label.textContent = retentionChipLabel(a.retention);
  chip.appendChild(label);

  if (a.retention.editable) {
    const input = document.createElement("input");
    input.type = "text";
    input.className = "set-utility-input";
    input.value = a.retention.value ?? "";
    input.placeholder = a.retention.recommended ?? "";
    const save = document.createElement("button");
    save.className = "set-utility-btn";
    save.textContent = "Extend";
    save.addEventListener("click", () => void doSetRetention(a.name, input.value, container, generation));
    chip.appendChild(input);
    chip.appendChild(save);
  }
  return chip;
}

async function doRescanAdapters(container: HTMLElement, btn: HTMLButtonElement, generation: number): Promise<void> {
  btn.disabled = true;
  try {
    await invoke(FrontendCommand.AdapterRescan, {});
  } catch (e) {
    console.warn("adapter.rescan failed", e);
    showInAppToast("Re-scan failed", (e as Error).message, () => {});
  } finally {
    if (!isCurrentToolsDestination(container, generation)) return;
    btn.disabled = false;
    await renderToolsTab(container);
  }
}

async function doAddAdapterFromFolder(container: HTMLElement, generation: number): Promise<void> {
  let picked: unknown;
  try {
    picked = await invokeNative(FrontendCommand.DialogOpenFolder, { initialPath: activeProjectDir() || "/" });
  } catch (e) {
    console.warn("adapter folder picker failed", e);
    return;
  }
  if (!isCurrentToolsDestination(container, generation)) return;
  if (picked === null) return;
  const path = typeof picked === "string" ? picked.trim() : "";
  if (!path) { console.warn("adapter folder picker returned nothing", picked); return; }
  try {
    const res = await invoke<{ name: string }>(FrontendCommand.AdapterInstallLocal, { path });
    showInAppToast("Adapter added", `${res.name} installed from folder.`, () => {});
  } catch (e) {
    showInAppToast("Adapter not added", (e as Error).message, () => {});
  }
  if (!isCurrentToolsDestination(container, generation)) return;
  await renderToolsTab(container);
}

function openRemoveAdapterDialog(a: ToolsAdapter, container: HTMLElement, generation: number): void {
  const scrim = document.createElement("div");
  scrim.className = "modal-scrim open";
  const box = document.createElement("div");
  box.className = "settings-confirm-dialog";
  const title = document.createElement("div");
  title.className = "settings-dialog-title";
  title.textContent = `Remove ${a.displayName || a.name}?`;
  const desc = document.createElement("div");
  desc.className = "set-desc";
  desc.textContent = "This deletes the adapter folder. Bundled adapters are not affected.";
  const purgeLabel = document.createElement("label");
  purgeLabel.className = "settings-dialog-check";
  const purge = document.createElement("input");
  purge.type = "checkbox";
  const purgeText = document.createElement("span");
  purgeText.textContent = "Also delete session records for this adapter";
  purgeLabel.appendChild(purge);
  purgeLabel.appendChild(purgeText);
  const btnRow = document.createElement("div");
  btnRow.className = "settings-dialog-actions";
  const cancel = document.createElement("button");
  cancel.className = "set-utility-btn";
  cancel.textContent = "Cancel";
  const confirm = document.createElement("button");
  confirm.className = "set-utility-btn";
  confirm.textContent = "Remove";
  const close = (): void => scrim.remove();
  cancel.addEventListener("click", close);
  scrim.addEventListener("mousedown", (e) => { if (e.target === scrim) close(); });
  confirm.addEventListener("click", () => {
    close();
    void doRemoveAdapter(a.name, purge.checked, container, generation);
  });
  btnRow.appendChild(cancel);
  btnRow.appendChild(confirm);
  box.appendChild(title);
  box.appendChild(desc);
  box.appendChild(purgeLabel);
  box.appendChild(btnRow);
  scrim.appendChild(box);
  ownedOverlays.add(scrim);
  document.body.appendChild(scrim);
}

async function doRemoveAdapter(name: string, purgeSessions: boolean, container: HTMLElement, generation: number): Promise<void> {
  try {
    const res = await invoke<{ name: string; purgedSessions: number }>(FrontendCommand.AdapterRemove, { name, purgeSessions });
    const suffix = res.purgedSessions > 0 ? ` (${res.purgedSessions} session records purged)` : "";
    showInAppToast("Adapter removed", `${res.name} removed${suffix}.`, () => {});
  } catch (e) {
    showInAppToast("Remove failed", (e as Error).message, () => {});
  }
  if (!isCurrentToolsDestination(container, generation)) return;
  await renderToolsTab(container);
}

async function doSetRetention(name: string, value: string, container: HTMLElement, generation: number): Promise<void> {
  try {
    await invoke(FrontendCommand.AdapterRetentionSet, { name, value: value.trim() });
    showInAppToast("Retention updated", `${name} retention set to ${value.trim()}.`, () => {});
  } catch (e) {
    showInAppToast("Retention not saved", (e as Error).message, () => {});
  }
  if (!isCurrentToolsDestination(container, generation)) return;
  await renderToolsTab(container);
}

function diagnosticsSectionHeader(text: string): HTMLElement {
  const header = document.createElement("div");
  header.className = "set-section-header";
  header.textContent = text;
  return header;
}

function renderDiagnosticsExtras(container: HTMLElement): void {
  container.appendChild(diagnosticsSectionHeader("Performance overlay"));

  const hudRow = document.createElement("div");
  hudRow.className = "set-row";
  const hudLabel = document.createElement("label");
  const hudLabelText = document.createElement("span");
  hudLabelText.textContent = "Live HUD";
  const hudDesc = document.createElement("span");
  hudDesc.className = "set-desc";
  hudDesc.textContent = "In-page GUI frame rate, frame time, and webview JS heap. Off by default; also toggleable from the command palette.";
  hudLabel.appendChild(hudLabelText);
  hudLabel.appendChild(hudDesc);
  const hudToggle = document.createElement("button");
  hudToggle.className = "set-utility-toggle" + (perfHudState.enabled ? " on" : "");
  hudToggle.textContent = perfHudState.enabled ? "On" : "Off";
  hudToggle.addEventListener("click", () => doTogglePerfHud());
  hudRow.appendChild(hudLabel);
  hudRow.appendChild(hudToggle);
  container.appendChild(hudRow);

  container.appendChild(diagnosticsSectionHeader("Snapshot inspector"));
  const snapCaption = document.createElement("div");
  snapCaption.className = "set-utility-caption";
  snapCaption.textContent = "Capture a live diagnostics snapshot from the engine, or paste an exported one (a single object or an array — the same JSON the engine writes to diagnostics-snapshots.json inside a performance bundle).";
  container.appendChild(snapCaption);

  const textarea = document.createElement("textarea");
  textarea.className = "set-utility-input";
  textarea.placeholder = '{ "takenAt": "…", "managedMemoryBytes": … }';
  container.appendChild(textarea);

  const snapActions = document.createElement("div");
  snapActions.className = "set-utility-actions";
  container.appendChild(snapActions);

  const renderBtn = document.createElement("button");
  renderBtn.className = "set-utility-btn";
  renderBtn.textContent = "Inspect snapshot";
  snapActions.appendChild(renderBtn);

  const takeBtn = document.createElement("button");
  takeBtn.className = "set-utility-btn";
  takeBtn.textContent = "Take snapshot";
  snapActions.appendChild(takeBtn);

  const loadBtn = document.createElement("button");
  loadBtn.className = "set-utility-btn";
  loadBtn.textContent = "Load snapshots";
  snapActions.appendChild(loadBtn);

  const output = document.createElement("div");
  output.className = "set-utility-snap";
  container.appendChild(output);

  renderBtn.addEventListener("click", () => renderSnapshotInspection(textarea.value, output));
  takeBtn.addEventListener("click", () => void doTakeSnapshot(textarea, output));
  loadBtn.addEventListener("click", () => void doLoadSnapshots(textarea, output));

  container.appendChild(diagnosticsSectionHeader("Performance bundles"));
  renderPerfBundles(container);

  container.appendChild(diagnosticsSectionHeader("Not yet available"));
  const note = document.createElement("div");
  note.className = "set-utility-note";
  note.textContent = "In-page flame graphs are not available yet: a bundle's optional trace is a binary .nettrace with no in-webview parser or viewer — open it in an external profiler such as PerfView or dotnet-trace. Per-nook element inspection is available now from any browser nook menu (DevTools).";
  container.appendChild(note);
}

async function doTakeSnapshot(textarea: HTMLTextAreaElement, output: HTMLElement): Promise<void> {
  try {
    const snapshot = await invoke<DiagnosticsSnapshot>(FrontendCommand.DiagnosticsSnapshotTake, {});
    textarea.value = JSON.stringify(snapshot, null, 2);
    renderSnapshotInspection(textarea.value, output);
  } catch (e) {
    showSnapshotError(output, `Take snapshot failed: ${(e as Error).message}`);
  }
}

async function doLoadSnapshots(textarea: HTMLTextAreaElement, output: HTMLElement): Promise<void> {
  try {
    const snapshots = await invoke<DiagnosticsSnapshot[]>(FrontendCommand.DiagnosticsSnapshotList, {});
    textarea.value = JSON.stringify(snapshots, null, 2);
    renderSnapshotInspection(textarea.value, output);
  } catch (e) {
    showSnapshotError(output, `Load snapshots failed: ${(e as Error).message}`);
  }
}

function showSnapshotError(output: HTMLElement, message: string): void {
  output.innerHTML = "";
  const err = document.createElement("div");
  err.className = "set-utility-error";
  err.textContent = message;
  output.appendChild(err);
}

function renderPerfBundles(container: HTMLElement): void {
  let state: PerfBundlesState = initialPerfBundlesState();

  const caption = document.createElement("div");
  caption.className = "set-utility-caption";
  caption.textContent = "Create a performance bundle to package the engine's diagnostics snapshots into a shareable .zip, then manage the saved bundles below.";
  container.appendChild(caption);

  const createBtn = document.createElement("button");
  createBtn.className = "set-utility-btn";
  container.appendChild(createBtn);

  const errorEl = document.createElement("div");
  errorEl.className = "set-utility-error";
  container.appendChild(errorEl);

  const listEl = document.createElement("div");
  listEl.className = "set-utility-snap";
  container.appendChild(listEl);

  const paint = (): void => {
    createBtn.textContent = state.creating ? "Creating…" : "Create bundle";
    createBtn.disabled = state.creating;
    errorEl.textContent = state.error ?? "";
    renderPerfBundleList(state, listEl, run);
  };

  const run = (next: PerfBundlesState): void => {
    if (!listEl.isConnected || activeSettingsTab !== "diagnostics") return;
    state = next;
    paint();
  };

  const refresh = async (): Promise<void> => {
    try {
      const result = await invoke<PerfBundleListResult>(FrontendCommand.PerfBundleList, {});
      run(applyBundleList(state, result));
    } catch (e) {
      run(surfaceError(state, `List bundles failed: ${(e as Error).message}`));
    }
  };

  createBtn.addEventListener("click", () => {
    if (state.creating) return;
    run(beginCreate(state));
    void (async () => {
      try {
        await invoke<PerfBundleDto>(FrontendCommand.PerfBundleCreate, {});
        run(finishCreate(state));
        await refresh();
      } catch (e) {
        run(surfaceError(state, `Create bundle failed: ${(e as Error).message}`));
      }
    })();
  });

  paint();
  void refresh();
}

function renderPerfBundleList(state: PerfBundlesState, listEl: HTMLElement, run: (next: PerfBundlesState) => void): void {
  listEl.innerHTML = "";
  const rows = bundleRows(state);
  if (rows.length === 0) {
    const empty = document.createElement("div");
    empty.className = "set-utility-caption";
    empty.textContent = PERF_BUNDLES_EMPTY_TEXT;
    listEl.appendChild(empty);
    return;
  }

  for (const row of rows) {
    const card = document.createElement("div");
    card.className = "set-utility-snap-card";

    const info = document.createElement("div");
    const name = document.createElement("div");
    name.textContent = row.name;
    name.title = row.bundlePath;
    const meta = document.createElement("div");
    meta.textContent = `${row.createdAtLabel} · ${row.sizeLabel} · ${row.detail}`;
    info.appendChild(name);
    info.appendChild(meta);
    card.appendChild(info);

    const actions = document.createElement("div");
    if (row.confirmingDelete) {
      const confirm = document.createElement("button");
      confirm.className = "set-utility-btn";
      confirm.textContent = "Confirm";
      confirm.addEventListener("click", () => void doDeleteBundle(state, row.bundlePath, run));
      const cancel = document.createElement("button");
      cancel.className = "set-utility-btn";
      cancel.textContent = "Cancel";
      cancel.addEventListener("click", () => run(cancelDelete(state)));
      actions.appendChild(confirm);
      actions.appendChild(cancel);
    } else {
      const del = document.createElement("button");
      del.className = "set-utility-btn";
      del.textContent = "Delete";
      del.addEventListener("click", () => run(requestDelete(state, row.bundlePath)));
      actions.appendChild(del);
    }
    card.appendChild(actions);
    listEl.appendChild(card);
  }
}

async function doDeleteBundle(state: PerfBundlesState, bundlePath: string, run: (next: PerfBundlesState) => void): Promise<void> {
  try {
    await invoke(FrontendCommand.PerfBundleDelete, { bundlePath });
    const result = await invoke<PerfBundleListResult>(FrontendCommand.PerfBundleList, {});
    run(applyBundleList(cancelDelete(state), result));
  } catch (e) {
    run(surfaceError(cancelDelete(state), `Delete bundle failed: ${(e as Error).message}`));
  }
}

function renderSnapshotInspection(text: string, output: HTMLElement): void {
  output.innerHTML = "";
  const result = parseSnapshotExport(text);
  if (!result.ok) {
    const err = document.createElement("div");
    err.className = "set-utility-error";
    err.textContent = result.error ?? "Could not read snapshot.";
    output.appendChild(err);
    return;
  }

  const summary = summarizeSnapshots(result.snapshots);
  const summaryEl = document.createElement("div");
  summaryEl.className = "set-utility-caption";
  summaryEl.textContent = `${summary.count} snapshot${summary.count === 1 ? "" : "s"} · peak managed memory ${formatSnapshotBytes(summary.peakManagedMemoryBytes)}`;
  output.appendChild(summaryEl);

  for (const snapshot of result.snapshots) appendSnapshotCard(snapshot, output);
}

function appendSnapshotCard(snapshot: DiagnosticsSnapshot, output: HTMLElement): void {
  const card = document.createElement("div");
  card.className = "set-utility-snap-card";
  for (const row of snapshotRows(snapshot)) {
    const rowEl = document.createElement("div");
    rowEl.className = "set-utility-snap-row";
    const key = document.createElement("span");
    key.className = "k";
    key.textContent = row.label;
    const value = document.createElement("span");
    value.className = "v";
    value.textContent = row.value;
    rowEl.appendChild(key);
    rowEl.appendChild(value);
    card.appendChild(rowEl);
  }
  output.appendChild(card);
}

async function loadSettingValue(entry: ConfigSchemaEntry, row: HTMLElement, controlId: string, generation: number, tab: string): Promise<void> {
  let currentValue = "";
  try {
    const res = await invoke<{ value: string } | null>(FrontendCommand.ConfigGet, { key: entry.key });
    currentValue = res?.value ?? "";
  } catch { void 0; }

  if (generation !== renderGeneration || activeSettingsTab !== tab || !row.isConnected) return;
  const input = createSettingControl(entry, currentValue);
  input.id = controlId;
  input.addEventListener("change", () => void saveSetting(entry.key, input));
  row.appendChild(input);
}

function createSettingControl(entry: ConfigSchemaEntry, value: string): HTMLInputElement | HTMLSelectElement {
  if (entry.control === "select" && entry.options && entry.options.length > 0) {
    const select = document.createElement("select");
    for (const opt of entry.options) {
      const o = document.createElement("option");
      o.value = opt;
      o.textContent = opt;
      select.appendChild(o);
    }
    select.value = value;
    return select;
  }
  if (entry.type === "bool" || entry.control === "toggle") {
    const select = document.createElement("select");
    const t = document.createElement("option"); t.value = "true"; t.textContent = "On"; select.appendChild(t);
    const f = document.createElement("option"); f.value = "false"; f.textContent = "Off"; select.appendChild(f);
    select.value = value === "true" ? "true" : "false";
    return select;
  }
  if (entry.type === "int" || entry.type === "double") {
    const input = document.createElement("input");
    input.type = "number";
    input.value = value;
    return input;
  }
  const input = document.createElement("input");
  input.type = "text";
  input.value = value;
  return input;
}

async function saveSetting(key: string, input: HTMLInputElement | HTMLSelectElement): Promise<void> {
  const value = input.type === "checkbox" ? String((input as HTMLInputElement).checked) : input.value;
  try {
    await invoke(FrontendCommand.ConfigSet, { key, value });
    if (key.startsWith("terminal.")) { Object.assign(settings, await loadSettings()); applySettings(); }
    if (key.startsWith("appearance.")) { await applyAppearance(key); }
    if (key.startsWith("markdown_editor.")) {
      const mdKeys = [
        "markdown_editor.defaultFont", "markdown_editor.fontSize", "markdown_editor.textAlign",
        "markdown_editor.bookView", "markdown_editor.bookViewWidth", "markdown_editor.bookViewMargin",
        "markdown_editor.defaultViewMode",
      ];
      const raw: Record<string, string> = {};
      for (const k of mdKeys) {
        try { const r = await invoke<{ value: string } | null>(FrontendCommand.ConfigGet, { key: k }); if (r?.value) raw[k] = r.value; } catch { void 0; }
      }
      void dependencies.applyMarkdownSettings(raw);
    }
  } catch { void 0; }
}

async function applyAppearance(changedKey: string | null): Promise<void> {
  const get = async (k: string): Promise<string> => { try { const r = await invoke<{ ok: boolean; value?: string }>(FrontendCommand.AppConfigGet, { key: k }); return r.ok ? r.value ?? "" : ""; } catch { return ""; } };
  const root = document.documentElement;
  if (changedKey === null || changedKey === "appearance.uiScale") { const scale = parseFloat(await get("appearance.uiScale")) || 1; root.style.setProperty("--ui-scale", String(scale)); document.body.style.fontSize = `${13 * scale}px`; }
  if (changedKey === null || changedKey === "appearance.layoutGap") { const gap = parseInt(await get("appearance.layoutGap")) || 4; root.style.setProperty("--layout-gap", `${gap}px`); gridEl.style.gap = `${gap}px`; }
  if (changedKey === null || changedKey === "appearance.accent") { const accent = await get("appearance.accent"); if (accent) { root.style.setProperty("--accent", accent); root.style.setProperty("--accent-dim", accent); } }
  if (changedKey === null || changedKey === "appearance.wallpaper") { const wp = await get("appearance.wallpaper"); if (wp) { document.body.style.backgroundImage = `url("${wp}")`; document.body.style.backgroundSize = "cover"; } else { document.body.style.backgroundImage = ""; } }
  if (changedKey === null || changedKey === "appearance.nookLight") { const pl = await get("appearance.nookLight") === "true"; root.style.setProperty("--nook-light", pl ? "1" : "0"); }
  if (changedKey === null || changedKey === "appearance.iconSet") { const ic = (await get("appearance.iconSet")) || "default"; document.body.classList.remove("icon-set-outline", "icon-set-filled"); if (ic === "outline") document.body.classList.add("icon-set-outline"); else if (ic === "filled") document.body.classList.add("icon-set-filled"); }
}

let themeList: ThemeDto[] = [];

let themeActiveName: string | null = null;

let themeCustomNames: string[] = [];

let themeDraft: ThemeDraft = { ...DEFAULT_DRAFT };

let themeBuiltinNames: string[] = [];

let themeAppliedVars: Record<string, string> | null = null;

let themeAppliedTermTheme: Record<string, string> | null = null;

async function loadThemeData(): Promise<void> {
  try {
    const list = await invoke<{ themes: ThemeDto[] }>(FrontendCommand.ThemeList, {});
    themeList = list.themes ?? [];
    themeBuiltinNames = themeList.filter((t) => (t.name.startsWith("cove-") || t.name === "catppuccin-mocha") && !themeCustomNames.includes(t.name)).map((t) => t.name);
  } catch { themeList = []; }
  try {
    const active = await invoke<{ theme: ThemeDto | null }>(FrontendCommand.ThemeGetActive, {});
    themeActiveName = active.theme?.name ?? null;
    if (active.theme) { themeDraft = draftFromTheme(active.theme); }
  } catch { themeActiveName = null; }
  themeCustomNames = themeList.filter((t) => !themeBuiltinNames.includes(t.name)).map((t) => t.name);
}

function applyThemeVars(theme: ThemeDto): void {
  const vars = cssVarsFromTheme(theme);
  const root = document.documentElement;
  for (const [k, v] of Object.entries(vars)) { root.style.setProperty(k, v); }
  themeAppliedVars = vars;
  activeTheme = theme;
  const termTheme = xtermThemeFromDto(theme, settings.backgroundOpacity);
  themeAppliedTermTheme = termTheme;
  dependencies.applyTerminalTheme(termTheme);
}

function revertThemeVars(): void {
  if (!themeAppliedVars) return;
  const root = document.documentElement;
  for (const k of Object.keys(themeAppliedVars)) { root.style.removeProperty(k); }
  themeAppliedVars = null;
  activeTheme = null;
  if (themeAppliedTermTheme) {
    const restored = { ...dependencies.defaultTerminalTheme, background: themeBackgroundWithOpacity(settings.backgroundOpacity) };
    dependencies.applyTerminalTheme(restored);
    themeAppliedTermTheme = null;
  }
}

function renderThemeEditor(container: HTMLElement, footer: HTMLElement, generation: number, tab: string): void {
  void loadThemeData().then(() => {
    if (generation !== renderGeneration || activeSettingsTab !== tab || !container.isConnected) return;
    renderThemeEditorBody(container, footer);
  });
  container.innerHTML = '<div class="set-empty">Loading themes…</div>';
}

function renderThemeEditorBody(container: HTMLElement, footer: HTMLElement = setBodyEl.querySelector<HTMLElement>(".set-page-footer") ?? document.createElement("footer")): void {
  container.innerHTML = "";

  const dropdownRow = document.createElement("div");
  dropdownRow.className = "set-theme-picker";
  const dropdownLabel = document.createElement("span");
  dropdownLabel.className = "set-theme-picker-label";
  dropdownLabel.textContent = "Active theme";
  const dropdown = document.createElement("select");
  dropdown.className = "set-control set-control-wide";
  const noneOpt = document.createElement("option");
  noneOpt.value = ""; noneOpt.textContent = "— none —"; dropdown.appendChild(noneOpt);
  for (const t of themeList) {
    const o = document.createElement("option");
    o.value = t.name; o.textContent = t.name + (themeBuiltinNames.includes(t.name) ? "" : " (custom)");
    dropdown.appendChild(o);
  }
  dropdown.value = themeActiveName ?? "";
  dropdown.addEventListener("change", () => void onThemeSelect(dropdown.value));
  dropdownRow.appendChild(dropdownLabel);
  dropdownRow.appendChild(dropdown);

  const deleteBtn = document.createElement("button");
  deleteBtn.className = "set-action set-action-destructive";
  deleteBtn.textContent = "Delete";
  deleteBtn.disabled = !canDelete(themeActiveName ?? "", themeCustomNames);
  deleteBtn.addEventListener("click", () => void onThemeDelete(themeActiveName ?? ""));
  dropdownRow.appendChild(deleteBtn);
  container.appendChild(dropdownRow);

  const editorHeader = document.createElement("div");
  editorHeader.className = "set-section-header";
  editorHeader.textContent = "Edit & preview";
  container.appendChild(editorHeader);

  const nameRow = document.createElement("div");
  nameRow.className = "set-row";
  const nameLabel = document.createElement("label");
  nameLabel.textContent = "Theme name";
  const nameInput = document.createElement("input");
  nameInput.className = "set-control set-control-wide";
  nameInput.type = "text";
  nameInput.value = themeDraft.name;
  nameInput.addEventListener("input", () => { themeDraft.name = nameInput.value; updateThemePreview(); });
  nameLabel.appendChild(nameInput);
  nameRow.appendChild(nameLabel);
  container.appendChild(nameRow);

  const typeRow = document.createElement("div");
  typeRow.className = "set-row";
  const typeLabel = document.createElement("label");
  typeLabel.textContent = "Type";
  const typeSelect = document.createElement("select");
  typeSelect.className = "set-control";
  for (const tp of ["dark", "light"]) { const o = document.createElement("option"); o.value = tp; o.textContent = tp; typeSelect.appendChild(o); }
  typeSelect.value = themeDraft.type;
  typeSelect.addEventListener("change", () => { themeDraft.type = typeSelect.value; updateThemePreview(); });
  typeLabel.appendChild(typeSelect);
  typeRow.appendChild(typeLabel);
  container.appendChild(typeRow);

  for (const field of THEME_COLOR_FIELDS) {
    const row = document.createElement("div");
    row.className = "set-row set-theme-color-row";
    const label = document.createElement("label");
    const labelText = document.createElement("span");
    labelText.textContent = field.label;
    label.appendChild(labelText);
    if (field.desc) { const d = document.createElement("span"); d.className = "set-desc"; d.textContent = field.desc; label.appendChild(d); }
    const colorInput = document.createElement("input");
    colorInput.className = "set-color-control";
    colorInput.type = "color";
    colorInput.value = (themeDraft as unknown as Record<string, string>)[field.key];
    const hexInput = document.createElement("input");
    hexInput.className = "set-control set-code-control";
    hexInput.type = "text";
    hexInput.value = (themeDraft as unknown as Record<string, string>)[field.key];
    colorInput.addEventListener("input", () => {
      (themeDraft as unknown as Record<string, string>)[field.key] = colorInput.value;
      hexInput.value = colorInput.value;
      updateThemePreview();
    });
    hexInput.addEventListener("input", () => {
      if (isValidHex(hexInput.value)) { colorInput.value = hexInput.value; (themeDraft as unknown as Record<string, string>)[field.key] = hexInput.value; updateThemePreview(); }
    });
    label.appendChild(hexInput);
    row.appendChild(label);
    row.appendChild(colorInput);
    container.appendChild(row);
  }

  const contrastInfo = document.createElement("div");
  contrastInfo.id = "theme-contrast";
  contrastInfo.className = "set-contrast";
  container.appendChild(contrastInfo);

  const actions = document.createElement("div");
  actions.className = "set-footer-actions";
  const saveBtn = document.createElement("button");
  saveBtn.className = "set-action set-action-primary";
  saveBtn.dataset.setAction = "theme-save";
  saveBtn.textContent = "Save as custom";
  saveBtn.disabled = !canSaveDraft(themeDraft);
  saveBtn.addEventListener("click", () => void onThemeSave());
  const resetBtn = document.createElement("button");
  resetBtn.className = "set-action set-action-secondary";
  resetBtn.textContent = "Reset preview";
  resetBtn.addEventListener("click", () => { revertThemeVars(); if (themeActiveName) { const t = themeList.find((x) => x.name === themeActiveName); if (t) { themeDraft = draftFromTheme(t); } } else { themeDraft = { ...DEFAULT_DRAFT }; } renderThemeEditorBody(container); });
  actions.appendChild(saveBtn);
  actions.appendChild(resetBtn);
  footer.replaceChildren(actions);
  groupSettingsContent(container);
}

function updateThemePreview(): void {
  const theme = themeFromDraft(themeDraft);
  applyThemeVars(theme);
  const contrastEl = document.getElementById("theme-contrast");
  if (contrastEl) {
    const fgBg = contrastRatio(themeDraft.terminalForeground, themeDraft.terminalBackground);
    const tier = contrastTier(fgBg);
    contrastEl.textContent = `Terminal contrast: ${fgBg.toFixed(2)}:1 (${tier === "fail" ? "below AA" : tier})`;
  }
  const saveBtn = setBodyEl.querySelector<HTMLButtonElement>('[data-set-action="theme-save"]');
  if (saveBtn) {
    saveBtn.disabled = !canSaveDraft(themeDraft);
    saveBtn.setAttribute("data-valid", canSaveDraft(themeDraft) ? "1" : "0");
  }
}

async function onThemeSelect(name: string): Promise<void> {
  if (!name) { themeActiveName = null; revertThemeVars(); renderCurrentThemeEditor(); return; }
  try {
    const res = await invoke<{ theme: ThemeDto }>(FrontendCommand.ThemeSetActive, { name });
    themeActiveName = name;
    if (res.theme) { themeDraft = draftFromTheme(res.theme); applyThemeVars(res.theme); }
    await loadThemeData();
    renderCurrentThemeEditor();
  } catch { void 0; }
}

async function onThemeSave(): Promise<void> {
  if (!canSaveDraft(themeDraft)) return;
  try {
    await invoke(FrontendCommand.ThemeSaveCustom, themeDraft);
    await invoke(FrontendCommand.ThemeSetActive, { name: themeDraft.name });
    themeActiveName = themeDraft.name;
    await loadThemeData();
    renderCurrentThemeEditor();
  } catch { void 0; }
}

async function onThemeDelete(name: string): Promise<void> {
  if (!canDelete(name, themeCustomNames)) return;
  try {
    await invoke(FrontendCommand.ThemeDeleteCustom, { name });
    if (themeActiveName === name) { themeActiveName = null; revertThemeVars(); }
    await loadThemeData();
    renderCurrentThemeEditor();
  } catch { void 0; }
}

function renderCurrentThemeEditor(): void {
  const content = setBodyEl.querySelector<HTMLElement>(".set-page-content");
  const footer = setBodyEl.querySelector<HTMLElement>(".set-page-footer");
  if (activeSettingsTab === "theme" && content?.isConnected && footer?.isConnected) renderThemeEditorBody(content, footer);
}

let keybindList: KeybindDto[] = [];

let keybindConflicts: string[] = [];

let keybindRecordingAction: string | null = null;

let keybindRecordingDisposer: (() => void) | null = null;

function removeKeybindRecordingListener(): void {
  const dispose = keybindRecordingDisposer;
  keybindRecordingDisposer = null;
  dispose?.();
}

function cancelKeybindRecording(): void {
  keybindRecordingAction = null;
  removeKeybindRecordingListener();
}

lifecycle.own(cancelKeybindRecording);

async function loadKeybindData(): Promise<void> {
  try {
    const res = await invoke<{ bindings: KeybindDto[]; conflicts: string[] }>(FrontendCommand.KeybindList, {});
    keybindList = res.bindings ?? [];
    keybindConflicts = res.conflicts ?? [];
  } catch { keybindList = []; keybindConflicts = []; }
}

function renderKeyboardEditor(container: HTMLElement, generation: number, tab: string): void {
  void loadKeybindData().then(() => {
    if (generation !== renderGeneration || activeSettingsTab !== tab || !container.isConnected) return;
    renderKeyboardEditorBody(container);
  });
  container.innerHTML = '<div class="set-empty">Loading keybindings…</div>';
}

function renderKeyboardEditorBody(container: HTMLElement): void {
  removeKeybindRecordingListener();
  container.innerHTML = "";
  const categories = categorizeBindings(keybindList, keybindConflicts, []);

  if (keybindConflicts.length > 0) {
    const warn = document.createElement("div");
    warn.className = "set-warning";
    warn.textContent = `Conflicts: ${keybindConflicts.join(", ")} — two actions share the same chord`;
    container.appendChild(warn);
  }

  for (const cat of categories) {
    const header = document.createElement("div");
    header.className = "set-section-header";
    header.textContent = cat.name;
    container.appendChild(header);

    for (const row of cat.rows) {
      const rowEl = document.createElement("div");
      rowEl.className = "set-row set-keyboard-row";
      const label = document.createElement("label");
      const labelText = document.createElement("span");
      labelText.textContent = row.description ?? row.action;
      label.appendChild(labelText);
      const actionLabel = document.createElement("span");
      actionLabel.className = "set-desc";
      actionLabel.textContent = row.action;
      label.appendChild(actionLabel);
      rowEl.appendChild(label);

      const chordBtn = document.createElement("button");
      chordBtn.className = "set-key-control" + (row.hasConflict ? " has-conflict" : "") + (keybindRecordingAction === row.action ? " is-recording" : "");
      chordBtn.textContent = chordDisplay(row.chord);
      if (keybindRecordingAction === row.action) { chordBtn.textContent = "Press keys…"; }
      chordBtn.addEventListener("click", () => { keybindRecordingAction = keybindRecordingAction === row.action ? null : row.action; renderKeyboardEditorBody(container); });
      rowEl.appendChild(chordBtn);

      const clearBtn = document.createElement("button");
      clearBtn.className = "set-action set-action-icon";
      clearBtn.textContent = "×";
      clearBtn.addEventListener("click", () => void onKeybindClear(row.chord, container));
      rowEl.appendChild(clearBtn);

      container.appendChild(rowEl);
    }
  }

  if (keybindRecordingAction) {
    const hint = document.createElement("div");
    hint.className = "set-recording-hint";
    hint.textContent = `Recording for "${keybindRecordingAction}" — press a key combination, Esc to cancel.`;
    container.appendChild(hint);
    const escHandler = (e: KeyboardEvent): void => {
      e.preventDefault();
      e.stopPropagation();
      if (e.key === "Escape") { cancelKeybindRecording(); renderKeyboardEditorBody(container); return; }
      const chord = captureChord(e);
      if (chord) {
        removeKeybindRecordingListener();
        const act = keybindRecordingAction;
        if (act) { void onKeybindSet(act, chord, container); }
        keybindRecordingAction = null;
      }
    };
    settingsEl.addEventListener("keydown", escHandler, true);
    keybindRecordingDisposer = () => settingsEl.removeEventListener("keydown", escHandler, true);
  }
  groupSettingsContent(container);
}

function captureChord(e: KeyboardEvent): string {
  const parts: string[] = [];
  if (e.metaKey) parts.push("cmd");
  if (e.ctrlKey) parts.push("ctrl");
  if (e.altKey) parts.push("alt");
  if (e.shiftKey) parts.push("shift");
  const key = e.key.toLowerCase();
  if (!["meta", "control", "alt", "shift"].includes(key)) {
    parts.push(key === " " ? "space" : key);
  }
  if (parts.length === 0) return "";
  const chord = parts.join("+");
  return isValidChord(chord) ? chord : "";
}

async function onKeybindSet(action: string, chord: string, container: HTMLElement): Promise<void> {
  const normalized = normalizeChordStr(chord);
  const check = canRecordChord(normalized, action, keybindList);
  if (!check.valid) {
    if (isReservedChord(normalized)) { return; }
    if (check.conflictAction) { const proceed = confirm(`"${chordDisplay(normalized)}" is bound to "${check.conflictAction}". Rebind?`); if (!proceed) return; }
  }
  try {
    const res = await invoke<{ success: boolean; warning?: { warning: string } | null }>(FrontendCommand.KeybindSet, { chord: normalized, actionType: "app-command", action });
    if (res.success) { await loadKeybindData(); await reloadKeymap(); renderKeyboardEditorBody(container); }
  } catch { void 0; }
}

async function onKeybindClear(chord: string, container: HTMLElement): Promise<void> {
  try {
    await invoke(FrontendCommand.KeybindClear, { chord });
    await loadKeybindData();
    await reloadKeymap();
    renderKeyboardEditorBody(container);
  } catch { void 0; }
}

  lifecycle.listen(settingsEl, "mousedown", (event) => {
    if (event.target === settingsEl) closeSettings();
  });
  const closeButton = document.getElementById("set-close");
  if (!closeButton) throw new Error("Missing settings close button");
  lifecycle.listen(closeButton, "click", closeSettings);
  lifecycle.listen(settingsEl, "keydown", (event) => {
    const keyboardEvent = event as KeyboardEvent;
    if (keyboardEvent.key === "Escape") { closeSettings(); return; }
    if (keyboardEvent.key !== "Tab") return;
    const focusable = [...settingsEl.querySelectorAll<HTMLElement>('button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])')].filter((element) => element.getClientRects().length > 0 || element === document.activeElement);
    if (focusable.length === 0) { keyboardEvent.preventDefault(); settingsEl.focus(); return; }
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (keyboardEvent.shiftKey && document.activeElement === first) { keyboardEvent.preventDefault(); last.focus(); }
    else if (!keyboardEvent.shiftKey && document.activeElement === last) { keyboardEvent.preventDefault(); first.focus(); }
  });

  async function start(): Promise<void> {
    try {
      const result = await invoke<{ theme: ThemeDto | null }>(FrontendCommand.ThemeGetActive, {});
      if (result.theme) {
        themeActiveName = result.theme.name;
        themeDraft = draftFromTheme(result.theme);
        applyThemeVars(result.theme);
      }
    } catch (error) {
      console.warn("active theme load failed, using built-in defaults", error);
    }
  }

  async function dispose(): Promise<void> {
    cancelKeybindRecording();
    surfaceMotion.dispose();
    for (const overlay of ownedOverlays) overlay.remove();
    ownedOverlays.clear();
    await lifecycle.dispose();
  }

  return {
    get activeTheme() { return activeTheme; },
    get activeThemeName() { return themeActiveName; },
    get activeTab() { return activeSettingsTab; },
    open: openSettings,
    close: closeSettings,
    render: renderSettings,
    start,
    applyAppearance,
    resolveLauncherProfileSlug,
    launcherProfileSlugKey,
    openProfileEditor,
    dispose,
  };
}
