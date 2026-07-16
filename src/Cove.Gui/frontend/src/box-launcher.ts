export interface LauncherAdapter {
  name: string;
  displayName: string;
  accent: string;
  binary: string;
  version?: string;
  updateCommand?: string;
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
  version: string;
  disabled: boolean;
  note: string;
  updateCommand: string;
}

export function shouldShowLauncher(shoreCount: number): boolean {
  return shoreCount <= 0;
}

export interface ShoreTreeNode {
  kind: "leaf" | "split";
  subtabs?: { nookType: string }[];
}

export function isEmptyShoreTree(node: ShoreTreeNode | null | undefined): boolean {
  if (!node || node.kind !== "leaf") return false;
  const subs = node.subtabs ?? [];
  return subs.length === 0 || subs.every((s) => s.nookType === "empty");
}

export interface PlaceholderTreeNode {
  kind: "leaf" | "split";
  nookId?: string;
  subtabs?: { nookType: string }[];
  childA?: PlaceholderTreeNode;
  childB?: PlaceholderTreeNode;
}

export function isPlaceholderLeaf(node: PlaceholderTreeNode | null | undefined, nookId: string): boolean {
  if (!node) return false;
  if (node.kind === "leaf") {
    if (node.nookId !== nookId) return false;
    const subs = node.subtabs ?? [];
    return subs.length === 0 || subs.every((s) => s.nookType === "empty");
  }
  return isPlaceholderLeaf(node.childA, nookId) || isPlaceholderLeaf(node.childB, nookId);
}

export function resolveLaunchCwd(explicitCwd: string, inheritCwdFrom: string, activeBayDir: string): string {
  if (explicitCwd.trim().length > 0) return explicitCwd;
  if (inheritCwdFrom.trim().length > 0) return "";
  return activeBayDir;
}

export type LauncherPlacement = "replace" | "create";

export function launcherPlacement(activeShoreEmpty: boolean): LauncherPlacement {
  return activeShoreEmpty ? "replace" : "create";
}

export interface PlaceableNook {
  nookType: string;
  kind: "terminal" | "browser" | "tool";
  shoreName: string;
}

export function placeableNookForAction(action: string): PlaceableNook | null {
  switch (action) {
    case "shore.new":
      return { nookType: "terminal", kind: "terminal", shoreName: "Shore" };
    case "tool.browser":
      return { nookType: "browser", kind: "browser", shoreName: "Browser" };
    case "tool.search":
      return { nookType: "search", kind: "tool", shoreName: "Search" };
    case "tool.git":
      return { nookType: "git", kind: "tool", shoreName: "Source Control" };
    case "tool.tasks":
      return { nookType: "tasks-list", kind: "tool", shoreName: "Tasks" };
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
      version: (a.version ?? "").trim(),
      disabled: !detected,
      note: detected ? "" : "not detected",
      updateCommand: (a.updateCommand ?? "").trim(),
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
    version: "",
    disabled: false,
    note: "",
    updateCommand: "",
  }));
}

export function buildLauncherTiles(adapters: LauncherAdapter[], builtins: LauncherBuiltin[]): LauncherTile[] {
  return [...buildAdapterTiles(adapters), ...buildBuiltinTiles(builtins)];
}
