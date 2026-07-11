export type NookLifecycle = "live" | "crashed";

export function crashReasonText(reason: string | null | undefined): string {
  const trimmed = (reason ?? "").trim();
  if (trimmed.length === 0) return "The page stopped responding and its process was terminated.";
  return `The page process was terminated (${trimmed}).`;
}

export class NookCrashState {
  private state: NookLifecycle = "live";
  private lastReason: string | null = null;

  get lifecycle(): NookLifecycle {
    return this.state;
  }

  get isCrashed(): boolean {
    return this.state === "crashed";
  }

  get reason(): string | null {
    return this.lastReason;
  }

  crash(reason: string | null): boolean {
    if (this.state === "crashed") return false;
    this.state = "crashed";
    this.lastReason = reason;
    return true;
  }

  recover(): boolean {
    if (this.state !== "crashed") return false;
    this.state = "live";
    this.lastReason = null;
    return true;
  }
}
