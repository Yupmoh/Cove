import {
  nextUpdateState,
  updateAffordanceVisible,
  updateButtonLabel,
  type UpdateEvent,
  type UpdateState,
} from "../../update-flow";
import { LifecycleScope, type ComponentHandle } from "../../app/lifecycle";
import { FrontendCommand } from "../../app/frontend-command";

interface UpdateInfo {
  version: string;
  releaseUrl?: string;
  assetUrl?: string;
  signatureUrl?: string;
  releaseNotes?: string;
}

export interface UpdaterFeatureDependencies {
  document: Document;
  invokeNative(command: FrontendCommand, args: Record<string, unknown>): Promise<unknown>;
  shouldCheckOnLaunch(): Promise<boolean>;
  onStagedChanged(staged: boolean): void;
}

export interface UpdaterFeature extends ComponentHandle {
  readonly state: UpdateState;
  start(): Promise<void>;
  check(): Promise<void>;
  activate(): void;
  renderSettings(container: HTMLElement): void;
}

export function createUpdaterFeature(dependencies: UpdaterFeatureDependencies): UpdaterFeature {
  const document = dependencies.document;
  const lifecycle = new LifecycleScope();
  let state: UpdateState = { kind: "idle" };
  let latest: UpdateInfo | null = null;
  let operation = 0;
  let startPromise: Promise<void> | null = null;
  let renderedSettings: {
    header: HTMLElement;
    row: HTMLElement;
    button: HTMLButtonElement;
    onClick: (event: Event) => void;
  } | null = null;

  const currentVersion = (): string => {
    const raw = document.getElementById("wordmark-ver")?.textContent ?? "";
    return raw.replace(/^v/, "").trim() || "dev";
  };

  const statusText = (): string => {
    const current = `Current version ${currentVersion()}`;
    if (state.kind === "checking") return `${current} · checking…`;
    if (state.kind === "upToDate") return `${current} · you are on the latest release`;
    if (state.kind === "available") return `${current} · ${state.version} available`;
    if (state.kind === "downloading") return `${current} · downloading ${latest?.version ?? "update"}…`;
    if (state.kind === "readyToApply") return `${current} · ${state.version} downloaded — restart to apply`;
    if (state.kind === "applying") return `${current} · applying update — the app will restart`;
    if (state.kind === "failed") return `${current} · update failed: ${state.message}`;
    return current;
  };

  const renderState = (): void => {
    if (lifecycle.isDisposed) return;
    const button = document.getElementById("cove-update-btn") as HTMLButtonElement | null;
    const status = document.getElementById("cove-update-status");
    const notes = document.getElementById("cove-update-notes") as HTMLAnchorElement | null;
    if (button) {
      button.textContent = updateButtonLabel(state);
      button.disabled = state.kind === "checking" || state.kind === "downloading" || state.kind === "applying";
    }
    if (status) status.textContent = statusText();
    if (notes) {
      const href = latest?.releaseUrl ?? "";
      const show = (state.kind === "available" || state.kind === "readyToApply") && href.length > 0;
      notes.hidden = !show;
      if (show) notes.href = href;
    }
  };

  const dispatch = (event: UpdateEvent): UpdateState => {
    if (lifecycle.isDisposed) return state;
    state = nextUpdateState(state, event);
    dependencies.onStagedChanged(updateAffordanceVisible(state));
    renderState();
    return state;
  };

  const check = async (): Promise<void> => {
    if (lifecycle.isDisposed) return;
    const currentOperation = ++operation;
    dispatch({ type: "check" });
    try {
      const raw = await dependencies.invokeNative(FrontendCommand.UpdaterCheck, {});
      if (lifecycle.isDisposed || operation !== currentOperation) return;
      const info = typeof raw === "string" && raw !== "null" && raw.length > 0
        ? JSON.parse(raw) as UpdateInfo
        : null;
      if (!info?.version) {
        dispatch({ type: "checkedUpToDate" });
        return;
      }
      latest = info;
      dispatch({
        type: "checkedAvailable",
        version: info.version,
        notes: info.releaseUrl ?? null,
      });
    } catch (error) {
      if (lifecycle.isDisposed || operation !== currentOperation) return;
      console.warn("updater.check failed", error);
      dispatch({ type: "error", message: String(error) });
    }
  };

  const download = async (): Promise<void> => {
    if (lifecycle.isDisposed) return;
    const currentOperation = ++operation;
    dispatch({ type: "download" });
    try {
      const handle = await dependencies.invokeNative(FrontendCommand.UpdaterDownload, {});
      if (lifecycle.isDisposed || operation !== currentOperation) return;
      if (typeof handle !== "string" || handle.length === 0) {
        console.warn("updater.download returned no handle");
        dispatch({ type: "error", message: "no download handle" });
        return;
      }
      dispatch({ type: "downloaded", handle, version: latest?.version ?? "" });
    } catch (error) {
      if (lifecycle.isDisposed || operation !== currentOperation) return;
      console.warn("updater.download failed", error);
      dispatch({ type: "error", message: String(error) });
    }
  };

  const apply = async (handle: string): Promise<void> => {
    if (lifecycle.isDisposed) return;
    const currentOperation = ++operation;
    dispatch({ type: "apply" });
    try {
      await dependencies.invokeNative(FrontendCommand.UpdaterApply, { downloadHandle: handle });
    } catch (error) {
      if (lifecycle.isDisposed || operation !== currentOperation) return;
      console.warn("updater.apply failed", error);
      dispatch({ type: "error", message: String(error) });
    }
  };

  const activate = (): void => {
    if (lifecycle.isDisposed) return;
    if (state.kind === "idle" || state.kind === "upToDate") {
      void check();
    } else if (state.kind === "failed") {
      dispatch({ type: "retry" });
      void check();
    } else if (state.kind === "available") {
      void download();
    } else if (state.kind === "readyToApply") {
      void apply(state.handle);
    }
  };

  const clearRenderedSettings = (): void => {
    const rendered = renderedSettings;
    if (!rendered) return;
    renderedSettings = null;
    rendered.button.removeEventListener("click", rendered.onClick);
    rendered.header.remove();
    rendered.row.remove();
  };

  const renderSettings = (container: HTMLElement): void => {
    if (lifecycle.isDisposed) return;
    clearRenderedSettings();
    const header = document.createElement("div");
    header.className = "set-section-header";
    header.textContent = "Software updates";
    container.appendChild(header);
    const row = document.createElement("div");
    row.className = "set-row";
    const copy = document.createElement("label");
    const title = document.createElement("span");
    title.textContent = "Update Cove";
    const status = document.createElement("span");
    status.id = "cove-update-status";
    status.className = "set-desc";
    const notes = document.createElement("a");
    notes.id = "cove-update-notes";
    notes.className = "set-desc set-inline-link";
    notes.textContent = "View release notes";
    notes.target = "_blank";
    notes.rel = "noreferrer";
    notes.hidden = true;
    copy.append(title, status, notes);
    const button = document.createElement("button");
    button.id = "cove-update-btn";
    button.className = "set-action set-action-primary";
    const onClick = (event: Event): void => {
      event.stopPropagation();
      activate();
    };
    button.addEventListener("click", onClick);
    row.append(copy, button);
    container.appendChild(row);
    renderedSettings = { header, row, button, onClick };
    renderState();
  };

  const start = (): Promise<void> => {
    if (lifecycle.isDisposed) return Promise.resolve();
    if (startPromise) return startPromise;
    startPromise = (async () => {
      let checkOnLaunch: boolean;
      try {
        checkOnLaunch = await dependencies.shouldCheckOnLaunch();
      } catch (error) {
        if (!lifecycle.isDisposed) console.warn("update launch preference unavailable", error);
        return;
      }
      if (lifecycle.isDisposed || !checkOnLaunch) return;
      await check();
    })();
    return startPromise;
  };

  lifecycle.own(() => {
    operation += 1;
    clearRenderedSettings();
  });

  return {
    get state() { return state; },
    start,
    check,
    activate,
    renderSettings,
    dispose: () => lifecycle.dispose(),
  };
}
