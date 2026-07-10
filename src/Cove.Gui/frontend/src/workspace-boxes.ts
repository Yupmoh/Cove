export interface WorkspaceBoxInput {
  id: string;
  name: string;
}

export interface WorkspaceBox {
  id: string;
  name: string;
  initial: string;
  active: boolean;
}

export function workspaceInitial(name: string): string {
  const trimmed = name.trim();
  if (trimmed.length === 0) return "?";
  return trimmed[0].toUpperCase();
}

export function buildWorkspaceBoxes(items: WorkspaceBoxInput[], activeId: string | null): WorkspaceBox[] {
  return items.map((w) => ({
    id: w.id,
    name: w.name,
    initial: workspaceInitial(w.name),
    active: w.id === activeId,
  }));
}
