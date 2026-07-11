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
