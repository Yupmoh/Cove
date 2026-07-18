import { clusterTools } from "../title-cluster";

export interface TitleClusterState {
  updateStaged: boolean;
  zoom: number;
}

export class TitleClusterComponent {
  private readonly cluster: HTMLElement;
  private readonly right: HTMLElement;
  private readonly wordmark: HTMLElement;
  private disposed = false;

  constructor(
    private readonly document: Document,
    private readonly icon: (name: string) => string,
    private readonly onAction: (action: string) => void,
  ) {
    const cluster = document.getElementById("tb-cluster");
    const right = document.getElementById("tb-right");
    const wordmark = document.getElementById("wordmark");
    if (!cluster || !right || !wordmark) throw new Error("Missing title cluster shell");
    this.cluster = cluster;
    this.right = right;
    this.wordmark = wordmark;
  }

  update(state: TitleClusterState): void {
    if (this.disposed) throw new Error("TitleClusterComponent is disposed");
    this.cluster.replaceChildren(this.wordmark);
    this.right.replaceChildren();
    for (const tool of clusterTools({ updateStaged: state.updateStaged })) {
      if (tool.id === "find-anything") {
        this.cluster.appendChild(this.findAnything(tool.title, tool.action));
        continue;
      }
      if (tool.id === "zoom-in") this.right.appendChild(this.zoomLabel(state.zoom));
      this.right.appendChild(this.toolButton(tool.id, tool.title, tool.action));
    }
  }

  setZoom(zoom: number): void {
    const label = this.document.getElementById("tb-zoom-label");
    if (label) label.textContent = `${Math.round(zoom * 100)}%`;
  }

  dispose(): void {
    if (this.disposed) return;
    this.disposed = true;
    this.cluster.replaceChildren(this.wordmark);
    this.right.replaceChildren();
  }

  private findAnything(title: string, action: string): HTMLElement {
    const element = this.document.createElement("div");
    element.className = "tb-find-anything";
    element.title = title;
    element.setAttribute("data-webview-ignore", "");
    const glyph = this.document.createElement("span");
    glyph.className = "tb-find-ic";
    glyph.innerHTML = this.icon("search");
    glyph.style.display = "inline-flex";
    const placeholder = this.document.createElement("span");
    placeholder.className = "tb-find-ph";
    placeholder.textContent = "find anything…";
    element.append(glyph, placeholder);
    element.addEventListener("click", (event) => {
      event.stopPropagation();
      this.onAction(action);
    });
    return element;
  }

  private zoomLabel(zoom: number): HTMLElement {
    const label = this.document.createElement("div");
    label.id = "tb-zoom-label";
    label.setAttribute("aria-label", "Current app zoom");
    label.setAttribute("data-webview-ignore", "");
    label.textContent = `${Math.round(zoom * 100)}%`;
    return label;
  }

  private toolButton(id: string, title: string, action: string): HTMLElement {
    const button = this.document.createElement("div");
    button.className = `tbtn tb-cluster-btn${id === "update" ? " tb-update" : ""}`;
    button.title = title;
    button.setAttribute("data-webview-ignore", "");
    const icons: Record<string, string> = {
      settings: "gear",
      inspect: "inspect",
      "zoom-in": "plus",
      "zoom-out": "minus",
      update: "refresh",
    };
    button.innerHTML = this.icon(icons[id] ?? "gear");
    button.addEventListener("click", (event) => {
      event.stopPropagation();
      this.onAction(action);
    });
    return button;
  }
}
