import { FrontendCommand } from "../../app/frontend-command";
import { LifecycleScope, type ComponentHandle } from "../../app/lifecycle";
import { orderSettingsTabs, settingsTabLabel, resolveActiveSettingsTab } from "../../settings-tabs";
import { adapterStatusMeta, toolsSubtitle, retentionChipVisible, retentionChipLabel, type ToolsAdapter } from "../../tools-tab";
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

async function loadConfigSchema(): Promise<void> {
  try {
    const res = await invoke<{ entries: ConfigSchemaEntry[] }>(FrontendCommand.ConfigSchema, {});
    configSchema = res.entries ?? [];
  } catch {
    configSchema = [];
  }
}

function openSettings(tab?: string): void {
  if (tab) activeSettingsTab = tab;
  if (configSchema.length === 0) {
    void loadConfigSchema().then(() => renderSettings());
  } else {
    renderSettings();
  }
  settingsEl.classList.add("open");
}

function closeSettings(): void {
  cancelKeybindRecording();
  settingsEl.classList.remove("open");
  dependencies.focusActiveNook();
}

function isRealSetting(e: ConfigSchemaEntry): boolean {
  return e.control !== "section" && e.type !== "object";
}

function renderSettings(): void {
  const schemaTabs = [...new Set(configSchema.filter(isRealSetting).map((e) => e.tab))].sort();
  const tabs = orderSettingsTabs(schemaTabs);
  if (tabs.length === 0) {
    setTabsEl.innerHTML = "";
    setBodyEl.innerHTML = `<div style="padding:20px;color:var(--muted);text-align:center;">No settings available</div>`;
    return;
  }
  activeSettingsTab = resolveActiveSettingsTab(tabs, activeSettingsTab);

  setTabsEl.innerHTML = "";
  for (const tab of tabs) {
    const el = document.createElement("div");
    el.className = "set-nav-item" + (tab === activeSettingsTab ? " active" : "");
    const dot = document.createElement("span");
    dot.className = "set-nav-dot";
    const label = document.createElement("span");
    label.textContent = settingsTabLabel(tab);
    el.appendChild(dot);
    el.appendChild(label);
    el.addEventListener("click", () => { activeSettingsTab = tab; renderSettings(); });
    setTabsEl.appendChild(el);
  }

  setBodyEl.innerHTML = "";
  if (activeSettingsTab === "theme") {
    renderThemeEditor(setBodyEl);
    return;
  }
  if (activeSettingsTab === "keyboard") {
    renderKeyboardEditor(setBodyEl);
    return;
  }
  if (activeSettingsTab === "tools") {
    void renderToolsTab(setBodyEl);
    return;
  }
  if (activeSettingsTab === "dictation") {
    onboardingFeature.renderDictationTab(setBodyEl);
    return;
  }
  const entries = configSchema.filter((e) => e.tab === activeSettingsTab && (e.control === "section" || isRealSetting(e)));
  for (const entry of entries) {
    if (entry.control === "section") {
      const header = document.createElement("div");
      header.className = "set-section-header";
      header.style.cssText = "padding:12px 0 4px;font-size:11px;font-weight:600;color:var(--muted);text-transform:uppercase;letter-spacing:0.5px;border-bottom:1px solid var(--border);";
      header.textContent = entry.label;
      setBodyEl.appendChild(header);
      continue;
    }
    const row = document.createElement("div");
    row.className = "set-row";
    const label = document.createElement("label");
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

    void loadSettingValue(entry, row);
    setBodyEl.appendChild(row);
  }
  if (activeSettingsTab === "diagnostics") renderDiagnosticsExtras(setBodyEl);
  if (activeSettingsTab === "updates") updaterFeature.renderSettings(setBodyEl);
  if (activeSettingsTab === "audio") renderAudioExtras(setBodyEl);
}

