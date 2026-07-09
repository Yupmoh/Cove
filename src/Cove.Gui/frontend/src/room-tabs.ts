export interface TabRoom {
  id: string;
  name: string;
  pinned: boolean;
}

export const PaneTypeAccent: Record<string, string> = {
  terminal: "#4cc2d6",
  browser: "#5b9bd5",
  editor: "#c5c8c6",
  markdown: "#e5c07b",
  diff: "#56b6c2",
  "diff-review": "#2ec4b6",
  "source-control": "#e06c75",
  git: "#e06c75",
  search: "#c678dd",
  notepad: "#e0af68",
  image: "#d19a66",
  canvas: "#98c379",
  mermaid: "#61afef",
  html: "#e06c75",
};

export function accentForPaneType(paneType: string): string {
  return PaneTypeAccent[paneType] ?? "#6b7280";
}

export interface PinnedState {
  pinned: string[];
  unpinned: string[];
}

export function partitionPinned(rooms: TabRoom[]): PinnedState {
  const pinned: string[] = [];
  const unpinned: string[] = [];
  for (const r of rooms) {
    if (r.pinned) pinned.push(r.id);
    else unpinned.push(r.id);
  }
  return { pinned, unpinned };
}

export function togglePin(rooms: TabRoom[], roomId: string): TabRoom[] {
  return rooms.map((r) => (r.id === roomId ? { ...r, pinned: !r.pinned } : r));
}

export function reorderRoom<T>(rooms: T[], fromIdx: number, toIdx: number): T[] {
  if (fromIdx < 0 || fromIdx >= rooms.length || toIdx < 0 || toIdx >= rooms.length || fromIdx === toIdx) {
    return rooms;
  }
  const out = [...rooms];
  const [moved] = out.splice(fromIdx, 1);
  out.splice(toIdx, 0, moved);
  return out;
}

export function closeAllPreservePinned(rooms: TabRoom[]): TabRoom[] {
  return rooms.filter((r) => r.pinned);
}

export interface MiniDiagramCell {
  x: number;
  y: number;
  w: number;
  h: number;
  accent: string;
}

interface LayoutRect {
  x: number;
  y: number;
  w: number;
  h: number;
}

export interface MiniDiagramNode {
  kind: "leaf" | "split";
  paneType?: string;
  orientation?: string | number;
  ratio?: number;
  childA?: MiniDiagramNode;
  childB?: MiniDiagramNode;
}

export function buildMiniDiagram(node: MiniDiagramNode, rect: LayoutRect): MiniDiagramCell[] {
  if (node.kind === "leaf") {
    return [{
      x: rect.x,
      y: rect.y,
      w: rect.w,
      h: rect.h,
      accent: accentForPaneType(node.paneType ?? "terminal"),
    }];
  }
  const ratio = node.ratio ?? 0.5;
  const isRow = node.orientation === "row" || node.orientation === 1;
  if (isRow) {
    const leftW = Math.max(1, Math.round(rect.w * ratio));
    const rightW = rect.w - leftW;
    return [
      ...buildMiniDiagram(node.childA!, { x: rect.x, y: rect.y, w: leftW, h: rect.h }),
      ...buildMiniDiagram(node.childB!, { x: rect.x + leftW, y: rect.y, w: rightW, h: rect.h }),
    ];
  }
  const topH = Math.max(1, Math.round(rect.h * ratio));
  const botH = rect.h - topH;
  return [
    ...buildMiniDiagram(node.childA!, { x: rect.x, y: rect.y, w: rect.w, h: topH }),
    ...buildMiniDiagram(node.childB!, { x: rect.x, y: rect.y + topH, w: rect.w, h: botH }),
  ];
}

export interface WingModel {
  wings: { id: string; name: string; roomIds: string[] }[];
  activeWingId: string | null;
}

export function visibleRoomIds(wings: WingModel): string[] {
  const wing = wings.wings.find((w) => w.id === wings.activeWingId);
  return wing ? wing.roomIds : [];
}

export function switchWing(wings: WingModel, wingId: string): WingModel {
  return { ...wings, activeWingId: wingId };
}

export interface WingInfo { id: string; name: string; }
export interface RoomWingSummary { id: string; wingId: string; pinned: boolean; }

export function buildWingModel(wings: WingInfo[], rooms: RoomWingSummary[], activeWingId: string | null): WingModel {
  const wingRooms = new Map<string, string[]>();
  for (const r of rooms)
  {
    const list = wingRooms.get(r.wingId) ?? [];
    list.push(r.id);
    wingRooms.set(r.wingId, list);
  }
  const built = wings.map((w) => ({ id: w.id, name: w.name, roomIds: wingRooms.get(w.id) ?? [] }));
  return { wings: built, activeWingId: activeWingId ?? built[0]?.id ?? null };
}

export function filterRoomsByWing<T extends { id: string }>(rooms: T[], visibleIds: string[]): T[] {
  const set = new Set(visibleIds);
  return rooms.filter((r) => set.has(r.id));
}

export const WingSwitcherState = {
  Collapsed: "collapsed",
  Expanded: "expanded",
} as const;

export type WingSwitcherVisibility = (typeof WingSwitcherState)[keyof typeof WingSwitcherState];

export function toggleWingSwitcher(state: WingSwitcherVisibility): WingSwitcherVisibility {
  return state === WingSwitcherState.Collapsed ? WingSwitcherState.Expanded : WingSwitcherState.Collapsed;
}
