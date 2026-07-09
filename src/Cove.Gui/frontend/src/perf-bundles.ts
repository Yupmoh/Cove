import { formatBytes } from "./diagnostics-snapshot";

export interface PerfBundleDto {
  id: string;
  bundlePath: string;
  createdAt: string;
  sizeBytes: number;
  snapshotCount: number;
  containsTrace: boolean;
}

export interface PerfBundleListResult {
  bundles: PerfBundleDto[];
}

export interface PerfBundleRow {
  id: string;
  bundlePath: string;
  name: string;
  createdAtLabel: string;
  sizeLabel: string;
  detail: string;
  confirmingDelete: boolean;
}

export interface PerfBundlesState {
  bundles: PerfBundleDto[];
  creating: boolean;
  error: string | null;
  pendingDeletePath: string | null;
}

export const PERF_BUNDLES_EMPTY_TEXT =
  "No performance bundles yet. Create one to package the current diagnostics snapshots (and an optional trace) into a shareable .zip.";

export function initialPerfBundlesState(): PerfBundlesState {
  return { bundles: [], creating: false, error: null, pendingDeletePath: null };
}

export function applyBundleList(state: PerfBundlesState, result: PerfBundleListResult): PerfBundlesState {
  const bundles = Array.isArray(result?.bundles) ? result.bundles.filter(isBundle) : [];
  const stillPresent = state.pendingDeletePath !== null && bundles.some((b) => b.bundlePath === state.pendingDeletePath);
  return { ...state, bundles, error: null, pendingDeletePath: stillPresent ? state.pendingDeletePath : null };
}

export function beginCreate(state: PerfBundlesState): PerfBundlesState {
  return { ...state, creating: true, error: null };
}

export function finishCreate(state: PerfBundlesState): PerfBundlesState {
  return { ...state, creating: false };
}

export function surfaceError(state: PerfBundlesState, message: string): PerfBundlesState {
  return { ...state, creating: false, error: message };
}

export function requestDelete(state: PerfBundlesState, bundlePath: string): PerfBundlesState {
  return { ...state, pendingDeletePath: bundlePath, error: null };
}

export function cancelDelete(state: PerfBundlesState): PerfBundlesState {
  return { ...state, pendingDeletePath: null };
}

export function bundleRows(state: PerfBundlesState): PerfBundleRow[] {
  return state.bundles.map((bundle) => ({
    id: bundle.id,
    bundlePath: bundle.bundlePath,
    name: bundleName(bundle.bundlePath),
    createdAtLabel: formatCreatedAt(bundle.createdAt),
    sizeLabel: formatBytes(bundle.sizeBytes),
    detail: bundleDetail(bundle),
    confirmingDelete: state.pendingDeletePath === bundle.bundlePath,
  }));
}

function bundleName(bundlePath: string): string {
  const parts = bundlePath.split(/[\\/]/);
  const last = parts[parts.length - 1];
  return last.length > 0 ? last : bundlePath;
}

function bundleDetail(bundle: PerfBundleDto): string {
  const snapshots = `${bundle.snapshotCount} snapshot${bundle.snapshotCount === 1 ? "" : "s"}`;
  return `${snapshots} · ${bundle.containsTrace ? "trace included" : "no trace"}`;
}

function formatCreatedAt(createdAt: string): string {
  const parsed = Date.parse(createdAt);
  return Number.isNaN(parsed) ? createdAt : new Date(parsed).toLocaleString();
}

function isBundle(value: unknown): value is PerfBundleDto {
  if (typeof value !== "object" || value === null) return false;
  const record = value as Record<string, unknown>;
  return typeof record.id === "string" && typeof record.bundlePath === "string" && typeof record.sizeBytes === "number";
}
