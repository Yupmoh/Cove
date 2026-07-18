import type { ComponentHandle } from "./lifecycle";

export type ActionHandler = () => void | Promise<void>;

const coveActions = [
  "shore.new",
  "shore.close",
  "shore.next",
  "shore.prev",
  "shore.pin",
  "shore.omni-jump",
  "nook.close",
  "nook.split-right",
  "nook.split-down",
  "nook.focus-next",
  "nook.focus-prev",
  "nook.find",
  "nook.scroll-top",
  "nook.scroll-bottom",
  "nook.maximize",
  "bay.create",
  "view.toggle-sidebar",
  "view.toggle-notepad",
  "view.show-bays",
  "view.zen-mode",
  "view.zoom-in",
  "view.zoom-out",
  "view.zoom-reset",
  "view.toggle-backdrop",
  "view.toggle-performance",
  "tool.inspect",
  "tool.git",
  "tool.search",
  "tool.tasks",
  "tool.library",
  "tool.browser",
  "tool.notepad",
  "tool.palette",
  "tool.launcher",
  "app.settings",
  "app.zoom-in",
  "app.zoom-out",
  "app.update",
] as const;

export type CoveAction = (typeof coveActions)[number] | `bay.switch-${number}`;

const staticCoveActions: ReadonlySet<string> = new Set(coveActions);

export function parseCoveAction(value: string): CoveAction | null {
  if (staticCoveActions.has(value)) return value as CoveAction;
  if (/^bay\.switch-\d+$/.test(value)) return value as CoveAction;
  return null;
}

export class ActionRegistry<Action extends string = string> implements ComponentHandle {
  private readonly handlers = new Map<Action, ActionHandler>();
  private disposed = false;

  register(action: Action, handler: ActionHandler): ComponentHandle {
    this.assertActive();
    if (this.handlers.has(action)) throw new Error(`Action already registered: ${action}`);
    this.handlers.set(action, handler);
    let registered = true;
    return {
      dispose: () => {
        if (!registered) return;
        registered = false;
        if (this.handlers.get(action) === handler) this.handlers.delete(action);
      },
    };
  }

  async dispatch(action: Action): Promise<boolean> {
    const handler = this.handlers.get(action);
    if (!handler) return false;
    await handler();
    return true;
  }

  dispose(): void {
    if (this.disposed) return;
    this.disposed = true;
    this.handlers.clear();
  }

  private assertActive(): void {
    if (this.disposed) throw new Error("ActionRegistry is disposed");
  }
}
