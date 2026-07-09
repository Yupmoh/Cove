export interface PermissionRequest {
  requestId: string;
  kinds: string[];
  url: string;
}

export function permissionOrigin(url: string): string {
  try {
    return new URL(url).origin;
  } catch {
    return url;
  }
}

const kindLabels: Record<string, string> = {
  camera: "Camera",
  microphone: "Microphone",
  screenShare: "Screen sharing",
  geolocation: "Location",
  clipboard: "Clipboard",
  notifications: "Notifications",
  mouseLock: "Pointer lock",
  deviceInfo: "Device info",
};

export function formatPermissionKinds(kinds: string[]): string {
  if (kinds.length === 0) return "unknown access";
  return kinds.map((k) => kindLabels[k] ?? k).join(", ");
}

export class PermissionPromptQueue {
  private pending: PermissionRequest[] = [];

  add(req: PermissionRequest): void {
    if (this.pending.some((r) => r.requestId === req.requestId)) return;
    this.pending.push(req);
  }

  get active(): PermissionRequest | null {
    return this.pending[0] ?? null;
  }

  get count(): number {
    return this.pending.length;
  }

  has(requestId: string): boolean {
    return this.pending.some((r) => r.requestId === requestId);
  }

  remove(requestId: string): PermissionRequest | null {
    const idx = this.pending.findIndex((r) => r.requestId === requestId);
    if (idx < 0) return null;
    const [removed] = this.pending.splice(idx, 1);
    return removed;
  }

  clear(): void {
    this.pending = [];
  }
}
