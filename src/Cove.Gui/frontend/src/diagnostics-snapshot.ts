export interface DiagnosticsSnapshot {
  takenAt: string;
  managedMemoryBytes: number;
  workingSetBytes: number;
  threadCount: number;
  gcGen0Collections: number;
  gcGen1Collections: number;
  gcGen2Collections: number;
  activeNooks: number;
  activeBays: number;
  activeAgents: number;
  cpuUsagePercent: number;
  nookScrollbackBytes: Record<string, number>;
}

export interface SnapshotParseResult {
  ok: boolean;
  snapshots: DiagnosticsSnapshot[];
  error: string | null;
}

export interface SnapshotRow {
  label: string;
  value: string;
}

export interface SnapshotSummary {
  count: number;
  firstTakenAt: string | null;
  lastTakenAt: string | null;
  peakManagedMemoryBytes: number;
}

export function parseSnapshotExport(text: string): SnapshotParseResult {
  const trimmed = text.trim();
  if (trimmed.length === 0) return fail("No snapshot JSON provided.");

  let parsed: unknown;
  try {
    parsed = JSON.parse(trimmed);
  } catch {
    return fail("Input is not valid JSON.");
  }

  const candidates = Array.isArray(parsed) ? parsed : [parsed];
  const snapshots: DiagnosticsSnapshot[] = [];
  for (const candidate of candidates) {
    if (!isSnapshot(candidate)) return fail("JSON does not match the diagnostics snapshot export format.");
    snapshots.push(normalizeSnapshot(candidate));
  }
  if (snapshots.length === 0) return fail("No snapshots found in the export.");
  return { ok: true, snapshots, error: null };
}

export function totalScrollbackBytes(snapshot: DiagnosticsSnapshot): number {
  let total = 0;
  for (const value of Object.values(snapshot.nookScrollbackBytes)) total += value;
  return total;
}

export function snapshotRows(snapshot: DiagnosticsSnapshot): SnapshotRow[] {
  return [
    { label: "Taken at", value: snapshot.takenAt },
    { label: "Managed memory", value: formatBytes(snapshot.managedMemoryBytes) },
    { label: "Working set", value: formatBytes(snapshot.workingSetBytes) },
    { label: "Threads", value: String(snapshot.threadCount) },
    { label: "GC gen0 / gen1 / gen2", value: `${snapshot.gcGen0Collections} / ${snapshot.gcGen1Collections} / ${snapshot.gcGen2Collections}` },
    { label: "CPU", value: `${snapshot.cpuUsagePercent.toFixed(1)}%` },
    { label: "Active nooks", value: String(snapshot.activeNooks) },
    { label: "Active bays", value: String(snapshot.activeBays) },
    { label: "Active agents", value: String(snapshot.activeAgents) },
    { label: "Nook scrollback", value: formatBytes(totalScrollbackBytes(snapshot)) },
  ];
}

export function summarizeSnapshots(snapshots: DiagnosticsSnapshot[]): SnapshotSummary {
  if (snapshots.length === 0) return { count: 0, firstTakenAt: null, lastTakenAt: null, peakManagedMemoryBytes: 0 };
  let peak = 0;
  for (const snapshot of snapshots) {
    if (snapshot.managedMemoryBytes > peak) peak = snapshot.managedMemoryBytes;
  }
  return {
    count: snapshots.length,
    firstTakenAt: snapshots[0].takenAt,
    lastTakenAt: snapshots[snapshots.length - 1].takenAt,
    peakManagedMemoryBytes: peak,
  };
}

export function formatBytes(bytes: number): string {
  const units = ["B", "KB", "MB", "GB", "TB"];
  let value = bytes;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit++;
  }
  if (unit === 0) return `${Math.round(value)} B`;
  return `${value.toFixed(1)} ${units[unit]}`;
}

function isSnapshot(value: unknown): value is Record<string, unknown> {
  if (typeof value !== "object" || value === null) return false;
  const record = value as Record<string, unknown>;
  return typeof record.takenAt === "string" && typeof record.managedMemoryBytes === "number" && typeof record.threadCount === "number";
}

function normalizeSnapshot(record: Record<string, unknown>): DiagnosticsSnapshot {
  return {
    takenAt: asString(record.takenAt),
    managedMemoryBytes: asNumber(record.managedMemoryBytes),
    workingSetBytes: asNumber(record.workingSetBytes),
    threadCount: asNumber(record.threadCount),
    gcGen0Collections: asNumber(record.gcGen0Collections),
    gcGen1Collections: asNumber(record.gcGen1Collections),
    gcGen2Collections: asNumber(record.gcGen2Collections),
    activeNooks: asNumber(record.activeNooks),
    activeBays: asNumber(record.activeBays),
    activeAgents: asNumber(record.activeAgents),
    cpuUsagePercent: asNumber(record.cpuUsagePercent),
    nookScrollbackBytes: asByteMap(record.nookScrollbackBytes),
  };
}

function asString(value: unknown): string {
  return typeof value === "string" ? value : "";
}

function asNumber(value: unknown): number {
  return typeof value === "number" && Number.isFinite(value) ? value : 0;
}

function asByteMap(value: unknown): Record<string, number> {
  if (typeof value !== "object" || value === null) return {};
  const result: Record<string, number> = {};
  for (const [key, raw] of Object.entries(value as Record<string, unknown>)) {
    if (typeof raw === "number" && Number.isFinite(raw)) result[key] = raw;
  }
  return result;
}

function fail(message: string): SnapshotParseResult {
  return { ok: false, snapshots: [], error: message };
}
