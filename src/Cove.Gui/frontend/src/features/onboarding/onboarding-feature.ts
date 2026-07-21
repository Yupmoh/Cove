import { FrontendCommand } from "../../app/frontend-command";
import { LifecycleScope } from "../../app/lifecycle";
import { invoke, invokeNative } from "../../invoke";
import {
  INITIAL_ONBOARDING_STATE,
  nextStep,
  prevStep,
  dismiss as dismissOnboarding,
  currentStepData,
  isLastStep,
  isFirstStep,
  progressPercent,
  setDefaultBayDir,
  setAdapterYolo,
  setBackdrop as setOnboardingBackdrop,
  setTheme as setOnboardingTheme,
  setAgentChimes as setOnboardingAgentChimes,
  shouldShowOnboarding,
  onboardingSeenFromConfig,
  ONBOARDING_COMPLETED_KEY,
  partitionOnboardingAdapters,
  type OnboardingState,
} from "../../onboarding";
import { DICTATION_SPACE_KEY, DICTATION_LIVE_TYPING_KEY, dictationToggleEnabled, modelPollOutcome } from "../../dictation";
import { coerceMaterial, setBackdropMaterial, BACKDROP_PREF_KEY, type BackdropDeps, type BackdropMaterial } from "../../backdrop";
import { playChime } from "../../chime";
import type { ThemeDto } from "../../theme-editor";

export interface OnboardingLauncherAdapter {
  name: string;
  displayName: string;
  status?: string | null;
  version?: string | null;
  description?: string | null;
  installCommand?: string | null;
}

interface AdapterListResult {
  adapters: unknown[];
}

export interface OnboardingFeatureDependencies {
  root: HTMLElement;
  backdrop: BackdropDeps;
  getBackdropMaterial(): BackdropMaterial;
  updateBackdropMaterial(material: BackdropMaterial): void;
  getActiveThemeName(): string | null;
  setAgentChimesEnabled(enabled: boolean): void;
  agentChimesEnabled(): boolean;
  mapLauncherAdapters(adapters: unknown[] | null | undefined): OnboardingLauncherAdapter[];
  launchHarnessShellTask(command: string, shoreName: string): Promise<void>;
  launcherYolo(adapter: string): boolean;
  launcherYoloKey(adapter: string): string;
}

export interface OnboardingFeature {
  maybeShow(): Promise<void>;
  rerun(): void;
  renderDictationTab(container: HTMLElement): void;
  setDictationModelError(error: string | null): void;
  dispose(): Promise<void>;
}

