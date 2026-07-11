export interface TabShore {
  id: string;
  name: string;
  pinned: boolean;
}

export const NookTypeAccent: Record<string, string> = {
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

export function accentForNookType(nookType: string): string {
  return NookTypeAccent[nookType] ?? "#6b7280";
}

export const NookTypeGlyph: Record<string, string> = {
  terminal: "▌",
  browser: "◑",
  search: "⌕",
  git: "⎇",
  "source-control": "⎇",
  editor: "✐",
  markdown: "❡",
  notepad: "✎",
  tasks: "▤",
  "tasks-list": "▤",
  "tasks-kanban": "▤",
  "tasks-detail": "▤",
  diff: "±",
  "diff-review": "±",
  image: "▦",
  pdf: "▤",
  video: "▶",
  library: "▤",
  "session-picker": "≣",
  "snapshot-inspector": "◱",
  empty: "▌",
};

export function glyphForNookType(nookType: string): string {
  return NookTypeGlyph[nookType] ?? "▌";
}

export interface PinnedState {
  pinned: string[];
  unpinned: string[];
}

export function partitionPinned(shores: TabShore[]): PinnedState {
  const pinned: string[] = [];
  const unpinned: string[] = [];
  for (const r of shores) {
    if (r.pinned) pinned.push(r.id);
    else unpinned.push(r.id);
  }
  return { pinned, unpinned };
}

export function togglePin(shores: TabShore[], shoreId: string): TabShore[] {
  return shores.map((r) => (r.id === shoreId ? { ...r, pinned: !r.pinned } : r));
}

export function reorderShore<T>(shores: T[], fromIdx: number, toIdx: number): T[] {
  if (fromIdx < 0 || fromIdx >= shores.length || toIdx < 0 || toIdx >= shores.length || fromIdx === toIdx) {
    return shores;
  }
  const out = [...shores];
  const [moved] = out.splice(fromIdx, 1);
  out.splice(toIdx, 0, moved);
  return out;
}

export function closeAllPreservePinned(shores: TabShore[]): TabShore[] {
  return shores.filter((r) => r.pinned);
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
  nookType?: string;
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
      accent: accentForNookType(node.nookType ?? "terminal"),
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
  wings: { id: string; name: string; shoreIds: string[] }[];
  activeWingId: string | null;
}

export function visibleShoreIds(wings: WingModel): string[] {
  const wing = wings.wings.find((w) => w.id === wings.activeWingId);
  return wing ? wing.shoreIds : [];
}

export function switchWing(wings: WingModel, wingId: string): WingModel {
  return { ...wings, activeWingId: wingId };
}

export interface WingInfo { id: string; name: string; }
export interface ShoreWingSummary { id: string; wingId: string; pinned: boolean; }

export function buildWingModel(wings: WingInfo[], shores: ShoreWingSummary[], activeWingId: string | null): WingModel {
  const wingShores = new Map<string, string[]>();
  for (const r of shores)
  {
    const list = wingShores.get(r.wingId) ?? [];
    list.push(r.id);
    wingShores.set(r.wingId, list);
  }
  const built = wings.map((w) => ({ id: w.id, name: w.name, shoreIds: wingShores.get(w.id) ?? [] }));
  return { wings: built, activeWingId: activeWingId ?? built[0]?.id ?? null };
}

export function filterShoresByWing<T extends { id: string }>(shores: T[], visibleIds: string[]): T[] {
  const set = new Set(visibleIds);
  return shores.filter((r) => set.has(r.id));
}

export const WingSwitcherState = {
  Collapsed: "collapsed",
  Expanded: "expanded",
} as const;

export type WingSwitcherVisibility = (typeof WingSwitcherState)[keyof typeof WingSwitcherState];

export function toggleWingSwitcher(state: WingSwitcherVisibility): WingSwitcherVisibility {
  return state === WingSwitcherState.Collapsed ? WingSwitcherState.Expanded : WingSwitcherState.Collapsed;
}
