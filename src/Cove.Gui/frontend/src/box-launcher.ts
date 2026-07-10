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
