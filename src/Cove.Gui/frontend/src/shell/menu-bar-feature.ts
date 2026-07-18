import { FrontendCommand } from "../app/frontend-command";
import { FrontendEvent } from "../app/frontend-event";
import { LifecycleScope, type ComponentHandle } from "../app/lifecycle";
import { buildMenu } from "../menu-model";

export interface MenuBarFeatureDependencies {
  invoke(command: FrontendCommand, args: unknown): Promise<unknown>;
  observe(event: FrontendEvent, callback: (data: unknown) => void): () => void;
  actionChords(): { action: string; chord: string }[];
  runAction(action: string): void;
  nativeEventsBroken: boolean;
}

export class MenuBarFeature implements ComponentHandle {
  private readonly lifecycle = new LifecycleScope();
  private readonly menuActions = new Map<string, string>();
  private started = false;

  constructor(private readonly dependencies: MenuBarFeatureDependencies) {}

  start(): void {
    if (this.started) return;
    this.started = true;
    this.lifecycle.own(this.dependencies.observe(FrontendEvent.MenubarItemClicked, (data) => {
      const id = typeof data === "string" ? data : null;
      if (!id) {
        console.warn("menu click missing item identifier");
        return;
      }
      const action = this.menuActions.get(id);
      if (!action) {
        console.warn("menu item without an action", id);
        return;
      }
      this.dependencies.runAction(action);
    }));
    this.refresh();
  }

  refresh(): void {
    const menu = buildMenu(this.dependencies.actionChords(), this.dependencies.nativeEventsBroken);
    this.menuActions.clear();
    for (const section of menu) {
      for (const item of section.items ?? []) {
        if (item.id && item.action) this.menuActions.set(item.id, item.action);
      }
    }
    void this.dependencies.invoke(FrontendCommand.MenubarSetMenu, { items: menu }).catch((error) => {
      console.warn("menu synchronization failed", error);
    });
  }

  dispose(): Promise<void> {
    this.menuActions.clear();
    return this.lifecycle.dispose();
  }
}
