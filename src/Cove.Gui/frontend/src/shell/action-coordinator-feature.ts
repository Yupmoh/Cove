import {
  ActionRegistry,
  parseCoveAction,
  type ActionHandler,
  type CoveAction,
} from "../app/action-registry";
import { FrontendCommand } from "../app/frontend-command";
import type { FrontendEvent } from "../app/frontend-event";
import { LifecycleScope, type ComponentHandle } from "../app/lifecycle";
import {
  buildChordMap,
  defaultBindings,
  eventToChord,
  resolveDispatch,
  type ResolvedBinding,
} from "../keymap-dispatch";
import { menuChordSet } from "../menu-model";
import { normalizeChord } from "../keyboard-editor";
import { MenuBarFeature } from "./menu-bar-feature";

export interface ActionCoordinatorDependencies {
  window: Window;
  actions: ActionRegistry<CoveAction>;
  invoke<T>(command: FrontendCommand, args: Record<string, unknown>): Promise<T>;
  observe(event: FrontendEvent, callback: (data: unknown) => void): () => void;
  handlers: readonly (readonly [CoveAction, ActionHandler])[];
  switchBayByIndex(index: number): Promise<void>;
  isPaletteOpen(): boolean;
  nativeMenuEventsBroken: boolean;
}

export interface ActionCoordinatorFeature extends ComponentHandle {
  run(action: string): void;
  start(): void;
  reloadKeymap(): Promise<void>;
}

export function createActionCoordinatorFeature(
  dependencies: ActionCoordinatorDependencies,
): ActionCoordinatorFeature {
  const lifecycle = new LifecycleScope();
  const registry = dependencies.actions;
  let resolvedBindings: ResolvedBinding[] = defaultBindings();
  let chordMap = buildChordMap(resolvedBindings);
  let menuChords = menuChordSet(actionChords());
  let started = false;

  function actionChords(): { action: string; chord: string }[] {
    return resolvedBindings.map((binding) => ({
      action: binding.action,
      chord: binding.chord,
    }));
  }

  function run(action: string): void {
    const parsed = parseCoveAction(action);
    if (!parsed) {
      console.warn("unhandled keymap action", action);
      return;
    }
    if (parsed.startsWith("bay.switch-")) {
      void dependencies.switchBayByIndex(Number(parsed.slice("bay.switch-".length)));
      return;
    }
    void registry.dispatch(parsed).then((handled) => {
      if (!handled) console.warn("action has no registered owner", parsed);
    }).catch((error) => {
      console.warn("action dispatch failed", { action: parsed, error });
    });
  }

  const menuBar = new MenuBarFeature({
    invoke: dependencies.invoke,
    observe: dependencies.observe,
    actionChords,
    runAction: run,
    nativeEventsBroken: dependencies.nativeMenuEventsBroken,
  });

  async function reloadKeymap(): Promise<void> {
    const merged = new Map<string, ResolvedBinding>();
    for (const binding of defaultBindings()) {
      merged.set(normalizeChord(binding.chord), binding);
    }
    try {
      const result = await dependencies.invoke<{
        bindings: { chord: string; actionType: string; action: string }[];
      }>(FrontendCommand.KeybindList, {});
      for (const binding of result.bindings ?? []) {
        merged.set(normalizeChord(binding.chord), {
          chord: binding.chord,
          actionType: binding.actionType,
          action: binding.action,
        });
      }
    } catch (error) {
      console.warn("keybind.list unavailable, using default keymap", error);
    }
    resolvedBindings = [...merged.values()];
    chordMap = buildChordMap(resolvedBindings);
    menuChords = menuChordSet(actionChords());
    menuBar.refresh();
  }

  function start(): void {
    if (started) return;
    started = true;
    for (const [action, handler] of dependencies.handlers) {
      const registration = registry.register(action, handler);
      lifecycle.own(() => registration.dispose());
    }
    menuBar.start();
    lifecycle.listen(dependencies.window, "keydown", (event) => {
      const keyEvent = event as KeyboardEvent;
      const chord = eventToChord({
        metaKey: keyEvent.metaKey,
        ctrlKey: keyEvent.ctrlKey,
        altKey: keyEvent.altKey,
        shiftKey: keyEvent.shiftKey,
        key: keyEvent.key,
      });
      if (!chord) return;
      const decision = resolveDispatch(chord, chordMap, menuChords);
      const dispatchable = decision.kind === "dispatch"
        || (dependencies.nativeMenuEventsBroken && decision.kind === "menu-owned");
      if (!dispatchable) return;
      if (dependencies.isPaletteOpen() && decision.action !== "tool.palette") return;
      keyEvent.preventDefault();
      run(decision.action);
    }, true);
  }

  return {
    run,
    start,
    reloadKeymap,
    async dispose() {
      await menuBar.dispose();
      registry.dispose();
      await lifecycle.dispose();
    },
  };
}
