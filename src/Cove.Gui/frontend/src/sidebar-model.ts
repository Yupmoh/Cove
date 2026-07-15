export type SidebarSide = "left" | "right";

export type SidebarMode =
  | "bays"
  | "overview"
  | "skills"
  | "activity"
  | "timeline"
  | "notepad";

export interface SidebarModeMeta {
  mode: SidebarMode;
  icon: string;
  label: string;
  functional: boolean;
}

export const SIDEBAR_MODES: SidebarModeMeta[] = [
  { mode: "bays", icon: "▤", label: "Bays", functional: true },
  { mode: "overview", icon: "▦", label: "Overview", functional: false },
  { mode: "skills", icon: "◈", label: "Skills", functional: false },
  { mode: "activity", icon: "☷", label: "Activity", functional: false },
  { mode: "timeline", icon: "≣", label: "Timeline", functional: false },
  { mode: "notepad", icon: "✎", label: "Notepad", functional: true },
];

export const SIDEBAR_RAIL_MODES = SIDEBAR_MODES.filter((meta) =>
  meta.mode === "bays" || meta.mode === "skills" || meta.mode === "timeline",
);

export const SIDEBAR_MODE_META: Record<SidebarMode, SidebarModeMeta> = SIDEBAR_MODES.reduce(
  (acc, m) => { acc[m.mode] = m; return acc; },
  {} as Record<SidebarMode, SidebarModeMeta>,
);

export const SIDEBAR_MIN_WIDTH = 176;
export const SIDEBAR_MAX_WIDTH = 520;
export const SIDEBAR_DEFAULT_WIDTH = 248;

export interface SidebarModel {
  leftMode: SidebarMode;
  leftCollapsed: boolean;
  rightCollapsed: boolean;
  leftWidth: number;
  rightWidth: number;
}

export function initialSidebarModel(): SidebarModel {
  return {
    leftMode: "bays",
    leftCollapsed: false,
    rightCollapsed: false,
    leftWidth: SIDEBAR_DEFAULT_WIDTH,
    rightWidth: SIDEBAR_DEFAULT_WIDTH,
  };
}

export function collapsedOf(model: SidebarModel, side: SidebarSide): boolean {
  return side === "left" ? model.leftCollapsed : model.rightCollapsed;
}

export function widthOf(model: SidebarModel, side: SidebarSide): number {
  return side === "left" ? model.leftWidth : model.rightWidth;
}

export function clampWidth(width: number): number {
  if (!Number.isFinite(width)) return SIDEBAR_DEFAULT_WIDTH;
  return Math.max(SIDEBAR_MIN_WIDTH, Math.min(SIDEBAR_MAX_WIDTH, Math.round(width)));
}

export function selectLeftMode(model: SidebarModel, mode: SidebarMode): SidebarModel {
  return { ...model, leftMode: mode, leftCollapsed: false };
}

export function setCollapsed(model: SidebarModel, side: SidebarSide, collapsed: boolean): SidebarModel {
  return side === "left"
    ? { ...model, leftCollapsed: collapsed }
    : { ...model, rightCollapsed: collapsed };
}

export function toggleSide(model: SidebarModel, side: SidebarSide): SidebarModel {
  return setCollapsed(model, side, !collapsedOf(model, side));
}

export function setWidth(model: SidebarModel, side: SidebarSide, width: number): SidebarModel {
  const w = clampWidth(width);
  return side === "left" ? { ...model, leftWidth: w } : { ...model, rightWidth: w };
}
