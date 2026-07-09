export const DiffViewMode = {
  SideBySide: "side-by-side" as const,
  Unified: "unified" as const,
  toggle(mode: string): string {
    return mode === DiffViewMode.SideBySide ? DiffViewMode.Unified : DiffViewMode.SideBySide;
  },
};

export interface RefSpec {
  ref: string;
  isWorkingTree: boolean;
}

export function parseRefSpec(input: string): RefSpec {
  if (input.length === 0) return { ref: "HEAD", isWorkingTree: false };
  if (input === "WORKING") return { ref: "HEAD", isWorkingTree: true };
  return { ref: input, isWorkingTree: false };
}