function renderAudioExtras(container: HTMLElement): void {
  const header = document.createElement("div");
  header.className = "set-section-header";
  header.style.cssText = "padding:12px 0 4px;font-size:11px;font-weight:600;color:var(--muted);text-transform:uppercase;letter-spacing:0.5px;border-bottom:1px solid var(--border);";
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
  controls.style.cssText = "display:flex;align-items:center;gap:8px;";
  const preview = document.createElement("button");
  preview.className = "diag-btn";
  preview.style.marginTop = "0";
  preview.textContent = "Preview";
  preview.addEventListener("click", () => playChime("done"));
  const toggle = document.createElement("button");
  const paint = (): void => {
    const on = agentChimesEnabled();
    toggle.className = "diag-toggle" + (on ? " on" : "");
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

async function renderToolsTab(container: HTMLElement): Promise<void> {
  container.innerHTML = "";

  const actions = document.createElement("div");
  actions.className = "tools-actions";
  actions.style.cssText = "display:flex;gap:8px;flex-wrap:wrap;margin-bottom:12px;";
  const rescanBtn = document.createElement("button");
  rescanBtn.className = "diag-btn";
  rescanBtn.style.marginTop = "0";
  rescanBtn.textContent = "Re-scan";
  rescanBtn.addEventListener("click", () => void doRescanAdapters(container, rescanBtn));
  const addBtn = document.createElement("button");
  addBtn.className = "diag-btn";
  addBtn.style.marginTop = "0";
  addBtn.textContent = "Add adapter from folder…";
  addBtn.addEventListener("click", () => void doAddAdapterFromFolder(container));
  const wizardBtn = document.createElement("button");
  wizardBtn.className = "diag-btn";
  wizardBtn.style.marginTop = "0";
  wizardBtn.textContent = "Re-run setup wizard";
  wizardBtn.addEventListener("click", () => onboardingFeature.rerun());
  actions.appendChild(rescanBtn);
  actions.appendChild(addBtn);
  actions.appendChild(wizardBtn);
  container.appendChild(actions);

  let adapters: ToolsAdapter[];
  try {
    const result = await invoke<ToolsListResponse>(FrontendCommand.AdapterToolsList, {});
    adapters = result.adapters ?? [];
  } catch (err) {
    console.warn("adapter.tools-list failed for tools tab", err);
    const failed = document.createElement("div");
    failed.className = "tools-empty";
    failed.textContent = "adapter list unavailable";
    container.appendChild(failed);
    return;
  }
  if (adapters.length === 0) {
    const empty = document.createElement("div");
    empty.className = "tools-empty";
    empty.textContent = "no adapters installed";
    container.appendChild(empty);
    return;
  }
  const list = document.createElement("div");
  list.className = "tools-list";
  for (const a of adapters) list.appendChild(buildToolsCard(a, container));
  container.appendChild(list);
}

function buildToolsCard(a: ToolsAdapter, container: HTMLElement): HTMLElement {
  const card = document.createElement("div");
  card.className = "tools-card";

  if (a.iconSvg) {
    const icon = document.createElement("span");
    icon.className = "tools-icon";
    icon.style.cssText = "width:18px;height:18px;display:inline-flex;color:" + (a.accent || "var(--accent)") + ";";
    icon.innerHTML = a.iconSvg;
    const svg = icon.querySelector("svg");
    if (svg) { svg.setAttribute("width", "18"); svg.setAttribute("height", "18"); }
    card.appendChild(icon);
  } else {
    const swatch = document.createElement("span");
    swatch.className = "tools-accent";
    swatch.style.background = a.accent || "var(--accent)";
    card.appendChild(swatch);
  }

  const body = document.createElement("div");
  body.className = "tools-body";

  const titleRow = document.createElement("div");
  titleRow.className = "tools-title-row";
  const name = document.createElement("span");
  name.className = "tools-name";
  name.textContent = a.displayName || a.name;
  titleRow.appendChild(name);

  const meta = adapterStatusMeta(a.status);
  const status = document.createElement("span");
  status.className = "tools-status";
  const dot = document.createElement("span");
  dot.className = "tools-dot";
  dot.style.background = meta.cssColor;
  const statusLabel = document.createElement("span");
  statusLabel.textContent = meta.label;
  statusLabel.style.color = meta.cssColor;
  status.appendChild(dot);
  status.appendChild(statusLabel);
  titleRow.appendChild(status);
  body.appendChild(titleRow);

  const subtitle = document.createElement("div");
  subtitle.className = "tools-subtitle";
  subtitle.textContent = toolsSubtitle(a.status, a.version, a.binaryPath, a.installHint);
  body.appendChild(subtitle);

  const manifestRow = document.createElement("div");
  manifestRow.className = "tools-manifest";
  manifestRow.style.cssText = "display:flex;align-items:center;gap:8px;justify-content:space-between;";
  const manifestName = document.createElement("span");
  manifestName.textContent = a.bundled ? `${a.name} · bundled` : a.name;
  manifestRow.appendChild(manifestName);
  if (a.removable) {
    const removeBtn = document.createElement("button");
    removeBtn.className = "diag-btn";
    removeBtn.style.cssText = "margin-top:0;padding:2px 8px;font-size:11px;";
    removeBtn.textContent = "Remove";
    removeBtn.addEventListener("click", () => openRemoveAdapterDialog(a, container));
    manifestRow.appendChild(removeBtn);
  }
  body.appendChild(manifestRow);

  if (retentionChipVisible(a.retention)) body.appendChild(buildRetentionChip(a, container));

  if (a.status === "detected") void buildProfilesSection(a, container).then((el) => body.appendChild(el));

  card.appendChild(body);
  return card;
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

async function buildProfilesSection(a: ToolsAdapter, container: HTMLElement): Promise<HTMLElement> {
  const section = document.createElement("div");
  section.className = "tools-profiles";
  section.style.cssText = "margin-top:10px;border-top:1px solid var(--border);padding-top:8px;";

  const header = document.createElement("div");
  header.style.cssText = "display:flex;align-items:center;justify-content:space-between;margin-bottom:6px;";
  const label = document.createElement("span");
  label.className = "tools-subtitle";
  label.textContent = "Launch profiles";
  header.appendChild(label);

  const newBtn = document.createElement("button");
  newBtn.className = "diag-btn";
  newBtn.style.cssText = "margin-top:0;padding:2px 8px;font-size:11px;";
  newBtn.textContent = "New profile";
  newBtn.addEventListener("click", () => openProfileEditor(a, null, () => renderToolsTab(container)));
  header.appendChild(newBtn);
  section.appendChild(header);

  const listEl = document.createElement("div");
  listEl.style.cssText = "display:flex;flex-direction:column;gap:4px;";
  section.appendChild(listEl);

  try {
    const result = await invoke<ProfileListResult>(FrontendCommand.LaunchProfileList, { adapter: a.name });
    renderProfileList(a, result.profiles ?? [], listEl, container);
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
    row.style.cssText = "display:flex;align-items:center;gap:6px;font-size:12px;";
    const radio = document.createElement("input");
    radio.type = "radio";
    radio.name = `profile-radio-${a.name}`;
    const storedSlug = storage.getItem(launcherProfileSlugKey(a.name));
    radio.checked = storedSlug ? storedSlug === p.slug : p.isDefault;
    radio.addEventListener("change", () => {
      storage.setItem(launcherProfileSlugKey(a.name), p.slug);
      void invoke(FrontendCommand.LaunchProfileSetDefault, { adapter: a.name, slug: p.slug }).catch((err) => console.warn("launch-profile.set-default failed", a.name, p.slug, err));
    });
    row.appendChild(radio);
    const name = document.createElement("span");
    name.textContent = profilePickerLabel(p);
    name.style.flex = "1";
    row.appendChild(name);
    const editBtn = document.createElement("button");
    editBtn.className = "diag-btn";
    editBtn.style.cssText = "margin-top:0;padding:1px 6px;font-size:11px;";
    editBtn.textContent = "Edit";
    editBtn.addEventListener("click", () => openProfileEditor(a, p.slug, () => renderToolsTab(container)));
    row.appendChild(editBtn);
    if (profiles.length > 1) {
      const delBtn = document.createElement("button");
      delBtn.className = "diag-btn";
      delBtn.style.cssText = "margin-top:0;padding:1px 6px;font-size:11px;";
      delBtn.textContent = "Delete";
      delBtn.addEventListener("click", async () => {
        await invoke(FrontendCommand.LaunchProfileDelete, { adapter: a.name, slug: p.slug });
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
  overlay.style.cssText = "position:fixed;inset:0;background:rgba(0,0,0,0.5);display:flex;align-items:center;justify-content:center;z-index:1000;";
  const dialog = document.createElement("div");
  dialog.className = "settings-dialog";
  dialog.style.cssText = "background:var(--bg);border:1px solid var(--border);border-radius:8px;padding:16px;min-width:380px;max-width:520px;display:flex;flex-direction:column;gap:10px;";
  dialog.addEventListener("click", (e) => e.stopPropagation());

  const title = document.createElement("div");
  title.style.fontWeight = "600";
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
  envArea.rows = 4;
  envArea.style.cssText = "width:100%;font-family:var(--font-mono,monospace);font-size:12px;background:var(--bg-alt);color:var(--fg);border:1px solid var(--border);border-radius:4px;padding:4px;";
  dialog.appendChild(envArea);

  const argsLabel = document.createElement("div");
  argsLabel.className = "tools-subtitle";
  argsLabel.textContent = "Extra CLI args (one per line)";
  dialog.appendChild(argsLabel);
  const argsArea = document.createElement("textarea");
  argsArea.rows = 3;
  argsArea.style.cssText = "width:100%;font-family:var(--font-mono,monospace);font-size:12px;background:var(--bg-alt);color:var(--fg);border:1px solid var(--border);border-radius:4px;padding:4px;";
  dialog.appendChild(argsArea);

  const defaultCb = document.createElement("input");
  defaultCb.type = "checkbox";
  const defaultLabel = document.createElement("label");
  defaultLabel.style.cssText = "display:flex;align-items:center;gap:6px;font-size:12px;";
  defaultLabel.appendChild(defaultCb);
  const defaultText = document.createElement("span");
  defaultText.textContent = "Set as default profile";
  defaultLabel.appendChild(defaultText);
  dialog.appendChild(defaultLabel);

  const errorEl = document.createElement("div");
  errorEl.style.cssText = "color:#e0a44a;font-size:11px;min-height:14px;";
  dialog.appendChild(errorEl);

  const buttonRow = document.createElement("div");
  buttonRow.style.cssText = "display:flex;gap:8px;justify-content:flex-end;";
  const cancelBtn = document.createElement("button");
  cancelBtn.className = "diag-btn";
  cancelBtn.textContent = "Cancel";
  cancelBtn.addEventListener("click", () => overlay.remove());
  const saveBtn = document.createElement("button");
  saveBtn.className = "diag-btn";
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
  row.style.cssText = "display:flex;flex-direction:column;gap:2px;";
  const lab = document.createElement("label");
  lab.className = "tools-subtitle";
  lab.textContent = label;
  row.appendChild(lab);
  if (hint) {
    const hintEl = document.createElement("div");
    hintEl.style.cssText = "font-size:10.5px;color:var(--muted);opacity:0.85;";
    hintEl.textContent = hint;
    row.appendChild(hintEl);
  }
  const input = document.createElement("input");
  input.type = "text";
  input.value = value;
  input.placeholder = placeholder;
  input.style.cssText = "width:100%;background:var(--bg-alt);color:var(--fg);border:1px solid var(--border);border-radius:4px;padding:4px;font-size:12px;";
  row.appendChild(input);
  parent.appendChild(row);
  return input;
}

function buildRetentionChip(a: ToolsAdapter, container: HTMLElement): HTMLElement {
  const chip = document.createElement("div");
  chip.className = "tools-retention";
  chip.style.cssText = "display:flex;align-items:center;gap:8px;margin-top:6px;";
  const label = document.createElement("span");
  label.className = "set-desc";
  label.textContent = retentionChipLabel(a.retention);
  chip.appendChild(label);

  if (a.retention.editable) {
    const input = document.createElement("input");
    input.type = "text";
    input.className = "diag-input";
    input.style.cssText = "width:64px;padding:2px 6px;margin:0;";
    input.value = a.retention.value ?? "";
    input.placeholder = a.retention.recommended ?? "";
    const save = document.createElement("button");
    save.className = "diag-btn";
    save.style.cssText = "margin-top:0;padding:2px 8px;font-size:11px;";
    save.textContent = "Extend";
    save.addEventListener("click", () => void doSetRetention(a.name, input.value, container));
    chip.appendChild(input);
    chip.appendChild(save);
  }
  return chip;
}

async function doRescanAdapters(container: HTMLElement, btn: HTMLButtonElement): Promise<void> {
  btn.disabled = true;
  try {
    await invoke(FrontendCommand.AdapterRescan, {});
  } catch (e) {
    console.warn("adapter.rescan failed", e);
    showInAppToast("Re-scan failed", (e as Error).message, () => {});
  } finally {
    btn.disabled = false;
    await renderToolsTab(container);
  }
}

async function doAddAdapterFromFolder(container: HTMLElement): Promise<void> {
  let picked: unknown;
  try {
    picked = await invokeNative(FrontendCommand.DialogOpenFolder, { initialPath: activeProjectDir() || "/" });
  } catch (e) {
    console.warn("adapter folder picker failed", e);
    return;
  }
  if (picked === null) return;
  const path = typeof picked === "string" ? picked.trim() : "";
  if (!path) { console.warn("adapter folder picker returned nothing", picked); return; }
  try {
    const res = await invoke<{ name: string }>(FrontendCommand.AdapterInstallLocal, { path });
    showInAppToast("Adapter added", `${res.name} installed from folder.`, () => {});
  } catch (e) {
    showInAppToast("Adapter not added", (e as Error).message, () => {});
  }
  await renderToolsTab(container);
}

function openRemoveAdapterDialog(a: ToolsAdapter, container: HTMLElement): void {
  const scrim = document.createElement("div");
  scrim.className = "modal-scrim open";
  scrim.style.cssText = "position:fixed;inset:0;background:rgba(0,0,0,0.4);display:flex;align-items:center;justify-content:center;z-index:9999;";
  const box = document.createElement("div");
  box.style.cssText = "background:var(--surface,#1e1e2e);border:1px solid var(--border);border-radius:10px;padding:18px;width:320px;max-width:90vw;";
  const title = document.createElement("div");
  title.style.cssText = "font-weight:600;margin-bottom:8px;";
  title.textContent = `Remove ${a.displayName || a.name}?`;
  const desc = document.createElement("div");
  desc.className = "set-desc";
  desc.style.marginBottom = "12px";
  desc.textContent = "This deletes the adapter folder. Bundled adapters are not affected.";
  const purgeLabel = document.createElement("label");
  purgeLabel.style.cssText = "display:flex;align-items:center;gap:8px;font-size:12px;margin-bottom:14px;";
  const purge = document.createElement("input");
  purge.type = "checkbox";
  const purgeText = document.createElement("span");
  purgeText.textContent = "Also delete session records for this adapter";
  purgeLabel.appendChild(purge);
  purgeLabel.appendChild(purgeText);
  const btnRow = document.createElement("div");
  btnRow.style.cssText = "display:flex;gap:8px;justify-content:flex-end;";
  const cancel = document.createElement("button");
  cancel.className = "diag-btn";
  cancel.style.marginTop = "0";
  cancel.textContent = "Cancel";
  const confirm = document.createElement("button");
  confirm.className = "diag-btn";
  confirm.style.marginTop = "0";
  confirm.textContent = "Remove";
  const close = (): void => scrim.remove();
  cancel.addEventListener("click", close);
  scrim.addEventListener("mousedown", (e) => { if (e.target === scrim) close(); });
  confirm.addEventListener("click", () => {
    close();
    void doRemoveAdapter(a.name, purge.checked, container);
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

async function doRemoveAdapter(name: string, purgeSessions: boolean, container: HTMLElement): Promise<void> {
  try {
    const res = await invoke<{ name: string; purgedSessions: number }>(FrontendCommand.AdapterRemove, { name, purgeSessions });
    const suffix = res.purgedSessions > 0 ? ` (${res.purgedSessions} session records purged)` : "";
    showInAppToast("Adapter removed", `${res.name} removed${suffix}.`, () => {});
  } catch (e) {
    showInAppToast("Remove failed", (e as Error).message, () => {});
  }
  await renderToolsTab(container);
}

async function doSetRetention(name: string, value: string, container: HTMLElement): Promise<void> {
  try {
    await invoke(FrontendCommand.AdapterRetentionSet, { name, value: value.trim() });
    showInAppToast("Retention updated", `${name} retention set to ${value.trim()}.`, () => {});
  } catch (e) {
    showInAppToast("Retention not saved", (e as Error).message, () => {});
  }
  await renderToolsTab(container);
}

function diagnosticsSectionHeader(text: string): HTMLElement {
  const header = document.createElement("div");
  header.className = "set-section-header";
  header.style.cssText = "padding:12px 0 4px;font-size:11px;font-weight:600;color:var(--muted);text-transform:uppercase;letter-spacing:0.5px;border-bottom:1px solid var(--border);";
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
  hudToggle.className = "diag-toggle" + (perfHudState.enabled ? " on" : "");
  hudToggle.textContent = perfHudState.enabled ? "On" : "Off";
  hudToggle.addEventListener("click", () => doTogglePerfHud());
  hudRow.appendChild(hudLabel);
  hudRow.appendChild(hudToggle);
  container.appendChild(hudRow);

  container.appendChild(diagnosticsSectionHeader("Snapshot inspector"));
  const snapCaption = document.createElement("div");
  snapCaption.className = "diag-caption";
  snapCaption.textContent = "Capture a live diagnostics snapshot from the engine, or paste an exported one (a single object or an array — the same JSON the engine writes to diagnostics-snapshots.json inside a performance bundle).";
  container.appendChild(snapCaption);

  const textarea = document.createElement("textarea");
  textarea.className = "diag-input";
  textarea.placeholder = '{ "takenAt": "…", "managedMemoryBytes": … }';
  container.appendChild(textarea);

  const snapActions = document.createElement("div");
  snapActions.style.cssText = "display:flex;gap:8px;flex-wrap:wrap;";
  container.appendChild(snapActions);

  const renderBtn = document.createElement("button");
  renderBtn.className = "diag-btn";
  renderBtn.textContent = "Inspect snapshot";
  snapActions.appendChild(renderBtn);

  const takeBtn = document.createElement("button");
  takeBtn.className = "diag-btn";
  takeBtn.textContent = "Take snapshot";
  snapActions.appendChild(takeBtn);

  const loadBtn = document.createElement("button");
  loadBtn.className = "diag-btn";
  loadBtn.textContent = "Load snapshots";
  snapActions.appendChild(loadBtn);

  const output = document.createElement("div");
  output.className = "diag-snap";
  container.appendChild(output);

  renderBtn.addEventListener("click", () => renderSnapshotInspection(textarea.value, output));
  takeBtn.addEventListener("click", () => void doTakeSnapshot(textarea, output));
  loadBtn.addEventListener("click", () => void doLoadSnapshots(textarea, output));

  container.appendChild(diagnosticsSectionHeader("Performance bundles"));
  renderPerfBundles(container);

  container.appendChild(diagnosticsSectionHeader("Not yet available"));
  const note = document.createElement("div");
  note.className = "diag-note";
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
  err.className = "diag-error";
  err.textContent = message;
  output.appendChild(err);
}

function renderPerfBundles(container: HTMLElement): void {
  let state: PerfBundlesState = initialPerfBundlesState();

  const caption = document.createElement("div");
  caption.className = "diag-caption";
  caption.textContent = "Create a performance bundle to package the engine's diagnostics snapshots into a shareable .zip, then manage the saved bundles below.";
  container.appendChild(caption);

  const createBtn = document.createElement("button");
  createBtn.className = "diag-btn";
  container.appendChild(createBtn);

  const errorEl = document.createElement("div");
  errorEl.className = "diag-error";
  container.appendChild(errorEl);

  const listEl = document.createElement("div");
  listEl.className = "diag-snap";
  container.appendChild(listEl);

  const paint = (): void => {
    createBtn.textContent = state.creating ? "Creating…" : "Create bundle";
    createBtn.disabled = state.creating;
    errorEl.textContent = state.error ?? "";
    errorEl.style.display = state.error ? "block" : "none";
    renderPerfBundleList(state, listEl, run);
  };

  const run = (next: PerfBundlesState): void => {
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
    empty.className = "diag-caption";
    empty.textContent = PERF_BUNDLES_EMPTY_TEXT;
    listEl.appendChild(empty);
    return;
  }

  for (const row of rows) {
    const card = document.createElement("div");
    card.className = "diag-snap-card";
    card.style.cssText = "display:flex;gap:12px;align-items:center;justify-content:space-between;";

    const info = document.createElement("div");
    info.style.cssText = "min-width:0;flex:1;";
    const name = document.createElement("div");
    name.style.cssText = "font-size:12px;color:var(--fg);overflow:hidden;text-overflow:ellipsis;white-space:nowrap;";
    name.textContent = row.name;
    name.title = row.bundlePath;
    const meta = document.createElement("div");
    meta.style.cssText = "font-size:11px;color:var(--muted);";
    meta.textContent = `${row.createdAtLabel} · ${row.sizeLabel} · ${row.detail}`;
    info.appendChild(name);
    info.appendChild(meta);
    card.appendChild(info);

    const actions = document.createElement("div");
    actions.style.cssText = "display:flex;gap:6px;flex-shrink:0;";
    if (row.confirmingDelete) {
      const confirm = document.createElement("button");
      confirm.className = "diag-btn";
      confirm.textContent = "Confirm";
      confirm.addEventListener("click", () => void doDeleteBundle(state, row.bundlePath, run));
      const cancel = document.createElement("button");
      cancel.className = "diag-btn";
      cancel.textContent = "Cancel";
      cancel.addEventListener("click", () => run(cancelDelete(state)));
      actions.appendChild(confirm);
      actions.appendChild(cancel);
    } else {
      const del = document.createElement("button");
      del.className = "diag-btn";
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
    err.className = "diag-error";
    err.textContent = result.error ?? "Could not read snapshot.";
    output.appendChild(err);
    return;
  }

  const summary = summarizeSnapshots(result.snapshots);
  const summaryEl = document.createElement("div");
  summaryEl.className = "diag-caption";
  summaryEl.textContent = `${summary.count} snapshot${summary.count === 1 ? "" : "s"} · peak managed memory ${formatSnapshotBytes(summary.peakManagedMemoryBytes)}`;
  output.appendChild(summaryEl);

  for (const snapshot of result.snapshots) appendSnapshotCard(snapshot, output);
}

function appendSnapshotCard(snapshot: DiagnosticsSnapshot, output: HTMLElement): void {
  const card = document.createElement("div");
  card.className = "diag-snap-card";
  for (const row of snapshotRows(snapshot)) {
    const rowEl = document.createElement("div");
    rowEl.className = "diag-snap-row";
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

async function loadSettingValue(entry: ConfigSchemaEntry, row: HTMLElement): Promise<void> {
  let currentValue = "";
  try {
    const res = await invoke<{ value: string } | null>(FrontendCommand.ConfigGet, { key: entry.key });
    currentValue = res?.value ?? "";
  } catch { void 0; }

  const input = createSettingControl(entry, currentValue);
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
    select.style.cssText = "width:140px;";
    return select;
  }
  if (entry.type === "bool" || entry.control === "toggle") {
    const select = document.createElement("select");
    const t = document.createElement("option"); t.value = "true"; t.textContent = "On"; select.appendChild(t);
    const f = document.createElement("option"); f.value = "false"; f.textContent = "Off"; select.appendChild(f);
    select.value = value === "true" ? "true" : "false";
    select.style.cssText = "width:120px;";
    return select;
  }
  if (entry.type === "int" || entry.type === "double") {
    const input = document.createElement("input");
    input.type = "number";
    input.value = value;
    input.style.cssText = "width:120px;";
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

function renderThemeEditor(container: HTMLElement): void {
  void loadThemeData().then(() => renderThemeEditorBody(container));
  container.innerHTML = `<div style="padding:20px;color:var(--muted);text-align:center;">Loading themes…</div>`;
}

function renderThemeEditorBody(container: HTMLElement): void {
  container.innerHTML = "";

  const dropdownRow = document.createElement("div");
  dropdownRow.style.cssText = "padding:12px 0;display:flex;align-items:center;gap:10px;border-bottom:1px solid var(--border);";
  const dropdownLabel = document.createElement("span");
  dropdownLabel.textContent = "Active theme";
  dropdownLabel.style.cssText = "font-size:12px;color:var(--muted);";
  const dropdown = document.createElement("select");
  dropdown.style.cssText = "background:var(--panel-2);border:1px solid var(--border);color:var(--fg);border-radius:6px;padding:4px 8px;min-width:160px;";
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
  deleteBtn.textContent = "Delete";
  deleteBtn.style.cssText = "margin-left:auto;background:transparent;border:1px solid var(--border);color:var(--muted);border-radius:6px;padding:4px 10px;font-size:11px;cursor:pointer;";
  deleteBtn.disabled = !canDelete(themeActiveName ?? "", themeCustomNames);
  deleteBtn.addEventListener("click", () => void onThemeDelete(themeActiveName ?? ""));
  dropdownRow.appendChild(deleteBtn);
  container.appendChild(dropdownRow);

  const editorHeader = document.createElement("div");
  editorHeader.style.cssText = "padding:12px 0 4px;font-size:11px;font-weight:600;color:var(--muted);text-transform:uppercase;letter-spacing:0.5px;";
  editorHeader.textContent = "Edit & preview";
  container.appendChild(editorHeader);

  const nameRow = document.createElement("div");
  nameRow.className = "set-row";
  const nameLabel = document.createElement("label");
  nameLabel.textContent = "Theme name";
  const nameInput = document.createElement("input");
  nameInput.type = "text";
  nameInput.value = themeDraft.name;
  nameInput.style.cssText = "background:var(--panel-2);border:1px solid var(--border);color:var(--fg);border-radius:6px;padding:4px 8px;width:180px;";
  nameInput.addEventListener("input", () => { themeDraft.name = nameInput.value; updateThemePreview(); });
  nameLabel.appendChild(nameInput);
  nameRow.appendChild(nameLabel);
  container.appendChild(nameRow);

  const typeRow = document.createElement("div");
  typeRow.className = "set-row";
  const typeLabel = document.createElement("label");
  typeLabel.textContent = "Type";
  const typeSelect = document.createElement("select");
  for (const tp of ["dark", "light"]) { const o = document.createElement("option"); o.value = tp; o.textContent = tp; typeSelect.appendChild(o); }
  typeSelect.value = themeDraft.type;
  typeSelect.style.cssText = "background:var(--panel-2);border:1px solid var(--border);color:var(--fg);border-radius:6px;padding:4px 8px;width:120px;";
  typeSelect.addEventListener("change", () => { themeDraft.type = typeSelect.value; updateThemePreview(); });
  typeLabel.appendChild(typeSelect);
  typeRow.appendChild(typeLabel);
  container.appendChild(typeRow);

  for (const field of THEME_COLOR_FIELDS) {
    const row = document.createElement("div");
    row.className = "set-row";
    row.style.cssText = "flex-direction:row;align-items:center;gap:10px;";
    const label = document.createElement("label");
    label.style.cssText = "flex-direction:column;gap:2px;flex:1;";
    const labelText = document.createElement("span");
    labelText.textContent = field.label;
    label.appendChild(labelText);
    if (field.desc) { const d = document.createElement("span"); d.className = "set-desc"; d.textContent = field.desc; label.appendChild(d); }
    const colorInput = document.createElement("input");
    colorInput.type = "color";
    colorInput.value = (themeDraft as unknown as Record<string, string>)[field.key];
    colorInput.style.cssText = "width:40px;height:28px;border:1px solid var(--border);border-radius:6px;background:transparent;cursor:pointer;";
    const hexInput = document.createElement("input");
    hexInput.type = "text";
    hexInput.value = (themeDraft as unknown as Record<string, string>)[field.key];
    hexInput.style.cssText = "background:var(--panel-2);border:1px solid var(--border);color:var(--fg);border-radius:6px;padding:4px 8px;width:100px;font-family:monospace;";
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
  contrastInfo.style.cssText = "padding:8px 0;font-size:11px;color:var(--muted);";
  container.appendChild(contrastInfo);

  const actions = document.createElement("div");
  actions.style.cssText = "padding:12px 0;display:flex;gap:10px;";
  const saveBtn = document.createElement("button");
  saveBtn.textContent = "Save as custom";
  saveBtn.style.cssText = "background:var(--accent);border:none;color:#000;border-radius:6px;padding:6px 14px;font-size:12px;cursor:pointer;font-weight:600;";
  saveBtn.addEventListener("click", () => void onThemeSave());
  const resetBtn = document.createElement("button");
  resetBtn.textContent = "Reset preview";
  resetBtn.style.cssText = "background:transparent;border:1px solid var(--border);color:var(--muted);border-radius:6px;padding:6px 14px;font-size:12px;cursor:pointer;";
  resetBtn.addEventListener("click", () => { revertThemeVars(); if (themeActiveName) { const t = themeList.find((x) => x.name === themeActiveName); if (t) { themeDraft = draftFromTheme(t); } } else { themeDraft = { ...DEFAULT_DRAFT }; } renderThemeEditorBody(container); });
  actions.appendChild(saveBtn);
  actions.appendChild(resetBtn);
  container.appendChild(actions);
}

function updateThemePreview(): void {
  const theme = themeFromDraft(themeDraft);
  applyThemeVars(theme);
  const contrastEl = document.getElementById("theme-contrast");
  if (contrastEl) {
    const fgBg = contrastRatio(themeDraft.terminalForeground, themeDraft.terminalBackground);
    const tier = contrastTier(fgBg);
    contrastEl.textContent = `Terminal contrast: ${fgBg.toFixed(2)}:1 (${tier === "fail" ? "below AA" : tier})`;
    contrastEl.style.color = tier === "fail" ? "#e06c75" : "var(--muted)";
  }
  const saveBtn = document.querySelector("#set-body button");
  if (saveBtn && saveBtn.textContent === "Save as custom") {
    saveBtn.setAttribute("data-valid", canSaveDraft(themeDraft) ? "1" : "0");
  }
}

async function onThemeSelect(name: string): Promise<void> {
  if (!name) { themeActiveName = null; revertThemeVars(); renderThemeEditor(setBodyEl); return; }
  try {
    const res = await invoke<{ theme: ThemeDto }>(FrontendCommand.ThemeSetActive, { name });
    themeActiveName = name;
    if (res.theme) { themeDraft = draftFromTheme(res.theme); applyThemeVars(res.theme); }
    await loadThemeData();
    renderThemeEditorBody(setBodyEl);
  } catch { void 0; }
}

async function onThemeSave(): Promise<void> {
  if (!canSaveDraft(themeDraft)) return;
  try {
    await invoke(FrontendCommand.ThemeSaveCustom, themeDraft);
    await invoke(FrontendCommand.ThemeSetActive, { name: themeDraft.name });
    themeActiveName = themeDraft.name;
    await loadThemeData();
    renderThemeEditorBody(setBodyEl);
  } catch { void 0; }
}

async function onThemeDelete(name: string): Promise<void> {
  if (!canDelete(name, themeCustomNames)) return;
  try {
    await invoke(FrontendCommand.ThemeDeleteCustom, { name });
    if (themeActiveName === name) { themeActiveName = null; revertThemeVars(); }
    await loadThemeData();
    renderThemeEditorBody(setBodyEl);
  } catch { void 0; }
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

function renderKeyboardEditor(container: HTMLElement): void {
  void loadKeybindData().then(() => renderKeyboardEditorBody(container));
  container.innerHTML = `<div style="padding:20px;color:var(--muted);text-align:center;">Loading keybindings…</div>`;
}

function renderKeyboardEditorBody(container: HTMLElement): void {
  removeKeybindRecordingListener();
  container.innerHTML = "";
  const categories = categorizeBindings(keybindList, keybindConflicts, []);

  if (keybindConflicts.length > 0) {
    const warn = document.createElement("div");
    warn.style.cssText = "padding:8px 12px;margin-bottom:8px;background:color-mix(in srgb, #e06c75 15%, transparent);border:1px solid #e06c75;border-radius:6px;font-size:11px;color:#e5a0a8;";
    warn.textContent = `Conflicts: ${keybindConflicts.join(", ")} — two actions share the same chord`;
    container.appendChild(warn);
  }

  for (const cat of categories) {
    const header = document.createElement("div");
    header.className = "set-section-header";
    header.style.cssText = "padding:12px 0 4px;font-size:11px;font-weight:600;color:var(--muted);text-transform:uppercase;letter-spacing:0.5px;border-bottom:1px solid var(--border);";
    header.textContent = cat.name;
    container.appendChild(header);

    for (const row of cat.rows) {
      const rowEl = document.createElement("div");
      rowEl.className = "set-row";
      rowEl.style.cssText = "flex-direction:row;align-items:center;gap:10px;";
      const label = document.createElement("label");
      label.style.cssText = "flex-direction:column;gap:2px;flex:1;";
      const labelText = document.createElement("span");
      labelText.textContent = row.description ?? row.action;
      label.appendChild(labelText);
      const actionLabel = document.createElement("span");
      actionLabel.className = "set-desc";
      actionLabel.textContent = row.action;
      label.appendChild(actionLabel);
      rowEl.appendChild(label);

      const chordBtn = document.createElement("button");
      chordBtn.textContent = chordDisplay(row.chord);
      chordBtn.style.cssText = `background:var(--panel-2);border:1px solid ${row.hasConflict ? "#e06c75" : "var(--border)"};color:var(--fg);border-radius:6px;padding:4px 10px;font-size:11px;font-family:monospace;min-width:80px;cursor:pointer;${keybindRecordingAction === row.action ? "outline:2px solid var(--accent);" : ""}`;
      if (keybindRecordingAction === row.action) { chordBtn.textContent = "Press keys…"; }
      chordBtn.addEventListener("click", () => { keybindRecordingAction = keybindRecordingAction === row.action ? null : row.action; renderKeyboardEditorBody(container); });
      rowEl.appendChild(chordBtn);

      const clearBtn = document.createElement("button");
      clearBtn.textContent = "×";
      clearBtn.style.cssText = "background:transparent;border:1px solid var(--border);color:var(--muted);border-radius:6px;padding:4px 8px;font-size:13px;cursor:pointer;";
      clearBtn.addEventListener("click", () => void onKeybindClear(row.chord, container));
      rowEl.appendChild(clearBtn);

      container.appendChild(rowEl);
    }
  }

  if (keybindRecordingAction) {
    const hint = document.createElement("div");
    hint.style.cssText = "padding:8px 0;font-size:11px;color:var(--muted);";
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
    if ((event as KeyboardEvent).key === "Escape") closeSettings();
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
    settingsEl.classList.remove("open");
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
