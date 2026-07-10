export interface LauncherAdapter {
  name: string;
  displayName: string;
  accent: string;
  binary: string;
}

export interface LauncherBuiltin {
  id: string;
  label: string;
  icon: string;
  action: string;
}

export type LauncherTileKind = "builtin" | "adapter";

export interface LauncherTile {
  kind: LauncherTileKind;
  id: string;
  label: string;
  icon: string;
  accent: string;
  action: string;
  adapterName: string;
  binary: string;
  disabled: boolean;
  note: string;
}

export function shouldShowLauncher(roomCount: number): boolean {
  return roomCount <= 0;
}

export interface RoomTreeNode {
  kind: "leaf" | "split";
  subtabs?: { paneType: string }[];
}

export function isEmptyRoomTree(node: RoomTreeNode | null | undefined): boolean {
  if (!node || node.kind !== "leaf") return false;
  const subs = node.subtabs ?? [];
  return subs.length === 0 || subs.every((s) => s.paneType === "empty");
}

export type LauncherPlacement = "replace" | "create";

export function launcherPlacement(activeRoomEmpty: boolean): LauncherPlacement {
  return activeRoomEmpty ? "replace" : "create";
}

export function placeablePaneForAction(action: string): { paneType: string; kind: "terminal" | "browser" | "tool" } | null {
  switch (action) {
    case "room.new":
      return { paneType: "terminal", kind: "terminal" };
    case "tool.browser":
      return { paneType: "browser", kind: "browser" };
    case "tool.search":
      return { paneType: "search", kind: "tool" };
    case "tool.git":
      return { paneType: "git", kind: "tool" };
    case "tool.tasks":
      return { paneType: "tasks-list", kind: "tool" };
    default:
      return null;
  }
}

export function buildAdapterTiles(adapters: LauncherAdapter[]): LauncherTile[] {
  return adapters.map((a) => {
    const detected = a.binary.trim().length > 0;
    return {
      kind: "adapter",
      id: `adapter:${a.name}`,
      label: a.displayName.trim().length > 0 ? a.displayName : a.name,
      icon: "◆",
      accent: a.accent,
      action: "",
      adapterName: a.name,
      binary: a.binary,
      disabled: !detected,
      note: detected ? "" : "not detected",
    };
  });
}

export function buildBuiltinTiles(builtins: LauncherBuiltin[]): LauncherTile[] {
  return builtins.map((b) => ({
    kind: "builtin",
    id: `builtin:${b.id}`,
    label: b.label,
    icon: b.icon,
    accent: "",
    action: b.action,
    adapterName: "",
    binary: "",
    disabled: false,
    note: "",
  }));
}

export function buildLauncherTiles(adapters: LauncherAdapter[], builtins: LauncherBuiltin[]): LauncherTile[] {
  return [...buildAdapterTiles(adapters), ...buildBuiltinTiles(builtins)];
}
