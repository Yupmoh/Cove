import type { WorkspaceController } from "../workspace/workspace-controller";
import type { WorkspaceStore } from "../workspace/workspace-store";
import type { ActionRegistry, CoveAction } from "./action-registry";
import type { EnginePort } from "./engine-client";
import type { EngineEventRouter } from "./engine-event-router";
import type { LifecycleScope } from "./lifecycle";

export interface AppContext {
  readonly document: Document;
  readonly window: Window;
  readonly storage: Storage;
  readonly engine: EnginePort;
  readonly events: EngineEventRouter;
  readonly actions: ActionRegistry<CoveAction>;
  readonly lifecycle: LifecycleScope;
  readonly workspace: WorkspaceStore;
  readonly workspaceController: WorkspaceController;
}

export function createAppContext(context: AppContext): AppContext {
  return Object.freeze({ ...context });
}
