import {
  hudLines,
  hudMetrics,
  initHud,
  readJsHeapBytes,
  recordFrame,
  toggleHud,
  type HudState,
  type JsHeapProbe,
} from "../../perf-hud";
import type { ComponentHandle } from "../../app/lifecycle";

export interface PerformanceHudDependencies {
  document: Document;
  root: HTMLElement;
  readHeap(): JsHeapProbe | null;
  requestFrame(callback: FrameRequestCallback): number;
  cancelFrame(handle: number): void;
  onToggled(enabled: boolean): void;
}

export class PerformanceHudFeature implements ComponentHandle {
  private state: HudState = initHud();
  private frame: number | null = null;
  private disposed = false;

  constructor(private readonly dependencies: PerformanceHudDependencies) {}

  get enabled(): boolean {
    return this.state.enabled;
  }

  toggle(): void {
    if (this.disposed) throw new Error("PerformanceHudFeature is disposed");
    this.state = toggleHud(this.state);
    this.dependencies.root.classList.toggle("open", this.state.enabled);
    if (this.state.enabled) {
      this.render();
      if (this.frame === null) this.frame = this.dependencies.requestFrame((timestamp) => this.onFrame(timestamp));
    } else if (this.frame !== null) {
      this.dependencies.cancelFrame(this.frame);
      this.frame = null;
    }
    this.dependencies.onToggled(this.state.enabled);
  }

  dispose(): void {
    if (this.disposed) return;
    this.disposed = true;
    if (this.frame !== null) this.dependencies.cancelFrame(this.frame);
    this.frame = null;
    this.dependencies.root.classList.remove("open");
  }

  private onFrame(timestamp: number): void {
    this.frame = null;
    if (this.disposed || !this.state.enabled) return;
    this.state = recordFrame(this.state, timestamp);
    this.render();
    this.frame = this.dependencies.requestFrame((nextTimestamp) => this.onFrame(nextTimestamp));
  }

  private render(): void {
    const { document, root } = this.dependencies;
    root.replaceChildren();
    for (const line of hudLines(hudMetrics(this.state), readJsHeapBytes(this.dependencies.readHeap()))) {
      const row = document.createElement("div");
      row.className = "hud-row";
      const label = document.createElement("span");
      label.className = "hud-label";
      label.textContent = line.label;
      const value = document.createElement("span");
      value.className = "hud-value";
      value.textContent = line.value;
      row.append(label, value);
      root.appendChild(row);
    }
    const caption = document.createElement("div");
    caption.className = "hud-caption";
    caption.textContent = "GUI render loop (requestAnimationFrame); JS heap from the webview.";
    root.appendChild(caption);
  }
}
