export const MarkdownViewMode = {
  Rte: "rte" as const,
  Source: "source" as const,
};

export function toggleViewMode(mode: string): string {
  return mode === MarkdownViewMode.Rte ? MarkdownViewMode.Source : MarkdownViewMode.Rte;
}
