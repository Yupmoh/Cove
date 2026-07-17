export interface HarnessUpdate {
  name: string;
  displayName: string;
  installedVersion: string;
  latestVersion: string;
  updateCommand: string | null;
}

export const HARNESS_UPDATE_CHECK_INTERVAL_MS = 600_000;
export const HARNESS_UPDATE_DISMISSED_KEY = "cove:harness-updates:dismissed";

export function parseDismissed(raw: string | null): Record<string, string> {
  if (!raw) return {};
  let parsed: unknown;
  try {
    parsed = JSON.parse(raw);
  } catch {
    return {};
  }
  if (typeof parsed !== "object" || parsed === null || Array.isArray(parsed)) return {};
  const out: Record<string, string> = {};
  for (const [name, version] of Object.entries(parsed)) {
    if (typeof version === "string") out[name] = version;
  }
  return out;
}

export function filterToastableUpdates(updates: HarnessUpdate[], dismissed: Record<string, string>): HarnessUpdate[] {
  const out: HarnessUpdate[] = [];
  for (const u of updates) {
    if (dismissed[u.name] === u.latestVersion) continue;
    out.push(u);
  }
  return out;
}

export function recordDismissal(dismissed: Record<string, string>, update: HarnessUpdate): Record<string, string> {
  const next = { ...dismissed };
  next[update.name] = update.latestVersion;
  return next;
}
