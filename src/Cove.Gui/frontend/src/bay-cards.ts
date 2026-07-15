import type { BayIcon } from "./bay-icons";

export interface BayCardEntry {
  id: string;
  name: string;
  projectDir: string;
  icon?: BayIcon | null;
}

export interface FsEntry {
  name: string;
  isDir: boolean;
  status?: "M" | "A" | "D";
}

export interface FsStatusEntry {
  path: string;
  status: "M" | "A" | "D";
}

export const BAY_ACCENTS = [
  "#cba6f7", "#89b4fa", "#a6e3a1", "#f9e2af", "#f38ba8", "#94e2d5", "#fab387", "#f5c2e7",
];

export function bayAccent(id: string): string {
  let hash = 0;
  for (let i = 0; i < id.length; i++) hash = (hash * 31 + id.charCodeAt(i)) >>> 0;
  return BAY_ACCENTS[hash % BAY_ACCENTS.length];
}

export function resolveActiveBayId(items: BayCardEntry[], activeId: string | null): string | null {
  if (items.length === 0) return null;
  return (items.find((w) => w.id === activeId) ?? items[0]).id;
}

export interface BayHeadNavigation {
  switchRequired: boolean;
  showLauncher: boolean;
}

export function bayHeadNavigation(activeBayId: string | null, clickedBayId: string): BayHeadNavigation {
  return { switchRequired: activeBayId !== clickedBayId, showLauncher: true };
}

export function sortFsEntries(entries: FsEntry[]): FsEntry[] {
  const rank = (e: FsEntry): number => (e.isDir ? 0 : 2) + (e.name.startsWith(".") ? 1 : 0);
  return [...entries].sort((a, b) => {
    const ra = rank(a);
    const rb = rank(b);
    if (ra !== rb) return ra - rb;
    return a.name.localeCompare(b.name, undefined, { sensitivity: "base" });
  });
}

export function joinPath(dir: string, name: string): string {
  return dir.endsWith("/") ? dir + name : `${dir}/${name}`;
}

export function mergeFsStatus(entries: FsEntry[], relativeDir: string, statuses: FsStatusEntry[]): FsEntry[] {
  const normalizedDir = relativeDir.replace(/^\.\//, "").replace(/^\/+|\/+$/g, "");
  const prefix = normalizedDir ? `${normalizedDir}/` : "";
  const merged = new Map(entries.map((entry) => [entry.name, { ...entry }]));
  for (const item of statuses) {
    const path = item.path.replace(/^\.\//, "").replace(/^\/+/, "");
    if (!path.startsWith(prefix)) continue;
    const remainder = path.slice(prefix.length);
    if (!remainder) continue;
    const slash = remainder.indexOf("/");
    if (slash >= 0) {
      const name = remainder.slice(0, slash);
      if (!merged.has(name)) merged.set(name, { name, isDir: true });
      continue;
    }
    const existing = merged.get(remainder);
    merged.set(remainder, { name: remainder, isDir: existing?.isDir ?? false, status: item.status });
  }
  return sortFsEntries([...merged.values()]);
}

export interface ScmSummary {
  ok: boolean;
  branch?: string;
  ahead?: number;
  behind?: number;
  dirty?: number;
  files?: FsStatusEntry[];
  error?: string | null;
}

export function scmChipText(s: ScmSummary): string {
  if (!s.ok || !s.branch) return "";
  const parts = [s.branch];
  if ((s.ahead ?? 0) > 0) parts.push(`↑${s.ahead}`);
  if ((s.behind ?? 0) > 0) parts.push(`↓${s.behind}`);
  if ((s.dirty ?? 0) > 0) parts.push(`●${s.dirty}`);
  return parts.join(" ");
}

export function parseCollapsedCardIds(json: string | null): Set<string> {
  if (!json) return new Set();
  try {
    const parsed = JSON.parse(json);
    if (!Array.isArray(parsed)) return new Set();
    return new Set(parsed.filter((v): v is string => typeof v === "string"));
  } catch {
    return new Set();
  }
}

export function serializeCollapsedCardIds(ids: ReadonlySet<string>): string {
  return JSON.stringify([...ids]);
}

export function toggleCardCollapsed(ids: ReadonlySet<string>, id: string): Set<string> {
  const next = new Set(ids);
  if (next.has(id)) next.delete(id);
  else next.add(id);
  return next;
}

export function dirBasename(p: string): string {
  const parts = p.split("/").filter((s) => s.length > 0);
  return parts.length > 0 ? parts[parts.length - 1] : "";
}
