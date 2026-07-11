export interface BayBoxInput {
  id: string;
  name: string;
  projectDir?: string;
  icon?: { kind: string; value: string } | null;
}

export interface BayBox {
  id: string;
  name: string;
  initial: string;
  active: boolean;
}

export function bayInitial(name: string): string {
  const trimmed = name.trim();
  if (trimmed.length === 0) return "?";
  return trimmed[0].toUpperCase();
}

export function nextBayName(input: string, current: string): string {
  const trimmed = input.trim();
  return trimmed.length > 0 ? trimmed : current;
}

export function buildBayBoxes(items: BayBoxInput[], activeId: string | null): BayBox[] {
  return items.map((w) => ({
    id: w.id,
    name: w.name,
    initial: bayInitial(w.name),
    active: w.id === activeId,
  }));
}
