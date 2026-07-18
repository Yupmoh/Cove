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
  type OnboardingState,
} from "../../onboarding";
import { DICTATION_SPACE_KEY, DICTATION_LIVE_TYPING_KEY, dictationToggleEnabled, modelPollOutcome } from "../../dictation";
import { coerceMaterial, setBackdropMaterial, BACKDROP_PREF_KEY, type BackdropDeps, type BackdropMaterial } from "../../backdrop";
import { playChime } from "../../chime";
import { adapterStatusMeta, type ToolsAdapter } from "../../tools-tab";
import type { ThemeDto } from "../../theme-editor";

export interface OnboardingLauncherAdapter {
  name: string;
  displayName: string;
  status?: string | null;
  installCommand?: string | null;
}

interface ToolsListResponse {
  adapters: ToolsAdapter[];
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

let onboardingState: OnboardingState = { ...INITIAL_ONBOARDING_STATE };

async function maybeShowOnboarding(): Promise<void> {
  try {
    const seen = await invoke<{ value?: string }>(FrontendCommand.AppConfigGet, { key: ONBOARDING_COMPLETED_KEY });
    const hasSeen = onboardingSeenFromConfig(seen.value);
    if (!shouldShowOnboarding(hasSeen)) return;
    onboardingEl.classList.add("open");
    renderOnboarding();
  } catch { void 0; }
}

function renderOnboarding(): void {
  const step = currentStepData(onboardingState);
  (onboardingEl.querySelector(".ob-title") as HTMLElement).textContent = step.title;
  (onboardingEl.querySelector(".ob-progress-bar") as HTMLElement).style.width = `${progressPercent(onboardingState)}%`;
  const body = onboardingEl.querySelector(".ob-body") as HTMLElement;
  body.innerHTML = "";
  const p = document.createElement("p");
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

async function loadWizardAdapters(): Promise<ToolsAdapter[]> {
  try {
    const result = await invoke<ToolsListResponse>(FrontendCommand.AdapterToolsList, {});
    return result.adapters ?? [];
  } catch (e) {
    console.warn("wizard adapter list failed", e);
    return [];
  }
}

async function renderHarnessStep(body: HTMLElement): Promise<void> {
  const grid = document.createElement("div");
  grid.className = "ob-adapter-list";
  body.appendChild(grid);
  const adapters = await loadWizardAdapters();
  grid.className = "ob-adapter-list" + (adapters.length > 4 ? " ob-grid-2" : "");
  if (adapters.length === 0) {
    const none = document.createElement("div");
    none.className = "ob-adapter";
    none.textContent = "No tools detected yet — add one later from Settings → Tools.";
    grid.appendChild(none);
  }
  for (const a of adapters) {
    const el = document.createElement("div");
    el.className = "ob-adapter";
    const meta = adapterStatusMeta(a.status);
    const name = document.createElement("span");
    name.className = "ob-adapter-name";
    name.textContent = a.displayName || a.name;
    const dot = document.createElement("span");
    dot.className = "tools-dot";
    dot.style.cssText = `background:${meta.cssColor};margin-left:8px;`;
    name.appendChild(dot);
    el.appendChild(name);
    grid.appendChild(el);
  }

  try {
    const listed = await invoke<AdapterListResult>(FrontendCommand.AppAdapterList, {});
    const installable = mapLauncherAdapters(listed.adapters).filter((a) => a.status !== "detected" && (a.installCommand ?? "").trim().length > 0);
    if (installable.length > 0) {
      const installLabel = document.createElement("div");
      installLabel.className = "tools-subtitle";
      installLabel.style.marginTop = "12px";
      installLabel.textContent = "Install more harnesses";
      body.appendChild(installLabel);
      const installGrid = document.createElement("div");
      installGrid.className = "ob-adapter-list" + (installable.length > 4 ? " ob-grid-2" : "");
      for (const a of installable) {
        const row = document.createElement("div");
        row.className = "ob-adapter";
        const rowName = document.createElement("span");
        rowName.className = "ob-adapter-name";
        rowName.textContent = a.displayName || a.name;
        const installBtn = document.createElement("button");
        installBtn.className = "diag-btn ob-install-btn";
        installBtn.textContent = "+";
        installBtn.title = a.installCommand ?? "";
        installBtn.addEventListener("click", () => {
          completeOnboarding();
          void launchHarnessShellTask(a.installCommand ?? "", `Install ${a.displayName || a.name}`);
        });
        row.appendChild(rowName);
        row.appendChild(installBtn);
        installGrid.appendChild(row);
      }
      body.appendChild(installGrid);
    }
  } catch (err) {
    console.warn("onboarding install list failed", err);
  }

  const dirRow = document.createElement("div");
  dirRow.style.cssText = "display:flex;gap:8px;align-items:center;margin-top:12px;";
  const dirInput = document.createElement("input");
  dirInput.type = "text";
  dirInput.className = "diag-input";
  dirInput.style.cssText = "flex:1;margin:0;padding:4px 8px;";
  dirInput.placeholder = "Default bay directory";
  dirInput.value = onboardingState.defaultBayDir ?? "";
  dirInput.addEventListener("input", () => { onboardingState = setDefaultBayDir(onboardingState, dirInput.value.trim() || null); });
  const browse = document.createElement("button");
  browse.className = "diag-btn";
  browse.style.marginTop = "0";
  browse.textContent = "Browse…";
  browse.addEventListener("click", async () => {
    try {
      const picked = await invokeNative(FrontendCommand.DialogOpenFolder, { initialPath: dirInput.value.trim() || "/" });
      if (typeof picked === "string" && picked.trim()) {
        dirInput.value = picked.trim();
        onboardingState = setDefaultBayDir(onboardingState, picked.trim());
      }
    } catch (e) { console.warn("wizard folder picker failed", e); }
  });
  dirRow.appendChild(dirInput);
  dirRow.appendChild(browse);
  body.appendChild(dirRow);
}

async function renderPermissionsStep(body: HTMLElement): Promise<void> {
  const list = document.createElement("div");
  list.className = "ob-adapter-list";
  body.appendChild(list);
  const adapters = await loadWizardAdapters();
  if (adapters.length === 0) {
    const none = document.createElement("div");
    none.className = "ob-adapter";
    none.textContent = "No adapters to configure yet.";
    list.appendChild(none);
    return;
  }
  for (const a of adapters) {
    const row = document.createElement("label");
    row.className = "ob-telemetry-toggle";
    row.style.cssText = "display:flex;align-items:center;gap:8px;justify-content:space-between;";
    const name = document.createElement("span");
    name.textContent = `${a.displayName || a.name} — bypass permissions (YOLO)`;
    name.style.fontSize = "12px";
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
  backdropRow.style.cssText = "display:flex;gap:8px;align-items:center;margin-top:10px;";
  const backdropLabel = document.createElement("span");
  backdropLabel.style.cssText = "font-size:12px;min-width:80px;";
  backdropLabel.textContent = "Backdrop";
  const backdropSel = document.createElement("select");
  backdropSel.className = "diag-input";
  backdropSel.style.cssText = "margin:0;padding:4px 8px;";
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
  themeRow.style.cssText = "display:flex;gap:8px;align-items:center;margin-top:10px;";
  const themeLabel = document.createElement("span");
  themeLabel.style.cssText = "font-size:12px;min-width:80px;";
  themeLabel.textContent = "Theme";
  const themeSel = document.createElement("select");
  themeSel.className = "diag-input";
  themeSel.style.cssText = "margin:0;padding:4px 8px;";
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
  toggle.style.cssText = "display:flex;align-items:center;gap:8px;";
  const cb = document.createElement("input");
  cb.type = "checkbox";
  cb.checked = onboardingState.agentChimes;
  const label = document.createElement("span");
  label.textContent = "Agent chimes — soft tone when an agent finishes or needs input";
  label.style.cssText = "font-size:12px;color:var(--fg);";
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
  label.style.cssText = "display:flex;flex-direction:column;gap:2px;";
  const name = document.createElement("span");
  name.textContent = title;
  const sub = document.createElement("span");
  sub.style.cssText = "font-size:11px;color:var(--muted);";
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
  status.style.cssText = "display:flex;gap:10px;align-items:center;margin-top:10px;";
  const text = document.createElement("span");
  text.style.cssText = "font-size:12px;color:var(--muted);";
  text.textContent = "Speech model: checking…";
  const btn = document.createElement("button");
  btn.className = "diag-btn";
  btn.style.cssText = "margin:0;";
  btn.textContent = "Download now";
  btn.style.display = "none";
  status.appendChild(text);
  status.appendChild(btn);
  container.appendChild(status);

  const refresh = async (): Promise<void> => {
    const s = await dictationStatus();
    if (!status.isConnected) return;
    if (s.modelReady) {
      text.textContent = "Speech model: Parakeet TDT 0.6B v3 — downloaded";
      btn.style.display = "none";
    } else {
      text.textContent = "Speech model: Parakeet TDT 0.6B v3 (487 MB) — not downloaded";
      btn.style.display = "";
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
      btn.style.display = "";
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
  info.style.cssText = "font-size:12px;color:var(--muted);margin:12px 0;line-height:1.5;";
  info.textContent = "Hold F9 — or hold Space in a terminal or text field — to dictate. Speech is recognized on this machine with NVIDIA Parakeet; audio never leaves it. Words stream in live and settle when you release.";
  container.appendChild(info);
  container.appendChild(dictationPrefRow(DICTATION_SPACE_KEY, "Hold Space to dictate", "Long-press Space (~300 ms) starts dictation; a quick tap still types a space."));
  container.appendChild(dictationPrefRow(DICTATION_LIVE_TYPING_KEY, "Type live preview into the focused target", "Off shows the running transcript in the status pill only; text lands on release."));
  buildDictationModelControls(container);
}

function renderDictationStep(body: HTMLElement): void {
  const toggle = document.createElement("label");
  toggle.className = "ob-telemetry-toggle";
  toggle.style.cssText = "display:flex;align-items:center;gap:8px;";
  const cb = document.createElement("input");
  cb.type = "checkbox";
  cb.checked = dictationToggleEnabled(localStorage.getItem(DICTATION_SPACE_KEY));
  cb.addEventListener("change", () => localStorage.setItem(DICTATION_SPACE_KEY, cb.checked ? "true" : "false"));
  const label = document.createElement("span");
  label.textContent = "Hold Space to dictate — a quick tap still types a space (F9 always works)";
  label.style.cssText = "font-size:12px;color:var(--fg);";
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
}

function rerunOnboarding(): void {
  onboardingState = { ...INITIAL_ONBOARDING_STATE, backdrop: dependencies.getBackdropMaterial(), theme: dependencies.getActiveThemeName(), agentChimes: agentChimesEnabled() };
  onboardingEl.classList.add("open");
  renderOnboarding();
}

  const next = onboardingEl.querySelector(".ob-next");
  const previous = onboardingEl.querySelector(".ob-prev");
  const skip = onboardingEl.querySelector(".ob-skip");
  if (!next || !previous || !skip) throw new Error("Missing onboarding controls");
  lifecycle.listen(next, "click", () => void onOnboardingNext());
  lifecycle.listen(previous, "click", () => onOnboardingPrev());
  lifecycle.listen(skip, "click", () => void onOnboardingSkip());

  return {
    maybeShow: maybeShowOnboarding,
    rerun: rerunOnboarding,
    renderDictationTab,
    setDictationModelError(error: string | null): void { dictationModelError = error; },
    async dispose(): Promise<void> {
      onboardingEl.classList.remove("open");
      for (const poll of modelPolls) window.clearInterval(poll);
      modelPolls.clear();
      await lifecycle.dispose();
    },
  };
}
