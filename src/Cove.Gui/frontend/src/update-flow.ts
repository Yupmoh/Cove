export type UpdateState =
  | { kind: "idle" }
  | { kind: "checking" }
  | { kind: "upToDate" }
  | { kind: "available"; version: string; notes: string | null }
  | { kind: "downloading" }
  | { kind: "readyToApply"; handle: string; version: string }
  | { kind: "applying" }
  | { kind: "failed"; message: string };

export type UpdateEvent =
  | { type: "check" }
  | { type: "checkedUpToDate" }
  | { type: "checkedAvailable"; version: string; notes: string | null }
  | { type: "download" }
  | { type: "downloaded"; handle: string; version: string }
  | { type: "apply" }
  | { type: "error"; message: string }
  | { type: "retry" };

const CHECKABLE: ReadonlySet<UpdateState["kind"]> = new Set(["idle", "upToDate", "available", "readyToApply", "failed"]);

export function nextUpdateState(current: UpdateState, event: UpdateEvent): UpdateState {
  switch (event.type) {
    case "error":
      return { kind: "failed", message: event.message };
    case "check":
      return CHECKABLE.has(current.kind) ? { kind: "checking" } : current;
    case "retry":
      return current.kind === "failed" ? { kind: "checking" } : current;
    case "checkedUpToDate":
      return current.kind === "checking" ? { kind: "upToDate" } : current;
    case "checkedAvailable":
      return current.kind === "checking" ? { kind: "available", version: event.version, notes: event.notes } : current;
    case "download":
      return current.kind === "available" ? { kind: "downloading" } : current;
    case "downloaded":
      return current.kind === "downloading" ? { kind: "readyToApply", handle: event.handle, version: event.version } : current;
    case "apply":
      return current.kind === "readyToApply" ? { kind: "applying" } : current;
    default:
      return current;
  }
}

export function updateButtonLabel(state: UpdateState): string {
  switch (state.kind) {
    case "idle":
      return "Check for updates";
    case "checking":
      return "Checking…";
    case "upToDate":
      return "Up to date";
    case "available":
      return `Update to ${state.version}`;
    case "downloading":
      return "Downloading…";
    case "readyToApply":
      return "Restart to update";
    case "applying":
      return "Applying…";
    case "failed":
      return "Retry update";
  }
}

export function updateAffordanceVisible(state: UpdateState): boolean {
  return state.kind === "available" || state.kind === "readyToApply";
}
