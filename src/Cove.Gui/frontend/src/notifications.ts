export interface NotificationDeliverPayload {
  id: string;
  title: string;
  body: string;
  nookId: string;
}

export type PermissionState = "unknown" | "granted" | "denied";

export interface NotificationBridgeDeps {
  isPermissionGranted: () => Promise<boolean>;
  requestPermission: () => Promise<boolean>;
  send: (payload: NotificationDeliverPayload) => Promise<void>;
  reveal: (nookId: string) => void;
  toast: (payload: NotificationDeliverPayload) => void;
  warn: (message: string) => void;
}

export function shouldRequestPermission(state: PermissionState, requestedThisSession: boolean): boolean {
  return state !== "granted" && !requestedThisSession;
}

export function shouldSend(state: PermissionState): boolean {
  return state === "granted";
}

export function resolveActivated(active: ReadonlyMap<string, string>, id: string): string | null {
  const nookId = active.get(id);
  return nookId ?? null;
}

export class NotificationBridge {
  private permission: PermissionState = "unknown";
  private requestedThisSession = false;
  private degradedWarned = false;
  private readonly active = new Map<string, string>();

  constructor(private readonly deps: NotificationBridgeDeps) {}

  get permissionState(): PermissionState {
    return this.permission;
  }

  trackedNookFor(id: string): string | null {
    return resolveActivated(this.active, id);
  }

  async deliver(payload: NotificationDeliverPayload): Promise<boolean> {
    if (!payload?.id) {
      this.deps.warn("notification.deliver: missing id, dropping");
      return false;
    }

    if (this.permission === "unknown") {
      const granted = await this.deps.isPermissionGranted();
      if (granted) this.permission = "granted";
    }

    if (shouldRequestPermission(this.permission, this.requestedThisSession)) {
      this.requestedThisSession = true;
      const granted = await this.deps.requestPermission();
      this.permission = granted ? "granted" : "denied";
    }

    if (!shouldSend(this.permission)) {
      if (!this.degradedWarned) {
        this.degradedWarned = true;
        this.deps.warn("notification permission denied; degrading to in-app signals only");
      }
      this.active.set(payload.id, payload.nookId);
      this.deps.toast(payload);
      return false;
    }

    this.active.set(payload.id, payload.nookId);
    await this.deps.send(payload);
    return true;
  }

  onActivated(id: string): void {
    const nookId = resolveActivated(this.active, id);
    if (!nookId) {
      this.deps.warn(`notification.activated: no tracked nook for id ${id}`);
      return;
    }
    this.deps.reveal(nookId);
  }

  onDismissed(id: string): void {
    this.active.delete(id);
  }
}
