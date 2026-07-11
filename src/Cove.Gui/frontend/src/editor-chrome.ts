export interface BreadcrumbSegment {
  label: string;
  path: string;
}

export function buildBreadcrumbs(filePath: string): BreadcrumbSegment[] {
  const normalized = filePath.replace(/\\/g, "/");
  const absolute = normalized.startsWith("/");
  const parts = normalized.split("/").filter((p) => p.length > 0);
  const segments: BreadcrumbSegment[] = [];
  let acc = "";
  for (const part of parts) {
    acc = `${acc}/${part}`;
    segments.push({ label: part, path: absolute ? acc : acc.slice(1) });
  }
  return segments;
}

export type WordWrap = "on" | "off";

export function toggleWordWrap(current: WordWrap): WordWrap {
  return current === "on" ? "off" : "on";
}

export function wordWrapStorageKey(nookId: string): string {
  return `cove.editor.wordWrap.${nookId}`;
}

export function minimapStorageKey(nookId: string): string {
  return `cove.editor.minimap.${nookId}`;
}

export interface AttributionEntryLike {
  sessionId: string;
  toolUseId: string;
  startLine: number;
  endLine: number;
  at: string;
}

export interface AgentEditChip {
  toolUseId: string;
  sessionId: string;
  lineRange: string;
}

export function latestAgentEdit(entries: AttributionEntryLike[]): AgentEditChip | null {
  if (entries.length === 0) return null;
  const latest = entries.reduce((a, b) => (b.at > a.at ? b : a));
  return {
    toolUseId: latest.toolUseId,
    sessionId: latest.sessionId,
    lineRange: latest.startLine === latest.endLine ? `${latest.startLine}` : `${latest.startLine}-${latest.endLine}`,
  };
}

export function formatAgentEditChip(chip: AgentEditChip): string {
  return `\u{1F916} ${chip.toolUseId} · ${chip.sessionId} · L${chip.lineRange}`;
}
