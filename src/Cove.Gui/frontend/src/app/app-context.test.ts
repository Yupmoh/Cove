import { describe, expect, it } from "vitest";
import type { ActionRegistry, CoveAction } from "./action-registry";
import type { EnginePort } from "./engine-client";
import type { EngineEventRouter } from "./engine-event-router";
import type { LifecycleScope } from "./lifecycle";
import type { WorkspaceController } from "../workspace/workspace-controller";
import type { WorkspaceStore } from "../workspace/workspace-store";
import { createAppContext } from "./app-context";

describe("createAppContext", () => {
  it("freezes one immutable dependency graph without copying shared owners", () => {
    const document = {} as Document;
    const window = {} as Window;
    const storage = {} as Storage;
    const engine = {} as EnginePort;
    const events = {} as EngineEventRouter;
    const actions = {} as ActionRegistry<CoveAction>;
    const lifecycle = {} as LifecycleScope;
    const workspace = {} as WorkspaceStore;
    const workspaceController = {} as WorkspaceController;

    const context = createAppContext({
      document,
      window,
      storage,
      engine,
      events,
      actions,
      lifecycle,
      workspace,
      workspaceController,
    });

    expect(Object.isFrozen(context)).toBe(true);
    expect(context).toEqual({
      document,
      window,
      storage,
      engine,
      events,
      actions,
      lifecycle,
      workspace,
      workspaceController,
    });
    expect(Reflect.set(context, "workspace", {})).toBe(false);
  });
});
