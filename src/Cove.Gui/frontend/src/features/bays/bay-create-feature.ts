import { FrontendCommand } from "../../app/frontend-command";
import { LifecycleScope, type ComponentHandle } from "../../app/lifecycle";
import { createSurfaceMotion, type SurfaceMotion } from "../../app/surface-motion";

export interface BayCreateDependencies {
  document: Document;
  root: HTMLElement;
  nameInput: HTMLInputElement;
  pathInput: HTMLInputElement;
  error: HTMLElement;
  invoke<T>(command: FrontendCommand, args: Record<string, unknown>): Promise<T>;
  invokeNative<T>(command: FrontendCommand, args: Record<string, unknown>): Promise<T>;
  defaultDirectory(): string;
  activeProjectDirectory(): string;
  buildIconGrid(selected: string | null, onSelect: (icon: string | null) => void): HTMLElement;
  loadBays(): Promise<void>;
  showToast(title: string, body: string, action?: () => void): void;
}

export class BayCreateFeature implements ComponentHandle {
  private readonly lifecycle = new LifecycleScope();
  private selectedIcon: string | null = null;
  private readonly surfaceMotion: SurfaceMotion;

  constructor(private readonly dependencies: BayCreateDependencies) {
    const { document, root } = dependencies;
    this.surfaceMotion = createSurfaceMotion(root);
    this.lifecycle.listen(this.required(document, "wsc-close"), "click", () => this.close());
    this.lifecycle.listen(this.required(document, "wsc-cancel"), "click", () => this.close());
    this.lifecycle.listen(this.required(document, "wsc-browse"), "click", () => { void this.browse(); });
    this.lifecycle.listen(this.required(document, "wsc-create"), "click", () => { void this.submit(); });
    this.lifecycle.listen(root, "mousedown", (event) => {
      if (event.target === root) this.close();
    });
    this.lifecycle.listen(root, "keydown", (event) => {
      const keyEvent = event as KeyboardEvent;
      if (keyEvent.key === "Escape") {
        keyEvent.stopPropagation();
        this.close();
      } else if (keyEvent.key === "Enter") {
        keyEvent.stopPropagation();
        void this.submit();
      }
    });
  }

  open(): void {
    const { root, nameInput, pathInput, error } = this.dependencies;
    nameInput.value = "";
    pathInput.value = this.dependencies.defaultDirectory();
    error.textContent = "";
    this.selectedIcon = null;
    this.renderIconGrid();
    this.surfaceMotion.open();
    nameInput.focus();
  }

  close(): void {
    this.surfaceMotion.close();
  }

  async dispose(): Promise<void> {
    this.surfaceMotion.dispose();
    await this.lifecycle.dispose();
  }

  private renderIconGrid(): void {
    const host = this.required(this.dependencies.document, "wsc-icon-grid");
    host.replaceChildren(this.dependencies.buildIconGrid(this.selectedIcon, (icon) => {
      this.selectedIcon = icon;
    }));
  }

  private async browse(): Promise<void> {
    const typed = this.dependencies.pathInput.value.trim();
    const initial = typed.startsWith("/") ? typed : (this.dependencies.activeProjectDirectory() || "/");
    try {
      const picked = await this.dependencies.invokeNative<string | null>(FrontendCommand.DialogOpenFolder, { initialPath: initial });
      const path = typeof picked === "string" ? picked.trim() : "";
      if (path) this.dependencies.pathInput.value = path;
      else if (picked === null) console.info("folder picker cancelled");
      else console.warn("folder picker returned nothing", initial, picked);
    } catch (error) {
      console.warn("folder picker failed", error);
    }
  }

  private async submit(): Promise<void> {
    const { nameInput, pathInput, error } = this.dependencies;
    const name = nameInput.value.trim();
    const projectDir = pathInput.value.trim();
    if (!name) {
      console.warn("bay creation rejected without a name");
      error.textContent = "Name is required.";
      nameInput.focus();
      return;
    }
    if (!projectDir) {
      console.warn("bay creation rejected without a directory");
      error.textContent = "Directory is required.";
      pathInput.focus();
      return;
    }
    try {
      const created = await this.dependencies.invoke<{ id: string }>(FrontendCommand.BayCreate, {
        name,
        projectDir,
        collectionId: "",
      });
      this.close();
      if (this.selectedIcon && created?.id) {
        try {
          await this.dependencies.invoke(FrontendCommand.BaySetIcon, {
            id: created.id,
            kind: "mark",
            value: this.selectedIcon,
          });
        } catch (iconError) {
          console.warn("bay.set-icon failed", created.id, iconError);
          this.dependencies.showToast("Icon not set", "Bay created without the chosen icon.", () => {});
        }
      }
      await this.dependencies.loadBays();
    } catch (creationError) {
      console.warn("bay.create failed", creationError);
      error.textContent = "Could not create bay at that directory.";
    }
  }

  private required(document: Document, id: string): HTMLElement {
    const element = document.getElementById(id);
    if (!element) throw new Error(`Missing #${id}`);
    return element;
  }
}
