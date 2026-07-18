import { FrontendCommand } from "../../app/frontend-command";
import { buildAdapterTiles, type LauncherAdapter, type LauncherTile } from "../../box-launcher";
import {
  buildFeedbackReport,
  cssPath,
  feedbackSlug,
  harnessPrompt,
  type InspectElementLike,
  type InspectTarget,
} from "../../inspect-mode";
import { adapterAccent, detectedHarnessTiles } from "../../launcher-model";

interface Rect {
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface InspectFeatureDependencies {
  document: Document;
  viewport(): { width: number; height: number };
  adapters(): ReadonlyArray<LauncherAdapter>;
  workspaceNames(): { bay: string; shore: string };
  invoke<T>(command: FrontendCommand, args: Record<string, unknown>): Promise<T>;
  buildAdapterLaunch(adapter: {
    name: string;
    displayName: string;
    accent: string;
    binary: string;
  }): Promise<{ command: string; args: string[]; yolo: boolean }>;
  spawnNook(params: Record<string, unknown>): Promise<{ nookId: string }>;
  createShore(nookId: string, name: string): Promise<{ shoreId: string }>;
  selectShore(shoreId: string): void;
  reload(): Promise<unknown>;
  focusNook(nookId: string): void;
}

export interface InspectFeature {
  start(): void;
  dispose(): Promise<void>;
}

export function createInspectFeature(dependencies: InspectFeatureDependencies): InspectFeature {
  const document = dependencies.document;
  let teardownOverlay: (() => void) | null = null;
  const panels = new Set<HTMLElement>();

  const targetOf = (element: Element): InspectTarget => {
    const rect = element.getBoundingClientRect();
    return {
      selector: cssPath(element as unknown as InspectElementLike),
      tag: element.tagName.toLowerCase(),
      classes: [...element.classList],
      rect: {
        x: Math.round(rect.left),
        y: Math.round(rect.top),
        width: Math.round(rect.width),
        height: Math.round(rect.height),
      },
      textExcerpt: (element.textContent ?? "").trim().slice(0, 120),
    };
  };

  const spawnFeedbackAgent = async (
    tile: LauncherTile,
    prompt: string,
    shoreName: string,
  ): Promise<void> => {
    const launch = await dependencies.buildAdapterLaunch({
      name: tile.adapterName,
      displayName: tile.label,
      accent: tile.accent,
      binary: tile.binary,
    });
    const spawned = await dependencies.spawnNook({
      command: launch.command,
      args: [...launch.args, prompt],
      cwd: "",
      inheritCwdFrom: "",
      cols: 80,
      rows: 24,
      adapter: tile.adapterName,
      agentName: tile.label,
      bay: "",
      shore: "",
      yolo: launch.yolo,
    });
    const created = await dependencies.createShore(spawned.nookId, shoreName);
    dependencies.selectShore(created.shoreId);
    await dependencies.reload();
    dependencies.focusNook(spawned.nookId);
  };

  const submit = async (
    note: string,
    element: Element | null,
    target: InspectTarget | null,
    regionRect: Rect | null,
    harness: LauncherTile | null,
  ): Promise<void> => {
    const trimmed = note.trim() || "(no note)";
    const names = dependencies.workspaceNames();
    const htmlElementType = document.defaultView?.HTMLElement;
    const report = buildFeedbackReport({
      note: trimmed,
      target,
      regionRect,
      bay: names.bay,
      shore: names.shore,
      appVersion: document.getElementById("wordmark-ver")?.textContent ?? "dev",
      htmlExcerpt: htmlElementType && element instanceof htmlElementType ? element.outerHTML : "",
      nowIso: new Date().toISOString(),
    });
    try {
      const result = await dependencies.invoke<{ path: string }>(FrontendCommand.AppFeedbackSave, {
        json: JSON.stringify(report, null, 2),
        slug: feedbackSlug(trimmed),
      });
      if (harness) {
        await spawnFeedbackAgent(harness, harnessPrompt(report, result.path), `Fix: ${feedbackSlug(trimmed)}`);
      } else {
        console.warn("feedback report saved", result.path);
      }
    } catch (error) {
      console.warn("feedback save failed", error);
    }
  };

  const openNote = (element: Element | null, regionRect: Rect | null): void => {
    const panel = document.createElement("div");
    panel.className = "inspect-note";
    panels.add(panel);
    const target = element ? targetOf(element) : null;
    const summary = document.createElement("div");
    summary.className = "inspect-note-summary";
    summary.textContent = target ? target.selector : `region ${regionRect?.width}×${regionRect?.height}`;
    panel.appendChild(summary);
    const input = document.createElement("textarea");
    input.className = "inspect-note-input";
    input.placeholder = "what's wrong here?";
    panel.appendChild(input);
    const row = document.createElement("div");
    row.className = "inspect-send-row";
    const harnesses = detectedHarnessTiles(buildAdapterTiles([...dependencies.adapters()]));
    const removePanel = (): void => {
      panels.delete(panel);
      panel.remove();
    };
    for (const tile of harnesses) {
      const button = document.createElement("button");
      button.className = "inspect-btn inspect-send";
      button.style.setProperty("--card-accent", adapterAccent(tile.adapterName, tile.accent));
      button.textContent = `Send to ${tile.label}`;
      button.addEventListener("click", () => {
        void submit(input.value, element, target, regionRect, tile);
        removePanel();
      });
      row.appendChild(button);
    }
    const save = document.createElement("button");
    save.className = "inspect-btn";
    save.textContent = "Save report";
    save.addEventListener("click", () => {
      void submit(input.value, element, target, regionRect, null);
      removePanel();
    });
    row.appendChild(save);
    const cancel = document.createElement("button");
    cancel.className = "inspect-btn inspect-cancel";
    cancel.textContent = "Cancel";
    cancel.addEventListener("click", removePanel);
    row.appendChild(cancel);
    panel.appendChild(row);
    document.body.appendChild(panel);
    const anchor = target?.rect ?? regionRect;
    const viewport = dependencies.viewport();
    panel.style.left = `${Math.min(viewport.width - 360, Math.max(8, anchor?.x ?? 100))}px`;
    panel.style.top = `${Math.min(viewport.height - 220, Math.max(8, (anchor?.y ?? 100) + (anchor?.height ?? 0) + 8))}px`;
    input.focus();
  };

  const start = (): void => {
    if (teardownOverlay) return;
    const overlay = document.createElement("div");
    overlay.id = "inspect-overlay";
    const highlight = document.createElement("div");
    highlight.className = "inspect-highlight";
    const tag = document.createElement("div");
    tag.className = "inspect-tag";
    tag.textContent = "inspect mode — click an element, drag a region, esc to exit";
    overlay.append(highlight, tag);
    document.body.appendChild(overlay);
    tag.style.left = "50%";
    tag.style.top = "10px";
    tag.style.transform = "translateX(-50%)";
    let dragStart: { x: number; y: number } | null = null;
    let marquee = false;
    let marqueeRect: Rect = { x: 0, y: 0, width: 0, height: 0 };
    const pick = (x: number, y: number): Element | null => {
      return document.elementsFromPoint(x, y)
        .find((element) => element !== overlay && !overlay.contains(element)) ?? null;
    };
    const placeHighlight = (rect: { left: number; top: number; width: number; height: number }): void => {
      highlight.style.left = `${rect.left}px`;
      highlight.style.top = `${rect.top}px`;
      highlight.style.width = `${rect.width}px`;
      highlight.style.height = `${rect.height}px`;
    };
    const placeTag = (text: string, x: number, y: number): void => {
      tag.style.transform = "none";
      tag.textContent = text;
      tag.style.left = `${Math.max(4, x)}px`;
      tag.style.top = `${Math.max(4, y - 22)}px`;
    };
    const onMove = (event: MouseEvent): void => {
      if (dragStart && (Math.abs(event.clientX - dragStart.x) > 8 || Math.abs(event.clientY - dragStart.y) > 8)) {
        marquee = true;
      }
      if (marquee && dragStart) {
        const left = Math.min(dragStart.x, event.clientX);
        const top = Math.min(dragStart.y, event.clientY);
        const width = Math.abs(event.clientX - dragStart.x);
        const height = Math.abs(event.clientY - dragStart.y);
        marqueeRect = { x: left, y: top, width, height };
        placeHighlight({ left, top, width, height });
        placeTag("region", left, top);
        return;
      }
      const element = pick(event.clientX, event.clientY);
      if (!element) return;
      const rect = element.getBoundingClientRect();
      placeHighlight(rect);
      placeTag(cssPath(element as unknown as InspectElementLike, 3), rect.left, rect.top);
    };
    const onKey = (event: KeyboardEvent): void => {
      if (event.key !== "Escape") return;
      event.preventDefault();
      event.stopPropagation();
      teardownOverlay?.();
    };
    const teardown = (): void => {
      overlay.remove();
      document.removeEventListener("keydown", onKey, true);
      teardownOverlay = null;
    };
    teardownOverlay = teardown;
    overlay.addEventListener("mousemove", onMove);
    overlay.addEventListener("mousedown", (event) => {
      dragStart = { x: event.clientX, y: event.clientY };
    });
    overlay.addEventListener("mouseup", (event) => {
      const wasMarquee = marquee;
      const startPoint = dragStart;
      dragStart = null;
      marquee = false;
      if (wasMarquee && startPoint) {
        teardown();
        openNote(null, { ...marqueeRect });
        return;
      }
      const element = pick(event.clientX, event.clientY);
      teardown();
      openNote(element, null);
    });
    document.addEventListener("keydown", onKey, true);
  };

  return {
    start,
    async dispose() {
      teardownOverlay?.();
      for (const panel of panels) panel.remove();
      panels.clear();
    },
  };
}
