export type TreeRowKind = "workspace" | "room" | "pane";

export interface TreeLeaf {
  paneId: string;
  paneType: string;
  title: string;
}

export interface TreeRoomInput {
  id: string;
  name: string;
  leaves: TreeLeaf[];
}

export interface WorkspaceTreeInput {
  workspaceName: string;
  activeRoomId: string | null;
  focusedPaneId: string | null;
  rooms: TreeRoomInput[];
  collapsedRoomIds: Set<string>;
  workspaceCollapsed: boolean;
}

export interface TreeRow {
  kind: TreeRowKind;
  key: string;
  label: string;
  depth: number;
  roomId: string | null;
  paneId: string | null;
  paneType: string | null;
  active: boolean;
  expandable: boolean;
  collapsed: boolean;
  count: number;
}

export const PANE_TYPE_LABELS: Record<string, string> = {
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

export const NO_WORKSPACES_MESSAGE = "No workspaces yet, create or open a workspace";

export function workspaceTreeEmptyMessage(workspaceCount: number): string | null {
  return workspaceCount <= 0 ? NO_WORKSPACES_MESSAGE : null;
}

export function paneLabel(leaf: TreeLeaf): string {
  const title = leaf.title.trim();
  if (title.length > 0) return title;
  return PANE_TYPE_LABELS[leaf.paneType] ?? leaf.paneType ?? "pane";
}

export function buildWorkspaceTree(input: WorkspaceTreeInput): TreeRow[] {
  const rows: TreeRow[] = [];
  rows.push({
    kind: "workspace",
    key: `ws:${input.workspaceName}`,
    label: input.workspaceName,
    depth: 0,
    roomId: null,
    paneId: null,
    paneType: null,
    active: false,
    expandable: input.rooms.length > 0,
    collapsed: input.workspaceCollapsed,
    count: input.rooms.length,
  });
  if (input.workspaceCollapsed) return rows;

  for (const room of input.rooms) {
    const roomCollapsed = input.collapsedRoomIds.has(room.id);
    rows.push({
      kind: "room",
      key: `room:${room.id}`,
      label: room.name,
      depth: 1,
      roomId: room.id,
      paneId: null,
      paneType: null,
      active: room.id === input.activeRoomId,
      expandable: room.leaves.length > 1,
      collapsed: roomCollapsed,
      count: room.leaves.length,
    });
    if (roomCollapsed || room.leaves.length <= 1) continue;
    for (const leaf of room.leaves) {
      rows.push({
        kind: "pane",
        key: `pane:${room.id}:${leaf.paneId}`,
        label: paneLabel(leaf),
        depth: 2,
        roomId: room.id,
        paneId: leaf.paneId,
        paneType: leaf.paneType,
        active: room.id === input.activeRoomId && leaf.paneId === input.focusedPaneId,
        expandable: false,
        collapsed: false,
        count: 0,
      });
    }
  }
  return rows;
}
