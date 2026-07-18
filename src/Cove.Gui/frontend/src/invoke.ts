import { EngineClient, type EnginePort } from "./app/engine-client";
import type { FrontendCommand } from "./app/frontend-command";
import type { FrontendEvent } from "./app/frontend-event";

let client: EngineClient | null = null;
let bridge: RynBridge | null = null;

export function getFrontendEngineClient(): EngineClient {
  if (!client || bridge !== window.__ryn) {
    const replaced = client;
    bridge = window.__ryn;
    client = new EngineClient(bridge);
    void replaced?.dispose().catch((error: unknown) => {
      console.warn("replaced frontend transport disposal failed", error);
    });
  }
  return client;
}

const frontendEnginePort: EnginePort = Object.freeze({
  invoke: <T>(command: FrontendCommand, args: unknown) =>
    getFrontendEngineClient().invoke<T>(command, args),
  native: <T>(command: FrontendCommand, args: Record<string, unknown>) =>
    getFrontendEngineClient().native<T>(command, args),
  on: <T>(event: FrontendEvent, listener: (data: T) => void) =>
    getFrontendEngineClient().on(event, listener),
});

export function getFrontendEnginePort(): EnginePort {
  return frontendEnginePort;
}

export function invoke<T>(command: FrontendCommand, args: unknown): Promise<T> {
  return getFrontendEngineClient().invoke<T>(command, args);
}

export function invokeNative<T>(command: FrontendCommand, args: Record<string, unknown>): Promise<T> {
  return getFrontendEngineClient().native<T>(command, args);
}

export function onRyn<T>(event: FrontendEvent, listener: (data: T) => void): () => void {
  return getFrontendEngineClient().on(event, listener);
}

export async function disposeFrontendTransport(): Promise<void> {
  const activeClient = client;
  client = null;
  bridge = null;
  await activeClient?.dispose();
}
