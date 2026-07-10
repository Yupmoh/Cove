export interface WorkspaceCardEntry {
  id: string;
  name: string;
  projectDir: string;
}

export interface FsEntry {
  name: string;
  isDir: boolean;
}

export const WORKSPACE_ACCENTS = [
  "#cba6f7", "#89b4fa", "#a6e3a1", "#f9e2af", "#f38ba8", "#94e2d5", "#fab387", "#f5c2e7",
];

export function workspaceAccent(id: string): string {
  let hash = 0;
  for (let i = 0; i < id.length; i++) hash = (hash * 31 + id.charCodeAt(i)) >>> 0;
  return WORKSPACE_ACCENTS[hash % WORKSPACE_ACCENTS.length];
}

export function splitWorkspaceCards(items: WorkspaceCardEntry[], activeId: string | null): { active: WorkspaceCardEntry | null; others: WorkspaceCardEntry[] } {
  if (items.length === 0) return { active: null, others: [] };
  const active = items.find((w) => w.id === activeId) ?? items[0];
  return { active, others: items.filter((w) => w.id !== active.id) };
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

export interface ScmSummary {
  ok: boolean;
  branch?: string;
  ahead?: number;
  behind?: number;
  dirty?: number;
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

export function dirBasename(p: string): string {
  const parts = p.split("/").filter((s) => s.length > 0);
  return parts.length > 0 ? parts[parts.length - 1] : "";
}
