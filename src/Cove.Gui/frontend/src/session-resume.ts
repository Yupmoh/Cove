import { relativeTime, type RecentSessionRow } from "./launcher-model";

export interface VaultResumeResult {
  ok: boolean;
  adapter: string;
  command: string[];
  cwd: string;
  fallback: "none" | "fresh";
  error: string | null;
}

export interface AdapterLabel {
  name: string;
  displayName: string;
}

export interface RecentSessionEntry {
  adapter: string;
  sessionId: string;
  cwd: string;
  label: string;
  relative: string;
}

export interface AdapterSessionGroup {
  adapter: string;
  displayName: string;
  sessions: RecentSessionEntry[];
}

export type ResumeAction =
  | {
      kind: "spawn";
      adapter: string;
      command: string;
      args: string[];
      cwd: string;
      shoreName: string;
      sessionId: string | null;
      yolo: boolean;
      toast: { title: string; body: string } | null;
    }
  | { kind: "error"; toast: { title: string; body: string } };

function normalizeDir(dir: string): string {
  return (dir ?? "").replace(/[/\\]+$/, "");
}

export function sessionLabel(row: RecentSessionRow, nowMs: number): string {
  const summary = (row as { label?: string | null }).label;
  if (summary && summary.trim().length > 0) return summary.trim();
  const rel = relativeTime(row.startedAt, nowMs);
  return rel ? `${rel} session` : "session";
}

export function recentsForProjectDir(rows: RecentSessionRow[], projectDir: string): RecentSessionRow[] {
  const target = normalizeDir(projectDir);
  if (target.length === 0) return [];
  return rows.filter((r) => normalizeDir(r.cwd) === target);
}

export function groupRecentsByAdapter(
  rows: RecentSessionRow[],
  projectDir: string,
  adapters: AdapterLabel[],
  nowMs: number,
): AdapterSessionGroup[] {
  const scoped = recentsForProjectDir(rows, projectDir);
  const displayFor = new Map(adapters.map((a) => [a.name, a.displayName]));
  const order: string[] = [];
  const grouped = new Map<string, RecentSessionEntry[]>();
  for (const r of scoped) {
    if (!grouped.has(r.adapter)) {
      grouped.set(r.adapter, []);
      order.push(r.adapter);
    }
    grouped.get(r.adapter)!.push({
      adapter: r.adapter,
      sessionId: r.sessionId,
      cwd: r.cwd,
      label: sessionLabel(r, nowMs),
      relative: relativeTime(r.startedAt, nowMs),
    });
  }
  return order.map((name) => ({
    adapter: name,
    displayName: displayFor.get(name) ?? name,
    sessions: grouped.get(name)!,
  }));
}

export function resumeSpawnPlan(result: VaultResumeResult, projectDir: string, displayName: string, sessionId?: string, yolo?: boolean): ResumeAction {
  const name = displayName || result.adapter;
  if (!result.ok || result.command.length === 0) {
    return { kind: "error", toast: { title: "Resume failed", body: result.error ?? "could not resume session" } };
  }
  const [command, ...args] = result.command;
  const toast =
    result.fallback === "fresh"
      ? { title: "Couldn't resume", body: `couldn't resume — started a fresh ${name} session` }
      : null;
  const resumedSessionId = result.fallback === "fresh" ? null : (sessionId ?? null);
  return { kind: "spawn", adapter: result.adapter, command, args, cwd: projectDir, shoreName: name, sessionId: resumedSessionId, yolo: yolo ?? false, toast };
}