export function createOnboardingFeature(dependencies: OnboardingFeatureDependencies): OnboardingFeature {
  const onboardingEl = dependencies.root;
  const document = onboardingEl.ownerDocument;
  const window = document.defaultView ?? globalThis.window;
  const backdropDeps = dependencies.backdrop;
  const setAgentChimesEnabled = dependencies.setAgentChimesEnabled;
  const agentChimesEnabled = dependencies.agentChimesEnabled;
  const mapLauncherAdapters = dependencies.mapLauncherAdapters;
  const launchHarnessShellTask = dependencies.launchHarnessShellTask;
  const launcherYolo = dependencies.launcherYolo;
  const launcherYoloKey = dependencies.launcherYoloKey;
  const lifecycle = new LifecycleScope();
  const modelPolls = new Set<number>();
  const setModelPoll = (callback: () => void, delayMs: number): number => {
    const poll = window.setInterval(callback, delayMs);
    modelPolls.add(poll);
    return poll;
  };
  const clearModelPoll = (poll: number): void => {
    window.clearInterval(poll);
    modelPolls.delete(poll);
  };
  let previousFocus: HTMLElement | null = null;
  let wizardAdapters: OnboardingLauncherAdapter[] | null = null;
  let scanSequence = 0;

let onboardingState: OnboardingState = { ...INITIAL_ONBOARDING_STATE };

async function maybeShowOnboarding(): Promise<void> {
  try {
    const seen = await invoke<{ value?: string }>(FrontendCommand.AppConfigGet, { key: ONBOARDING_COMPLETED_KEY });
    const hasSeen = onboardingSeenFromConfig(seen.value);
    if (!shouldShowOnboarding(hasSeen)) return;
    openOnboarding();
  } catch { void 0; }
}

function renderOnboarding(): void {
  const step = currentStepData(onboardingState);
  const title = onboardingEl.querySelector(".ob-title") as HTMLElement;
  title.textContent = step.title;
  (onboardingEl.querySelector(".ob-progress-bar") as HTMLProgressElement).value = progressPercent(onboardingState);
  const body = onboardingEl.querySelector(".ob-body") as HTMLElement;
  body.innerHTML = "";
  const p = document.createElement("p");
  p.id = "ob-step-description";
  p.className = "ob-intro";
  p.textContent = step.body;
  body.appendChild(p);

  if (step.id === "harness") { void renderHarnessStep(body); }
  if (step.id === "permissions") { void renderPermissionsStep(body); }
  if (step.id === "appearance") { renderAppearanceStep(body); }
  if (step.id === "sound") { renderSoundStep(body); }
  if (step.id === "dictation") { renderDictationStep(body); }

  const prevBtn = onboardingEl.querySelector(".ob-prev") as HTMLButtonElement;
  const nextBtn = onboardingEl.querySelector(".ob-next") as HTMLButtonElement;
  prevBtn.disabled = isFirstStep(onboardingState);
  nextBtn.textContent = isLastStep(onboardingState) ? "Finish" : "Next";
}

async function renderHarnessStep(body: HTMLElement): Promise<void> {
  const results = document.createElement("div");
  results.className = "ob-scan-results";
  results.setAttribute("aria-live", "polite");
  results.setAttribute("aria-busy", "true");
  body.appendChild(results);
  renderScanResults(results, wizardAdapters, true);
  void refreshWizardAdapters(results);
  const dirRow = document.createElement("div");
  dirRow.className = "ob-directory";
  const dirLabel = document.createElement("label");
  dirLabel.htmlFor = "ob-default-bay-dir";
  dirLabel.textContent = "Default bay directory";
  const dirControls = document.createElement("div");
  dirControls.className = "ob-directory-controls";
  const dirInput = document.createElement("input");
  dirInput.type = "text";
  dirInput.id = "ob-default-bay-dir";
  dirInput.className = "ob-directory-input";
  dirInput.setAttribute("aria-describedby", "ob-default-bay-help ob-default-bay-error");
  dirInput.value = onboardingState.defaultBayDir ?? "";
  dirInput.addEventListener("input", () => { onboardingState = setDefaultBayDir(onboardingState, dirInput.value.trim() || null); });
  const browse = document.createElement("button");
  browse.type = "button";
  browse.className = "ob-browse";
  browse.textContent = "Browse…";
  browse.addEventListener("click", async () => {
    try {
      const picked = await invokeNative(FrontendCommand.DialogOpenFolder, { initialPath: dirInput.value.trim() || "/" });
      if (typeof picked === "string" && picked.trim()) {
        dirInput.value = picked.trim();
        onboardingState = setDefaultBayDir(onboardingState, picked.trim());
      }
    } catch (e) {
      console.warn("wizard folder picker failed", e);
      error.textContent = "Couldn’t open the folder picker. Your current directory is unchanged.";
    }
  });
  const help = document.createElement("div");
  help.id = "ob-default-bay-help";
  help.className = "ob-directory-help";
  help.textContent = "New bays start here. You can change this later in Settings.";
  const error = document.createElement("div");
  error.id = "ob-default-bay-error";
  error.className = "ob-directory-error";
  dirControls.append(dirInput, browse);
  dirRow.append(dirLabel, dirControls, help, error);
  body.appendChild(dirRow);
}

async function refreshWizardAdapters(results: HTMLElement): Promise<void> {
  const sequence = ++scanSequence;
  results.setAttribute("aria-busy", "true");
  renderScanResults(results, wizardAdapters, true);
  try {
    const listed = await invoke<AdapterListResult>(FrontendCommand.AppAdapterList, {});
    if (sequence !== scanSequence || !results.isConnected) return;
    wizardAdapters = mapLauncherAdapters(listed.adapters);
    results.setAttribute("aria-busy", "false");
    renderScanResults(results, wizardAdapters, false);
  } catch (err) {
    console.warn("wizard adapter list failed", err);
    if (sequence !== scanSequence || !results.isConnected) return;
    results.setAttribute("aria-busy", "false");
    results.replaceChildren();
    const message = document.createElement("p");
    message.className = "ob-scan-message";
    message.textContent = "Cove couldn’t scan your tools.";
    const retry = document.createElement("button");
    retry.type = "button";
    retry.className = "ob-scan-retry";
    retry.textContent = "Retry";
    retry.addEventListener("click", () => { void refreshWizardAdapters(results); });
    results.append(message, retry);
  }
}

function renderScanResults(results: HTMLElement, adapters: OnboardingLauncherAdapter[] | null, loading: boolean): void {
  results.replaceChildren();
  if (loading) {
    const scanning = document.createElement("p");
    scanning.className = "ob-scan-message";
    scanning.textContent = "Scanning your login shell…";
    results.appendChild(scanning);
    if (!adapters) return;
  }
  if (!adapters) return;
  const { installed, installable } = partitionOnboardingAdapters(adapters);
  const installedSection = document.createElement("section");
  installedSection.className = "ob-tool-section ob-installed";
  const installedTitle = document.createElement("h3");
  installedTitle.textContent = `Installed · ${installed.length}`;
  installedSection.appendChild(installedTitle);
  if (installed.length === 0) {
    const empty = document.createElement("p");
    empty.className = "ob-empty";
    empty.textContent = "No coding tools detected yet.";
    installedSection.appendChild(empty);
  } else {
    const installedRows = document.createElement("div");
    installedRows.className = "ob-installed-rows";
    for (const adapter of installed) {
      const row = document.createElement("div");
      row.className = "ob-installed-tool";
      const mark = document.createElement("span");
      mark.className = "ob-status-mark";
      mark.setAttribute("aria-hidden", "true");
      mark.textContent = "✓";
      const name = document.createElement("span");
      name.className = "ob-tool-name";
      name.textContent = adapter.displayName || adapter.name;
      row.append(mark, name);
      if (adapter.version?.trim()) {
        const version = document.createElement("span");
        version.className = "ob-tool-version";
        version.textContent = adapter.version.trim();
        row.appendChild(version);
      }
      installedRows.appendChild(row);
    }
    installedSection.appendChild(installedRows);
  }
  results.appendChild(installedSection);
  if (installable.length > 0) {
    const installableSection = document.createElement("section");
    installableSection.className = "ob-tool-section ob-installable";
    const installableTitle = document.createElement("h3");
    installableTitle.textContent = `Install more · ${installable.length}`;
    const installableRows = document.createElement("div");
    installableRows.className = "ob-installable-rows";
    for (const adapter of installable) installableRows.appendChild(buildInstallableRow(adapter));
    installableSection.append(installableTitle, installableRows);
    results.appendChild(installableSection);
  } else if (installed.length === 0) {
    const available = document.createElement("p");
    available.className = "ob-empty ob-all-available";
    available.textContent = "Every available tool is already installed or has no automatic installer.";
    results.appendChild(available);
  }
}

function buildInstallableRow(adapter: OnboardingLauncherAdapter): HTMLElement {
  const row = document.createElement("div");
  row.className = "ob-installable-tool";
  const details = document.createElement("div");
  details.className = "ob-tool-details";
  const name = document.createElement("span");
  name.className = "ob-tool-name";
  name.textContent = adapter.displayName || adapter.name;
  details.appendChild(name);
  const descriptionText = adapter.description?.trim() || (adapter.status === "broken" ? "Installation needs repair." : "Not installed.");
  const description = document.createElement("span");
  description.className = "ob-tool-description";
  description.textContent = descriptionText;
  details.appendChild(description);
  const action = adapter.status === "broken" ? "Reinstall" : "Install";
  const button = document.createElement("button");
  button.type = "button";
  button.className = "ob-install-btn";
  button.textContent = action;
  button.setAttribute("aria-label", `${action} ${adapter.displayName || adapter.name}`);
  button.addEventListener("click", () => {
    button.disabled = true;
    const label = `${action} ${adapter.displayName || adapter.name}`;
    void completeOnboarding().then(() => launchHarnessShellTask((adapter.installCommand ?? "").trim(), label));
  });
  row.append(details, button);
  return row;
}

async function renderPermissionsStep(body: HTMLElement): Promise<void> {
  const list = document.createElement("div");
  list.className = "ob-adapter-list";
  body.appendChild(list);
  const installed = partitionOnboardingAdapters(wizardAdapters ?? []).installed;
  if (installed.length === 0) {
    const none = document.createElement("div");
    none.className = "ob-adapter";
    none.textContent = "No adapters to configure yet.";
    list.appendChild(none);
    return;
  }
  for (const a of installed) {
    const row = document.createElement("label");
    row.className = "ob-telemetry-toggle";
    const name = document.createElement("span");
    name.textContent = `${a.displayName || a.name} — bypass permissions (YOLO)`;
    name.className = "ob-toggle-copy";
    const cb = document.createElement("input");
    cb.type = "checkbox";
    cb.checked = onboardingState.adapterYolo[a.name] ?? launcherYolo(a.name);
    cb.addEventListener("change", () => { onboardingState = setAdapterYolo(onboardingState, a.name, cb.checked); });
    row.appendChild(name);
    row.appendChild(cb);
    list.appendChild(row);
  }
}

function renderAppearanceStep(body: HTMLElement): void {
  const backdropRow = document.createElement("div");
  backdropRow.className = "ob-preference-row";
  const backdropLabel = document.createElement("span");
  backdropLabel.className = "ob-preference-label";
  backdropLabel.textContent = "Backdrop";
  const backdropSel = document.createElement("select");
  backdropSel.className = "ob-preference-input";
  for (const m of ["none", "blur", "acrylic", "mica"]) {
    const opt = document.createElement("option");
    opt.value = m;
    opt.textContent = m;
    backdropSel.appendChild(opt);
  }
  backdropSel.value = onboardingState.backdrop || dependencies.getBackdropMaterial();
  backdropSel.addEventListener("change", () => {
    onboardingState = setOnboardingBackdrop(onboardingState, backdropSel.value);
    dependencies.updateBackdropMaterial(coerceMaterial(backdropSel.value));
    void setBackdropMaterial(dependencies.getBackdropMaterial(), backdropDeps);
  });
  backdropRow.appendChild(backdropLabel);
  backdropRow.appendChild(backdropSel);
  body.appendChild(backdropRow);

  const themeRow = document.createElement("div");
  themeRow.className = "ob-preference-row";
  const themeLabel = document.createElement("span");
  themeLabel.className = "ob-preference-label";
  themeLabel.textContent = "Theme";
  const themeSel = document.createElement("select");
  themeSel.className = "ob-preference-input";
  themeRow.appendChild(themeLabel);
  themeRow.appendChild(themeSel);
  body.appendChild(themeRow);
  void invoke<{ themes: ThemeDto[] }>(FrontendCommand.ThemeList, {}).then((r) => {
    for (const t of r.themes ?? []) {
      const opt = document.createElement("option");
      opt.value = t.name;
      opt.textContent = t.name;
      themeSel.appendChild(opt);
    }
    const activeThemeName = dependencies.getActiveThemeName();
    if (onboardingState.theme) themeSel.value = onboardingState.theme;
    else if (activeThemeName) themeSel.value = activeThemeName;
  }).catch(() => { void 0; });
  themeSel.addEventListener("change", () => {
    onboardingState = setOnboardingTheme(onboardingState, themeSel.value);
    void invoke(FrontendCommand.ThemeSetActive, { name: themeSel.value }).catch((e) => console.warn("wizard theme set failed", e));
  });
}

function renderSoundStep(body: HTMLElement): void {
  const toggle = document.createElement("label");
  toggle.className = "ob-telemetry-toggle";
  const cb = document.createElement("input");
  cb.type = "checkbox";
  cb.checked = onboardingState.agentChimes;
  const label = document.createElement("span");
  label.textContent = "Agent chimes — soft tone when an agent finishes or needs input";
  label.className = "ob-toggle-copy";
  cb.addEventListener("change", () => {
    onboardingState = setOnboardingAgentChimes(onboardingState, cb.checked);
    setAgentChimesEnabled(cb.checked);
    if (cb.checked) playChime("done");
  });
  toggle.appendChild(cb);
  toggle.appendChild(label);
  body.appendChild(toggle);
}

interface DictationStatusResult { state?: string; modelReady?: boolean }

async function dictationStatus(): Promise<DictationStatusResult> {
  try {
    return JSON.parse(String(await invokeNative(FrontendCommand.AppDictationStatus, {}))) as DictationStatusResult;
  } catch (e) {
    console.warn("dictation status failed", e);
    return {};
  }
}

function dictationPrefRow(key: string, title: string, hint: string): HTMLElement {
  const row = document.createElement("div");
  row.className = "set-row";
  const label = document.createElement("label");
  label.className = "ob-pref-copy";
  const name = document.createElement("span");
  name.textContent = title;
  const sub = document.createElement("span");
  sub.className = "ob-pref-hint";
  sub.textContent = hint;
  label.appendChild(name);
  label.appendChild(sub);
  const cb = document.createElement("input");
  cb.type = "checkbox";
  cb.checked = dictationToggleEnabled(localStorage.getItem(key));
  cb.addEventListener("change", () => localStorage.setItem(key, cb.checked ? "true" : "false"));
  row.appendChild(label);
  row.appendChild(cb);
  return row;
}

let dictationModelError: string | null = null;

function buildDictationModelControls(container: HTMLElement): void {
  const status = document.createElement("div");
  status.className = "ob-model-status";
  const text = document.createElement("span");
  text.className = "ob-model-copy";
  text.textContent = "Speech model: checking…";
  const btn = document.createElement("button");
  btn.className = "ob-model-action";
  btn.textContent = "Download now";
  btn.classList.add("ob-hidden");
  status.appendChild(text);
  status.appendChild(btn);
  container.appendChild(status);

  const refresh = async (): Promise<void> => {
    const s = await dictationStatus();
    if (!status.isConnected) return;
    if (s.modelReady) {
      text.textContent = "Speech model: Parakeet TDT 0.6B v3 — downloaded";
      btn.classList.add("ob-hidden");
    } else {
      text.textContent = "Speech model: Parakeet TDT 0.6B v3 (487 MB) — not downloaded";
      btn.classList.remove("ob-hidden");
    }
  };
  void refresh();
  btn.addEventListener("click", () => {
    btn.disabled = true;
    btn.textContent = "Downloading…";
    dictationModelError = null;
    const fail = (msg: string): void => {
      if (!status.isConnected) return;
      text.textContent = `Speech model: download failed — ${msg}`;
      btn.disabled = false;
      btn.textContent = "Retry";
      btn.classList.remove("ob-hidden");
    };
    void invokeNative(FrontendCommand.AppDictationEnsureModel, {}).catch((e) => {
      console.warn("dictation model download failed", e);
      fail(String(e));
    });
    const poll = setModelPoll(() => {
      void dictationStatus().then((s) => {
        if (!status.isConnected) {
          clearModelPoll(poll);
          return;
        }
        const outcome = modelPollOutcome(s.modelReady, dictationModelError);
        if (outcome.kind === "ready") {
          clearModelPoll(poll);
          btn.disabled = false;
          btn.textContent = "Download now";
          void refresh();
        } else if (outcome.kind === "failed") {
          clearModelPoll(poll);
          fail(outcome.error);
        }
      });
    }, 2000);
  });
}

function renderDictationTab(container: HTMLElement): void {
  container.innerHTML = "";
  const info = document.createElement("p");
  info.className = "ob-dictation-info";
  info.textContent = "Hold F9 — or hold Space in a terminal or text field — to dictate. Speech is recognized on this machine with NVIDIA Parakeet; audio never leaves it. Words stream in live and settle when you release.";
  container.appendChild(info);
  container.appendChild(dictationPrefRow(DICTATION_SPACE_KEY, "Hold Space to dictate", "Long-press Space (~300 ms) starts dictation; a quick tap still types a space."));
  container.appendChild(dictationPrefRow(DICTATION_LIVE_TYPING_KEY, "Type live preview into the focused target", "Off shows the running transcript in the status pill only; text lands on release."));
  buildDictationModelControls(container);
}

function renderDictationStep(body: HTMLElement): void {
  const toggle = document.createElement("label");
  toggle.className = "ob-telemetry-toggle";
  const cb = document.createElement("input");
  cb.type = "checkbox";
  cb.checked = dictationToggleEnabled(localStorage.getItem(DICTATION_SPACE_KEY));
  cb.addEventListener("change", () => localStorage.setItem(DICTATION_SPACE_KEY, cb.checked ? "true" : "false"));
  const label = document.createElement("span");
  label.textContent = "Hold Space to dictate — a quick tap still types a space (F9 always works)";
  label.className = "ob-toggle-copy";
  toggle.appendChild(cb);
  toggle.appendChild(label);
  body.appendChild(toggle);
  buildDictationModelControls(body);
}

async function onOnboardingNext(): Promise<void> {
  if (isLastStep(onboardingState)) {
    await completeOnboarding();
    return;
  }
  onboardingState = nextStep(onboardingState);
  renderOnboarding();
}

function onOnboardingPrev(): void {
  onboardingState = prevStep(onboardingState);
  renderOnboarding();
}

async function onOnboardingSkip(): Promise<void> {
  onboardingState = dismissOnboarding(onboardingState);
  await completeOnboarding();
}

async function completeOnboarding(): Promise<void> {
  onboardingEl.classList.remove("open");
  for (const [adapter, on] of Object.entries(onboardingState.adapterYolo)) {
    localStorage.setItem(launcherYoloKey(adapter), String(on));
  }
  setAgentChimesEnabled(onboardingState.agentChimes);
  try {
    await invoke(FrontendCommand.AppConfigSet, { key: ONBOARDING_COMPLETED_KEY, value: "true" });
    if (onboardingState.defaultBayDir) { await invoke(FrontendCommand.AppConfigSet, { key: "bays.defaultDir", value: onboardingState.defaultBayDir }); }
    await invoke(FrontendCommand.AppConfigSet, { key: BACKDROP_PREF_KEY, value: onboardingState.backdrop });
    if (onboardingState.theme) { await invoke(FrontendCommand.AppConfigSet, { key: "theme", value: onboardingState.theme }); }
  } catch (e) { console.warn("onboarding persist failed", e); }
  restorePreviousFocus();
}

function rerunOnboarding(): void {
  onboardingState = { ...INITIAL_ONBOARDING_STATE, backdrop: dependencies.getBackdropMaterial(), theme: dependencies.getActiveThemeName(), agentChimes: agentChimesEnabled() };
  wizardAdapters = null;
  openOnboarding();
}

function openOnboarding(): void {
  if (!onboardingEl.classList.contains("open")) previousFocus = document.activeElement as HTMLElement | null;
  onboardingEl.classList.add("open");
  renderOnboarding();
  (onboardingEl.querySelector("#ob-step-title") as HTMLElement).focus();
}

function restorePreviousFocus(): void {
  previousFocus?.focus();
  previousFocus = null;
}

function focusableElements(): HTMLElement[] {
  return Array.from(onboardingEl.querySelectorAll<HTMLElement>('button:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex="0"]'));
}

function onDialogKeyDown(event: KeyboardEvent): void {
  if (!onboardingEl.classList.contains("open")) return;
  if (event.key === "Escape") {
    event.preventDefault();
    void onOnboardingSkip();
    return;
  }
  if (event.key !== "Tab") return;
  const focusable = focusableElements();
  if (focusable.length === 0) return;
  const first = focusable[0];
  const last = focusable[focusable.length - 1];
  if (event.shiftKey && document.activeElement === first) {
    event.preventDefault();
    last.focus();
  } else if (!event.shiftKey && document.activeElement === last) {
    event.preventDefault();
    first.focus();
  }
}

  const next = onboardingEl.querySelector(".ob-next");
  const previous = onboardingEl.querySelector(".ob-prev");
  const skip = onboardingEl.querySelector(".ob-skip");
  if (!next || !previous || !skip) throw new Error("Missing onboarding controls");
  lifecycle.listen(next, "click", () => void onOnboardingNext());
  lifecycle.listen(previous, "click", () => onOnboardingPrev());
  lifecycle.listen(skip, "click", () => void onOnboardingSkip());
  const dialog = onboardingEl.querySelector(".ob-box");
  if (!dialog) throw new Error("Missing onboarding dialog");
  lifecycle.listen(dialog, "keydown", (event) => onDialogKeyDown(event as KeyboardEvent));

  return {
    maybeShow: maybeShowOnboarding,
    rerun: rerunOnboarding,
    renderDictationTab,
    setDictationModelError(error: string | null): void { dictationModelError = error; },
    async dispose(): Promise<void> {
      onboardingEl.classList.remove("open");
      scanSequence += 1;
      for (const poll of modelPolls) window.clearInterval(poll);
      modelPolls.clear();
      await lifecycle.dispose();
      restorePreviousFocus();
    },
  };
}
