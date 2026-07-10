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

export interface WorkspaceEntry {
  id: string;
  name: string;
}

export interface WorkspaceTreeInput {
  workspaceName: string;
  activeRoomId: string | null;
  focusedPaneId: string | null;
  rooms: TreeRoomInput[];
  collapsedRoomIds: Set<string>;
  workspaceCollapsed: boolean;
  workspaces?: WorkspaceEntry[];
  activeWorkspaceId?: string | null;
}

export interface TreeRow {
  kind: TreeRowKind;
  key: string;
  label: string;
  depth: number;
  roomId: string | null;
  paneId: string | null;
  paneType: string | null;
  workspaceId: string | null;
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
  const activeId = input.activeWorkspaceId ?? null;
  const workspaces: WorkspaceEntry[] = input.workspaces && input.workspaces.length > 0
    ? input.workspaces
    : [{ id: activeId ?? "active", name: input.workspaceName }];

  for (const ws of workspaces) {
    const isActive = workspaces.length === 1 || ws.id === activeId;
    rows.push({
      kind: "workspace",
      key: `ws:${ws.id}`,
      label: ws.name,
      depth: 0,
      roomId: null,
      paneId: null,
      paneType: null,
      workspaceId: ws.id,
      active: isActive,
      expandable: isActive && input.rooms.length > 0,
      collapsed: isActive ? input.workspaceCollapsed : true,
      count: isActive ? input.rooms.length : 0,
    });
    if (!isActive || input.workspaceCollapsed) continue;

    for (const room of input.rooms) {
      const roomCollapsed = input.collapsedRoomIds.has(room.id);
      const realLeaves = room.leaves.filter((l) => l.paneType !== "empty");
      rows.push({
        kind: "room",
        key: `room:${room.id}`,
        label: room.name,
        depth: 1,
        roomId: room.id,
        paneId: null,
        paneType: null,
        workspaceId: ws.id,
        active: room.id === input.activeRoomId,
        expandable: realLeaves.length > 0,
        collapsed: roomCollapsed,
        count: realLeaves.length,
      });
      if (roomCollapsed || realLeaves.length === 0) continue;
      for (const leaf of realLeaves) {
        rows.push({
          kind: "pane",
          key: `pane:${room.id}:${leaf.paneId}`,
          label: paneLabel(leaf),
          depth: 2,
          roomId: room.id,
          paneId: leaf.paneId,
          paneType: leaf.paneType,
          workspaceId: ws.id,
          active: room.id === input.activeRoomId && leaf.paneId === input.focusedPaneId,
          expandable: false,
          collapsed: false,
          count: 0,
        });
      }
    }
  }
  return rows;
}
