import { LifecycleScope } from "./lifecycle";
import { FrontendCommand } from "./frontend-command";
import type { FrontendEvent } from "./frontend-event";

export interface EngineBridge {
  invoke(command: string, args?: Record<string, unknown>): Promise<unknown>;
  on(event: string, callback: (data: unknown) => void): void;
  off(event: string, callback: (data: unknown) => void): void;
}

export interface EnginePort {
  invoke<T>(command: FrontendCommand, args: unknown): Promise<T>;
  native<T>(command: FrontendCommand, args: Record<string, unknown>): Promise<T>;
  on<T>(event: FrontendEvent, listener: (data: T) => void): () => void;
}

export class EngineClient implements EnginePort {
  private readonly lifecycle = new LifecycleScope();

  constructor(private readonly bridge: EngineBridge) {}

  async invoke<T>(command: FrontendCommand, args: unknown): Promise<T> {
    this.assertActive();
    let result: unknown;
    if (command.startsWith("cove://")) {
      result = await this.bridge.invoke(FrontendCommand.AppCallEngine, {
        uri: command,
        argsJson: JSON.stringify(args ?? {}),
      });
    } else {
      result = await this.bridge.invoke(command, args as Record<string, unknown>);
    }
    return JSON.parse(result as string) as T;
  }

  async native<T>(command: FrontendCommand, args: Record<string, unknown>): Promise<T> {
    this.assertActive();
    return await this.bridge.invoke(command, args) as T;
  }

  on<T>(event: FrontendEvent, listener: (data: T) => void): () => void {
    this.assertActive();
    const callback = listener as (data: unknown) => void;
    let active = true;
    this.bridge.on(event, callback);
    const unsubscribe = () => {
      if (!active) return;
      active = false;
      this.bridge.off(event, callback);
    };
    this.lifecycle.own(unsubscribe);
    return unsubscribe;
  }

  dispose(): Promise<void> {
    return this.lifecycle.dispose();
  }

  private assertActive(): void {
    if (this.lifecycle.isDisposed) throw new Error("EngineClient is disposed");
  }
}
