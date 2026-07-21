import { LifecycleScope, type ComponentHandle } from "./lifecycle";

export interface EngineEventPayloads {
  "agent.changed": unknown;
  "browser.automation.exec": unknown;
  "config.changed": { key?: string };
  "dictation.partial": { text?: string };
  "dictation.progress": { percent?: number };
  "dictation.model": { ready?: boolean; error?: string };
  "dock.badge": { nookId?: string };
  "dock.badge.clear": unknown;
  "engine.reconnected": unknown;
  "needs-input.clear": { nookId?: string };
  "notification.deliver": unknown;
  "restore.summary": { restored?: number; fresh?: number; skipped?: number; bootedAt?: string };
  "state.changed": unknown;
  "session.recents.changed": { revision: number };
  "workspace.changed": { revision: number; uri: string };
}

interface EngineEventEnvelope {
  channel?: string;
  payload?: unknown;
}

type EventHandler = (payload: unknown) => void;
type EventSubscriber = (listener: (data: unknown) => void) => () => void;

export class EngineEventRouter implements ComponentHandle {
  private readonly lifecycle = new LifecycleScope();
  private readonly handlers = new Map<string, Set<EventHandler>>();
  private started = false;
  private starting = false;

  constructor(private readonly subscribe: EventSubscriber) {}

  register<K extends keyof EngineEventPayloads>(
    channel: K,
    handler: (payload: EngineEventPayloads[K]) => void,
  ): ComponentHandle {
    const handlers = this.handlers.get(channel) ?? new Set<EventHandler>();
    const stored = handler as EventHandler;
    handlers.add(stored);
    this.handlers.set(channel, handlers);
    let active = true;
    const dispose = (): void => {
      if (!active) return;
      active = false;
      handlers.delete(stored);
      if (handlers.size === 0) this.handlers.delete(channel);
    };
    this.lifecycle.own(dispose);
    return { dispose };
  }

  start(registerConsumers: () => void = () => {}): void {
    if (this.started || this.starting) return;
    this.starting = true;
    try {
      registerConsumers();
      const unsubscribe = this.subscribe((data) => {
        const event = data as EngineEventEnvelope | null;
        if (!event?.channel) {
          console.warn("engine event missing channel", data);
          return;
        }
        for (const handler of this.handlers.get(event.channel) ?? []) handler(event.payload);
      });
      this.lifecycle.own(unsubscribe);
      this.started = true;
    } finally {
      this.starting = false;
    }
  }

  dispose(): Promise<void> {
    this.handlers.clear();
    return this.lifecycle.dispose();
  }
}
