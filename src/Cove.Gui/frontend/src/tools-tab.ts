export interface AdapterStatusMeta {
  label: string;
  cssColor: string;
}

export function adapterStatusMeta(status: string | null | undefined): AdapterStatusMeta {
  switch (status) {
    case "detected":
      return { label: "detected", cssColor: "#5fc08a" };
    case "broken":
      return { label: "broken", cssColor: "#e0a44a" };
    case "missing":
      return { label: "missing", cssColor: "var(--muted)" };
    default:
      return { label: "unknown", cssColor: "var(--muted)" };
  }
}

export function adapterCardSubtitle(version: string | null | undefined, binaryPath: string | null | undefined): string {
  const versionPart = version ? `v${version}` : "version unknown";
  const pathPart = binaryPath ? binaryPath : "binary not found";
  return `${versionPart} · ${pathPart}`;
}

export interface ToolsRetention {
  present: boolean;
  editable: boolean;
  hidden: boolean;
  value: string | null;
  recommended: string | null;
}

export interface ToolsAdapter {
  name: string;
  displayName: string;
  accent: string;
  binary: string;
  status?: string | null;
  version?: string | null;
  binaryPath?: string | null;
  iconSvg?: string | null;
  installHint: string;
  bundled: boolean;
  removable: boolean;
  retention: ToolsRetention;
}

export interface ToolsAdapterProjection {
  installed: ToolsAdapter[];
  available: ToolsAdapter[];
  unavailable: ToolsAdapter[];
}

export function projectToolsAdapters(adapters: readonly ToolsAdapter[]): ToolsAdapterProjection {
  const installed: ToolsAdapter[] = [];
  const available: ToolsAdapter[] = [];
  const unavailable: ToolsAdapter[] = [];
  for (const adapter of adapters) {
    if (adapter.status === "detected" || adapter.status === "broken") installed.push(adapter);
    else if (adapter.installHint.trim()) available.push(adapter);
    else unavailable.push(adapter);
  }
  return { installed, available, unavailable };
}

export function toolsSubtitle(
  status: string | null | undefined,
  version: string | null | undefined,
  binaryPath: string | null | undefined,
  installHint: string,
): string {
  if (status === "detected") return adapterCardSubtitle(version, binaryPath);
  const hint = installHint.trim();
  return hint ? `not found · ${hint}` : "not found";
}

export function retentionChipVisible(retention: ToolsRetention | null | undefined): boolean {
  return !!retention && retention.present && !retention.hidden;
}

export function retentionChipLabel(retention: ToolsRetention): string {
  const value = retention.value && retention.value.trim() ? retention.value.trim() : "default";
  return `Retention: ${value}`;
}
