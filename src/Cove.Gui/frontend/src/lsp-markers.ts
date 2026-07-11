export interface LspDiagnosticLike {
  startLine: number;
  startCol: number;
  endLine: number;
  endCol: number;
  severity: string;
  message: string;
  code?: string | null;
}

export interface LspMarkerData {
  severity: number;
  message: string;
  code?: string;
  startLineNumber: number;
  startColumn: number;
  endLineNumber: number;
  endColumn: number;
  source: string;
}

export const MARKER_SEVERITY = {
  Hint: 1,
  Info: 2,
  Warning: 4,
  Error: 8,
} as const;

function toMarkerSeverity(severity: string): number {
  switch (severity) {
    case "error": return MARKER_SEVERITY.Error;
    case "warning": return MARKER_SEVERITY.Warning;
    case "hint": return MARKER_SEVERITY.Hint;
    default: return MARKER_SEVERITY.Info;
  }
}

export function diagnosticsToMarkers(diags: LspDiagnosticLike[]): LspMarkerData[] {
  return diags.map((d) => {
    const marker: LspMarkerData = {
      severity: toMarkerSeverity(d.severity),
      message: d.message,
      startLineNumber: d.startLine + 1,
      startColumn: d.startCol + 1,
      endLineNumber: d.endLine + 1,
      endColumn: d.endCol + 1,
      source: "cove-lsp",
    };
    if (d.code != null) marker.code = d.code;
    return marker;
  });
}

const LSP_LANGUAGE_MAP: Record<string, string> = {
  ts: "typescript", mts: "typescript", cts: "typescript",
  tsx: "typescriptreact",
  js: "javascript", mjs: "javascript", cjs: "javascript",
  jsx: "javascriptreact",
  json: "json",
  css: "css",
  html: "html", htm: "html",
};

export function lspLanguageForPath(path: string): string | null {
  const lower = path.toLowerCase();
  const dotIdx = lower.lastIndexOf(".");
  if (dotIdx < 0 || dotIdx === lower.length - 1) return null;
  return LSP_LANGUAGE_MAP[lower.slice(dotIdx + 1)] ?? null;
}
