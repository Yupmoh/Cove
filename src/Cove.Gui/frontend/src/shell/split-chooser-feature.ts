import { buildAdapterTiles, type LauncherAdapter } from "../box-launcher";
import { adapterAccent, detectedHarnessTiles } from "../launcher-model";
import { adapterIconSvg } from "../icons";
import type { ComponentHandle } from "../app/lifecycle";

export interface SplitChooserDependencies {
  document: Document;
  window: Window;
  adapters(): LauncherAdapter[];
  prepare(): void;
  select(direction: "row" | "col", kind: string): void;
}

export class SplitChooserFeature implements ComponentHandle {
  private closeCurrent: () => void = () => {};
  private activationTimer: number | null = null;
  private disposed = false;

  constructor(private readonly dependencies: SplitChooserDependencies) {}

  open(event: MouseEvent, direction: "row" | "col"): void {
    if (this.disposed) throw new Error("SplitChooserFeature is disposed");
    this.dependencies.prepare();
    this.close();
    const { document, window } = this.dependencies;
    const popover = document.createElement("div");
    popover.id = "mini-launcher";
    popover.className = "mini-launcher";
    let closed = false;
    const close = (): void => {
      if (closed) return;
      closed = true;
      popover.remove();
      document.removeEventListener("mousedown", onAway, true);
      document.removeEventListener("keydown", onKey, true);
      if (this.activationTimer !== null) window.clearTimeout(this.activationTimer);
      this.activationTimer = null;
      if (this.closeCurrent === close) this.closeCurrent = () => {};
    };
    const onAway = (awayEvent: MouseEvent): void => {
      if (!popover.contains(awayEvent.target as Node)) close();
    };
    const onKey = (keyEvent: KeyboardEvent): void => {
      if (keyEvent.key !== "Escape") return;
      keyEvent.stopPropagation();
      close();
    };
    this.closeCurrent = close;

    const grid = document.createElement("div");
    grid.className = "ml-grid";
    const addTile = (accent: string, icon: { svg?: string; glyph?: string }, label: string, kind: string): void => {
      const tile = document.createElement("button");
      tile.className = "ml-tile";
      tile.style.setProperty("--card-accent", accent);
      const badge = document.createElement("span");
      badge.className = "ml-badge";
      if (icon.svg) badge.innerHTML = icon.svg;
      else badge.textContent = icon.glyph ?? "";
      const name = document.createElement("span");
      name.className = "ml-name";
      name.textContent = label;
      tile.append(badge, name);
      tile.addEventListener("click", (clickEvent) => {
        clickEvent.stopPropagation();
        close();
        this.dependencies.select(direction, kind);
      });
      grid.appendChild(tile);
    };
    addTile("var(--accent)", { glyph: "▌" }, "Terminal", "terminal");
    for (const harness of detectedHarnessTiles(buildAdapterTiles(this.dependencies.adapters()))) {
      addTile(
        adapterAccent(harness.adapterName, harness.accent),
        { svg: adapterIconSvg(harness.adapterName) },
        harness.label,
        `adapter:${harness.adapterName}`,
      );
    }
    popover.appendChild(grid);

    const tools = document.createElement("div");
    tools.className = "ml-tools";
    for (const tool of [
      { glyph: "◑", label: "Browser", kind: "browser" },
      { glyph: "⌕", label: "Search", kind: "search" },
      { glyph: "⎇", label: "Git", kind: "git" },
      { glyph: "▤", label: "Tasks", kind: "tasks-list" },
    ]) {
      const button = document.createElement("button");
      button.className = "ml-tool";
      const glyph = document.createElement("span");
      glyph.textContent = tool.glyph;
      const label = document.createElement("span");
      label.textContent = tool.label;
      button.append(glyph, label);
      button.addEventListener("click", (clickEvent) => {
        clickEvent.stopPropagation();
        close();
        this.dependencies.select(direction, tool.kind);
      });
      tools.appendChild(button);
    }
    popover.appendChild(tools);

    document.body.appendChild(popover);
    const rect = popover.getBoundingClientRect();
    popover.style.left = `${Math.max(8, Math.min(event.clientX, window.innerWidth - rect.width - 8))}px`;
    popover.style.top = `${Math.max(8, Math.min(event.clientY + 6, window.innerHeight - rect.height - 8))}px`;
    this.activationTimer = window.setTimeout(() => {
      this.activationTimer = null;
      if (closed) return;
      document.addEventListener("mousedown", onAway, true);
      document.addEventListener("keydown", onKey, true);
    }, 0);
  }

  close(): void {
    this.closeCurrent();
  }

  dispose(): void {
    if (this.disposed) return;
    this.disposed = true;
    this.close();
  }
}
