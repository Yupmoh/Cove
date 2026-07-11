export type TreeRowKind = "bay" | "shore" | "nook";

export interface TreeLeaf {
  nookId: string;
  nookType: string;
  title: string;
}

export interface TreeShoreInput {
  id: string;
  name: string;
  leaves: TreeLeaf[];
}

export interface BayEntry {
  id: string;
  name: string;
}

export interface BayTreeInput {
  bayName: string;
  activeShoreId: string | null;
  focusedNookId: string | null;
  shores: TreeShoreInput[];
  collapsedShoreIds: Set<string>;
  bayCollapsed: boolean;
  bays?: BayEntry[];
  activeBayId?: string | null;
}

export interface TreeRow {
  kind: TreeRowKind;
  key: string;
  label: string;
  depth: number;
  shoreId: string | null;
  nookId: string | null;
  nookType: string | null;
  bayId: string | null;
  active: boolean;
  expandable: boolean;
  collapsed: boolean;
  count: number;
}

export const NOOK_TYPE_LABELS: Record<string, string> = {
  terminal: "terminal",
  browser: "browser",
  git: "source control",
  search: "search",
  editor: "editor",
  notepad: "notepad",
  markdown: "markdown",
  "tasks-kanban": "tasks",
  "tasks-list": "tasks",
  "tasks-detail": "task",
  "timeline-feed": "timeline",
  "diff-review": "diff review",
  diff: "diff",
  image: "image",
  pdf: "pdf",
  video: "video",
  library: "library",
  "session-picker": "sessions",
  "snapshot-inspector": "snapshots",
  empty: "empty",
};

export const NO_BAYS_MESSAGE = "No bays yet, create or open a bay";

export function bayTreeEmptyMessage(bayCount: number): string | null {
  return bayCount <= 0 ? NO_BAYS_MESSAGE : null;
}

export function nookLabel(leaf: TreeLeaf): string {
  const title = leaf.title.trim();
  if (title.length > 0) return title;
  return NOOK_TYPE_LABELS[leaf.nookType] ?? leaf.nookType ?? "nook";
}

export function buildBayTree(input: BayTreeInput): TreeRow[] {
  const rows: TreeRow[] = [];
  const activeId = input.activeBayId ?? null;
  const bays: BayEntry[] = input.bays && input.bays.length > 0
    ? input.bays
    : [{ id: activeId ?? "active", name: input.bayName }];

  for (const ws of bays) {
    const isActive = bays.length === 1 || ws.id === activeId;
    rows.push({
      kind: "bay",
      key: `ws:${ws.id}`,
      label: ws.name,
      depth: 0,
      shoreId: null,
      nookId: null,
      nookType: null,
      bayId: ws.id,
      active: isActive,
      expandable: isActive && input.shores.length > 0,
      collapsed: isActive ? input.bayCollapsed : true,
      count: isActive ? input.shores.length : 0,
    });
    if (!isActive || input.bayCollapsed) continue;

    for (const shore of input.shores) {
      const shoreCollapsed = input.collapsedShoreIds.has(shore.id);
      const realLeaves = shore.leaves.filter((l) => l.nookType !== "empty");
      rows.push({
        kind: "shore",
        key: `shore:${shore.id}`,
        label: shore.name,
        depth: 1,
        shoreId: shore.id,
        nookId: null,
        nookType: null,
        bayId: ws.id,
        active: shore.id === input.activeShoreId,
        expandable: realLeaves.length > 0,
        collapsed: shoreCollapsed,
        count: realLeaves.length,
      });
      if (shoreCollapsed || realLeaves.length === 0) continue;
      for (const leaf of realLeaves) {
        rows.push({
          kind: "nook",
          key: `nook:${shore.id}:${leaf.nookId}`,
          label: nookLabel(leaf),
          depth: 2,
          shoreId: shore.id,
          nookId: leaf.nookId,
          nookType: leaf.nookType,
          bayId: ws.id,
          active: shore.id === input.activeShoreId && leaf.nookId === input.focusedNookId,
          expandable: false,
          collapsed: false,
          count: 0,
        });
      }
    }
  }
  return rows;
}
