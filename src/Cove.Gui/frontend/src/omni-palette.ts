export type PaletteCategory = "all" | "commands" | "rooms" | "panes" | "tasks" | "files" | "workspaces";

export const CategoryPrefix: Record<string, PaletteCategory> = {
  ">": "commands",
  "~": "workspaces",
  "@": "rooms",
  "$": "panes",
  "#": "tasks",
  "/": "files",
};

export interface PaletteItem {
  id: string;
  label: string;
  category: PaletteCategory;
  icon: string;
  key?: string;
  run: () => void;
}

export interface ParsedQuery {
  category: PaletteCategory;
  text: string;
}

export function parseQuery(input: string): ParsedQuery {
  const trimmed = input.trimStart();
  if (trimmed.length === 0) return { category: "all", text: "" };
  const first = trimmed[0];
  if (CategoryPrefix[first]) {
    return { category: CategoryPrefix[first], text: trimmed.slice(1).trim() };
  }
  return { category: "all", text: trimmed.trim() };
}

export function fuzzyMatch(query: string, target: string): boolean {
  if (query.length === 0) return true;
  const q = query.toLowerCase();
  const t = target.toLowerCase();
  let qi = 0;
  for (let ti = 0; ti < t.length && qi < q.length; ti++) {
    if (t[ti] === q[qi]) qi++;
  }
  return qi === q.length;
}

export function fuzzyScore(query: string, target: string): number {
  if (query.length === 0) return 1;
  const q = query.toLowerCase();
  const t = target.toLowerCase();
  if (t === q) return 100;
  if (t.startsWith(q)) return 80;
  let qi = 0;
  let consecutive = 0;
  let maxConsecutive = 0;
  let firstMatch = -1;
  for (let ti = 0; ti < t.length && qi < q.length; ti++) {
    if (t[ti] === q[qi]) {
      if (firstMatch < 0) firstMatch = ti;
      consecutive++;
      maxConsecutive = Math.max(maxConsecutive, consecutive);
      qi++;
    } else {
      consecutive = 0;
    }
  }
  if (qi < q.length) return 0;
  return 50 + maxConsecutive * 5 - firstMatch;
}

export function filterAndSort(items: PaletteItem[], query: ParsedQuery): PaletteItem[] {
  const filtered = query.category === "all"
    ? items.filter((i) => fuzzyMatch(query.text, i.label))
    : items.filter((i) => i.category === query.category && fuzzyMatch(query.text, i.label));
  return filtered.sort((a, b) => fuzzyScore(query.text, b.label) - fuzzyScore(query.text, a.label));
}

export interface MruEntry {
  id: string;
  timestamp: number;
}

export class MruTracker {
  private entries: MruEntry[] = [];

  constructor(stored?: MruEntry[]) {
    if (stored) this.entries = [...stored];
  }

  record(id: string): void {
    this.entries = this.entries.filter((e) => e.id !== id);
    this.entries.push({ id, timestamp: Date.now() });
    if (this.entries.length > 50) this.entries = this.entries.slice(-50);
  }

  sortByIds(ids: string[]): string[] {
    const order = new Map(this.entries.map((e, i) => [e.id, i]));
    return [...ids].sort((a, b) => {
      const ai = order.get(a);
      const bi = order.get(b);
      if (ai === undefined && bi === undefined) return 0;
      if (ai === undefined) return 1;
      if (bi === undefined) return -1;
      return bi - ai;
    });
  }

  toList(): MruEntry[] {
    return [...this.entries];
  }
}

export function cycleCategory(current: PaletteCategory, direction: 1 | -1): PaletteCategory {
  const order: PaletteCategory[] = ["all", "commands", "rooms", "panes", "tasks", "files", "workspaces"];
  const idx = order.indexOf(current);
  const next = (idx + direction + order.length) % order.length;
  return order[next];
}

export function categoryLabel(category: PaletteCategory): string {
  const labels: Record<PaletteCategory, string> = {
    all: "All",
    commands: "Commands",
    rooms: "Rooms",
    panes: "Panes",
    tasks: "Tasks",
    files: "Files",
    workspaces: "Workspaces",
  };
  return labels[category];
}
